# Phase 9 — administrator / DBA execution handoff (side-by-side acceptance deployment)

**Status: NOT EXECUTED.** This project (Claude Code operating under `CLAUDE.md`) does not install system-wide software, does not create
IIS sites/pools/bindings/certificates, and does not create SQL Server logins or grant/deny permissions — see
`docs/iis-deployment-runbook.md` §E and `docs/superpowers/specs/2026-09-16-iis-deployment-design.md` §22–23. Everything below is for a
human administrator and a human DBA, with their own elevated/sysadmin credentials on `DESKTOP-BVH8F8L`, to run themselves.

**Rule for every checkpoint below: run the precondition check first. If it shows anything other than the stated expected/clean state,
STOP and investigate — do not overwrite, do not recreate, do not proceed to the mutating command.** After each checkpoint's mutation,
stop and record/report the output before moving to the next checkpoint; do not run two checkpoints back-to-back unreviewed.

Execution order (corrected — SQL identity moved after app pool creation, since `CREATE LOGIN ... FROM WINDOWS` requires the
`IIS AppPool\INVC-Web` virtual account to already exist):

A. Pre-change snapshots → B. Hosting Bundle → C. Fresh audited release package → D. Versioned folders → E. INVC-Web app pool →
F. SQL login/user/permissions → G. Acceptance certificate → H. INVC-Web site/bindings → I. Production environment variables →
J. Windows Authentication → K. Start/test new site → L. Verify legacy `/INVC` unchanged → M. Client acceptance pending.

Immutable throughout every checkpoint: Default Web Site, the `/INVC` virtual directory, `C:\INVC\Web` as the legacy physical path,
`DefaultAppPool`, the `192.168.1.99:8080` / `invc-ksl.ddns.net` binding, Laragon, port 443, router/NAT/DDNS/firewall configuration.
None of the steps below touch any of these; if a step ever appears to require touching one of them, STOP instead.

---

## Checkpoint A — Pre-change snapshots (read-only)

```powershell
Import-Module WebAdministration
Get-Website | Format-Table Name, ID, State, PhysicalPath -AutoSize
Get-ChildItem IIS:\AppPools | Format-Table Name, State -AutoSize
Get-WebBinding | Format-Table protocol, bindingInformation -AutoSize
Get-ChildItem Cert:\LocalMachine\My | Format-Table Subject, DnsNameList, Thumbprint, NotAfter -AutoSize

New-Item -ItemType Directory -Force D:\Apps\InvcWeb\backups | Out-Null
Copy-Item "$env:windir\System32\inetsrv\config\applicationHost.config" `
  "D:\Apps\InvcWeb\backups\applicationHost.config.$(Get-Date -Format yyyyMMdd-HHmm).bak"
Invoke-WebRequest http://localhost/INVC/default.asp -UseBasicParsing | Select-Object StatusCode   # record: expect 200
```
Record the full output (sites, pools, bindings, certs, legacy status code) before proceeding. This is the R0 rollback baseline.

**STOP AND REPORT** the snapshot output before Checkpoint B.

---

## Checkpoint B — .NET 10 Hosting Bundle (administrator)

Precondition check:
```powershell
Test-Path "$env:windir\System32\inetsrv\aspnetcorev2.dll"   # expect False — if True, Hosting Bundle already installed: STOP,
                                                              # record the existing version instead of reinstalling
```
If absent, proceed:
1. Download the **ASP.NET Core 10.0.x Hosting Bundle** for Windows x64 from the official Microsoft page
   (`https://dotnet.microsoft.com/download/dotnet/10.0` → "Hosting Bundle" under ASP.NET Core Runtime). No mirrors.
2. Verify signature:
   ```powershell
   Get-AuthenticodeSignature .\dotnet-hosting-10.0.x-win.exe | Format-List Status, StatusMessage, SignerCertificate
   ```
   `Status` must be `Valid`; `SignerCertificate.Subject` must reference Microsoft Corporation.
3. Record the hash:
   ```powershell
   Get-FileHash .\dotnet-hosting-10.0.x-win.exe -Algorithm SHA256
   ```
4. Install (this restarts `W3SVC` — brief interruption to **all** IIS sites, including legacy `/INVC`):
   ```powershell
   Start-Process .\dotnet-hosting-10.0.x-win.exe -ArgumentList '/quiet','/norestart' -Wait
   ```
5. Verify:
   ```powershell
   Test-Path "$env:windir\System32\inetsrv\aspnetcorev2.dll"   # expect True
   dotnet --list-runtimes                                       # expect Microsoft.AspNetCore.App 10.0.x, Microsoft.NETCore.App 10.0.x
   ```
6. **Immediately** re-check legacy:
   ```powershell
   Invoke-WebRequest http://localhost/INVC/default.asp -UseBasicParsing | Select-Object StatusCode   # expect 200
   ```
   If not 200, stop and roll back the Hosting Bundle (see Rollback) before continuing.

No OS reboot required or authorized here.

**STOP AND REPORT**: signature status, SHA-256, `dotnet --list-runtimes` output, legacy status code.

---

## Checkpoint C — Fresh audited release package (project side, from current HEAD)

Run the full release pipeline, in order — all four must succeed:
```powershell
git rev-parse HEAD   # record — this becomes the release's commit SHA
.\scripts\verify.ps1
.\scripts\publish-iis.ps1
.\scripts\test-release-artifact.ps1
.\scripts\new-release-manifest.ps1
```
The manifest is written to `.work\release\manifest\release-manifest.json` (the script's default `-ManifestPath`, distinct from the
publish output at `.work\release\publish\`). Record and check it before proceeding:
```powershell
$manifestPath = 'C:\INVC\Web\.work\release\manifest\release-manifest.json'
Test-Path $manifestPath   # expect True — if False, STOP: do not deploy a release without its manifest
Get-FileHash $manifestPath -Algorithm SHA256

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$manifest.commit -eq (git rev-parse HEAD)   # expect True
$manifest.workingTreeDirty                   # expect False
$manifest.fileCount -gt 0                    # expect True
```
If any of the three checks above is not as expected, STOP — do not deploy this package.

Derive the release ID as `yyyyMMdd-HHmm_<sha7>` from the commit SHA and current time.

**STOP AND REPORT**: commit SHA, release ID, manifest path, manifest SHA-256, and the three verification results.

---

## Checkpoint D — Versioned folders

Precondition check:
```powershell
Test-Path D:\Apps\InvcWeb\releases\<release-id>   # expect False — if True, pick a different release-id, never overwrite
```
```powershell
New-Item -ItemType Directory -Force D:\Apps\InvcWeb\releases\<release-id> | Out-Null
New-Item -ItemType Directory -Force D:\Apps\InvcWeb\logs | Out-Null
New-Item -ItemType Directory -Force D:\Apps\InvcWeb\certs | Out-Null
Copy-Item C:\INVC\Web\.work\release\publish\* D:\Apps\InvcWeb\releases\<release-id>\ -Recurse

$manifestPath = 'C:\INVC\Web\.work\release\manifest\release-manifest.json'
if (-not (Test-Path $manifestPath)) { throw "STOP: manifest not found at $manifestPath — do not deploy a release without its manifest" }
$sourceHash = (Get-FileHash $manifestPath -Algorithm SHA256).Hash
Copy-Item $manifestPath "D:\Apps\InvcWeb\releases\<release-id>\release-manifest.json"
$deployedHash = (Get-FileHash "D:\Apps\InvcWeb\releases\<release-id>\release-manifest.json" -Algorithm SHA256).Hash
if ($deployedHash -ne $sourceHash) { throw "STOP: deployed manifest hash ($deployedHash) does not match source hash ($sourceHash)" }

Add-Content D:\Apps\InvcWeb\RELEASES.md "<release-id> | <commit-sha> | $sourceHash | $(Get-Date -Format s) | $env:USERNAME | new"
```

**STOP AND REPORT**: folder listing of `D:\Apps\InvcWeb\releases\<release-id>`, source manifest SHA-256, deployed manifest SHA-256 (must match).

---

## Checkpoint E — Create INVC-Web app pool

Precondition check:
```powershell
Get-ChildItem IIS:\AppPools\INVC-Web -ErrorAction SilentlyContinue   # expect nothing — if it already exists, STOP,
                                                                       # do not alter an existing pool blindly
```
```powershell
New-WebAppPool -Name "INVC-Web"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name managedRuntimeVersion -Value ""      # No Managed Code
Set-ItemProperty IIS:\AppPools\INVC-Web -Name managedPipelineMode -Value "Integrated"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name enable32BitAppOnWin64 -Value $false
```
This materializes the `IIS AppPool\INVC-Web` virtual account required by Checkpoint F.

**STOP AND REPORT**: `Get-ItemProperty IIS:\AppPools\INVC-Web` output.

---

## Checkpoint F — SQL read-only identity (DBA — requires Checkpoint E complete)

Precondition check:
```sql
SELECT name, type_desc, is_disabled FROM sys.server_principals WHERE name = 'IIS AppPool\INVC-Web';
-- expect 0 rows — if a row already exists, STOP, do not alter an existing login/permission set blindly
```
If clean, DBA-reviewed, exactly this and nothing more:
```sql
CREATE LOGIN [IIS AppPool\INVC-Web] FROM WINDOWS;
GO
USE INV;
GO
CREATE USER [IIS AppPool\INVC-Web] FOR LOGIN [IIS AppPool\INVC-Web];
GO
ALTER ROLE db_datareader ADD MEMBER [IIS AppPool\INVC-Web];
GO
DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [IIS AppPool\INVC-Web];
GO
```
Verify:
```sql
SELECT name, type_desc, is_disabled FROM sys.server_principals WHERE name = 'IIS AppPool\INVC-Web';
SELECT dp.permission_name, dp.state_desc, o.name AS schema_or_object
FROM sys.database_permissions dp
JOIN sys.database_principals p ON p.principal_id = dp.grantee_principal_id
LEFT JOIN sys.objects o ON o.object_id = dp.major_id
WHERE p.name = 'IIS AppPool\INVC-Web';
```
Nothing beyond `CREATE LOGIN`/`CREATE USER`/`ALTER ROLE ADD MEMBER`/`DENY ... ON SCHEMA::dbo` is authorized — no `db_owner`,
`db_datawriter`, `sysadmin`, `CONTROL`, `ALTER`, `IMPERSONATE`, table/view/procedure/function/trigger/index/constraint DDL,
compatibility level or database option changes, no `sa`.

**STOP AND REPORT**: both verification query outputs.

---

## Checkpoint G — Acceptance certificate

Precondition checks:
```powershell
Test-Path D:\Apps\InvcWeb\certs   # expect True (created in Checkpoint D); if False, create it now before exporting anything
Get-ChildItem Cert:\LocalMachine\My | Where-Object { $_.DnsNameList.Unicode -contains "DESKTOP-BVH8F8L" }
# if this already returns a valid, non-expired cert with Server Authentication EKU, reuse it — do not create a duplicate
```
If no suitable certificate exists:
```powershell
$cert = New-SelfSignedCertificate -DnsName "DESKTOP-BVH8F8L" -CertStoreLocation Cert:\LocalMachine\My `
  -KeyExportPolicy NonExportable -KeyUsage DigitalSignature,KeyEncipherment `
  -Type SSLServerAuthentication -NotAfter (Get-Date).AddYears(5)
Export-Certificate -Cert $cert -FilePath D:\Apps\InvcWeb\certs\invc-web-acceptance.cer   # public cert only, no .pfx
```
Distribute `invc-web-acceptance.cer` to client machines' Trusted Root store for acceptance testing — never export or distribute a
private key/`.pfx`.

**STOP AND REPORT**: certificate thumbprint, SAN, expiry.

---

## Checkpoint H — Create INVC-Web site and bindings

Precondition checks:
```powershell
Get-Website -Name "INVC-Web" -ErrorAction SilentlyContinue   # expect nothing — if it exists, STOP
Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object LocalPort -in 8090,8443   # expect nothing
```
```powershell
icacls "D:\Apps\InvcWeb\releases\<release-id>" /grant "IIS AppPool\INVC-Web:(OI)(CI)RX" /T
icacls "D:\Apps\InvcWeb\logs" /grant "IIS AppPool\INVC-Web:(OI)(CI)M" /T

New-Website -Name "INVC-Web" -PhysicalPath "D:\Apps\InvcWeb\releases\<release-id>" -ApplicationPool "INVC-Web" `
  -Port 8090 -HostHeader "DESKTOP-BVH8F8L"

New-WebBinding -Name "INVC-Web" -Protocol https -Port 8443 -HostHeader "DESKTOP-BVH8F8L"
$binding = Get-WebBinding -Name "INVC-Web" -Protocol https
$binding.AddSslCertificate($cert.Thumbprint, "My")
```

**STOP AND REPORT**: `Get-WebBinding -Name "INVC-Web"` output.

---

## Checkpoint I — Production environment variables (edit the site's own `web.config`)

Precondition check:
```powershell
Test-Path "D:\Apps\InvcWeb\releases\<release-id>\web.config"   # expect True (generated by publish-iis.ps1)
```
The officially documented way to set environment variables for ANCM V2 in-process hosting is inside that site's own `web.config`, in the
`<aspNetCore>` element's `<environmentVariables>` child — not a server-level `applicationHost.config` write, since the `aspNetCore`
section ships with the published app and is read per-application. Open
`D:\Apps\InvcWeb\releases\<release-id>\web.config` and add, inside the existing `<aspNetCore ...>` element:

```xml
<aspNetCore processPath="dotnet" arguments=".\Invc.Web.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="AllowedHosts" value="DESKTOP-BVH8F8L" />
    <environmentVariable name="InvDatabase__ConnectionString" value="Server=DESKTOP-BVH8F8L;Initial Catalog=INV;Integrated Security=True;ApplicationIntent=ReadOnly;Encrypt=True;TrustServerCertificate=True;Application Name=Invc.Web" />
    <environmentVariable name="ASPNETCORE_HTTPS_PORT" value="8443" />
  </environmentVariables>
</aspNetCore>
```
(No password in the connection string — Integrated Security uses the `IIS AppPool\INVC-Web` identity from Checkpoints E/F.)

After saving, recycle only the new pool:
```powershell
Restart-WebAppPool -Name "INVC-Web"
```
Alternative (only if you prefer IIS Manager over hand-editing web.config): IIS Manager → site `INVC-Web` → Configuration Editor →
section `system.webServer/aspNetCore` → `environmentVariables` collection → add each entry there; verify afterward with
`Get-WebConfiguration -Filter "system.webServer/aspNetCore/environmentVariables" -PSPath "IIS:\Sites\INVC-Web"` before trusting it —
this path can silently no-op if delegation isn't set the way expected, so confirm the read-back matches before moving on.

**STOP AND REPORT**: the `<environmentVariables>` block as saved, and the `Get-WebConfiguration` read-back.

---

## Checkpoint J — Windows Authentication ON / Anonymous OFF (INVC-Web only)

Precondition check — confirm the PSPath targets only the new site:
```powershell
$pspath = "IIS:\Sites\INVC-Web"   # never "Default Web Site"
```
```powershell
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -Value true -PSPath $pspath
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value false -PSPath $pspath
```

**STOP AND REPORT**:
```powershell
Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -PSPath $pspath
Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -PSPath $pspath
Get-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -PSPath "IIS:\Sites\Default Web Site"   # confirm UNCHANGED
```

---

## Checkpoint K — Start/test the new site

```powershell
Invoke-WebRequest http://desktop-bvh8f8l:8090/ -UseBasicParsing -ErrorAction SilentlyContinue   # expect 401 challenge, no anonymous 200
Invoke-WebRequest https://desktop-bvh8f8l:8443/Health -UseBasicParsing                            # expect 200, "INVC Web · Production", no technical detail
```
Then, from a browser with valid Windows credentials on the host: `/`, `/Inventory/Status`, `/Inventory/Detail/{code}`, `/Reorder`,
`/Reorder/Print`, `/PurchaseOrders`, `/Receipts`, print pages — check Thai rendering, security headers, and compare key figures against
the legacy `/INVC` reports (parity matrices A–D).

**STOP AND REPORT**: status codes, screenshots/figures compared.

---

## Checkpoint L — Verify legacy `/INVC` unchanged

```powershell
Invoke-WebRequest http://localhost/INVC/default.asp -UseBasicParsing | Select-Object StatusCode      # expect 200, same as Checkpoint A
Get-WebBinding -Name "Default Web Site"                                                                # expect *:80: and 192.168.1.99:8080:invc-ksl.ddns.net, unchanged
Invoke-WebRequest "http://invc-ksl.ddns.net:8080/INVC/default.asp" -UseBasicParsing -ErrorAction SilentlyContinue | Select-Object StatusCode  # external legacy path, expect 200
```

**STOP AND REPORT**: all three outputs compared against the Checkpoint A baseline.

---

## Checkpoint M — Client acceptance (pending owner verification)

From at least two separate client PCs on the LAN:
1. Install `invc-web-acceptance.cer` into the client's Trusted Root store.
2. Browse to `https://desktop-bvh8f8l:8443/`.
3. Record whether Windows Authentication was transparent (matching local account) or prompted for credentials.
4. Confirm parity against legacy `/INVC` for the same reports/date.

`TWO-CLIENT ACCEPTANCE STATUS = PENDING OWNER VERIFICATION` until this checkpoint is completed and reported. **No cutover step exists in
this document** — cutover (`httpRedirect` on `/INVC`, or moving the `:80` binding) is a separate, later, explicitly-approved action.

---

## Rollback (dependency-aware — undo the new pieces first, not the Hosting Bundle)

Rollback order mirrors creation order in reverse, and stops at whichever checkpoint actually ran:

1. **New site/bindings/auth (Checkpoints H–J)**: `Remove-Website INVC-Web` (this also removes its bindings/auth config); verify
   `Get-WebBinding -Name "Default Web Site"` and legacy `/INVC` status are unaffected.
2. **App pool (Checkpoint E)**: `Remove-WebAppPool INVC-Web`.
3. **Certificate (Checkpoint G)**: `Remove-Item Cert:\LocalMachine\My\<thumbprint>` — only after the site binding using it is removed.
4. **SQL identity (Checkpoint F)**: in `INV`, `DROP USER [IIS AppPool\INVC-Web]`; at the server level, `DROP LOGIN [IIS AppPool\INVC-Web]`.
5. **Release folder (Checkpoint D)**: delete `D:\Apps\InvcWeb\releases\<release-id>` (keep at least the previous good release, if any).
6. **Hosting Bundle (Checkpoint B) — last resort only**, and only if it is itself determined to be the cause of a problem (e.g. an
   unexpected legacy regression that Checkpoint B.6 or L did not catch immediately): uninstall via "Apps & Features"
   (`Microsoft ASP.NET Core 10.0.x - Windows Hosting Bundle`), which restarts `W3SVC` again. Do not treat this as the default or first
   rollback action — a failed acceptance deployment normally only needs steps 1–5, leaving the Hosting Bundle (and everything else on
   the shared IIS instance) untouched.

No database rollback of business data is ever needed — the application only reads. Record every rollback action taken (checkpoint,
command, time, operator) the same way `docs/iis-deployment-runbook.md` §D asks for the design-level rollback log.

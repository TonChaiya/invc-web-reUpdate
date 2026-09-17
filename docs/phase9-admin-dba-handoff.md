# Phase 9 — administrator / DBA handoff artifacts (side-by-side acceptance deployment)

**Status: NOT EXECUTED.** These are reviewable, copy/paste-ready instructions for a human administrator and a human DBA to run themselves,
on `DESKTOP-BVH8F8L`, with their own elevated/sysadmin credentials. This project (Claude Code operating under `CLAUDE.md`) does not install
system-wide software, does not create IIS sites/pools/bindings, and does not create SQL Server logins or grant/deny permissions — see
`docs/iis-deployment-runbook.md` §E and `docs/superpowers/specs/2026-09-16-iis-deployment-design.md` §22–23 for why those three tasks are
reserved to a human administrator/DBA. Everything here reproduces the values already agreed in that design spec; nothing new is introduced.

Do not run any block below unless you hold the corresponding role and have re-read the current state of the host (some of this may drift
between when this file was written and when you act — always re-check the read-only facts in `docs/phase8-environment-discovery.md` first).

---

## Part 1 — Administrator: .NET 10 Hosting Bundle (ANCM V2)

1. Download the **ASP.NET Core 10.0.x Hosting Bundle** for Windows x64 from the official Microsoft page
   (`https://dotnet.microsoft.com/download/dotnet/10.0` → "Hosting Bundle" under ASP.NET Core Runtime). Do not use a mirror.
2. Verify the Authenticode signature before running it:
   ```powershell
   Get-AuthenticodeSignature .\dotnet-hosting-10.0.x-win.exe | Format-List Status, StatusMessage, SignerCertificate
   ```
   `Status` must be `Valid` and `SignerCertificate.Subject` must reference Microsoft Corporation.
3. Record the file's SHA-256 for the audit trail:
   ```powershell
   Get-FileHash .\dotnet-hosting-10.0.x-win.exe -Algorithm SHA256
   ```
4. Install silently (this restarts `W3SVC` as part of the installer — expect a brief interruption to **all** IIS sites, including legacy `/INVC`):
   ```powershell
   Start-Process .\dotnet-hosting-10.0.x-win.exe -ArgumentList '/quiet','/norestart' -Wait
   ```
5. Verify installation:
   ```powershell
   Test-Path "$env:windir\System32\inetsrv\aspnetcorev2.dll"   # expect True
   dotnet --list-runtimes                                       # expect Microsoft.AspNetCore.App 10.0.x and Microsoft.NETCore.App 10.0.x
   ```
6. **Immediately verify legacy `/INVC` still works** (it should be unaffected — ANCM only activates for apps that request it):
   ```powershell
   Invoke-WebRequest http://localhost/INVC/default.asp -UseBasicParsing | Select-Object StatusCode
   ```
   Expect `200`. If not, stop and roll back the Hosting Bundle install before proceeding.

No OS reboot is required for the Hosting Bundle; do not reboot without a separate decision.

---

## Part 2 — DBA: read-only database identity for `IIS AppPool\INVC-Web`

**Prerequisite: the `INVC-Web` application pool must already exist** (Part 3 below) before `IIS AppPool\INVC-Web` exists as a Windows
principal — `CREATE LOGIN ... FROM WINDOWS` will fail otherwise. Take a pre-change snapshot first:

```sql
-- Pre-change snapshot (read-only) — run and save the output before making any change
SELECT name, type_desc, is_disabled FROM sys.server_principals WHERE name = 'IIS AppPool\INVC-Web';
SELECT dp.name AS user_name, r.name AS role_name
FROM sys.database_principals dp
LEFT JOIN sys.database_role_members drm ON drm.member_principal_id = dp.principal_id
LEFT JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
WHERE dp.name = 'IIS AppPool\INVC-Web';
```

Then, DBA-reviewed, exactly this and nothing more:

```sql
-- Server-level login for the INVC-Web app pool's virtual account
CREATE LOGIN [IIS AppPool\INVC-Web] FROM WINDOWS;
GO

USE INV;
GO

-- Database user mapped to that login
CREATE USER [IIS AppPool\INVC-Web] FOR LOGIN [IIS AppPool\INVC-Web];
GO

-- Read-only role membership
ALTER ROLE db_datareader ADD MEMBER [IIS AppPool\INVC-Web];
GO

-- Defense in depth: explicit deny of all writes/execute, even though the app never issues them
DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [IIS AppPool\INVC-Web];
GO
```

Verify afterward:
```sql
SELECT name, type_desc, is_disabled FROM sys.server_principals WHERE name = 'IIS AppPool\INVC-Web';
SELECT dp.permission_name, dp.state_desc, o.name AS schema_or_object
FROM sys.database_permissions dp
JOIN sys.database_principals p ON p.principal_id = dp.grantee_principal_id
LEFT JOIN sys.objects o ON o.object_id = dp.major_id
WHERE p.name = 'IIS AppPool\INVC-Web';
```
Nothing beyond `CREATE LOGIN`/`CREATE USER`/`ALTER ROLE ADD MEMBER`/`DENY` on `SCHEMA::dbo` is authorized — no `db_owner`, `db_datawriter`,
`sysadmin`, `CONTROL`, `ALTER`, `IMPERSONATE`, table/view/procedure/function/trigger/index/constraint DDL, compatibility level or database
option changes, and no use of `sa`.

---

## Part 3 — Administrator: IIS app pool, site, certificate, bindings

App pool:
```powershell
Import-Module WebAdministration
New-WebAppPool -Name "INVC-Web"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name managedRuntimeVersion -Value ""      # No Managed Code
Set-ItemProperty IIS:\AppPools\INVC-Web -Name managedPipelineMode -Value "Integrated"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name processModel.identityType -Value "ApplicationPoolIdentity"
Set-ItemProperty IIS:\AppPools\INVC-Web -Name enable32BitAppOnWin64 -Value $false
```

Release folder (copy only the audited `.work/release/publish` output produced by this project's `scripts/verify.ps1` →
`publish-iis.ps1` → `test-release-artifact.ps1` → `new-release-manifest.ps1`):
```powershell
New-Item -ItemType Directory -Force D:\Apps\InvcWeb\releases\<release-id>
New-Item -ItemType Directory -Force D:\Apps\InvcWeb\logs
Copy-Item C:\INVC\Web\.work\release\publish\* D:\Apps\InvcWeb\releases\<release-id>\ -Recurse
# Least-privilege ACL: read/execute for the pool identity, no write except logs\
icacls "D:\Apps\InvcWeb\releases\<release-id>" /grant "IIS AppPool\INVC-Web:(OI)(CI)RX" /T
icacls "D:\Apps\InvcWeb\logs" /grant "IIS AppPool\INVC-Web:(OI)(CI)M" /T
```

Site (new site only, does not touch Default Web Site or `/INVC`):
```powershell
New-Website -Name "INVC-Web" -PhysicalPath "D:\Apps\InvcWeb\releases\<release-id>" -ApplicationPool "INVC-Web" `
  -Port 8090 -HostHeader "DESKTOP-BVH8F8L"
```

Certificate (self-signed acceptance cert; SAN must be `DESKTOP-BVH8F8L`; export public only):
```powershell
$cert = New-SelfSignedCertificate -DnsName "DESKTOP-BVH8F8L" -CertStoreLocation Cert:\LocalMachine\My `
  -KeyExportPolicy NonExportable -KeyUsage DigitalSignature,KeyEncipherment `
  -Type SSLServerAuthentication -NotAfter (Get-Date).AddYears(5)
Export-Certificate -Cert $cert -FilePath D:\Apps\InvcWeb\certs\invc-web-acceptance.cer   # public cert only, no .pfx
New-WebBinding -Name "INVC-Web" -Protocol https -Port 8443 -HostHeader "DESKTOP-BVH8F8L"
(Get-WebBinding -Name "INVC-Web" -Protocol https).AddSslCertificate($cert.Thumbprint, "My")
```
Distribute `invc-web-acceptance.cer` to client machines' Trusted Root store for the acceptance test — never distribute a `.pfx`/private key.

Authentication (INVC-Web site only — Default Web Site/`/INVC` untouched):
```powershell
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/windowsAuthentication -Name enabled -Value true -PSPath "IIS:\Sites\INVC-Web"
Set-WebConfigurationProperty -Filter /system.webServer/security/authentication/anonymousAuthentication -Name enabled -Value false -PSPath "IIS:\Sites\INVC-Web"
```

Environment variables (external configuration only — never in a committed file):
```powershell
Set-ItemProperty "IIS:\Sites\INVC-Web" -Name applicationDefaults.preloadEnabled -Value True
Set-WebConfigurationProperty "/system.webServer/aspNetCore/environmentVariables" -PSPath "IIS:\Sites\INVC-Web" -Name "." -Value @{
  name = "ASPNETCORE_ENVIRONMENT"; value = "Production"
}
# repeat -Value @{name="AllowedHosts"; value="DESKTOP-BVH8F8L"}
# repeat -Value @{name="InvDatabase__ConnectionString"; value="Server=DESKTOP-BVH8F8L;Initial Catalog=INV;Integrated Security=True;ApplicationIntent=ReadOnly;Encrypt=True;TrustServerCertificate=True;Application Name=Invc.Web"}
# repeat -Value @{name="ASPNETCORE_HTTPS_PORT"; value="8443"}
```

**Do not**, anywhere in this part: touch Default Web Site, the `/INVC` virtual directory, the `192.168.1.99:8080`/`invc-ksl.ddns.net`
binding, Laragon, port 443, firewall, DNS, DHCP, or create Windows user accounts.

---

## Part 4 — Verification (either role, read-only)

```powershell
Invoke-WebRequest http://localhost/INVC/default.asp -UseBasicParsing | Select-Object StatusCode          # legacy unaffected, expect 200
Invoke-WebRequest http://desktop-bvh8f8l:8090/ -UseBasicParsing -ErrorAction SilentlyContinue             # expect 401 (Windows Auth challenge) before credentials
Invoke-WebRequest https://desktop-bvh8f8l:8443/Health -UseBasicParsing                                    # expect 200, "INVC Web · Production", no technical detail
```
Record: Hosting Bundle SHA-256 + version, release-id + commit SHA + manifest hash, SQL login/permission verification query output,
certificate thumbprint, and the exact commands run, in `docs/iis-deployment-runbook.md` §D-style rollback log.

---

## Rollback (if any step above needs to be undone)

- Hosting Bundle: uninstall via "Apps & Features" (`Microsoft ASP.NET Core 10.0.x - Windows Hosting Bundle`); restarts `W3SVC` again.
- IIS site/pool: `Remove-Website INVC-Web; Remove-WebAppPool INVC-Web` — legacy `/INVC` on Default Web Site is untouched by this.
- Certificate: `Remove-Item Cert:\LocalMachine\My\<thumbprint>`.
- SQL: `DROP USER [IIS AppPool\INVC-Web]` in `INV`, then `DROP LOGIN [IIS AppPool\INVC-Web]` at the server level.
- Release folder: delete `D:\Apps\InvcWeb\releases\<release-id>` (previous releases are kept for exactly this reason).

No database rollback of business data is ever needed — the application only reads.

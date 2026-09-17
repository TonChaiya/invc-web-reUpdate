# IIS deployment runbook — INVC Web (FUTURE — REQUIRES EXPLICIT OWNER APPROVAL for every infrastructure step)

**Status: NOT EXECUTED.** Phase 7 produced the release package; Phase 8 (2026-09-16) discovered the environment read-only and wrote the
design spec `docs/superpowers/specs/2026-09-16-iis-deployment-design.md`. This runbook now carries the discovered facts and the
owner-approved access model. No step in §C–§E has been run. The application reads INV read-only; the database never changes.

## A0. Discovered facts used below (see `docs/phase8-environment-discovery.md`)
| Fact | Value |
|---|---|
| Host | `DESKTOP-BVH8F8L`, Windows 10 Pro 22H2, **WORKGROUP**, Wi-Fi DHCP `192.168.1.10/24`, firewall disabled |
| IIS | 10.0; Default Web Site :80; legacy Classic ASP at **`/invc` → `C:\INVC\Web` (repository working copy, INFERRED)**; Windows Authentication feature installed; no ANCM |
| Ports | 80 IIS · **443 Laragon Apache** · **8080 IIS, owner-confirmed intentional DDNS/port-forward (`invc-ksl.ddns.net`) exposing legacy `/INVC` externally — never modify (see phase8-environment-discovery.md §16)** · 8081 Laragon · 1433 SQL Server |
| SQL Server | 2022 Enterprise Evaluation, default instance, **same machine**, Mixed mode, `INV` ONLINE; logins `sa` + developer (sysadmin); **no read-only principal** |
| HTTPS | no IIS binding; only a SAN-less self-issued certificate (`CN=192.168.1.99, CN=desktop-bvh8f8l`) → unusable |
| Package | `.work/release/publish`, 236 files, 25.6 MB, framework-dependent, audit PASSED |

## A1. Owner-approved access model (design target — NOT applied)
```
AUTHENTICATION TARGET (INVC-Web site only)
  IIS Windows Authentication : ENABLED   (Negotiate/NTLM; NTLM in this workgroup)
  IIS Anonymous Authentication : DISABLED
  ASP.NET Core application login : NONE   (no Login.cshtml / Account pages, no AddAuthentication, no Identity, no passwords stored)
  Authorization : none (all IIS-authenticated users have the same read-only capability)
```
A Windows credential prompt on clients whose account does not match a local account on the host is expected in a workgroup and is an
infrastructure/client-policy matter — it never justifies an application login form.

## A. Prerequisites (all must be true before any deployment step)
| # | Prerequisite | Owner | Status after Phase 8 |
|---|---|---|---|
| 1 | Target host = `DESKTOP-BVH8F8L` (IIS and SQL Server on the same machine) | owner | **CONFIRMED by discovery**; owner to accept Windows 10 Pro/laptop/Wi-Fi/Evaluation-edition risks (OWNER DECISION) |
| 2 | **.NET 10 Hosting Bundle** (ANCM V2 + ASP.NET Core Runtime 10.0.x) installed | administrator | **BLOCKED — not installed** (no `aspnetcorev2.dll`, no machine-wide dotnet) |
| 3 | Certificate for the chosen hostname + HTTPS port decision (443 is held by Laragon Apache → free it or use 8443) | administrator/owner | **BLOCKED — EXTERNAL**; no usable certificate |
| 4 | Access-control model | owner | **APPROVED: IIS Windows Authentication, no application login** (§A1) |
| 5 | Production `AllowedHosts` | owner | proposed `DESKTOP-BVH8F8L;desktop-bvh8f8l.local` — **OWNER DECISION** (keep name, fix address, optional DNS alias) |
| 6 | **Read-only database identity**: recommended `IIS AppPool\INVC-Web` (virtual account of the new pool) as Windows login + `INV` user in `db_datareader` with `DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo`; fallback: SQL login `invc_web_ro` | DBA | **BLOCKED — DBA/ADMIN ACTION REQUIRED** (no such principal exists) |
| 7 | `InvDatabase__ConnectionString` supplied only as an IIS environment variable — Option A shape: `Server=DESKTOP-BVH8F8L;Initial Catalog=INV;Integrated Security=True;ApplicationIntent=ReadOnly;Encrypt=True;TrustServerCertificate=True;Application Name=Invc.Web` (no password) | administrator | pending #6 |
| 8 | Application folder `D:\Apps\InvcWeb\releases\<id>` (D: exists, 29 GB free) — **never** `C:\INVC\Web`, never the legacy folder, never `C:\inetpub\wwwroot` | owner | proposed — folder not created |
| 9 | Filesystem permissions: `IIS AppPool\INVC-Web` read/execute on the release folder; write only on `D:\Apps\InvcWeb\logs\` if file logging is ever enabled | administrator | pending |
| 10 | Backup of `%windir%\System32\inetsrv\config\applicationHost.config` and an **elevated read-only** export of current sites/pools/auth settings | administrator | pending (Phase 8 could not read IIS config unelevated) |
| 11 | Local Windows accounts on the host for INVC users (or a domain join) so that Windows Authentication can succeed | owner/administrator | **OWNER DECISION** |
| 12 | Decision on isolating the legacy `/invc` application from the repository working copy (re-point to a copy / remove after acceptance) | owner | **OWNER DECISION (safety)** |
| 13 | Maintenance / acceptance window; legacy `/invc` stays available as fallback | owner | pending |

## B. Package preparation (inside the project — the only part this project performs)
1. `scripts/verify.ps1` — restore, build (Release), all tests, `git diff --check`.
2. `scripts/publish-iis.ps1` — framework-dependent Release publish to `.work/release/publish` (refuses any path outside the project; shared guard `scripts/project-path-guard.ps1`).
3. `scripts/test-release-artifact.ps1` — fails on legacy ASP, source, symbols, dev config, shipped secrets, wrong web.config.
4. `scripts/new-release-manifest.ps1` — SHA-256 manifest with commit SHA (copied alongside the package for traceability).
5. Published Production-mode smoke and negative smoke (see `docs/phase7-release-readiness.md` §6–7).

## C. Future deployment steps — FUTURE — REQUIRES EXPLICIT OWNER APPROVAL (side-by-side; do not run)
0. Elevated **read-only** confirmation: `Get-Website`, `Get-WebApplication -Site 'Default Web Site'`, `Get-ChildItem IIS:\AppPools`, authentication GETs; back up `applicationHost.config`.
1. Administrator installs the Hosting Bundle (restarts `W3SVC` as part of the installer). Verify `%windir%\System32\inetsrv\aspnetcorev2.dll` exists.
2. Confirm every prerequisite in §A; record the manifest commit SHA of the package to be deployed.
3. Copy the audited `.work/release/publish` contents to **`D:\Apps\InvcWeb\releases\<yyyyMMdd-HHmm>_<sha7>\`** (new folder; never overwrite a live folder); copy `release-manifest.json` next to it; append to `D:\Apps\InvcWeb\RELEASES.md`.
4. Create app pool **`INVC-Web`**: No Managed Code, Integrated, ApplicationPoolIdentity, 32-bit off. (Creating it materialises `IIS AppPool\INVC-Web`.)
5. DBA provisions the read-only principal for `IIS AppPool\INVC-Web` (§A.6) — SQL statements are the DBA's, reviewed, never run by this project.
6. Create site **`INVC-Web`** → physical path `releases\<id>`, HTTP binding on the acceptance port (proposed **8090**), host header `DESKTOP-BVH8F8L`.
7. Set IIS environment variables on the site's `aspNetCore` element / pool: `ASPNETCORE_ENVIRONMENT=Production`, `AllowedHosts=<approved>`, `InvDatabase__ConnectionString=<§A.7>`, and `ASPNETCORE_HTTPS_PORT` if the HTTPS port is not 443.
8. On **INVC-Web only**: enable Windows Authentication, disable Anonymous Authentication (§A1). Default Web Site and `/invc` unchanged.
9. Add the HTTPS binding with the owner-provided certificate (443 or 8443 per decision); keep HTTP only for redirection.
10. Verify: unauthenticated request → 401 challenge; authenticated `/Health` → 200 `INVC Web · Production` without technical details; `/`, `/Inventory/Status`,
    `/Reorder`, `/PurchaseOrders`, `/Receipts` and print pages over HTTPS; security headers; forged `Host` → 400; `Dashboard` figures vs legacy `/invc` reports.
11. Acceptance from ≥ 2 client PCs; record SSO behaviour (transparent vs prompt) per client.
12. Record deployment: release-id, commit SHA, manifest hash, time, operator.

## C2. Cutover (after acceptance; FUTURE — REQUIRES EXPLICIT OWNER APPROVAL)
Preferred reversible method: `httpRedirect` on Default Web Site `/invc` → `https://DESKTOP-BVH8F8L:<port>/` (302). Alternative: move the :80 host
binding to INVC-Web. Never overwrite `/invc`, never delete legacy files. Separately (safety, §A.12): re-point `/invc` to a **copy** of the legacy
folder so the repository working copy stops being a web root.

## D. Rollback — FUTURE — REQUIRES EXPLICIT OWNER APPROVAL (do not run now)
| Checkpoint | Restore | Verify |
|---|---|---|
| R0 | backup of `applicationHost.config`; note current physical paths, bindings, auth values, live release-id | — |
| R1 new site/pool exist | remove only site/pool `INVC-Web` | `/invc` still 200 |
| R2 new release live | re-point INVC-Web physical path to the previous `releases\<id>`; recycle pool | `/Health` 200, manifest SHA of the previous release |
| R3 auth changed | restore auth values on INVC-Web from R0 (new site only) | 401/200 behaviour as before |
| R4 HTTPS binding | remove binding (certificate stays) | HTTP acceptance URL works |
| R5 cutover redirect | remove `httpRedirect` on `/invc` (or move the binding back) | `http://DESKTOP-BVH8F8L/invc/` serves legacy |
No database rollback is needed (read-only application). Record every rollback (which release-id is live, why, when, who).

## E. Explicitly out of scope for this project
Installing runtimes/bundles, creating IIS sites/pools/bindings, installing certificates, enabling/disabling authentication, creating SQL logins/users or
granting permissions, creating local Windows accounts, copying files to any web root, `appcmd`/`msdeploy`/IIS PowerShell **write** operations,
`iisreset`, service restarts, Laragon/Apache, firewall, DNS, registry, Access frontends. Phase 9 may perform the items of spec §23 only after the
owner approves the spec and prerequisites §A.2, §A.3 and §A.6 are completed by the administrator/DBA.

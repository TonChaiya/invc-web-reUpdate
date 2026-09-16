# IIS deployment runbook — INVC Web (FUTURE, owner-approved deployment only)

**Status: NOT EXECUTED.** This document describes how the release package produced in Phase 7 would be deployed and rolled back.
Every step below changes infrastructure and therefore requires explicit owner approval and an approved maintenance window.
No step has been run against this machine. The application reads INV read-only; the database never changes.

## A. Prerequisites (all must be true before any deployment step)
| # | Prerequisite | Owner | Status |
|---|---|---|---|
| 1 | Target Windows/IIS host identified (this machine `DESKTOP-BVH8F8L` or another) | owner | OWNER DECISION |
| 2 | **.NET 10 Hosting Bundle** (ASP.NET Core Module V2 + runtime 10.0.x) installed on the host | administrator | EXTERNAL — not verified/installed |
| 3 | HTTPS certificate obtained and an HTTPS binding planned; HTTP→HTTPS redirect + HSTS are enforced by the app once the binding exists | administrator | EXTERNAL |
| 4 | Access-control model decided: IIS Windows Authentication (recommended for intranet) or another approved mechanism; the app has no built-in login | owner | OWNER DECISION |
| 5 | Production `AllowedHosts` value (host name(s) users will type) | owner | OWNER DECISION |
| 6 | **Read-only database identity**: either the app-pool Windows identity granted `db_datareader` on `INV`, or a dedicated SQL login (e.g. `invc_web_ro`) with `db_datareader` + `DENY INSERT/UPDATE/DELETE/EXECUTE` — provisioned by the DBA, never by the app or this project | DBA | **BLOCKED until provisioned** |
| 7 | `InvDatabase__ConnectionString` supplied externally (IIS `environmentVariables` on the site/app pool) — never in a file in the repository; never `sa` | administrator | pending #6 |
| 8 | Application folder chosen (e.g. `D:\Apps\InvcWeb\current`) — **not** the legacy Classic ASP folder and **not** `C:\INVC\Web` | owner | OWNER DECISION |
| 9 | Filesystem permissions: app-pool identity read/execute on the app folder; write only if stdout logging is later enabled to a `logs\` folder | administrator | pending |
| 10 | Backup of any existing site/config (`%windir%\System32\inetsrv\config\applicationHost.config`, current site folder) | administrator | pending |
| 11 | Maintenance / cutover window agreed; legacy Classic ASP site remains available as fallback | owner | pending |

## B. Package preparation (inside the project — the only part this project performs)
1. `scripts\verify.ps1` — restore, build (Release), all tests, `git diff --check`.
2. `scripts\publish-iis.ps1` — framework-dependent Release publish to `.work\release\publish` (refuses any path outside the project).
3. `scripts\test-release-artifact.ps1` — fails on legacy ASP, source, symbols, dev config, shipped secrets, wrong web.config.
4. `scripts\new-release-manifest.ps1` — SHA-256 manifest with commit SHA (keep alongside the package for traceability).
5. Published Production-mode smoke and negative smoke (see `docs/phase7-release-readiness.md` §6–7).

## C. Future deployment steps (high level — DO NOT RUN without approval)
1. Confirm every prerequisite in §A; record the manifest commit SHA of the package to be deployed.
2. Copy the audited `.work\release\publish` contents to a **new versioned folder** on the host (e.g. `…\InvcWeb\releases\<sha>`); never overwrite the running folder.
3. Create/verify the IIS application pool: **No Managed Code**, identity per §A.4/§A.6, `Start Mode` as required.
4. Create the IIS site/application pointing at the versioned folder (or repoint the physical path of an existing site).
5. Set `ASPNETCORE_ENVIRONMENT=Production`, `AllowedHosts=<hosts>`, `InvDatabase__ConnectionString=<read-only identity>` as IIS environment variables (app pool / site level).
6. Add the HTTPS binding with the certificate; keep an HTTP binding only for redirection.
7. Apply the chosen access control (e.g. enable Windows Authentication, disable Anonymous) in IIS.
8. Start the site; verify `/Health` returns 200 and shows `INVC Web · Production` with no server/login details; verify `/`, `/Inventory/Status`,
   `/Reorder`, `/PurchaseOrders`, `/Receipts` over HTTPS; verify the response headers; verify a forged Host header is rejected (400).
9. Record deployment: commit SHA, manifest hash, time, operator.

## D. Rollback (future — DO NOT RUN now)
1. Before any deployment: keep the previously deployed package folder intact; export/backup IIS configuration; note the current physical path and the commit SHA/manifest of the running package.
2. To roll back: stop or recycle the app pool **only within the approved window**; repoint the site's physical path to the previous versioned folder
   (or restore the backed-up folder); restore the backed-up IIS configuration if bindings/pool settings were changed; start the pool.
3. Re-run the smoke checks of §C.8 against the rolled-back site.
4. Record the rollback (which SHA is now live, why, when, who).

## E. Explicitly out of scope for this project
Installing runtimes/bundles, creating IIS sites/pools/bindings, installing certificates, enabling authentication, creating SQL logins/users or
granting permissions, copying files to `inetpub` or any web root, `appcmd`/`msdeploy`/IIS PowerShell modules, `iisreset`, service restarts.

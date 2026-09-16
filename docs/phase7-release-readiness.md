# Phase 7 — Release readiness + security hardening (PACKAGE readiness only — nothing deployed)

Base commit `f0427fd` · executed **2026-09-16** · this phase proves that a release **package** can be produced, audited and
smoke-tested inside the project. IIS, Windows, SQL Server and Access were not touched. **The system is not deployed.**

## 1. Security review of the NEW application (src/, tests/, scripts/)
| Area | Finding | Status |
|---|---|---|
| SQL construction | Every statement is a constant `SELECT` passed through `ReadOnlySql.Ensure`; parameters via Dapper; no request value is concatenated | ✅ verified by `ReadOnlySqlReleaseAssertionTests` (33 statements) and a source scan for write SQL / credential literals |
| Connection identity | `ReadOnlySqlConnectionFactory` forces `ApplicationIntent=ReadOnly`, disables MARS, sets the application name, refuses `sa`, requires Initial Catalog; a configured `ReadWrite` intent cannot override it | ✅ tests |
| Configuration at startup | `ProductionConfigurationValidator` fails fast on missing/malformed connection string, `sa`, missing database, and (Production only) wildcard/missing `AllowedHosts`. Shape only — never opens a connection, so a DB outage cannot block startup | ✅ tests + published run |
| Error rendering | `UseExceptionHandler("/Error")` + HSTS outside Development; Thai error page shows only a Request ID | ✅ |
| /Health disclosure | Production shows status / elapsed / "INVC Web · Production"; server, database, login, SQL version and raw errors appear in Development only; unhealthy → 503 | ✅ tests + negative smoke |
| Security headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin`, `X-Frame-Options: SAMEORIGIN`, `Permissions-Policy: camera=(), microphone=(), geolocation=()` (no duplicates if the host sets them) | ✅ tests + smoke |
| Content-Security-Policy | **Not set** — data-driven `style` attributes (composition/trend bars) and the Bootstrap bundle need a verified policy | 📝 future hardening |
| Logging | Built-in Microsoft logging; exceptions logged server-side with message templates; no connection strings or secrets logged; Production levels: Default Warning, Invc Information, AspNetCore Warning | ✅ |
| HTML output | Razor encoding everywhere; no `Html.Raw` | ✅ |
| Filesystem writes | none from the application; scripts write only under `.work\` | ✅ |
| Legacy Classic ASP vulnerabilities (Phase 0) | addressed by **artifact isolation**: the package is built from `src/Invc.Web` and contains no `.asp`, `Connections/`, `_mmServerScripts/`, archives or source | ✅ audit script |

## 2. Production configuration
`appsettings.json` (base, empty connection string) + `appsettings.Production.json` (non-secret logging defaults; `DetailedErrors=false`).
Deployment **must** supply, outside the repository (IIS `environmentVariables` or machine-level for the app pool):
`InvDatabase__ConnectionString` (dedicated read-only identity — never `sa`) and `AllowedHosts` (explicit host names).
`appsettings.Development.json` stays in the repository for developers and is excluded from publish (`CopyToPublishDirectory=Never`).

## 3. Publish isolation and artifact audit (`scripts/publish-iis.ps1` → `scripts/test-release-artifact.ps1`)
Package: `dotnet publish src/Invc.Web/Invc.Web.csproj -c Release` (framework-dependent, portable) → `.work\release\publish` (git-ignored);
the script **refuses** any output path outside `C:\INVC\Web` (demonstrated with `C:\inetpub\wwwroot\invc` → exit 2, nothing created; unit-tested).
Baseline (before Phase 7) shipped `appsettings.Development.json` and three `.pdb` files; the Phase 7 package (236 files) contains
**none** of: `*.asp *.mdb *.accdb *.rar *.zip *.7z *.cs *.csproj *.sln *.slnx *.pdb *.mno *.inc appsettings.Development.json`,
nor the `Connections/`, `_mmServerScripts/`, `_notes/`, `tests/`, `docs/`, `scripts/`, `Login_v4/` directories.
Required files present: `Invc.Web.dll`, `Invc.Core.dll`, `Invc.Infrastructure.dll`, `appsettings.json`, `appsettings.Production.json`,
`web.config`, `Invc.Web.runtimeconfig.json`, `wwwroot/css/site.css`, `wwwroot/css/print.css`, Bootstrap CSS.
**Classic ASP exclusion = RELEASE GATE** (audit fails on any `.asp` or `_mmServerScripts`).

## 4. Generated IIS web.config (reviewed, not deployed)
ASP.NET Core hosting configuration only: `AspNetCoreModuleV2` handler, `processPath="dotnet" arguments=".\Invc.Web.dll"`, in-process,
stdout logging disabled. No `httpErrors`, no Classic ASP handlers, no `_mmServerScripts`, no credentials (audit-checked).

## 5. Release manifest (`scripts/new-release-manifest.ps1` → `.work\release\manifest\release-manifest.json`, not committed)
Records application, commit SHA, working-tree dirty flag, UTC/local timestamps, target framework (`net10.0`), runtime frameworks,
publish mode, file count/bytes and SHA-256 per file — a deployed package can be matched back to an exact commit.

## 6. Published Production-mode smoke (process-local env only: `ASPNETCORE_ENVIRONMENT=Production`, `AllowedHosts=localhost;127.0.0.1`, read-only Windows-auth connection)
`/`, `/Health`, `/Inventory/Status`, `/Inventory/Detail/1001250`, `/Reorder`, `/Reorder/Print`, `/PurchaseOrders`, `/PurchaseOrders/Detail/K6900001`,
`/Receipts`, `/Receipts/Detail/O6900085`, `site.css`, `print.css`, Bootstrap → **200**; unknown route 404; forged `Host: evil.example` → **400**
(host filtering active); security headers present; Thai UTF-8 intact; `appsettings.Development.json` absent from the directory;
Production environment confirmed on `/Health`; no exception text in any page. HTTPS-redirect port warning is expected without an HTTPS binding (IIS supplies it).

## 7. Negative smoke (published process, unreachable endpoint `127.0.0.1,1`, fake login/password, 2 s timeout — real database untouched)
`/Health` → **503**; `/` and `/Inventory/Status` → 200 with the safe unavailable states. Pages contain **none** of: the endpoint,
the login, the password, `SqlException`, stack frames, `Connect Timeout`, `Server=`. Server log contains the real `SqlException` (7 entries).

## 8. Startup fail-fast (published, Production)
Missing connection string / wildcard `AllowedHosts` / `sa` login → process refuses to start with a clear single-line reason.

## 9. Read-only SQL release assertion
33 application statements (Inventory 6, Reorder 2, PO 9, Receipts 6, Dashboard 9, health 1) accepted by `ReadOnlySql.Ensure`; regex
scan finds no DML/DDL keywords; source scan finds no write SQL or credential literals. `ReadOnlySql` itself unchanged.

## 10. Release-mode dashboard performance
Published Release process, warm: `/` **111 ms**, module pages 12–50 ms, static 2–7 ms; still 15–17 sequential SELECTs per dashboard request.
Acceptable for this dataset — **no cache, fan-out or index introduced** (Phase 6 decision stands).

## 11. Known risks (external infrastructure — not application defects)
1. **Production database identity**: only `sa` and Windows logins exist on the instance (Phase 0). A read-only identity (IIS app-pool Windows
   identity with `db_datareader`, or a dedicated SQL login) must be provisioned by an administrator. Not done here.
2. **.NET 10 Hosting Bundle / ANCM** must be installed on the IIS host (not present/verified on this machine; not installed by this phase).
3. **HTTPS certificate and binding** are an IIS/server task; the app enforces redirection + HSTS only once a binding exists.
4. **Access control**: the application has **no authentication layer**. Suitable only for a restricted intranet deployment unless
   IIS/Windows authentication (or another owner-approved mechanism) is applied in front of it.
5. **SQL Server 2022 Enterprise Evaluation** (installed ~2026-07-27) expires after 180 days (Phase 0 finding).
6. INV has entered `RESTORING` state several times during Phases 2–5 (external); the app degrades safely (503 / unavailable states).

## 12. Release checklist
| Item | Classification |
|---|---|
| App build / tests (384) | **READY** |
| Publish isolation (no legacy ASP, source, symbols, dev config) | **READY** (audit passes) |
| Generated web.config | **READY** |
| Production configuration validation / fail-fast | **READY** |
| Health redaction, error page, security headers | **READY** |
| Read-only SQL guarantee | **READY** |
| Database schema | **NO CHANGE REQUIRED** |
| Read-only production DB identity + `InvDatabase__ConnectionString` | **BLOCKED** until provisioned/confirmed externally |
| Production `AllowedHosts` value | **OWNER DECISION REQUIRED** (host name) |
| IIS access-control model (Windows auth / other) | **OWNER DECISION REQUIRED** |
| .NET 10 Hosting Bundle on target | **EXTERNAL PREREQUISITE** |
| HTTPS certificate + binding | **EXTERNAL PREREQUISITE** |
| Content-Security-Policy | **FUTURE HARDENING** |

## 13. Release verdict
**PACKAGE READY — DEPLOYMENT BLOCKED** on external prerequisites (read-only DB identity, hosting bundle, HTTPS, access-control decision, AllowedHosts).
The runbook for the future deployment and rollback is `docs/iis-deployment-runbook.md`. Nothing was deployed in Phase 7.

## 14. Tooling added
`scripts/dev.ps1`, `scripts/verify.ps1` (`-SkipIntegration` optional), `scripts/publish-iis.ps1` (package only, path guard, `-GuardOnly`),
`scripts/test-release-artifact.ps1`, `scripts/new-release-manifest.ps1`. Pipeline order: verify → publish → audit → Production smoke →
negative smoke → manifest → `git diff --check`.

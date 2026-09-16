# Development setup (Phase 1)

## Portable SDK
The machine has no system-wide .NET. The SDK is unpacked inside the project:

| Item | Value |
|---|---|
| Package | `dotnet-sdk-10.0.401-win-x64.zip` from `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.401/` |
| SHA-512 | `24b670ad3d923bfcf47df6c3b034152398b42f6dbc388e10d783aee1cfb5e5817d399fc0ae2a12cfa822a55e61d34830ccb15c50ef6efee437ab874bb7c79430` (matches Microsoft's `checksums/10.0.12-sha.txt`) |
| Location | `C:\INVC\Web\.work\dotnet\` (git-ignored; zip kept in `.work\downloads\`) |
| Runtimes | Microsoft.NETCore.App / AspNetCore.App 10.0.12 |

Re-creating it on another machine: download the same zip, verify the hash, `Expand-Archive` into `.work\dotnet`.

## Always use the wrapper
`scripts\dotnet-local.ps1 <dotnet args>` sets, for the current process only:
`DOTNET_ROOT`, `DOTNET_CLI_HOME`, `NUGET_PACKAGES`, `NUGET_HTTP_CACHE_PATH`, `NUGET_SCRATCH`, `TEMP`, `TMP`
→ all under `.work\`, plus `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`, telemetry opt-out, no first-run experience,
no global tools on PATH. Nothing is persisted to user or machine environment.

Known limitation: NuGet still creates its **user-level** defaults on first use —
`%APPDATA%\NuGet\NuGet.Config` (default nuget.org source) and `%LOCALAPPDATA%\NuGet\Migrations\1` (empty marker).
There is no environment variable that redirects these. They were created once on 2026-09-16 and are reported in the
Phase 1 safety report; they are not modified by this project afterwards.

## Common commands
```powershell
.\scripts\dotnet-local.ps1 build Invc.slnx
.\scripts\dotnet-local.ps1 test  Invc.slnx
.\scripts\dev.ps1                       # run the site (Development) on http://127.0.0.1:5265
.\scripts\verify.ps1                    # info, restore, build (Release), all tests, git diff --check
.\scripts\publish-iis.ps1               # package only -> .work
elease\publish (never deploys)
.\scripts	est-release-artifact.ps1     # audit the package
.\scripts
ew-release-manifest.ps1      # SHA-256 manifest -> .work
elease\manifest
```
HTTPS is not configured in development (no dev certificate is generated); use the http profile.

## Database configuration (READ ONLY)
Section `InvDatabase` in `appsettings*.json`:

| Key | Meaning |
|---|---|
| `ConnectionString` | Base connection string. **Never `sa`, never committed with a password.** |
| `CommandTimeoutSeconds` | Query timeout (default 30). |

`appsettings.Development.json` uses Windows authentication to the local default instance
(`Server=.;Database=INV;Integrated Security=true;Encrypt=false;TrustServerCertificate=true`).
Override without editing tracked files:
```powershell
$env:InvDatabase__ConnectionString = "Server=<host>;Database=INV;User ID=<readonly-login>;Password=<…>;Encrypt=true"
```
or create the git-ignored `appsettings.Development.local.json` / use `dotnet user-secrets` (project-local via `DOTNET_CLI_HOME`).

`ReadOnlySqlConnectionFactory` always adds `ApplicationIntent=ReadOnly`, `ApplicationName=INVC Web (read-only)`,
disables MARS, and throws if the login is `sa` or the database is missing.

**BLOCKED (owner action):** a dedicated read-only SQL login does not exist yet — the instance has only `sa` and Windows
logins. Recommended (to be executed by the owner/DBA, never by this project):
```sql
-- for reference only — NOT executed by the project
CREATE LOGIN invc_web_ro WITH PASSWORD = '<strong password>', CHECK_POLICY = ON;
USE INV; CREATE USER invc_web_ro FOR LOGIN invc_web_ro;
ALTER ROLE db_datareader ADD MEMBER invc_web_ro;
DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO invc_web_ro;
```

## Tests
- `tests\Invc.UnitTests` — no database. Rules ported from legacy ASP, `ReadOnlySql` guard, connection-string policy, LIKE escaping.
- `tests\Invc.IntegrationTests` — **read-only** against INV. Uses `INVC_TEST_CONNECTION` if set, otherwise the Windows-auth
  default with a 5 s connect timeout. When the server is unreachable every test is *skipped* (`Xunit.SkippableFact`).
  They never create fixtures, seed, clean up, or open transactions. Included parity checks: A1–A10 (see
  `docs/phase2-inventory-parity.md`); borrow-rule scenarios use table-value constructors (`VALUES`) so nothing is written.

## Git
Repository root is `C:\INVC\Web` (remote `TonChaiya/invc-web-reUpdate`). `.gitignore` excludes `.work/`, build output and
local secret files. The legacy Classic ASP baseline (including its `Connections/` files and archives) is tracked as it was in the
original upload, by owner decision; `.gitattributes` stores text as LF and marks archives/images binary.
Repository-local git config only; global git config is never changed.

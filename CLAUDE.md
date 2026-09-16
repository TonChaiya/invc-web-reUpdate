# INVC Web — project guide for Claude Code

Pharmacy/medical-supply inventory reporting for a Thai CUP sub-unit store. The new application is an
ASP.NET Core 10 Razor Pages site that **reads** the existing production SQL Server database `INV`
(the operational store behind the Microsoft Access "INVC" frontend). The legacy Classic ASP site in this
same directory is kept as reference only.

## Absolute boundaries (owner-mandated — never relax)
- Modify files **only** under `C:\INVC\Web`. Nothing else: no `C:\INVC\*.mdb`, IIS, services, registry, machine/user env vars, global NuGet/git config.
- SQL Server `INV` is **STRICTLY READ-ONLY**: `SELECT` and catalog queries only. No DML/DDL, no logins/permissions, no migrations, no seeding, no auto-initialisation. The app adapts to the schema; never the reverse. If a change seems needed → document as BLOCKED / DESIGN ISSUE.
- Never connect as `sa` (the connection factory refuses it). Never commit credentials.
- Unverifiable facts are marked **UNRESOLVED**, never guessed.
- Work one phase at a time; end each phase with build/test results, git status and the safety report, then HARD STOP.

## Toolchain (portable, project-local)
- .NET SDK **10.0.401** lives in `.work\dotnet\` (git-ignored). No machine-wide SDK exists.
- Always invoke through `scripts\dotnet-local.ps1` (sets `DOTNET_ROOT`, `DOTNET_CLI_HOME`, `NUGET_*`, `TEMP` to `.work\…` for the process only):
  - build: `.\scripts\dotnet-local.ps1 build Invc.slnx`
  - tests: `.\scripts\dotnet-local.ps1 test Invc.slnx`
  - run:   `.\scripts\dotnet-local.ps1 run --project src\Invc.Web`
- `global.json` pins SDK 10.0.401 (`rollForward: latestPatch`).
- No `dotnet workload install`, no global tools.

## Layout
```
Invc.slnx
src/Invc.Core            domain records, business rules (ported verbatim from legacy ASP), repository interfaces
src/Invc.Infrastructure  Dapper + Microsoft.Data.SqlClient; ReadOnlySqlConnectionFactory, ReadOnlySql guard, SQL text
src/Invc.Web             Razor Pages (th-TH culture). Pages: Index, Inventory/Status, Inventory/Detail/{code}, Health
tests/Invc.UnitTests     offline tests (rules, SQL guard, connection-string policy)
tests/Invc.IntegrationTests  read-only parity tests against INV; auto-skip when unreachable
docs/                    Phase 0 audit + business rules + parity matrix + setup
scripts/                 dotnet-local.ps1 and future helpers (never executed against infrastructure)
legacy *.asp, Connections/, css/, js/…   legacy Classic ASP (Windows-874 encoding) — reference only
```

## Data access conventions
- Every SQL string passes `ReadOnlySql.Ensure()` and is a single `SELECT`; parameters via Dapper (`@Name`, `DbString` for nvarchar).
- Explicit column lists (INV_MD has an `nvarchar(max)` column); no `SELECT *`.
- Reproduce legacy query semantics first (see `docs/legacy-query-map.md`), then document any deliberate deviation in the SQL class comment.
- `COMPANY.COMPANY_CODE` and the `DRUG_VN` key are not unique in production: use `OUTER APPLY (SELECT TOP 1 …)` for name lookups, never a plain LEFT JOIN that can multiply rows.
- Legacy relationships have no FKs; model them in SQL (`MS_PO_C.PO_NO = MS_PO.PO_NO`, `MS_IVO.PO_NO = MS_PO.REAL_PO`, fiscal year = `LEFT(PO_NO,2)`).
- DB compatibility level is 100: no `TRY_CAST`, `STRING_AGG`, `OPENJSON`, `FORMAT`.

## Key docs
`docs/data-source-map.md` (schema + evidence), `docs/inventory-business-rules.md`, `docs/purchase-order-business-rules.md`,
`docs/report-parity-matrix.md`, `docs/phase2-inventory-parity.md` (executed A1–A10 + deviation decisions),
`docs/phase0-security-and-data-access.md`, `docs/development-setup.md`.

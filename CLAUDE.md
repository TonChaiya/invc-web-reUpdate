# INVC Web — project guide for Claude Code

Pharmacy/medical-supply inventory reporting for a Thai CUP sub-unit store. The new application is an
ASP.NET Core 10 Razor Pages site that **reads** the existing production SQL Server database `INV`
(the operational store behind the Microsoft Access "INVC" frontend). The legacy Classic ASP site in this
same directory is kept as reference only.

## Absolute boundaries (owner-mandated — never relax)
- Modify files **only** under `C:\INVC\Web`. Nothing else: no `C:\INVC\*.mdb`, IIS, services, registry, machine/user env vars, global NuGet/git config.
- SQL Server `INV` is **STRICTLY READ-ONLY**: `SELECT` and catalog queries only. No DML/DDL, no logins/permissions, no migrations, no seeding, no auto-initialisation. The app adapts to the schema; never the reverse. If a change seems needed → document as BLOCKED / DESIGN ISSUE.
- **Application-owned MySQL `invc_web`** (Laragon MySQL, section `AppDatabase`) is the only writable store: the Borrow module mirrors INV type-09 receipts there and the append-only return trail `borrow_return_event` (migration 002) lives there. Writes go only through `IAppDbConnectionFactory`/`BorrowMirrorRepository`/`BorrowReturnRepository` (MySqlConnector); schema changes only via versioned `db/mysql/NNN_*.sql` applied with `scripts/mysql-migrate.ps1` (never at startup, never DROP). This does **not** relax anything above — INV stays SELECT-only and the MySQL factory refuses connection strings that name `INV`, use SQL Server keywords or `sa`. Never commit the MySQL connection string (use `appsettings.{Env}.local.json`, git-ignored).
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
- Release tooling (package only, never deploys): `scripts/verify.ps1` → `scripts/publish-iis.ps1` (refuses output outside the project) → `scripts/test-release-artifact.ps1` → `scripts/new-release-manifest.ps1`; all three dot-source `scripts/project-path-guard.ps1` (exact normalized-descendant check, reparse points refused); artifacts live in `.work/release/` (git-ignored). `scripts/dev.ps1` starts the site locally.

## Layout
```
Invc.slnx
src/Invc.Core            domain records, business rules (ported verbatim from legacy ASP), repository interfaces
                         Inventory/ (status, detail, InventoryRules) · Reorder/ (ReorderItem, ReorderReport) · PurchaseOrders/ (rules, report, detail) · Receipts/ (non-PO receipts: rules, report, detail) · Dashboard/ (DashboardService composes the other modules; DashboardRules only for legacy KPIs without an owner)
src/Invc.Infrastructure  Dapper + Microsoft.Data.SqlClient; ReadOnlySqlConnectionFactory, ReadOnlySql guard, SQL text
                         AppData/ (MySQL factory + options + AppDatabaseHealth) · Borrow/ (BorrowSourceRepository + BorrowReceiptAdvisoryRepository = INV SELECT, BorrowMirrorRepository + BorrowReturnRepository = MySQL DML)
src/Invc.Web             Razor Pages (th-TH culture). Pages: Index (= operational Dashboard), Inventory/Status, Inventory/Detail/{code}, Reorder, Reorder/Print, PurchaseOrders, PurchaseOrders/Detail|Print/{realPo}, Receipts, Receipts/Detail|Print/{receiveNo}, Borrow, Borrow/Outstanding (สรุปคงค้าง), Borrow/Facility/{facilityCode}, Borrow/Return/{item}, Borrow/ReturnBill/{bill}, Borrow/History, Borrow/Correct/{event}, Health
tests/Invc.UnitTests     offline tests (rules, SQL guard, connection-string policy)
tests/Invc.IntegrationTests  read-only parity tests against INV; auto-skip when unreachable
docs/                    Phase 0 audit + business rules + parity matrix + setup
scripts/                 dotnet-local.ps1 and future helpers (never executed against infrastructure)
old-code/                (git-ignored) local archive of the legacy Classic ASP site (Windows-874) — reference only, never built/published;
                         the legacy /INVC runtime copy lives in D:\Apps\InvcLegacy\www since 2026-09-22
```

## Data access conventions
- Every SQL string passes `ReadOnlySql.Ensure()` and is a single `SELECT`; parameters via Dapper (`@Name`, `DbString` for nvarchar).
- Explicit column lists (INV_MD has an `nvarchar(max)` column); no `SELECT *`.
- Reproduce legacy query semantics first (see `docs/legacy-query-map.md`), then document any deliberate deviation in the SQL class comment.
- `COMPANY.COMPANY_CODE` and the `DRUG_VN` key are not unique in production: use `OUTER APPLY (SELECT TOP 1 …)` for name lookups, never a plain LEFT JOIN that can multiply rows.
- Legacy relationships have no FKs; model them in SQL (`MS_PO_C.PO_NO = MS_PO.PO_NO`, `MS_IVO.PO_NO = MS_PO.REAL_PO`, `MS_IVO_C.RECEIVE_NO = MS_IVO.RECEIVE_NO`, fiscal year = first two digits of PO_NO passed as a `LIKE @PrefixPattern` parameter). PO status buckets live only in `PurchaseOrderRules`; never encode them in SQL. `OTH_IVO/OTH_IVOC` (non-PO receipts) are NOT purchase orders — keep them out of the PO module.
- Non-PO receipts: header key `OTH_IVO.RECEIVE_NO`, lines `OTH_IVOC.RECEIVE_NO` (proven 1:N, `INVOICE_NO` is NOT unique); type `RTRIM(RCV_TYPE)` → `RCV_TYPE.RCV_TYPE_CODE` (LEFT JOIN); source `DPT_CODE` → `COMPANY` via OUTER APPLY TOP 1; fiscal year = Thai FY of `DATE_RECEIVE` (typed date window, never the PO prefix rule); `UNIT_VALUE` is price per pack and `TOTAL_VALUE = Σ UNIT_VALUE×QTY_ORDER/PACK_RATIO`; `TOTAL_COST`/`BUY_UNIT_COST` are always 0 — never use them.
- Reorder rules live only in `InventoryRules` (Core); SQL reads raw MIN_LEVEL/REORDER_QTY/MAX_LEVEL — never classify in SQL. `INV_Report_Purchase*.asp` is a reorder report, not a PO report; keep the "คำแนะนำการสั่งซื้อ" wording.
- Dashboard rule: the root page composes `InventorySummary`, `ReorderReport`, `PurchaseOrderReport` and `ReceiptReport`; dashboard-only SQL (`DashboardSql`) exists solely for BUDGET, SUBSTOCK, MBS_RE_M/MNTH_SUM coverage, legacy ED/NED filter, Agreement, CARD movement/item trend and PO process time. Never re-encode module rules there. Main-store **value** on the dashboard = latest processed month-end (Σ `MNTH_SUM.TOTAL_VALUE` of the newest MBS_RE_M period, the same figure as the last row of ความเคลื่อนไหวรายเดือน) — owner rule 2026-09-22, because the live `INV_MD.TOTAL_VALUE` snapshot diverges from the processed ledger until the known valuation defects are repaired; item count / units stay the Phase 2 active-item definition. Every dashboard section is isolated via `DashboardSection<T>`.
- Production hardening rules: every deployment value (`InvDatabase__ConnectionString`, `AllowedHosts`) comes from external configuration — never a committed file; `ProductionConfigurationValidator` fails startup on missing/`sa`/wildcard-host config (shape only, no DB call); `/Health` shows technical details in Development only; `SecurityHeaders` middleware owns the response headers; `appsettings.Development.json` and `.pdb` never enter the publish output; the package is built from `src/Invc.Web` only, so legacy ASP is excluded by construction. The app has no authentication layer — access control is an IIS/owner decision (see `docs/iis-deployment-runbook.md`).
- Deployment rules (owner-approved, Phase 8): access control = **IIS Windows Authentication, no application login page, no ASP.NET Core Identity/cookie auth, no user/role tables** — never add `AddAuthentication`/login pages; the app uses one read-only app-pool identity to SQL (never impersonates users, never `sa`). Production web root is a versioned folder (`D:/Apps/InvcWeb/releases/<id>`), **never `C:/INVC/Web`** — the legacy `/INVC` virtual directory is served from `D:/Apps/InvcLegacy/www` (re-pointed 2026-09-22 with owner approval; the working copy is no longer a web root). Design: `docs/superpowers/specs/2026-09-16-iis-deployment-design.md`; facts: `docs/phase8-environment-discovery.md`.
- DB compatibility level is 100: no `TRY_CAST`, `STRING_AGG`, `OPENJSON`, `FORMAT`.

## UX / information-architecture rules (owner-mandated, 2026-09-18)
- **ONE CONCEPT = ONE PRIMARY CONTROL.** If a page has a dropdown/select/filter for a category, status or type, never repeat the same choices underneath as a card row, a table of every category, a chip group or a full breakdown unless that second view serves a clearly different operational purpose. After a selection show only: selected context, result count, one useful aggregate, the actual result list. A still-useful complete breakdown goes behind a collapsed secondary disclosure ("ดูสรุปทุกประเภท" / "ดูสรุปทุกหมวด") that never competes with the work area.
- Every page: the primary job the user came for owns the main visual area; supporting statistics and technical explanations go below or behind disclosure. Simplify presentation first — never drop business logic or queries for appearance.
- Dashboard priority: (1) needs attention/action now, (2) essential KPIs, (3) workflow state, (4) analytical/supporting; the same measure must not appear twice at equal visual priority.
- Inventory: search/filter → stock rows → drug detail; category/account dropdown must not be duplicated by permanent per-category cards/tables (compact summary of the selected category only).
- Reorder: the status filter strip is the single selector (no duplicate dropdown); priority ต้องสั่งซื้อทันที → ใกล้ถึงจุดสั่งซื้อ → มีสำรอง.
- Purchase Orders: bucket navigation stays the single selector (no status cards + selector for the same buckets); active/incomplete work before closed history.
- Receipts (known redundancy): keep the "ประเภทการรับ" dropdown primary; result summary + receipt list below; the TypeBreakdown table moves behind collapsed "ดูสรุปทุกประเภท" or is omitted.
- Borrow: primary = ค้างคืน / ต้องตรวจสอบ; คืนแล้ว / history is secondary and never equally prominent.
- Migration cadence: ONE module per checkpoint (Borrow → Inventory Status+Detail → Reorder → PO → Receipts → Dashboard → polish); owner reviews each before the next.

## Key docs
`docs/data-source-map.md` (schema + evidence), `docs/inventory-business-rules.md`, `docs/purchase-order-business-rules.md`,
`docs/report-parity-matrix.md`, `docs/phase2-inventory-parity.md` (A1–A10), `docs/phase3-reorder-parity.md` (B1–B6), `docs/phase4-purchase-order-parity.md` (C1–C9), `docs/phase5-receipts-data-map.md` + `docs/phase5-receipts-parity.md` (C10a–C10j), `docs/phase6-dashboard-parity.md` (D1–D10, C11), `docs/phase7-release-readiness.md`, `docs/iis-deployment-runbook.md`,
`docs/phase0-security-and-data-access.md`, `docs/development-setup.md`, `docs/borrow-data-map.md` (Borrow: type-09 source, MySQL mirror, reconciliation), `docs/borrow-workflow.md` (return events, statuses, corrections, advisory, production MySQL config).

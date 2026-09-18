# Phase 6 — Dashboard + UX integration: parity and decisions

Base commit `1b2e273` · parity executed **2026-09-16 14:55–15:00 (+07:00)** against live `DESKTOP-BVH8F8L.INV` (read-only).
Tests: `DashboardParityTests` (integration, D1–D10 + C11 + cross-domain), `DashboardTests` / `DashboardSqlTests` (unit).

## Architecture and data ownership
The root route `/` is the dashboard. `DashboardService` (Core) **composes**; it never re-implements module rules.

| Section | Owner / source | Kind |
|---|---|---|
| Inventory count / value / qty (D3, D8) | `IInventoryRepository.GetSummaryAsync` → `InventorySummary` (Phase 2) | current state |
| Reorder red / yellow / Σ red suggested | `IReorderRepository` + `ReorderReport` (Phase 3) | current state |
| PO pipeline cards (D2) | `IPurchaseOrderRepository` + `PurchaseOrderReport` / `PurchaseOrderRules` (Phase 4) | fiscal year |
| Non-PO receipts | `INonPoReceiptRepository` + `ReceiptReport` (Phase 5) | fiscal year |
| Budget (D1), Sub-store (D4), Coverage (D5), legacy ED/NED (D6), Agreements (D7), processed monthly movement (D9: MNTH_SUM + MBS_RE_M), item trend (D10: CARD), PO process time (C11) | `IDashboardAnalyticsRepository` (`DashboardAnalyticsRepository`, 10 explicit SELECTs) — legacy KPIs with no domain owner | D1/D9/C11 fiscal year; D4/D5/D6/D7/D10 current or own period |
Unit tests forbid the dashboard SQL from touching `OTH_IVO`, `MIN_LEVEL`, PO status sets or the legacy hard-coded item `2010930`.

## Fiscal-year behaviour
`?fy=` validated (2500–2599) else ignored; default reuses Phase 4 `DefaultFiscalYear.Choose` (single open BUDGET year → **2569**;
multiple/none → deterministic fallback with an on-page note). FY drives Budget, PO pipeline, receipts, processed monthly movement and process time
(typed `[1 Oct, 1 Oct)` window / PO prefix parameter). Inventory, reorder, sub-store, coverage, ED/NED and agreements are
current-state and ignore FY; the section headers say so. `?item=` (D10) is validated to 1–7 alphanumerics.

## Parity — executed 2026-09-16 14:55 +07:00, FY 2569
| Check | Raw / legacy | Dashboard | Result |
|---|---|---|---|
| D1 budget | open year 2569, Σ money 3 000 000.00 (1 row) | 2569 / 3 000 000.00 / HasBudget | **PASS** |
| D2 PO pipeline | issued 1 / 1 790.00 · received 0 · accounting 0 · finance 0 · closed 0 (legacy status sets) | identical; == Phase 4 `PurchaseOrderReport` | **PASS** |
| D3 main-store value | Dashboard.asp all rows 104 692.54 · default.asp ED-join 104 692.54 · active-only 104 692.54 | 104 692.54 (**Phase 2 active definition** standardised; all three agree today because inactive items carry no value) | **PASS** |
| D4 sub-store value | Σ SUBSTOCK 224 292.18 (261 rows); 3 departments | identical; Σ departments == total (ER01 5 505.28, ERB01 0.00, OPD01 218 786.90) | **PASS** |
| D5 stock coverage | latest 2026-08: MNTH_SUM 104 897.85 / SALE 40 073.42 = 2.6176… | identical; shown as 2.62 · งวด ส.ค. 2569 | **PASS** |
| D6 ED/NED (legacy filter NOUSE+OUT_OF_LIST+PO_INDIVIDUAL) | ED 173 / 73 638.64 · NED 8 / 461.08 · MES 86 / 30 592.82 | identical; equal to the Phase 2 NOUSE-only groups today (documented semantic difference kept: dashboard uses the legacy filter) | **PASS** |
| D7 agreements | 0 active | 0 → renders "ไม่มีสัญญาคงเหลือ" (not a financial zero) | **PASS** |
| D8 active item count | 267 (`NOUSE IS NULL`) | 267 == `InventorySummary` | **PASS** |
| D9 monthly movement (re-executed 2026-09-18) | INVC processed months only: ending = Σ MNTH_SUM.TOTAL_VALUE / QTY_REMAIN per CE month; receive/issue = Σ MBS_RE_M.RCV_VALUE / SALE_VALUE; opening = previous calendar month's MNTH_SUM (never bridged); type split = RTRIM(ED_NED) → TBLED_NED | identical for all 12 processed months of FY2569 (2568-10 … 2569-09); type rows re-add to month totals; identity prev + RCV − SALE = ending exact except **2025-10 / 2025-11 (−150.00, WORKING_CODE 1000170 — INVC data anomaly, see “Monthly movement — authority” below)**, which the page shows as “ส่วนต่างจากผลประมวลผล” | **PASS** (parity) / **MEASURED** (identity) |
| D10 item trend | 1001710 SIMVASTATIN, 1001460 AMLODIPINE (top issuers, chosen dynamically): 12 months each, qty & value per month | identical; unknown/malformed code → null/notice; no default item | **PASS** (interactive, `?item=`) |
| C11 process time | FY2569: 1 PO month (2026-03), send/doc/acc all NULL | identical; UI shows N/A | **MEASURED — no completed stage data** |
| Cross-domain | Inventory 267/104 692.54 · Reorder 47/0/39 997 · PO issued 1 · Receipts 81/575/352 744.24 | dashboard sections equal module reports (count, qty, value, buckets, type count) | **PASS** |
Unexplained deltas: **none**.

## New dashboard additions (not in legacy)
Reorder card (red count, yellow count, Σ red suggested → `/Reorder?status=red`), non-PO receipts card (headers, lines, verified
TOTAL_VALUE → `/Receipts?fy=`), as-of timestamp, section-level failure isolation.

## Monthly movement — authority (replaces the CARD-based table, 2026-09-18)
- Authority = INVC month-end processing: `MNTH_SUM` (complete snapshot, every item; CE `YEAR` nvarchar(4) + 2-char `MONTH`) for opening/ending value and quantity, `MBS_RE_M` (only items with movement) for RCV_*/SALE_* flows. `MBS_RE_M.REMAIN_*` is **not** a store total and is never used. `MBS_RE_Y` is UNRESOLVED and unused.
- Only processed months are listed (a month exists iff it has MNTH_SUM rows); the opening of a month is the immediately preceding calendar month's MNTH_SUM and is `null` (shown as “ไม่มียอดสิ้นเดือนก่อนหน้า”) when that month was not processed — never bridged.
- `CARD` remains the ledger for the per-item trend (D10) only.
- Known identity discrepancy: 2025-10 and 2025-11 differ by −150.00 (item 1000170: zero-valued return receipt O6800016 followed by a valued issue S6800013; the owner's manual correction of 2026-09-18 re-processed 2025-09 only). The report surfaces the difference unchanged; it is not corrected in the app (INV is read-only).

## Intentional deviations from legacy
1. Main-store value = Phase 2 active definition (Dashboard.asp used all rows). 2. Lookups LEFT JOIN / safe (SUBSTOCK dept, TBLED_NED).
3. No hard-coded item; D10 is user-selected. 4. Agreement rows with PACK_RATIO 0/NULL are counted but reported as "คำนวณมูลค่าไม่ได้"
instead of crashing. 5. Movement shows receive/issue/other rollups **plus** raw category codes so nothing is hidden; CANCEL_FLAG rows are
included exactly as the legacy query did. 6. No AJAX fragments, no auto-refresh, no chart library (CSS bars only).

## Error handling
Health probe first: DB unreachable → single top-level alert with link to `/Health`. Each section is wrapped
(`DashboardSection<T>`): a failing optional section shows "ไม่พร้อมใช้งาน" while others render; the error text is the exception
type only (integration test proves a connection string in an exception message never reaches the model); details go to the log.

## Performance (dev server, warm)
Dashboard request 55–180 ms end-to-end; statements per request: health 2 + PO years 2 + inventory 1 + reorder 1 + PO 1 + receipts 1 +
7 dashboard-only + item trend 0–2 = 15–17 sequential SELECTs, each ≤ 32 ms server-side. No caching, no parallel connections, no indexes.

## UX decisions
Root = dashboard (no second home page). Navigation: `active` + `aria-current="page"` derived from the page route, `navbar-expand-lg`
collapse below 992 px, consistent labels (แดชบอร์ด · สถานะคงคลัง · คำแนะนำการสั่งซื้อ · ใบสั่งซื้อ · รับเข้าอื่น / CUP · สถานะระบบ), navbar
search explicitly labelled "ค้นหาคงคลัง". Shared visual system in `site.css` (`page-header`, `section-header`, `metric-card` +
tone modifiers, `metric-list`, `empty-state`, `pipeline`, `composition-bar`, focus-visible outlines); module pages adopted `page-header`
only — their data/query semantics are unchanged. Colour always paired with text labels. Verified at 1280 px, 768 px and 375 px.

## Deferred
None required. C11 remains a measurement until milestone dates exist in production.

## Tests
Unit 307 (was 268): rules (coverage divide-by-zero, agreement pack guard, movement category/grouping, FY/month keys, input validation),
composition (movement partition, budget/coverage/agreement states, section results, snapshot), SQL guard + lane checks.
Integration 51 (was 39): D1–D10, C11, cross-domain, section-failure isolation.

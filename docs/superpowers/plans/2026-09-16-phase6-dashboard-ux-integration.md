# Phase 6 plan — Dashboard + UX integration

Owner-approved. Base `1b2e273` (== origin/main, clean tree, 307 tests green). Boundaries: modify only `C:\INVC\Web`;
INV strictly read-only; no legacy ASP, Access, IIS or SQL Server changes; no deployment/cutover.

Principle: the Dashboard **composes** trusted modules (Inventory Phase 2, Reorder Phase 3, PO Phase 4, Receipts Phase 5).
Dashboard-only SQL exists solely for legacy KPIs without a domain owner: BUDGET (D1), SUBSTOCK (D4), MBS_RE_M/MNTH_SUM coverage
(D5), legacy ED/NED filter (D6), Agreement (D7), CARD movement (D9/D10), PO process time (C11).

| # | Task | Output |
|---|---|---|
| 0 | Pre-flight: HEAD/origin/clean/307 tests | ✔ done |
| 1 | This plan | this file |
| 2 | Evidence review: parity matrix D1–D10, legacy default.asp / Dashboard.asp query map, phase 2–5 docs; inspect CARD category distribution and Agreement/BUDGET data read-only | notes in parity doc |
| 3 | Core `src/Invc.Core/Dashboard/`: `DashboardRules` (FY validation, coverage divide-by-zero, agreement remaining, movement grouping), records `DashboardBudget`, `DashboardStockCoverage`, `DashboardSubstock`, `DashboardEdNed`, `DashboardAgreements`, `DashboardMovementRow`, `DashboardProcessTime`, `DashboardItemTrendRow`, `DashboardSnapshot` with per-section results; `IDashboardAnalyticsRepository`; `DashboardService` orchestrating existing repositories + reports (TDD) | Core + unit tests |
| 4 | Infrastructure `src/Invc.Infrastructure/Dashboard/DashboardAnalyticsRepository` — explicit read-only SQL for the dashboard-only KPIs, guard-checked | repository |
| 5 | Root `/` becomes the Dashboard (`Pages/Index`), FY selector reusing `DefaultFiscalYear` (PO) semantics; current-state vs FY sections; as-of timestamp; section-level error isolation; optional item trend (`?item=`) | Razor |
| 6 | Navigation: active state / `aria-current`, consistent labels, inventory-search placeholder; shared CSS classes (page header, section header, metric cards, badges, empty/error states); remove obvious repeated inline styles in phase 2–5 pages without semantic change | `_Layout`, `site.css`, pages |
| 7 | Integration tests: D1–D10, C11 measurement, cross-domain consistency (Inventory/Reorder/PO/Receipts == Dashboard) | `DashboardParityTests` |
| 8 | Performance measurement; docs `phase6-dashboard-parity.md`; parity matrix D1–D10/C11 marked; CLAUDE.md durable rules | docs |
| 9 | restore/build/test, smoke (all entry pages, dashboard default/alternate/invalid FY, item trend, mobile), legacy integrity vs `1b2e273`, module regression | evidence |
| 10 | git status/diff/check, commit "Phase 6: integrate verified operational dashboard", fast-forward push (keep local commit if auth fails) | checkpoint |

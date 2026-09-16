# Phase 5 plan — Non-PO receipts (OTH_IVO / OTH_IVOC / RCV_TYPE)

Owner-approved design. Base commit `19230a7`. Baseline 247 tests. Boundaries: modify only `C:\INVC\Web`;
SQL Server INV strictly read-only; no Access/IIS/Windows changes; no Dashboard work; module stays separate from Purchase Orders.

| # | Task | Output |
|---|---|---|
| 1 | Read-only schema discovery: `sys.columns/types/indexes` for OTH_IVO, OTH_IVOC, RCV_TYPE; sample rows | evidence in data map |
| 2 | Prove header ↔ line relationship (candidate keys, counts, cardinality, orphans) | HEADER KEY / LINE KEY / CARDINALITY or UNRESOLVED |
| 3 | Prove `OTH_IVO.RCV_TYPE` → `RCV_TYPE` (orphans, duplicates, nulls, distribution) | lookup rule |
| 4 | Date / fiscal-year semantics of OTH_IVO (`DATE_RECEIVE` vs `INVOICE_DATE` vs `SYSDATE`), shared `ThaiFiscalYear` | date rule or UNRESOLVED |
| 5 | Data profile (counts, ranges, per-year, per-type, zero/multi-line headers, orphan lines, duplicate keys, `OTH_IVOC.PO_NO` population) | profile table |
| 6 | Quantity / value semantics of OTH_IVOC (QTY_ORDER, PACK_RATIO, BUY_UNIT_COST, UNIT_VALUE, EXPIRED_DATE, LOTNO, LOCATION) | verified field map |
| 7 | Write `docs/phase5-receipts-data-map.md` **before** feature code | source of truth |
| 8 | Core `src/Invc.Core/Receipts/` — NonPoReceiptSummary/Detail/Line, ReceiptTypeSummary, ReceiptReport, filter/key rules (TDD) | Core + unit tests |
| 9–10 | Infrastructure `src/Invc.Infrastructure/Receipts/` — explicit-column SQL, LEFT JOIN/OUTER APPLY lookups, one list query with line aggregates, guard-checked | repository |
| 11–14 | Default period (current FY if data, else latest FY with data), type filter (lookup-driven), FY filter (typed), search (verified fields only) | page model rules |
| 15–17 | `/Receipts` page: cards, type breakdown, filters, search, table, empty/error states | Razor |
| 18–24 | `/Receipts/Detail/{key}` and `/Receipts/Print/{key}`: verified header, lines with item lookup safety, lot/expiry highlight, reconciliation only if supported | Razor + print |
| 25–34 | Parity C10a–C10j integration tests (read-only, live comparisons) | `ReceiptParityTests` |
| 35–36 | Unit + integration coverage (key validation, FY boundary 30 Sep/1 Oct, filter parsing, search, packs, null handling, labels) | tests |
| 37 | Performance measurement (list, breakdown, header, lines) | numbers in parity doc |
| 39 | `docs/phase5-receipts-parity.md`, update `report-parity-matrix.md` (C10 expanded), CLAUDE.md durable rules | docs |
| 40–42 | restore/build/test, smoke (all routes incl. previous modules), mobile, legacy integrity vs `19230a7`, previous-module regression | evidence |
| 43 | git status/diff/check, commit "Phase 5: add verified non-PO receipt reporting", fast-forward push | checkpoint |

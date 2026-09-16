# Phase 3 — Reorder Recommendations (คำแนะนำการสั่งซื้อ): parity and decisions

Base commit `c8c09ec` · parity executed **2026-09-16 11:17 (+07:00)** against live `DESKTOP-BVH8F8L.INV` (read-only).
Values are point-in-time evidence; `tests/Invc.IntegrationTests/ReorderParityTests.cs` re-derives them on every run.

## Source report identity
`INV_Report_Purchase.asp` (screen) and `INV_Report_Purchase_Print.asp` (print) — despite the file name this is a
**reorder recommendation report over `dbo.INV_MD` only**. It never reads `MS_PO`/`MS_IVO`; it compares stock with
MIN / reorder point / MAX maintained by the Access application. The new module is therefore named
"คำแนะนำการสั่งซื้อ / Reorder Recommendations" and states on the page that it is not a purchase-order list.

## Eligibility filter (legacy, reproduced verbatim)
`(NOUSE IS NULL OR NOUSE = '') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST = '')` — `ReorderSql.EligibilityFilter`,
asserted by a unit test. This is **not** the Inventory Status filter (`NOUSE IS NULL`). At execution time both gave 267
(no `NOUSE = ''` rows; the 49 `OUT_OF_LIST` rows are all also `NOUSE = 'Y'`), but the filters are kept distinct.

## Formulas (Core, single implementation — `InventoryRules`, reused from Phase 1/2, unchanged)
| Rule | Legacy VBScript | Core |
|---|---|---|
| Numbers | `ToNumSoft`: NULL/blank → 0 | nullable decimals read raw; rules coalesce to 0 |
| Effective ROP | `If reorder = 0 Then reorder = minLv` | `EffectiveReorderPoint(reorderQty, minLevel)` |
| Status | red `stock < min`; yellow `min ≤ stock < rop`; green otherwise | `ClassifyReorder` |
| Suggested qty | `RoundUpASP(maxLv − stock)` = `−Int(−x)` (ceiling) | `SuggestedOrderQty` = `Math.Ceiling(max − stock)` |
SQL reads the raw columns only; nothing is classified in SQL, so the rule cannot drift.

## ROP fallback — B5
Rule holds for every row (asserted over all 267). **Live fallback cases (REORDER_QTY 0/NULL with MIN > 0): 0** —
every configured item has `REORDER_QTY == MIN_LEVEL`; unconfigured items have both 0/NULL. Covered by unit tests.

## ROP_EXCEPT — UNRESOLVED (not filtered)
The legacy report does not read `ROP_EXCEPT`; the Access changelog says it marks items "not to show when reaching the
reorder point". Production: `ROP_EXCEPT = 'Y'` **0 rows** (all NULL). Phase 3 preserves legacy parity and does **not**
filter on it (unit test asserts the SQL never mentions it). Intended behaviour remains UNRESOLVED; no inference made.

## Missing-threshold observations (eligible rows, 2026-09-16)
| Field | 0 or NULL | of 267 |
|---|---|---|
| MIN_LEVEL | 141 (25 NULL + 116 zero) | 53 % |
| REORDER_QTY | 141 | 53 % |
| MAX_LEVEL | 129 (25 NULL + 104 zero) | 48 % |
| RATE_PER_MONTH | 141 | 53 % |
Formulas unchanged; the UI shows a neutral "ไม่ได้กำหนด MIN/MAX" badge when both MIN and MAX are 0/NULL and a
per-page note with the counts. No thresholds are fabricated or recomputed from RATE_PER_MONTH — Access owns them.

## Negative suggested quantity
Raw `CEILING(MAX − stock)` is preserved in `ReorderItem.SuggestedOrderQty` (156 negatives today, all in GREEN rows;
0 in RED). **UI-only interpretation** (`DisplaySuggestedQty`): a non-RED item with raw ≤ 0 displays "ไม่ต้องสั่ง"
(raw value in the tooltip); RED items always display the raw legacy value (a negative would appear only if MAX is
unconfigured while MIN is set — 0 such rows today). Parity B3 compares the raw values.

## Status filter, default and "all"
`status=red|yellow|green|all`; blank/unknown → **red** (legacy default; legacy showed an empty table for unknown values,
the new page normalises instead — documented change). The value is parsed to an enum; SQL is never built from it.
"all" is a UX addition = union of the three buckets in source order (asserted in unit and B6 tests).

## Status summary
Counts (red / yellow / green / eligible) and `RedSuggestedTotal` are computed from the **same classified list** the table
is rendered from (`ReorderReport`); the screen and the print page build the report identically.

## Yellow bucket
0 rows. `REORDER_QTY == MIN_LEVEL` for all 126 configured items and 0 for the rest ⇒ the interval `MIN ≤ stock < ROP`
is empty by construction. The filter/card stay in the model and UI (showing 0) with an explanatory empty state; a
synthetic unit test proves yellow classification works when ROP > MIN. Data outcome, not an application bug.

## Search
Optional; same conventions as Inventory Status (trim, ≤ 50 chars, nvarchar parameter, `%`/`_`/`[` literal, four columns).
Search restricts the eligible set only; classification of each row is unchanged (asserted) and status + search is deterministic.

## Sorting decision
Legacy `ORDER BY WORKING_CODE` (text). New pages use the application's numeric-safe key (Inventory Status) for
consistency. Verified: all 267 codes are 7-digit numeric, so the sequences are **identical today** (test asserts equality
whenever codes are equal-length numeric, and membership equality always). If non-numeric or variable-length codes appear,
the order may differ from the legacy text order — this is an intentional deviation, not "exact parity".

## Print view
`/Reorder/Print?status=…&q=…` — layout-less Razor page, `print.css` (A4 portrait, 12 mm margins, repeated table header,
rows not split, navigation hidden, badges outlined for monochrome). Heading shows the selected status; same
`ReorderReport` as the screen (B6). No PDF library; browser print / save-as-PDF, print is not auto-triggered.

## Parity B1–B6 — executed 2026-09-16 11:17:30 +07:00
| Check | Legacy / raw | New | Result |
|---|---|---|---|
| B1 eligible rows | 267 (legacy filter) — Inventory Status active also 267 | 267, identical code set | **PASS** Δ0 |
| B2 red / yellow / green | 47 / 0 / 220 | 47 / 0 / 220 | **PASS** Δ0 |
| B3 Σ suggested (RED) | 39 997 | 39 997 | **PASS** Δ0 |
| B4 representative rows | red 1000130 (0/33/33/50 → 50), 1000140 (0/167/167/250 → 250), 1000250 (200/433/433/650 → 450); green 1000010 (100/100/100/150 → 50), 1000020 (100/0/0/0 → −100), 1000030 (1000/0/0/0 → −1000); yellow: none exist; red-with-negative: none exist | field-by-field equal | **PASS** |
| B5 ROP fallback | rule asserted on all 267; live cases 0 | equal | **PASS** |
| B6 screen = print, all = union | red 47 (1000130…3000640), yellow 0, green 220 (1000010…3000860), all 267 | identical | **PASS** |
| Sort (extra) | legacy text order == new order: **true** | | documented |
| Search (extra) | "10"→187 elig/33 red · "ยา"→24/4 · "tab"→95/19 · "50%"→1/0 | deterministic, classification unchanged | **PASS** |
Unexplained deltas: **none**.

## Performance
One statement per request (3 ms server-side for the eligible list); classification of 267 rows in memory is negligible;
`status=all` HTML ≈ 300 KB. No pagination (same reasoning as Phase 2). No indexes needed; theoretical
`INV_MD(NOUSE, OUT_OF_LIST)` filtered index would only matter at far larger volumes — documented only.

## Intentional deviations (summary)
1. Unknown `status` → red instead of an empty table. 2. Optional `all` filter and keyword search (additive).
3. Numeric-safe sort key (identical today). 4. Non-RED negative suggestions displayed as "ไม่ต้องสั่ง" (raw preserved).
5. Print page shows the suggested column and status badges (legacy print omitted the suggestion column).

## Tests
Unit 146 (was 96): rule matrix (null stock/min/max/rop, fallback, below/equal/between/equal-ROP/above, negative stock,
negative suggestion, fractional ceiling), filter parsing, report aggregation, all-filter union, display interpretation,
synthetic yellow bucket, SQL guard + eligibility assertion. Integration 22 (was 14): B1–B6, sort, search, threshold profile.

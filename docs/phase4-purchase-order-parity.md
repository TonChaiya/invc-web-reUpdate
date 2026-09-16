# Phase 4 — Actual Purchase Orders (ใบสั่งซื้อ): parity and decisions

Base commit `667cb07` · parity executed **2026-09-16 11:45 (+07:00)** against live `DESKTOP-BVH8F8L.INV` (read-only).
Values are point-in-time evidence; `tests/Invc.IntegrationTests/PurchaseOrderParityTests.cs` re-derives them on every run.

## Source
Actual purchase orders live in `dbo.MS_PO` (header), `dbo.MS_PO_C` (lines), `dbo.MS_IVO` / `dbo.MS_IVO_C` (PO-based receipts),
with lookups `COMPANY`, `TblPOStatus`, `BDG_TYPE`, `TBLBUY`, `TBLED_NED` and the open year in `BUDGET`.
Legacy pages replaced: `PO_Search.asp`, `PO.asp`, `PODetail.asp`, `table_ipiss.asp`, `ipiss_process.asp` (business semantics
only — their `Conn`-based plumbing was broken and is not reproduced). This is **not** the reorder report (Phase 3) and does
**not** include non-PO receipts (`OTH_IVO`, see C10).

## PO_NO vs REAL_PO — durable rule
| Relationship | Column pair | Where enforced |
|---|---|---|
| Lines | `MS_PO_C.PO_NO = MS_PO.PO_NO` | `PurchaseOrderSql.Lines` (`WHERE l.PO_NO = @PoNo`) |
| Receipts | `MS_IVO.PO_NO = MS_PO.REAL_PO` | `PurchaseOrderSql.Receipts` (`WHERE r.PO_NO = @RealPo`, no `@PoNo` parameter exists) |
| Receipt lines | `MS_IVO_C.RECEIVE_NO = MS_IVO.RECEIVE_NO` | `PurchaseOrderSql.ReceiptLines` |
`PurchaseOrderSqlTests.Relationship_rules_are_encoded_exactly` fails if any of these is swapped; the VALUES-based query-shape
test proves a receipt keyed by the internal PO_NO is *not* matched.

## Fiscal-year rule and default
FY = first two digits of `PO_NO` (`6900001` → 2569), via `PurchaseOrderRules.FiscalYearFromPoNumber` / `ThaiFiscalYear.ToPoNumberPrefix`.
SQL uses a parameter (`PO_NO LIKE @PrefixPattern`, value `69%`), never `LEFT(PO_NO,2)='…'` concatenation. User input `fy`
is parsed (2500–2599) or ignored. **Default:** the single `BUDGET.BudgetOpen='O'` year (2569 today); several open years → latest +
warning; none → latest PO year, else current Thai FY — each case flagged on the page (`DefaultFiscalYear`, unit-tested).

## Lookup / join safety and COMPANY duplicates (legacy defect correction)
Legacy used a five-way INNER JOIN, so a PO with an unknown vendor/status/budget/method/ED code vanished and a duplicated
`COMPANY_CODE` multiplied it. New header query: `LEFT JOIN` for the four unique-key lookups and `OUTER APPLY (SELECT TOP 1 …
ORDER BY RECORD_NUMBER)` for `COMPANY` (`MED015` is duplicated in production). Missing names render as the raw code plus
"ไม่พบชื่อบริษัท / ไม่พบชื่อสถานะ". Drug names on lines use the same OUTER APPLY on `INV_MD.WORKING_CODE`.

## Status buckets (single implementation: `PurchaseOrderRules`)
issued `NOT IN ('0','C')` · received `2,3,4,5,6,7,8,9,D` · accounting `4,5,6,7,8,9,D` · finance `4,5,7,8,9` · closed `5,9` ·
all (UX addition). NULL/blank status: legacy `NOT IN` evaluates UNKNOWN → excluded from issued; the new rule does the same
and shows such a PO only under "all". Buckets are never encoded in SQL: all POs of the year are loaded once and classified in Core;
cards, table and status breakdown derive from the same list (`PurchaseOrderReport`).

## Filters, search, sorting
`bucket` parsed to an enum (unknown → issued, legacy "PO" view). Search (optional): REAL_PO, PO_NO, DOC_NO, vendor names —
same normalisation/escaping as Inventory (trim, ≤ 50, nvarchar, literal `%`/`_`/`[`). Order: `REAL_PO, PO_NO` (legacy `ORDER BY REAL_PO`).
BILLOUT date selector: **not shown** — production has 0 BILLOUT dates (C9); `GetBillOutDatesAsync` exists with a typed parameter for when data appears.

## Detail (`/PurchaseOrders/Detail/{realPo}`, print `/PurchaseOrders/Print/{realPo}`)
Identifier validated (1–10 chars, letters/digits/`-_./`), else 404; unknown → 404. Four statements max: header, lines,
receipts, receipt lines (only when receipts exist). Packs = `QTY_ORDER / PACK_RATIO1`, null when ratio 0/NULL (legacy divided
blindly). Process days = `DATEDIFF(DAY, FIRST_RCVDATE, today)` preserved as legacy (keeps counting after payment); null → "–".
**Reconciliation:** `TOTAL_ITEM` vs line count and `TOTAL_COST` vs `SUM(BUY_VALUE)` shown in the footer with a warning row on mismatch.
**Receipts empty state:** "ยังไม่มีข้อมูลการรับของสำหรับใบสั่งซื้อนี้" (production has 0 MS_IVO rows).

## Parity C1–C11 — executed 2026-09-16 11:45:18 +07:00, FY 2569 (prefix 69)
| Check | Legacy / raw | New | Result |
|---|---|---|---|
| C1 issued count | 1 | 1 | **PASS** |
| C2 issued value | 1 790.00 | 1 790.00 | **PASS** |
| C3 received / accounting / finance / closed | 0/0 · 0/0 · 0/0 · 0/0 | identical | **PASS** |
| C4 per status | `1` ×1, 1 790.00 (ออกใบสั่งซื้อแล้วรอรับของ) | identical | **PASS** |
| C5 header K6900001 (PO_NO 6900001) | 19 มี.ค. 2569, CUB001 ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล, status 1, budget 03 เงินบำรุง, method 27 เฉพาะเจาะจง, ED 1, 1 item, 1 790.00, all milestone dates NULL, DOC_NO ชม 51006.8.2/00001 | 24 fields equal; detail by REAL_PO returns same header | **PASS** |
| C6 lines | 1 line: 1000010 Acyclovir 400 mg tab, 1 000 / 100 → 10 packs TAB, 179.00, 1 790.00; Σ BUY_VALUE 1 790.00 = TOTAL_COST, 1 = TOTAL_ITEM | identical, reconciliation consistent | **PASS** |
| C7 receipts by REAL_PO | 0 (by PO_NO also 0; table total 0) | 0 | **PASS** (zero valid) |
| C8 join-loss guard | legacy INNER JOIN 1 · authoritative MS_PO 1 · orphan lookup codes 0 · POs with duplicated vendor code 0 | safe lookup 1 | **PASS** — no loss today; test asserts inequality whenever an orphan/duplicate exists |
| C9 BILLOUT dates | 0 | 0 | **PASS** (zero valid) |
| C10 OTH_IVO receipts | — | — | **DEFERRED** — separate Receipts phase (110 headers / 1 018 lines exist, not shown here) |
| C11 process-time aggregate | 2026-03: SendDay/DocDay/AccDay all NULL (no BILLIN/BILLOUT/BILLEND) | — | **MEASURED / DEFERRED TO DASHBOARD** |
Receipt query shape (synthetic `VALUES` rows, nothing written): REAL_PO match only, RECEIVE_NO join, pack-ratio-0 safe, NULL dates, empty result. **PASS**.
Unexplained deltas: **none**.

## Performance
List: 3 statements/request (open years, PO years, headers) — 1 ms server-side; detail 2–4 statements, 0 ms. No pagination,
no cache, no indexes (theoretical `MS_PO(PO_NO)` / `MS_IVO(PO_NO)` indexes documented only; irrelevant at 1 row).

## Intentional deviations
1. LEFT JOIN / OUTER APPLY instead of INNER JOIN (PO never lost or multiplied). 2. Parameterised FY and search. 3. Unknown bucket → issued;
"all" bucket added. 4. Packs null on zero ratio. 5. BILLOUT selector hidden while no dates exist. 6. Status-code breakdown and
reconciliation footer are additions. 7. Search added (legacy had a REAL_PO lookup box only).

## UNRESOLVED
`REAL_PO` uniqueness (no constraint; detail takes the first in `REAL_PO, PO_NO` order) · meaning of `MS_PO.PROCESS`, `OK`, `PRINTED`,
`RCV_CHKER*`, `COM4-6`, `EDI*`, `QUICK_PAY` (not displayed) · `MS_IVO_C.SUB_PROCESS`, `LOTNO2`, `EXPIRED_DATE2/LOCATION2`
(second lot fields — not displayed until real data exists).

## Tests
Unit 215 (was 146): bucket matrix incl. NULL/unknown status, status sets, bucket parsing, FY prefix round-trip, FY input parsing,
packs, process days, identifier validation, report aggregation/breakdown/all-superset, reconciliation, default-year cases, SQL guard
+ relationship + parameter tests, receipt assembly. Integration 32 (was 22): C1–C9, receipt query shape, invalid identifiers.

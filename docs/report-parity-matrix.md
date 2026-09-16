# Report Parity Matrix — legacy vs. new application

Phase 0 deliverable, 2026-09-16. Defines the checks Phase 1+ must pass before a new report replaces a
legacy page. Every check runs the **same read-only SQL** against `DESKTOP-BVH8F8L.INV` at the same moment and
compares the legacy page output (or the legacy query executed directly) with the new page. Baselines below
were captured at audit time and will drift; they are illustrative, not acceptance values.

Legend: R = row count, Q = quantity total, V = value total, S = sample-record comparison, K = KPI value.

## A. Inventory status (INV_Status.asp → new Inventory page)

| # | Check | Legacy definition | Baseline (2026-09-16) |
|---|---|---|---|
| A1 | R active items | `SELECT COUNT(*) FROM INV_MD WHERE NOUSE IS NULL` | 267 |
| A2 | Q on hand | `SUM(QTY_ON_HAND)` same filter | 154 357 (all 316) — recapture for 267 |
| A3 | V main store | `SUM(TOTAL_VALUE)` same filter | 104 692.54 |
| A4 | V by ED/NED | `INV_MD ⋈ TBLED_NED GROUP BY EDCODE` (default.asp) | ED 173 / NED 8 / MES 86 items |
| A5 | S 10 items | WORKING_CODE, DRUG_NAME, QTY_ON_HAND, SALE_UNIT, LOCATION, RATE_PER_MONTH, months-left | pick 5 with rate>0, 5 with rate 0 ("N/A") |
| A6 | K months-left rule | `ROUND(QTY_ON_HAND/RATE_PER_MONTH,2)`, "N/A" if rate 0; leading "0" for 0<x<1 | rule test, not a value |
| A7 | Q borrowable | `VCAR − SUM(BORROW)` per item | 0 everywhere (tables empty) — test with synthetic expectation = 0 |
| A8 | R search | keyword search over DRUG_NAME/COMPOSITION/HOSP_CODE/WORKING_CODE | same count for 3 keywords |
| A9 | Sort order | `CAST(WORKING_CODE AS INT), DRUG_NAME COLLATE Thai_CI_AS` | first/last 5 codes identical |
| A10 | Lots (chkstock) | `INV_MD_C` per item: count, `SUM(QTY_ON_HAND)`, `SUM(LOT_VALUE)` = INV_MD totals | 249 lots; equality holds 267/267 |
| A11 | V sub-store | `SUM(SUBSTOCK.TOTAL_VALUE)` by DEPT_ID | 224 292.18 / 3 depts |

## B. Reorder recommendation (INV_Report_Purchase*.asp → new Reorder page)

| # | Check | Legacy definition | Baseline |
|---|---|---|---|
| B1 | R eligible | `NOUSE` null/'' AND `OUT_OF_LIST` null/'' | 267 |
| B2 | R by status | red / yellow / green per legacy VBScript rule (ROP fallback to MIN) | 47 / 0 / 220 |
| B3 | Q suggested | `SUM(CEILING(MAX_LEVEL − QTY_ON_HAND))` for red items | capture at run time |
| B4 | S 10 items | WORKING_CODE, QTY_ON_HAND, MIN, ROP, MAX, suggest, status | include ≥3 items with MIN=0 (negative suggest) |
| B5 | Fallback rule | item with REORDER_QTY=0 and MIN_LEVEL>0 must use MIN as ROP | rule test |
| B6 | Print view | same rows/order as screen for each `status` value | R equality ×3 |

## C. Actual purchase orders (PO_Search / PODetail / table_ipiss → new PO pages)

Because the legacy Idiom-B pages cannot run, the baseline is the **legacy SQL executed directly**.

| # | Check | Legacy definition | Baseline |
|---|---|---|---|
| C1 | R PO per FY | `COUNT(*) FROM MS_PO WHERE LEFT(PO_NO,2)='69' AND STATUS NOT IN ('0','C')` | 1 |
| C2 | V PO per FY | `SUM(TOTAL_COST)` same filter | 1 790.00 |
| C3 | R/V per bucket | RCV / ACC / FIN / END status sets (see PO rules §4) | 0 / 0 / 0 / 0 |
| C4 | R per status | `GROUP BY StatusCode` for FY | {1: 1} |
| C5 | S header | REAL_PO K6900001: vendor name, BDGNAME, BUYNAME, StatusName, dates | 1 record |
| C6 | S lines | MS_PO_C for PO_NO 6900001: packs = QTY_ORDER/PACK_RATIO1 = 10, BUY_VALUE 1 790 | 1 line |
| C7 | R receipts | MS_IVO where PO_NO = REAL_PO | 0 |
| C8 | Join-loss guard | count with INNER joins vs. LEFT joins must be equal, else report the orphan codes | 1 = 1 |
| C9 | BILLOUT date list | distinct BILLOUT for FY | 0 dates |
| C10 | Non-PO receipts (new) | `OTH_IVO` count / `SUM(OTH_IVOC.QTY_ORDER)` by RCV_TYPE for FY | 110 headers / 1 018 lines — no legacy equivalent; parity = raw SQL |
| C11 | Process-time KPI | avg DATEDIFF PO_DATE→BILLIN→BILLOUT→BILLEND per month | NULL (no dates yet) |

## D. Dashboard (default.asp + Dashboard.asp → new Dashboard)

| # | Check | Legacy definition | Baseline |
|---|---|---|---|
| D1 | K open budget year | `BUDGET.year WHERE BudgetOpen='O'`, `SUM(money)` | 2569 / 3 000 000 |
| D2 | K PO value cards | C1–C3 values | as above |
| D3 | K main-store value | `SUM(INV_MD.TOTAL_VALUE)` (no filter, Dashboard.asp) vs. active-only (default.asp) — **document which one is shown** | 104 692.54 both |
| D4 | K sub-store value | `SUM(SUBSTOCK.TOTAL_VALUE)` | 224 292.18 |
| D5 | K months-of-stock | latest MBS_RE_M month: `SUM(MNTH_SUM.TOTAL_VALUE)/SUM(MBS_RE_M.SALE_VALUE)` | 2026-08: 104 897.85 / 40 073.42 ≈ 2.62 |
| D6 | K ED:NED item ratio | counts excl. NOUSE/OUT_OF_LIST/PO_INDIVIDUAL | 173 : 8 |
| D7 | K agreement remaining | Agreement join (Expdate > today) | 0 rows → NULL; new app must render "no contracts" |
| D8 | K item count | `COUNT(WORKING_CODE) WHERE NoUse IS NULL` | 267 |
| D9 | Monthly movement table | `CARD` grouped by `R_S_STATUS+LEFT(R_S_NUMBER,1)` and Thai month for FY | R/S totals: RO 543 229.35, SS 453 542.09 (all time) |
| D10 | Top-24 sales by item | table_by_item.asp query for a chosen WORKING_CODE | S for 2 items |

## Method

1. Capture: run the legacy SQL (from `legacy-query-map.md`) through `sqlcmd -E` into CSV, timestamped.
2. Run the new page's query/endpoint at the same time; export the same shape.
3. Compare with a tolerance of 0.005 for decimals, exact for counts and strings (Thai collation aware).
4. Record deltas; any delta must be explained by a documented legacy defect (e.g. INNER-join loss) before sign-off.

# Inventory Business Rules & Data Map

Phase 0 audit, 2026-09-16. Source of truth for every field is `DESKTOP-BVH8F8L`.`INV`.`dbo`.
Formulas below are **as observed** in the legacy ASP code and in the live data; they are maintained by the
Access application and must not be changed or "improved" by the new web application.

## 1. Authoritative source per business field

| Business field | Authoritative column | Notes / evidence |
|---|---|---|
| Working code (รหัสยา) | `INV_MD.WORKING_CODE` (nvarchar 7) | Unique index `IX_INV_MD`. Business key used by every child table. Numeric-looking but stored as text; legacy sorts with `CAST(... AS INT)`. |
| Drug name | `INV_MD.DRUG_NAME` (+ `DRUG_NAME_KEY`, `COMPOSITION`, `DOSAGE_FORM`, `GROUP_D`) | Trade names per vendor live in `DRUG_VN.TRADE_NAME` (keyed by WORKING_CODE + VENDOR_CODE + MANUFAC_CODE + PACK_RATIO). |
| Hospital (HIS) code | `INV_MD.HOSP_CODE` | **NULL for all 316 rows** at this site. Legacy tried to resolve it against HIS `Med_inv` (unreachable). Treat as optional. |
| VEN | `INV_MD.VEN` (nchar 1: V/E/N) | Legacy displays raw; `pending.asp` orders V→E→N. **NULL for all active items** at this site. |
| ABC | `INV_MD.ABC` | populated (A/B/…) |
| ED / NED class | `INV_MD.ED_NED` → `TBLED_NED.EDCODE` (1 ED, 2 NED, 3 MES, 4 EA, 5 SAM) | `EDMAP` gives the short label. |
| Quantity on hand (main store) | `INV_MD.QTY_ON_HAND` (decimal 14,0, in **sale units**) | Verified = `SUM(INV_MD_C.QTY_ON_HAND)` for 267/267 active items. |
| Sale unit | `INV_MD.SALE_UNIT` | Pack ratios: `STD_RATIO1..3` with `STD_PRICE1..3`; `PACK_UNIT`; legacy divides by `STD_RATIO3` to show packs. |
| Location (shelf) | `INV_MD.LOCATION` → `LOCATION.LOCATION_NAME` (join by name, no FK) | `LOCATION.LOCATION_GROUP`, `RESP_PERSON`. Lot-level location: `INV_MD_C.LOCATION`. |
| Monthly usage rate | `INV_MD.RATE_PER_MONTH` | Observed = average of the last 3 months' `MBS_RE_M.SALE_QUAN` (exact match on sampled items when `RATE_CAL='Y'`); 13 items have `RATE_CAL` NULL and no rate. Recomputed by Access at month close — **do not recompute in the web app**. |
| Minimum stock | `INV_MD.MIN_LEVEL` | Observed = `RATE_PER_MONTH × 1` on the sampled items. `MIN_PER_MONTH` exists but is *not* the multiplier (UNRESOLVED semantics). 189/316 items have MIN_LEVEL 0/NULL. |
| Reorder level (จุดสั่งซื้อ) | `INV_MD.REORDER_QTY` | Observed = `MIN_LEVEL` for 126/126 items that have a rate. Legacy web falls back to MIN_LEVEL when 0. `ROP_EXCEPT='Y'` marks items to hide from reorder lists (none at this site). |
| Maximum stock | `INV_MD.MAX_LEVEL` | Observed = `RATE_PER_MONTH × 1.5` on sampled items. `MAX_PER_MONTH` is not the multiplier (UNRESOLVED). |
| Borrow quantity (ยืมได้) | `VCAR.BORROW_QTY − SUM(BORROW.BORROW_QTY)` per WORKING_CODE where `VCAR.CLOSE_STATUS <> 'C'` | From INV_Status.asp. Both tables are **empty** at this site → always 0. |
| Inventory value (main store) | `INV_MD.TOTAL_VALUE` | Verified = `SUM(INV_MD_C.LOT_VALUE)` (267/267). `TOTAL_COST` differs (only 87 equal) — cost vs. value are distinct concepts; the legacy dashboards sum `TOTAL_VALUE`. |
| Inventory value (sub-stores) | `SUBSTOCK.TOTAL_VALUE` grouped by `DEPT_ID` | 261 rows / 3 departments; total 224 292.18 at audit time. |
| Month-end stock snapshot | `MNTH_SUM` (YEAR, MONTH, WORKING_CODE, QTY_REMAIN, TOTAL_VALUE) | Used for months-of-stock KPI. 225/316 items still equal current INV_MD values for 2026-08 (the rest moved since). |
| Monthly movement | `MBS_RE_M` (RCV_/SALE_/REMAIN_ QUAN & VALUE per item-month); yearly `MBS_RE_Y` | Feeds rate calc and dashboards. |
| Transaction ledger | `CARD` (`R_S_STATUS` R=receive / S=issue, `R_S_NUMBER` prefix O=other-receipt, S=sub-store requisition, `VALUE`, `COST`, `REMAIN_QTY`) | 2 619 rows, 2025-08-20 … 2026-09-15. |
| Lots / expiry | `INV_MD_C` (PACK_RATIO, QTY_ON_HAND, EXPIRED_DATE, LOTNO, LOCATION, LOT_COST, LOT_VALUE) | 249 rows. |
| Active-item filter | `INV_MD.NOUSE IS NULL` (267 items); reorder report also requires `OUT_OF_LIST IS NULL`; dashboards also exclude `PO_INDIVIDUAL IS NOT NULL` | At this site all three filters yield 267. |

## 2. Derived measures used by the legacy pages (keep verbatim)

| Measure | Formula (legacy) | Where |
|---|---|---|
| Months of stock left (per item) | `ROUND(QTY_ON_HAND / RATE_PER_MONTH, 2)`; "N/A" when rate 0/NULL | INV_Status.asp |
| Reorder status | red: `QTY_ON_HAND < MIN_LEVEL`; yellow: `MIN_LEVEL <= QTY_ON_HAND < ROP` where `ROP = REORDER_QTY` or `MIN_LEVEL` if 0; green otherwise | INV_Report_Purchase*.asp |
| Suggested order qty | `CEILING(MAX_LEVEL − QTY_ON_HAND)` (can be negative in legacy output) | INV_Report_Purchase*.asp |
| Months-of-stock KPI (store level) | `SUM(MNTH_SUM.TOTAL_VALUE) / SUM(MBS_RE_M.SALE_VALUE)` for the latest YEAR/MONTH in MBS_RE_M | default.asp, Dashboard.asp, ipiss_item_ratio.asp |
| ED:NED item & value ratio | counts / `SUM(TOTAL_VALUE)` of INV_MD grouped by ED_NED, excluding NOUSE/OUT_OF_LIST/PO_INDIVIDUAL | default.asp |
| Sub-store value | `SUM(SUBSTOCK.TOTAL_VALUE)` by `DEPT_ID`/`DEPT_GROUP` | default.asp, ipiss_substock_remain.asp |
| Packs on hand | `QTY_ON_HAND / STD_RATIO3` | table_by_item.asp, chkstock.asp |
| Thai fiscal year of a date | `YEAR(d)+543 (+1 if MONTH(d) >= 10)` — `inc_functions.asp: YearBudget()` and inline CASE in table_* pages | multiple |
| Month key (Thai) | `LEFT(CONVERT(char(8),OPERATE_DATE,112),6) + 54300` → e.g. 202609 → 256909 | table_* pages |

## 3. Observations that affect Phase 1 (no changes made)

* Because `REORDER_QTY == MIN_LEVEL` for every item, the legacy **yellow** bucket is always empty (baseline at audit time: red 47, yellow 0, green 220 of 267).
* `MAX_LEVEL`/`MIN_LEVEL` are 0 for ~60 % of items; a faithful port must show the same result (negative "suggest") or explicitly flag "no min/max set", not silently change the rule.
* All lookups are by name/string codes with no foreign keys; the new app should tolerate missing lookup rows (LEFT JOIN) and surface them, rather than dropping items the way the legacy INNER joins do.
* `INV_MD.MEMO_NOTE` is `nvarchar(max)`; avoid `SELECT *` on INV_MD in the new app.

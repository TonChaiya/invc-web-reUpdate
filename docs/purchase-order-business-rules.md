# Purchase Order Business Rules & Data Map

Phase 0 audit, 2026-09-16. Source: `DESKTOP-BVH8F8L`.`INV`.`dbo` + legacy ASP query evidence.

## 1. Do the PO tables contain production data?

| Table | Rows | Content |
|---|---|---|
| `MS_PO` | **1** | PO_NO `6900001`, REAL_PO `K6900001`, PO_DATE 2026-03-19, vendor `CUB001`, 1 item, 1 790.00, STATUS `1` (ออกใบสั่งซื้อแล้วรอรับของ), budget `03` (เงินบำรุง), buy method `27` (เฉพาะเจาะจง), all bill/finance dates NULL |
| `MS_PO_C` | 1 | line for WORKING_CODE 1000010, 1 000 × 100 pack, 179.00/pack |
| `MS_IVO` / `MS_IVO_C` | 0 / 0 | no PO-based receipts have ever been recorded |
| `Agreement` | 0 | no contracts |
| `VCAR` / `BORROW` | 0 / 0 | no vendor claims / borrows |
| `REORDER` | 4 181 | 2023-11 … 2024-04, `PO_NO` values not present in `MS_PO` → seed data from another site (UNRESOLVED) |
| `BUDGET` | 2 | 2567 (closed) and **2569 (`BudgetOpen='O'`)**, type 03, 3 000 000 each; `deb1` 1 790 for 2569 (= the one PO) |
| `OTH_IVO` / `OTH_IVOC` | 110 / 1 018 | receipts **without PO**: RCV_TYPE 11 ยาเบิกจากแม่ข่าย CUP (16), 10 เวชภัณฑ์เบิกจาก CUP (11), 09 ยืมจากหน่วยงานอื่น (45), 05 คืนจากหน่วยเบิก (26), 01 บริจาค (4), 12 EPI vaccine (5), 13 (2), 08 (1) |

Conclusion: the PO tables are **real but nearly empty**. This installation is a CUP sub-unit that is
supplied by requisition from its parent (`OTH_IVO`), and has issued exactly one purchase order in FY 2569.

## 2. Field map (PO header)

| Business field | Column | Notes |
|---|---|---|
| PO number (internal) | `MS_PO.PO_NO` (nvarchar 10) | Format `YYnnnnn` where `YY` = last two digits of the Thai fiscal year (69 → 2569). Children (`MS_PO_C`) join on this. |
| Actual / document PO number | `MS_PO.REAL_PO` | e.g. `K6900001`. **`MS_IVO.PO_NO` and `OTH_IVOC.PO_NO` join on REAL_PO**, not PO_NO (PODetail.asp, table_ipiss.asp). `DOC_NO` = external document reference. |
| PO date | `MS_PO.PO_DATE` | |
| Vendor | `MS_PO.VENDOR_CODE` → `COMPANY.COMPANY_CODE`; display `COMPANY_NAME_PO` | COMPANY has no unique index on COMPANY_CODE (UNRESOLVED: duplicates possible) |
| Total items / total cost | `MS_PO.TOTAL_ITEM` (decimal 2,0 — max 99 lines), `MS_PO.TOTAL_COST`; also `OLD_TOTAL_COST`, `TOTAL_COST_RCV`, `DISCOUNT`, `DISCOUNT2`, `VAT` | |
| Status | `MS_PO.STATUS` → `TblPOStatus.StatusCode/StatusName` | codes: 0 ยกเลิก, 1 ออกใบสั่งซื้อแล้วรอรับของ, 2 รับของแล้ว, 3 รอตั้งเบิก, 4 ส่งเอกสารตั้งเบิก, 5 เสร็จสิ้น, 6 บัญชีตรวจสอบเอกสารตั้งเบิกแล้ว, 7 รอเสนอเช็ค, 8 รอจ่ายเช็ค, 9 เสร็จสิ้นกระบวนการ, A รอส่งให้คลัง, B รอยืนยันการสั่งซื้อ, C รอยกเลิก, D ส่งเอกสารให้บัญชี |
| Budget type | `MS_PO.BUDGET_TYPE` → `BDG_TYPE.BDGCODE/BDGNAME` | 14 types (03 เงินบำรุง …) |
| Buy method | `MS_PO.BUY_METHOD` → `TBLBUY.BUYCODE/BUYNAME` | 36 methods |
| ED/NED of the PO | `MS_PO.ED_NED` → `TBLED_NED.EDCODE` | legacy INNER-joins this too |
| Receive date | `MS_PO.FIRST_RCVDATE`; detail: `MS_IVO.DATE_RECEIVE` (max per REAL_PO in date mode) | |
| Accounting dates | `BILLIN` (documents in), `BILLOUT` (documents out), `BILLEND`, `BILLOUTACC` (ส่งตั้งหนี้), `BILL_ACC`, `BILL_END_ACC` (ปิดบัญชี) | Dashboard averages `PO_DATE→BILLIN→BILLOUT→BILLEND` |
| Finance dates | `BILLOUTFIN` (ส่งเอกสารการเงิน), `BILL_APPR`, `BILL_PAY`, `APPROVE_DATE_FIN`, `BILL_PAY_FIN` (ตัดจ่าย) | |
| Approval numbers | `APPROVE_NO_FIN` (char 7), `eGP_NO`, `CTRL_NO`, `AGREENO`, `BILLNO`, `BR_NO`, `BS_NO` | |
| Close / accounting status | derived from `STATUS` buckets (see §4) and `BILL_END_ACC IS NOT NULL` | |
| Process days | `DATEDIFF(DAY, FIRST_RCVDATE, GETDATE())` (legacy; the intended "stop at BILL_PAY_FIN" branch is a no-op) | table_ipiss.asp |

## 3. Field map (PO lines and receipts)

* `MS_PO_C` (join `PO_NO = MS_PO.PO_NO`): `RUNNO`, `WORKING_CODE`→INV_MD, `VENDOR_CODE`, `MANUFAC_CODE`,
  `BUY_UNIT_COST`, `QTY_ORDER` (sale units), `PACK_RATIO1` → packs = `QTY_ORDER/PACK_RATIO1`, `QTY_FREE`/`PACK_RATIO2`,
  `BUY_VALUE`, `PO_UNIT`, `REMAIN` (stock at ordering time), `USED_RATE`, `RCV_FLAG`, `QTY_ORDER_RCV`.
* `MS_IVO` (join `PO_NO = MS_PO.REAL_PO`): `RECEIVE_NO`, `INVOICE_NO`, `INVOICE_DATE`, `DATE_RECEIVE`, `TOTAL_COST`, `DATE_ACC`.
* `MS_IVO_C` (join `RECEIVE_NO = MS_IVO.RECEIVE_NO`; also carries `INVOICE_NO`): `WORKING_CODE`, `QTY_ORDER`, `PACK_RATIO1`,
  `QTY_FREE`, `EXPIRED_DATE1/2`, `LOCATION1/2`, `LOTNO`, `BUY_UNIT_COST`, `QTY_RECEIVE`.
* MS_IVO ↔ MS_PO relationship: **one PO (REAL_PO) → many invoices/receipts**; the legacy code shows all receipts of a PO and
  uses `MAX(DATE_RECEIVE)` as "received date". No FK exists; the join is by string equality on REAL_PO. (Verified from code only —
  no live rows to confirm at this site → mark data-level verification UNRESOLVED.)

## 4. Status buckets used by the dashboard (keep verbatim)

| Bucket | STATUS set |
|---|---|
| PO issued (มูลค่าใบสั่งซื้อ) | `NOT IN ('0','C')` |
| Received (ตรวจรับแล้ว) | `IN ('2','3','4','5','6','7','8','9','D')` |
| Sent to accounting (ส่งตั้งหนี้) | `IN ('4','5','6','7','8','9','D')` |
| Sent to finance (ส่งเอกสารการเงิน) | `IN ('4','5','7','8','9')` |
| Closed (เสร็จสิ้น) | `IN ('5','9')` |
| PODetail "received" column | `STATUS NOT IN ('1','A','B')` |
| Per-status summary | `GROUP BY TblPOStatus.StatusCode` (ipiss_process.asp) |

## 5. Fiscal-year logic

* Thai fiscal year = 1 Oct – 30 Sep, numbered by the พ.ศ. year in which it **ends**:
  `YearBudget(d) = YEAR(d)+543 + (MONTH(d) >= 10 ? 1 : 0)` (inc_functions.asp; inline `CASE` in table_*.asp).
* PO pages do **not** use PO_DATE; they filter `LEFT(MS_PO.PO_NO, 2) = RIGHT(FiscalYear, 2)` — i.e. the fiscal year is
  encoded in the PO number prefix (`69xxxxx` → FY 2569). Dashboard defaults to the open budget year `BUDGET.year WHERE BudgetOpen='O'` (currently 2569).
* Month keys in reports: `yyyymm + 54300` (Thai calendar).

## 6. Why the old web application does not show PO data — root-cause matrix

| Candidate cause | Finding | Pages affected |
|---|---|---|
| Wrong database / server | **Ruled out.** Web string, current Access links and live data all point to `DESKTOP-BVH8F8L.INV`. | – |
| Broken / missing connection object | **Confirmed.** `Conn` is never instantiated; `Connections/INVFlood.asp` has the `Set conn` lines commented out. Every page that opens recordsets with `Conn` fails before rendering. | Dashboard.asp, table_ipiss.asp, ipiss_process.asp, ipiss_budget.asp, ipiss_agree.asp, table_*.asp, EOC.asp, SMPO*.asp |
| Obsolete query / schema | **Confirmed for legacy PO monitor.** POStatus.asp queries `POMonitor/InvMonitor/PODetail/DRUG` (absent). API_EPOC.asp includes a missing file. | POStatus.asp, API_EPOC.asp |
| Filtering logic | **Contributing.** PO_Search.asp requires `POstatus` + `YYY` in the querystring but its form only sends `RPO` to PODetail; all six lookups are INNER joins, so a PO with a vendor/budget/buy-method code missing from the lookup table vanishes; the FY filter is on PO_NO prefix, not PO_DATE. | PO_Search.asp, PO.asp, table_ipiss.asp, PODetail.asp |
| Genuinely missing data | **Confirmed and dominant at this site.** `MS_PO` has 1 row, `MS_IVO` 0. Goods arrive via `OTH_IVO` (CUP requisition / borrow), which no legacy web page reports. | all PO pages |

Even after the `Conn` bug is fixed, the PO dashboard will show a single PO for FY 2569 unless the new
application also reports non-PO receipts (`OTH_IVO`/`OTH_IVOC` by `RCV_TYPE`), which is where this site's
inbound supply actually lives. Nothing was fixed in Phase 0.

## 7. Distinguishing the three report types (Step 9)

| Concept | Legacy page(s) | Source tables | Verdict |
|---|---|---|---|
| **A. Inventory status** | INV_Status.asp, ShelfList.asp, chkstock.asp, EOC.asp | INV_MD, INV_MD_C, VCAR/BORROW, SUBSTOCK, CARD | current stock per item |
| **B. Reorder recommendation** | **INV_Report_Purchase.asp, INV_Report_Purchase_Print.asp** | INV_MD only (QTY_ON_HAND vs MIN/ROP/MAX) | despite its name, **not a purchase-order report** — it never reads MS_PO. Access also has `REORDER`/`BUYPLAN` tables for its own proposal workflow. |
| **C. Actual purchase orders** | PO_Search.asp, PODetail.asp, PO.asp, POforIPISS.asp, table_ipiss.asp, ipiss_process.asp, Dashboard.asp (PO cards) | MS_PO, MS_PO_C, MS_IVO, MS_IVO_C + lookups | real POs; plus non-PO receipts in OTH_IVO which the legacy web ignores |

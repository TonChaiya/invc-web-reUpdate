# Legacy Query Map — Classic ASP pages → SQL

Phase 0 audit, 2026-09-16. All SQL is built by **string concatenation** in VBScript and executed
through ADODB. Two connection idioms exist:

* **Idiom A** — `rs.ActiveConnection = MM_INVFlood_STRING` (string; ADO opens an implicit connection). Works.
* **Idiom B** — `rs.Open sql, Conn, 1, 3` where `Conn` is expected to be an `ADODB.Connection` object.
  **`Conn` is never created anywhere in the code base** (the `set conn = …` lines in
  `Connections/INVFlood.asp` are commented out and there is no `global.asa`). Every Idiom-B page fails at
  its first `.Open`.

Connection include chain: `Connections/INVFlood.asp` defines `MM_INVFlood_STRING`.
`Connections/HOMC.asp` includes INVFlood.asp, then sets `On Error Resume Next` (a page-global side effect)
and `MM_HOMC_STRING = MM_INVFlood_STRING` — so "HOMC" queries actually hit the INV database.

## Dashboard.asp  (ACTIVE, BROKEN — Idiom B)
* Includes: `Connections/INVFlood.asp`
* Connection: `Conn` (undefined) — page errors at the first `Budget.Open sql, Conn`.
* Purpose: purchasing/finance dashboard for a fiscal year (`BudgetYear` querystring → `Session("BudgetYear")`); loads partials by AJAX.
* Queries (tables → fields):
  1. `BUDGET` — `[year]`, `SUM(money)` where `BudgetOpen='O'` (or chosen year). Fiscal year = `BUDGET.year` (พ.ศ.).
  2. `MS_PO` counts/sums by status bucket, filtered `LEFT(PO_NO,2) = RIGHT(FiscalYear,2)`:
     * POValue: `STATUS NOT IN ('0','C')`
     * RCVValue (received): `STATUS IN ('2','3','4','5','6','7','8','9','D')`
     * ACCValue (sent to accounting): `IN ('4','5','6','7','8','9','D')`
     * FINValue (finance): `IN ('4','5','7','8','9')`
     * ENDValue (closed): `IN ('5','9')`
  3. `INV_MD` — `SUM(TOTAL_VALUE)` (main store value); `COUNT(WORKING_CODE) WHERE NoUse IS NULL`.
  4. `Substock` — `SUM(TOTAL_VALUE)`.
  5. `MBS_RE_M` + `MNTH_SUM` — latest YEAR/MONTH: `SUM(MNTH_SUM.TOTAL_VALUE)/SUM(MBS_RE_M.SALE_VALUE)` = months-of-stock ratio.
  6. `Agreement ⋈ COMPANY ⋈ TBLBUY ⋈ INV_MD` — remaining contract value `(AgreeQty−BuyQty)/PACK_RATIO×UnitPrice`, `Expdate>GETDATE()`.
  7. `MS_PO` — avg `DATEDIFF` PO_DATE→BILLIN→BILLOUT→BILLEND per month (process time).
  8. `CARD ⋈ INV_MD` (`R_S_STATUS='S'`) monthly sales for a **hard-coded** `WORKING_CODE = "2010930"` (debug value left in code).
* Known problems: `Conn` undefined; hard-coded WORKING_CODE; fiscal-year filter relies on PO_NO prefix.

## INV_Status.asp  (ACTIVE — Idiom A, partially broken)
* Includes: `Connections/INVFlood.asp`, `Connections/HOMC.asp`, `menu.asp`
* Connection: `MM_INVFlood_STRING` (list, borrow); `MM_HOMC_STRING` (= INV string) for `Med_inv`.
* Purpose: inventory status list (menu "สถานะคงคลัง") with keyword search.
* Queries:
  1. `SELECT INV_MD.* FROM INV_MD WHERE NOUSE IS NULL [AND drug_name/composition/HOSP_CODE/WORKING_CODE LIKE '%kw%'] ORDER BY CAST(WORKING_CODE AS INT), DRUG_NAME COLLATE Thai_CI_AS`
  2. Per row: `VCAR` with correlated `BORROW` sum → borrowable qty `BO = VCAR.BORROW_QTY − SUM(BORROW.BORROW_QTY)` where `CLOSE_STATUS<>'C'`.
  3. Per row: `SELECT [name] FROM Med_inv WHERE code=HOSP_CODE AND site='1'` — **table does not exist in INV**; the error is swallowed by `On Error Resume Next`. `HOSP_CODE` is NULL for all 316 items anyway.
* Displayed fields: DRUG_NAME (+HOMC name), VEN, WORKING_CODE (HOSP_CODE), QTY_ON_HAND, SALE_UNIT, LOCATION, RATE_PER_MONTH, BO, months-left `ROUND(QTY_ON_HAND/RATE_PER_MONTH,2)` ("N/A" when rate = 0).
* Known problems: N+1 queries (2 per item); keyword escaped with `Replace("'","''")` but `CAST(WORKING_CODE AS INT)` throws on non-numeric codes; HOMC lookup unreachable; `On Error Resume Next` hides every error.

## INV_Report_Purchase.asp / INV_Report_Purchase_Print.asp  (ACTIVE — Idiom A)
* Includes: `Connections/INVFlood.asp` (+ `menu.asp` in the screen version)
* Connection: `rs.Open sql, MM_INVFlood_STRING, 1, 1`
* Purpose: **Reorder recommendation ("Dashboard สถานะยาที่ต้องสั่งซื้อ") — NOT actual purchase orders.** No PO table is touched.
* Query: `SELECT WORKING_CODE, DRUG_NAME, QTY_ON_HAND, MIN_LEVEL, MAX_LEVEL, REORDER_QTY FROM INV_MD WHERE (NOUSE IS NULL OR NOUSE='') AND (OUT_OF_LIST IS NULL OR OUT_OF_LIST='') ORDER BY WORKING_CODE`
* Logic (VBScript, per row): `reorder = REORDER_QTY; if reorder = 0 then reorder = MIN_LEVEL`;
  status **red** if `QTY_ON_HAND < MIN_LEVEL`; **yellow** if `MIN_LEVEL <= QTY < reorder`; else **green**;
  `suggest = CEILING(MAX_LEVEL − QTY_ON_HAND)`. Filter `?status=red|yellow|green` (default red).
* Known problems: filtering done in VBScript after fetching all rows; in the live DB 189/316 items have MIN_LEVEL 0/NULL and 177 have MAX_LEVEL 0 → most items are "green" and `suggest` is negative; `ROP_EXCEPT`, `NO_BUY`, `VMI`, `RATE_CAL`, `MIN_PER_MONTH/MAX_PER_MONTH` are ignored.

## table_ipiss.asp  (ACTIVE, BROKEN — Idiom B)
* Includes: `Connections/INVFlood.asp`; loaded by Dashboard.asp with `?FiscalYear=&flag=PO|RCV|ACC|FIN|END` or `?SelectedDate=dd/mm/yyyy`.
* Connection: `Conn` (undefined).
* Query (same shape for every flag; only the STATUS set differs — see Dashboard buckets):
  `MS_PO ⋈ COMPANY (VENDOR_CODE=COMPANY_CODE) ⋈ TBLED_NED (ED_NED=EDCODE) ⋈ BDG_TYPE (BUDGET_TYPE=BDGCODE) ⋈ TblPOStatus (STATUS=StatusCode) ⋈ TBLBUY (BUY_METHOD=BUYCODE)`
  fields: PO_NO, REAL_PO, PO_DATE, DOC_NO, STATUS, COMPANY_NAME_PO, TOTAL_ITEM, TOTAL_COST, StatusName, BILLIN, BILLOUT, BILLEND, BILLOUTACC, BILLOUTFIN, BDGNAME, BUYNAME, FIRST_RCVDATE, BILL_END_ACC, APPROVE_NO_FIN, `DATEDIFF(DAY,FIRST_RCVDATE,GETDATE()) AS PROCESSDAY`.
  Date mode adds `(SELECT MAX(DATE_RECEIVE) FROM MS_IVO WHERE PO_NO = MS_PO.REAL_PO) AS DateRCV` and filters `CONVERT(varchar,BILLOUT,112)=yyyymmdd`.
* Known problems: `Conn`; all joins are INNER — a PO whose vendor/budget/buy-method code is missing from a lookup table disappears silently; the PROCESSDAY `CASE` is a no-op (both branches identical).

## PO_Search.asp / PO.asp / POforIPISS.asp  (Idiom A)
* Query: same 6-table join as above, `WHERE STATUS='<POstatus>' AND LEFT(PO_NO,2)='<YYY>'` (PO.asp, PO_Search.asp) or `STATUS NOT IN ('0','C')` (POforIPISS).
* `dateList`: `SELECT DISTINCT CONVERT(varchar,BILLOUT,103), BILLOUT FROM MS_PO WHERE LEFT(PO_NO,2)=RIGHT(FiscalYear,2) AND BILLOUT IS NOT NULL`.
* Known problems: `POstatus`/`YYY` concatenated unescaped (SQL injection); the visible search form posts `RPO` to `PODetail.asp` — the status/year list is only reachable via a crafted URL, so a user opening the menu item sees an empty page.

## PODetail.asp / ProcessTime.asp  (Idiom A)
* Header: 6-table join `WHERE REAL_PO='<RPO>'` (unescaped).
* Lines: `MS_PO_C ⋈ INV_MD ON WORKING_CODE WHERE MS_PO_C.PO_NO = MS_PO.PO_NO` → QTY_ORDER/PACK_RATIO1, BUY_UNIT_COST, BUY_VALUE, QTY_FREE, REMAIN, USED_RATE.
* Receipts: `MS_IVO_C ⋈ MS_IVO ON RECEIVE_NO WHERE MS_IVO.PO_NO = MS_PO.REAL_PO` → INVOICE_NO/DATE, DATE_RECEIVE, lot/expiry/location.
* **Relationship evidence:** `MS_PO_C.PO_NO = MS_PO.PO_NO` (internal number) but `MS_IVO.PO_NO = MS_PO.REAL_PO` (document PO number). Same convention in table_ipiss.asp.

## default.asp  (ACTIVE — Idiom A)
Tables: `INV_MD ⋈ TBLED_NED` (value by ED/NED), `INV_MD` counts by ED_NED excluding NOUSE/OUT_OF_LIST/PO_INDIVIDUAL, `SUBSTOCK ⋈ DEPT_ID` value by dept, `BUDGET ⋈ BDG_TYPE` (BudgetOpen='O'), `MBS_RE_M` latest month sale/remain, `MNTH_SUM` remain for that month, plus agreement/contract summaries. Uses `inc_functions.asp` (`YearBudget`).

## chkstock.asp / seesubstock.asp / ShelfList.asp / pending.asp  (Idiom A)
* chkstock: `DRUG_VN` by BAR_CODE; `INV_MD` by code; `INV_MD_C` lots ⋈ `DRUG_VN` ⋈ `COMPANY`×2; `CARD`, `CARD_SUBS`, `SUBSTOCK` by code; `DEPT_ID`/`COMPANY` per row.
* ShelfList: `INV_MD` [⋈ `LOCATION` on LOCATION=LOCATION_NAME] filtered by `Session("Shelf_ID")`/`Session("RESP_PER")`; lots from `INV_MD_C ⟕ DRUG_VN`.
* pending: `PENDING ⋈ DEPT_ID ⋈ SM_PO ⋈ INV_MD` where `CLEAR_PENDING IS NULL`, ordered by VEN (V→E→N); `DEPT_ID WHERE PENDING='Y'`.

## Connections/INVFlood.asp (sanitised)
```
MM_INVFlood_STRING = "Provider=SQLOLEDB.1;User ID=sa;password=<REDACTED>;Initial Catalog=INV;Data Source = DESKTOP-BVH8F8L;"
' set conn = Server.CreateObject("ADODB.Connection")   <- commented out
' set cmd  = Server.CreateObject("ADODB.Command")       <- commented out
' MM_INVFlood_STRING = "dsn=INVFlood;pwd=<REDACTED>;"   <- older DSN variant, commented out
```
## Connections/HOMC.asp
```
<!--#include file="INVFlood.asp" -->
On Error Resume Next
If IsEmpty(MM_HOMC_STRING) Then MM_HOMC_STRING = MM_INVFlood_STRING
If IsObject(Conn) Then Set MM_HOMC_Conn = Conn        <- Conn is never an object
```
Historically HOMC = hospital HIS database (`CRHBACK` on 172.16.1.15, table `Med_inv`, per the Access links).
This include now silently redirects HOMC queries to INV, where `Med_inv` does not exist.

## Obsolete / non-functional pages (evidence)
* `POStatus.asp` — tables `POMonitor`, `InvMonitor`, `InvMonitorDetail`, `PODetail`, `DRUG`, `DataUpdated` absent from INV; uses Jet `Last()`.
* `DrugCatalog.asp` — `MM_INVFloodCalalog_STRING` undefined; table `DrugCatalog` absent.
* `API_EPOC.asp` — `Connections/EPOC.asp` missing; `bookPO` is in external DB `nsaraban` (DBAPPSRV).
* `FindGen.asp` — `dbo.Med_inv` absent from INV.

# Data Source Map — connections, servers, and the production SQL Server

Phase 0 audit, 2026-09-16. **No secrets are reproduced here.** Every password seen in source or in the
Access link strings was masked before being recorded.

## 1. Connection strings found in the web source

| Location | Variable | Provider | Server | Database | Login | State |
|---|---|---|---|---|---|---|
| `Connections/INVFlood.asp` | `MM_INVFlood_STRING` | SQLOLEDB.1 | `DESKTOP-BVH8F8L` (default instance) | `INV` | **`sa`** (password hard-coded) | ACTIVE |
| `Connections/INVFlood.asp` (commented) | `MM_INVFlood_STRING` | ODBC DSN `INVFlood` | – | – | pwd hard-coded | INACTIVE |
| `Connections/HOMC.asp` | `MM_HOMC_STRING` | alias of `MM_INVFlood_STRING` | same | `INV` | same | ACTIVE (semantically wrong: HOMC ≠ INV) |
| `Connections/HOMC.asp` | `MM_HOMC_Conn` | `Set … = Conn` if `IsObject(Conn)` | – | – | – | never assigned |
| `Connections/EPOC.asp` | `MM_EPOC_STRING` | – | – | – | – | **file missing** (used by API_EPOC.asp) |
| `DrugCatalog.asp` | `MM_INVFloodCalalog_STRING` | – | – | – | – | **never defined** |
| `_mmServerScripts/MMHTTPDB.asp` | `Request("ConnectionString")`, `Request("UserName")`, `Request("Password")` | any | any | any | any | **arbitrary DB access over HTTP** (Dreamweaver remote script) |

Server instances referenced anywhere in the source tree: only `DESKTOP-BVH8F8L`.
Databases referenced: `INV` (plus implied external DBs via table names — see §4).

## 2. Where `Conn` is expected vs. where it is defined

* Expected (`rs.Open sql, Conn, …`) in 19 files: `Dashboard.asp`, `EOC.asp`, `SMPOC.asp`, `SMPOCPrint.asp`,
  `SMPO_2nd_floor.asp`, `searchItem.asp`, `ipiss_agree/budget/item_ratio/maininv_remain/process/substock_remain.asp`,
  `table_by_item/disp/disp_diff/ipiss/ipiss_item/monthly_rpt.asp`, and referenced in `Connections/HOMC.asp`.
* Defined: **nowhere**. `grep -rn "Set Conn\|Conn.Open"` finds only the commented lines in INVFlood.asp and the
  unrelated `oConn` in `_mmServerScripts/MMHTTPDB.asp`. There is no `global.asa`.
* Consequence: every Idiom-B page throws at its first recordset open (VBScript runtime: Conn is `Empty`).
  With `web.config` `httpErrors errorMode="Detailed"`, the raw error is shown to the browser.

`MM_INVFlood_STRING` is used (Idiom A) in 30 files — these work.

## 3. Are the connection includes internally consistent?  **No.**

1. `INVFlood.asp` comments out the object creation but the newer pages (2022–2025, Dashboard/IPISS/table_*) were
   written against a `Conn` object → the include and the pages come from different generations of the app.
2. `HOMC.asp` was clearly meant to hold a second connection string to the HIS (`HOMC`/`CRHBACK`); it now
   aliases INV and adds `On Error Resume Next`, which leaks into every page that includes it.
3. Two idioms coexist for the same database, and `DrugCatalog.asp`/`API_EPOC.asp` refer to a third and fourth
   connection that no longer exist.
4. The source connection string says `DESKTOP-BVH8F8L`; the older Access frontend (`INVC.mdb`, 2016 era) pointed
   at `RXTEST` — the web source was re-pointed at the current server at some point, but the `Conn` object was not restored.

## 4. Production SQL Server discovery (READ ONLY, Windows auth, `SELECT` + catalog views only)

| Item | Value |
|---|---|
| Host / instance | `DESKTOP-BVH8F8L` (this machine), default instance `MSSQLSERVER` |
| Version / edition | SQL Server 2022 (16.0.1000.6) **Enterprise Evaluation Edition** |
| Database | `INV` — created 2026-07-27, compatibility level **100** (SQL 2008), collation `Thai_CI_AS` |
| Authentication mode | Mixed (SQL + Windows). SQL logins: only **`sa`**. Windows login: `DESKTOP-BVH8F8L\Mainam8888` |
| DB users | `dbo` (NT AUTHORITY\SYSTEM), `PHAROPD` (orphaned SQL user, no login) |
| Schema | everything is `dbo` |
| Objects | 116 user tables, **0 views, 0 stored procedures, 0 functions, 0 triggers, 0 foreign keys** |
| Other DBs on instance | DWConfiguration / DWDiagnostics / DWQueue (PolyBase), system DBs |

**Is this production?** Evidence says yes for this site:
* The three current Access frontends (`INVC_update.mdb`, `INVC25690219.mdb`, `INVC กอสะเลียม เพิ่มรายงาน.mdb`)
  link 86–87 tables to `SERVER=DESKTOP-BVH8F8L; DATABASE=INV` (links were created from workstation `ZENBOOKDUO`).
* Operational tables are being written daily: `CARD` max `OPERATE_DATE` = 2026-09-15, `SM_PO` max
  `SUB_PO_DATE` = 2026-09-15, `MNTH_SUM` through 2026-08.
* `INVENTORY` = `01 คลังเวชภัณฑ์หลัก`; departments are CUP สันโค้ง / รพ.สต. units → this is a **primary-care (CUP sub-unit) store**,
  not a large hospital. Data volumes are therefore small (INV_MD 316 items).

The old server `RXTEST` (Access `INVC.mdb` 2016, `DataMigration.mdb`) and the external hosts
`172.16.1.15` (CRHBACK), `IPISSSRV` (IPISSDB), `DBAPPSRV` (nsaraban) are **not reachable** from this machine (TCP 1433 test).

## 5. Object map for the tables named in the Phase 0 brief (database `INV`, schema `dbo`)

| Object | Type | Rows | PK | Important columns | Relationships (evidence) |
|---|---|---|---|---|---|
| `INV_MD` | table | 316 (267 active) | `RECORD_NUMBER`; unique `IX_INV_MD(WORKING_CODE)` | WORKING_CODE, DRUG_NAME, SALE_UNIT, QTY_ON_HAND, TOTAL_COST, TOTAL_VALUE, RATE_PER_MONTH, MIN_LEVEL, REORDER_QTY, MAX_LEVEL, MIN_PER_MONTH, MAX_PER_MONTH, RATE_CAL, LOCATION, HOSP_CODE, VEN, ABC, ED_NED, NOUSE, OUT_OF_LIST, PO_INDIVIDUAL, VMI, NO_BUY, ROP_EXCEPT, STD_RATIO1-3, STD_PRICE1-3 | WORKING_CODE is the item key used by every child table (query evidence, no FK) |
| `MS_PO` | table | **1** | `RECORD_NUMBER` | PO_NO, REAL_PO, DOC_NO, PO_DATE, VENDOR_CODE, BUDGET_TYPE, ED_NED, BUY_METHOD, TOTAL_ITEM, TOTAL_COST, STATUS, BILLIN, BILLOUT, BILLEND, BILLOUTACC, BILLOUTFIN, BILL_ACC, BILL_APPR, BILL_PAY, BILL_END_ACC, FIRST_RCVDATE, APPROVE_NO_FIN, APPROVE_DATE_FIN, BILL_PAY_FIN, eGP_NO, AGREENO | VENDOR_CODE→COMPANY.COMPANY_CODE; BUDGET_TYPE→BDG_TYPE.BDGCODE; BUY_METHOD→TBLBUY.BUYCODE; STATUS→TblPOStatus.StatusCode; ED_NED→TBLED_NED.EDCODE (all from legacy joins) |
| `MS_PO_C` | table | 1 | `RECORD_NUMBER` | PO_NO, RUNNO, WORKING_CODE, VENDOR_CODE, MANUFAC_CODE, BUY_UNIT_COST, QTY_ORDER, PACK_RATIO1, QTY_FREE, PACK_RATIO2, BUY_VALUE, REMAIN, USED_RATE, RCV_FLAG | PO_NO→MS_PO.PO_NO; WORKING_CODE→INV_MD |
| `MS_IVO` | table | **0** | `RECORD_NUMBER` | INVOICE_NO, INVOICE_DATE, DATE_RECEIVE, PO_NO, VENDOR_CODE, BUDGET_TYPE, TOTAL_COST, RECEIVE_NO, DATE_ACC, RCV_CTRL_NO | **PO_NO→MS_PO.REAL_PO** (query evidence in PODetail/table_ipiss) |
| `MS_IVO_C` | table | 0 | `RECORD_NUMBER` | INVOICE_NO, RECEIVE_NO, WORKING_CODE, BUY_UNIT_COST, QTY_ORDER, PACK_RATIO1, QTY_FREE, EXPIRED_DATE1/2, LOCATION1/2, LOTNO | RECEIVE_NO→MS_IVO.RECEIVE_NO (legacy join); INVOICE_NO also present |
| `CARD` | table | 2 619 | none declared (`RECORD_NUMBER` exists, not PK) | WORKING_CODE, OPERATE_DATE, R_S_STATUS (R=receive, S=issue), R_S_NUMBER, DEPT_ID, VALUE, COST, ACTIVE_QTY1-3, REMAIN_QTY, REMAIN_VALUE, CANCEL_FLAG | WORKING_CODE→INV_MD; DEPT_ID→DEPT_ID or COMPANY (legacy code tries both) |
| `VCAR` | table | 0 | `RECORD_NUMBER` | INVOICE_NO, VENDOR_CODE, PO_NO, WORKING_CODE, INVOICE_QTY, PO_QTY, BORROW_QTY, CLOSE_STATUS | vendor-claim / borrow ledger header |
| `BORROW` | table | 0 | `RECORD_NUMBER` | VCAR_CODE, DEPT_ID, WORKING_CODE, BORROW_DATE, RETURN_DATE, BORROW_QTY, BORROW_STATUS, PENDING_CODE | VCAR_CODE→VCAR.RECORD_NUMBER (INV_Status query) |
| `COMPANY` | table | 1 182 | `RECORD_NUMBER` | COMPANY_CODE, COMPANY_NAME, COMPANY_NAME_PO, VENDOR_FLAG, MANUFAC_FLAG, TAX_NO, HIDE_COMP | COMPANY_CODE is the business key (no unique index — UNRESOLVED) |
| `TblPOStatus` | table | 14 | `StatusCode` | StatusCode (0-9,A-D), StatusName | lookup |
| `BDG_TYPE` | table | 14 | `BDGCODE` | BDGCODE, BDGNAME | lookup (`BDG_TYPE from INV` is a stray 5-row copy) |
| `TBLBUY` | table | 36 | `BUYCODE` | BUYCODE, BUYNAME, REGULATION, MAX_VALUE, BUY_METHOD | lookup |
| `TBLED_NED` | table | 5 | `EDCODE` | EDCODE (1=ED,2=NED,3=MES,4=EA,5=SAM), EDNAME, EDMAP, DEBIT/CREDIT codes | lookup |
| `BUDGET` | table | 2 | `RECORD_NUMBER` | year (พ.ศ.), type→BDG_TYPE, money, deb1, deb2, BudgetOpen ('O' = open year) | year 2569 is open |
| `Agreement` | table | 0 | `AgreeNO` | AgreeYear, Agreement, Expdate, Company, WORKING_CODE, BuyMethod, UnitPrice, AgreeQty, BuyQty, PACK_RATIO, eGP_NO | Company→COMPANY.COMPANY_CODE; WORKING_CODE→INV_MD |
| `MNTH_SUM` | table | 3 911 | `RECORD_NUMBER` | YEAR, MONTH, DATE, WORKING_CODE, QTY_REMAIN, TOTAL_VALUE, TOTAL_COST, ED_NED | month-end stock snapshot per item (2025-08 … 2026-08) |
| `MBS_RE_M` | table | 1 350 | `RECORD_NUMBER` | YEAR, MONTH, WORKING_CODE, RCV_QUAN/VALUE, SALE_QUAN/VALUE, REMAIN_QUAN/VALUE, *_COST | monthly receive/issue/remain per item |
| `SUBSTOCK` | table | 261 | `RECORD_NUMBER` | DEPT_ID, WORKING_CODE, QTY_ON_HAND, PACK_RATIO, S_MIN, S_MAX, RATE_PER_DAY, TOTAL_VALUE, LOCATION | DEPT_ID→DEPT_ID; WORKING_CODE→INV_MD |

Additional objects that matter for this site (found during discovery):

| Object | Rows | Note |
|---|---|---|
| `OTH_IVO` / `OTH_IVOC` | 110 / 1 018 | Receipts **without a PO** (RCV_TYPE 09 borrow, 10/11 requisition from CUP parent, 05 returns, 01 donations). `OTH_IVOC.PO_NO` is empty in all rows. `TOTAL_COST` is 0.00 for all headers. |
| `RCV_TYPE` | 14 | lookup for OTH_IVO.RCV_TYPE |
| `SM_PO` / `SM_PO_C` | 302 / 1 561 | Sub-store requisitions (issues to departments) |
| `REORDER` | 4 181 | Reorder proposals dated 2023-11 … 2024-04 with `PO_NO` values that do not exist in `MS_PO` → looks like **template/seed data from another site** (UNRESOLVED) |
| `BUYPLAN` / `BUYPLAN_copy1/2` | 265 / 28 501 / 28 501 | Purchase plan; the copies are stray |
| `LOCATION` | 11 | shelf groups; `INV_MD.LOCATION` joins on `LOCATION_NAME` |
| `DEPT_ID` | 12 | sub-store departments |
| `USER` | 4 | web/Access users, MD5 password hex (32 chars) |
| `INV_MD_C` | 249 | lots per item (expiry, location, lot cost) |

`Substock` as written in the legacy code resolves to `dbo.SUBSTOCK` (case-insensitive collation).

## 6. Access linked tables → SQL Server (Step 6)

Read via DAO `TableDefs` (read-only open) from the frontends in `C:\INVC`. Password portions masked.

| Frontend | Linked tables | Target |
|---|---|---|
| `INVC_update.mdb` (2026-09-07) | 87 | `DESKTOP-BVH8F8L`.`INV`.`dbo.<same name>` — login `sa` |
| `INVC25690219.mdb` (2026-09-15) | 86 | same |
| `INVC กอสะเลียม เพิ่มรายงาน.mdb` (2026-09-15) | 86 | same |
| all three | `APPAY_D`, `APPAY_H`, `RECV_H` | `172.16.1.15`.`CRHBACK` (HIS back-office), login `homc` — unreachable |
| all three | `AudImport` | `IPISSSRV`.`IPISSDB`, login `homc` — unreachable |
| all three | `bookPO` | `DBAPPSRV`.`nsaraban`, login `dbappsrv` — unreachable |
| `INVC.mdb` (2016) | 56 | `RXTEST`.`INV` (+1 to `INV_BLANK`) — historical |
| `DataMigration.mdb` (2015) | 25 + 25 | DataFlex ODBC (`C:\DATA`) and `RXTEST`.`INV` — the original DataFlex→SQL migration tool |

Name mapping rule: every linked table keeps its SQL name (`X → dbo.X`) except **`TblUser → dbo.USER`**.
All tables named in the brief (INV_MD, MS_PO, MS_IVO, CARD, VCAR, BORROW, COMPANY, TblPOStatus, BDG_TYPE,
TBLBUY, TBLED_NED, BUDGET, Agreement, MNTH_SUM, MBS_RE_M, SUBSTOCK) are linked 1:1 by the current frontends
to `DESKTOP-BVH8F8L.INV.dbo`. Only `INVC_update.mdb` additionally links `QUICK_PAY`.

Local (non-linked) Access tables in the current frontends are import scratch tables (`Buy_dataset`,
`CO_Purchase*`, `Sheet1`, `Jun`, `May`, `TEST_JSON`, …) — not authoritative.

## 7. Conclusions for the connection problem

| Hypothesis | Verdict | Evidence |
|---|---|---|
| Wrong database/server | **No** | Web string, Access links, and live data all point to `DESKTOP-BVH8F8L.INV` |
| Broken/missing connection object | **Yes — primary cause** for Dashboard.asp, table_ipiss.asp, ipiss_*.asp, EOC.asp, SMPO* | `Conn` never instantiated |
| Obsolete query/schema | Yes for POStatus.asp, DrugCatalog.asp, FindGen.asp, API_EPOC.asp, HOMC name lookup | referenced tables/includes absent |
| Filtering logic | Contributing | PO_Search needs `POstatus`+`YYY` in the URL that the UI never sends; INNER joins drop POs with unknown codes |
| Genuinely missing data | **Yes — dominant for this site** | `MS_PO` = 1 row, `MS_IVO` = 0 rows. Stock is received through `OTH_IVO` (CUP requisitions/borrow), not purchase orders |

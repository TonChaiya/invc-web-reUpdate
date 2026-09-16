# Legacy File Inventory — INVC Web (Classic ASP)

Phase 0 audit, 2026-09-16. Root: `C:\INVC\Web`. No git repository exists (see phase report).
Source encoding for all `.asp` files: **Windows-874 (TIS-620) + CRLF**, `CODEPAGE="874"`.

Classification legend: **ACTIVE** = reachable from the live menu / used by an active page;
**REFERENCE** = kept for evidence/history, not executed; **DUPLICATE** = byte/logic copy of another file;
**BACKUP** = dated or `_old`/`_backup` copy; **OBSOLETE** = depends on tables/files that no longer exist;
**UNKNOWN** = purpose not determinable from source.

## 1. Entry points & navigation

| File | Class | Evidence |
|---|---|---|
| `index.html` | ACTIVE | Static login landing (Login_v4 template) → posts to `login.asp` |
| `login.asp` / `logout.asp` | ACTIVE | MD5 login against `[USER]`; sets session |
| `default.asp` | ACTIVE | Home dashboard (12 recordsets: ED/NED value, budget, month-stock, agreements) |
| `Menu.asp` | ACTIVE | Navbar included by most pages (`#include file="menu.asp"`) |
| `Dashboard.asp` | ACTIVE (BROKEN) | Purchasing dashboard; every recordset opens with `Conn`, which is never initialised (see data-source-map) |
| `Menu 25681208.asp` | BACKUP | Menu.asp minus the `<style>` block (dated 2568-12-08) |
| `Menu1.asp`, `Menu11.asp` | DUPLICATE | Identical 969-byte 2017 menus |
| `Menu_19082560.asp`, `Menu_old.asp` | BACKUP | 2017 menus |
| `default ก่อนเพิ่มบริหารสัญญา.asp`, `default_15102564.asp` | DUPLICATE/BACKUP | Byte-identical to each other; pre-"contract management" version of default.asp |
| `default_03122559.asp`, `default_old.asp` | BACKUP | 2016/2017 versions |
| `index1.html`, `index_old.html`, `Untitled-1.html` | OBSOLETE | Old landing pages / Dreamweaver scratch |

## 2. Inventory status family (`INV_Status*`)

| File | Class | Evidence |
|---|---|---|
| `INV_Status.asp` | ACTIVE | Menu "สถานะคงคลัง". Lists `INV_MD` (NOUSE IS NULL), per-row `VCAR/BORROW` subquery, HOMC name lookup (broken, see query map) |
| `INV_Status_30032560.asp` | BACKUP | 2017-03-30 version; 6 recordsets incl. KPI table (month-of-stock, ED:NED ratio) |
| `INV_Status_backup.asp` | BACKUP | Same as 30032560 plus an extra "sub-store value" table |
| `INV_Status_COVID19.asp` | REFERENCE | 2020 COVID variant of the status list |
| `INV_Status.rar` | BACKUP (archive) | 2025-10-14 archive of INV_Status.asp |
| `status_test.asp` | REFERENCE | 2017 test copy (8 recordsets) |
| `New Text Document.txt` | REFERENCE | 2012 Access-era snippet (`drug` table, `onhand28`, `rate3M`) — not SQL Server schema |

## 3. Reorder / report family

| File | Class | Evidence |
|---|---|---|
| `INV_Report_Purchase.asp` | ACTIVE | Menu "รายงานแนะนำซื้อ". **Reorder recommendation from `INV_MD` only — not a PO report** |
| `INV_Report_Purchase_Print.asp` | ACTIVE | Print view of the same logic |
| `EOC.asp` | ACTIVE (BROKEN) | V/E items with sub-stock qty; uses `Conn` |
| `ShelfList.asp` | ACTIVE | Menu "รายการตามชั้นเก็บ"; INV_MD by LOCATION / RESP_PERSON, lots from INV_MD_C |
| `ShelfList.rar` | BACKUP (archive) | 2025-10-14 |
| `chkstock.asp` | ACTIVE | Item drill-down (INV_MD, INV_MD_C, CARD, CARD_SUBS, SUBSTOCK) linked from INV_Status |
| `seesubstock.asp` | ACTIVE | Sub-store drill-down (CARD_SUBS, SUBSTOCK_C) |
| `pending.asp` | ACTIVE | Menu "รายการค้างจ่าย" (PENDING, SM_PO, DEPT_ID) |
| `DrugCatalog.asp` | OBSOLETE | Uses undefined `MM_INVFloodCalalog_STRING` and table `DrugCatalog` (not in INV) |
| `FindGen.asp` | OBSOLETE | Queries `dbo.Med_inv` (HOMC/CRHBACK table) through INV connection |

## 4. Purchase-order family

| File | Class | Evidence |
|---|---|---|
| `PO_Search.asp` | ACTIVE | Menu "ค้นหาใบสั่งซื้อ"; MS_PO by `POstatus` + `YYY` (fiscal-year prefix) via `MM_INVFlood_STRING`; also BILLOUT date list |
| `PODetail.asp` | ACTIVE | PO header + MS_PO_C lines + MS_IVO/MS_IVO_C receipts |
| `PO.asp` | DUPLICATE | Older copy of PO_Search (2017) |
| `POforIPISS.asp` | REFERENCE | PO list variant for IPISS dashboard (2022) |
| `ProcessTime.asp` | REFERENCE | 2016 PO process-time page, same queries as PODetail |
| `table_ipiss.asp` | ACTIVE (BROKEN) | Dashboard PO tables (PO/RCV/ACC/FIN/END + by BILLOUT date); uses `Conn` |
| `POStatus.asp` | OBSOLETE | 2013; tables `POMonitor`, `InvMonitor`, `PODetail`, `DRUG`, `DataUpdated` do not exist; Jet `Last()` function |
| `API_EPOC.asp` | OBSOLETE | Includes missing `Connections/EPOC.asp`; `bookPO` lives in external `nsaraban` DB |

## 5. Dashboard partials (loaded by Dashboard.asp via AJAX) — all use `Conn` → BROKEN

`ipiss_agree.asp`, `ipiss_budget.asp`, `ipiss_item_ratio.asp`, `ipiss_maininv_remain.asp`,
`ipiss_process.asp`, `ipiss_substock_remain.asp`, `table_by_item.asp`, `table_disp.asp`,
`table_disp_diff.asp`, `table_ipiss_item.asp`, `table_monthly_rpt.asp`, `searchItem.asp` — **ACTIVE (BROKEN)**.

## 6. Sub-store dispensing / checking workflow (write pages)

| File | Class | Evidence |
|---|---|---|
| `ChkDisp.asp`, `ChkDispDetail.asp`, `ChkConfirm.asp` | ACTIVE | Dispensing check screens (SM_PO / SM_PO_C / [USER]) |
| `SaveChecked.asp`, `SavePrintSlip.asp` | ACTIVE (WRITES) | Update `SM_PO_C.CHECKED`, `SM_PO.CHKUSER/ACC_NO` — the only pages that write to the DB |
| `SMPOC.asp`, `SMPOCPrint.asp`, `SMPO_2nd_floor.asp` | ACTIVE (BROKEN) | Second-floor pick lists; use `Conn` |

## 7. Connections & includes

| File | Class | Evidence |
|---|---|---|
| `Connections/INVFlood.asp` | ACTIVE | Defines `MM_INVFlood_STRING` (SQLOLEDB, sa, DB `INV`, host `DESKTOP-BVH8F8L`). `Set conn = …` lines are commented out |
| `Connections/HOMC.asp` | ACTIVE (INCONSISTENT) | Includes INVFlood.asp, aliases `MM_HOMC_STRING = MM_INVFlood_STRING`, turns on `On Error Resume Next` globally |
| `Connections/EPOC.asp` | MISSING | Referenced by API_EPOC.asp |
| `include/inc_functions.asp` | ACTIVE | `YearBudget()` (Thai fiscal year), `CMonth()` |
| `include/_desktop.ini` | OBSOLETE | Windows artefact |
| `web.config` | ACTIVE | Only `httpErrors errorMode="Detailed"` (leaks errors to clients) |
| `_mmServerScripts/*` | OBSOLETE | Dreamweaver MMHTTPDB remote-DB scripts; **accepts connection string/user/password via HTTP** — must be removed |
| `_notes/*.mno`, `_mmServerScripts/_notes` | OBSOLETE | Dreamweaver sync notes; mention non-existent pages (`editinv.asp`, `CheckExp.asp`, `save_edit_event.asp`) |

## 8. Static assets & archives

| File | Class |
|---|---|
| `bootstrap/`, `css/`, `js/`, `vendor/`, `fonts/`, `font-awesome-5.12.1/`, `Login_v4/` | ACTIVE (vendor) |
| `login-form-v4/` (237 files), `login-form-v4.zip`, `bootstrap-3.3.7-dist.zip`, `backup_css_js.rar`, `Web.rar` (2017 full-site archive) | BACKUP/DUPLICATE |
| `css/bootstrap.min - Copy.css`, `css/bootstrap.min_old.css`, `css/sb-admin*.js` (JS in css folder) | DUPLICATE |
| `Printer.gif`, `correct.jpg`, `correct.png`, `edit.png` | ACTIVE (images) |
| `test.asp`, `testtimefunc.asp` | UNKNOWN / scratch |

## 9. Summary counts

ACTIVE 31 (of which 17 BROKEN by the missing `Conn` object or missing includes) · REFERENCE 6 · DUPLICATE 6 · BACKUP 12 · OBSOLETE 9 · UNKNOWN 2 · vendor dirs 8.
Nothing was moved or deleted in Phase 0.

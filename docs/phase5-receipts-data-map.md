# Phase 5 — Non-PO receipts: data map (source of truth)

Discovered read-only on **2026-09-16 12:00 (+07:00)** from `DESKTOP-BVH8F8L.INV` (`sys.columns`, `sys.indexes`, sample and
aggregate SELECTs). No legacy web page reports these tables, so nothing here is inferred from column names alone.

## Tables
| Table | Rows | PK | Purpose (evidence) |
|---|---|---|---|
| `dbo.OTH_IVO` | 110 | `RECORD_NUMBER` | Receipt header for inbound stock **without a purchase order** (CUP requisition, borrow, return, donation, EPI vaccine, opening stock) |
| `dbo.OTH_IVOC` | 1 018 | `RECORD_NUMBER` | Receipt lines |
| `dbo.RCV_TYPE` | 14 | `RCV_TYPE_CODE` (nvarchar 2) | Receipt-type lookup (`RCV_NAME`, `RCV_TYPE_DATASET`) |

## Header ↔ line relationship — PROVEN
| | Value |
|---|---|
| HEADER KEY | `OTH_IVO.RECEIVE_NO` (nvarchar 10, format `O` + FY prefix + 5 digits, e.g. `O6900085`; 110 distinct of 110, none null; no declared unique index) |
| LINE FOREIGN KEY | `OTH_IVOC.RECEIVE_NO` — 1 018 / 1 018 lines match a header; 0 orphan lines; 0 headers without lines; 73 headers with > 1 line |
| CARDINALITY | 1 header → N lines (max observed 29 lines, `TOTAL_ITEM` = line count for 110/110 headers) |
| Rejected alternative | `INVOICE_NO` also matches every line but is **not unique** among headers (103 distinct / 110) → not a key |
`OTH_IVOC.PO_NO` is NULL/blank in **all 1 018 lines** (re-verified): these receipts do not belong to any purchase order.

## Receipt type — PROVEN
`OTH_IVO.RCV_TYPE` (nchar 2, never null) → `RCV_TYPE.RCV_TYPE_CODE` (PK, unique, 0 duplicates). Orphan header codes: 0.
Lookup is still done with LEFT JOIN so a header can never disappear; a missing name renders "ไม่พบชื่อประเภท".
Current distribution (headers / lines): 01 ยาบริจาค 4/13 · 05 ยาคืนจากหน่วยเบิก 26/429 · 08 ยาคืนจากผู้ป่วย 1/1 ·
09 ยายืมจากหน่วยงานอื่น 45/121 · 10 เวชภัณฑ์เบิกจาก CUP 11/135 · 11 ยาเบิกจาก CUP 16/297 · 12 EPI VACCIN 5/11 · 13 สนับสนุนจากหน่วยงานภายนอก 2/11.

## Date / fiscal year — PROVEN
Authoritative receipt date: **`OTH_IVO.DATE_RECEIVE`** (never null; range 2025-08-20 … 2026-09-15). Evidence: the fiscal-year
prefix embedded in `RECEIVE_NO` (`O69…`) equals the Thai fiscal year of `DATE_RECEIVE` for 110/110 headers; `INVOICE_DATE`
differs from `DATE_RECEIVE` on 11 headers (document date, not receipt date). Fiscal year = shared `ThaiFiscalYear.FromDate(DATE_RECEIVE)`
(1 Oct – 30 Sep). Current: FY 2568 = 29 headers, FY 2569 = 81. The PO_NO prefix rule of Phase 4 is **not** used.

## Source (DPT_CODE)
`OTH_IVO.DPT_CODE` resolves in `COMPANY.COMPANY_CODE` for 110/110 headers (0 in `DEPT_ID`): CUB001 ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล 73,
OPEN "รับยาครั้งแรก" 17, KSL001 8, SKP โรงพยาบาลสันกำแพง 7, PT001 2, GPO001 2, PAO001 1. Because `COMPANY_CODE` is not unique
(Phase 2), the name is resolved with `OUTER APPLY (SELECT TOP 1 … ORDER BY RECORD_NUMBER)`.

## Quantity / value semantics — PROVEN
| Column | Meaning (evidence) |
|---|---|
| `OTH_IVOC.QTY_ORDER` (decimal 14,0) | quantity received in **sale units**; never 0/null |
| `OTH_IVOC.PACK_RATIO` (decimal 7,0) | units per pack; never 0/null today (guarded anyway) |
| packs = `QTY_ORDER / PACK_RATIO` | consistent with header value formula below |
| `OTH_IVOC.UNIT_VALUE` (decimal 12,2) | **price per pack**; `SUM(UNIT_VALUE × QTY_ORDER / PACK_RATIO)` = `OTH_IVO.TOTAL_VALUE` for **110/110** headers (SUM(UNIT_VALUE) matches only 63, SUM(UNIT_VALUE×QTY) only 75) |
| line value = `UNIT_VALUE × QTY_ORDER / PACK_RATIO` | derived, verified by the header identity above |
| `OTH_IVO.TOTAL_VALUE` (decimal 14,2) | header value; 0.00 for 60 headers (borrow/donation/return types carry no value) |
| `OTH_IVO.TOTAL_COST`, `OTH_IVOC.BUY_UNIT_COST` | **0 for every row** — not meaningful, not displayed |
| `OTH_IVO.TOTAL_ITEM` | line count (110/110) — used for header/line reconciliation |
| `EXPIRED_DATE`, `LOTNO`, `LOCATION` | populated on every line; displayed (expired rows highlighted) |
| `MANUFAC_CODE`, `VENDORC` | company codes (= DPT_CODE on sampled rows); displayed as codes only |
| `INV_MD.WORKING_CODE` | every line's `WORKING_CODE` exists exactly once in `INV_MD` (unique index) → item name via OUTER APPLY TOP 1 |

## UNRESOLVED (not displayed / not used)
`OTH_IVO.PROCESS` ('A' 5, 'C' 105), `COMBINE_NO`, `COMBINE_STATUS` ('P' all), `ERROR_ENTER/ERROR_PROCESS` (all NULL), `USERID`,
`OTH_IVOC.PROCESS/SUB_PROCESS`, `RCV_TYPE.RCV_TYPE_DATASET` — semantics unknown; no Access documentation read in this phase.
`RECEIVE_NO` uniqueness is observed, not constrained.

## Distinction from purchase orders
`MS_PO/MS_PO_C/MS_IVO/MS_IVO_C` describe orders placed with vendors and their invoice receipts (0 receipts at this site).
`OTH_IVO/OTH_IVOC` describe every other inbound flow; `OTH_IVOC.PO_NO` is empty. The two modules share no tables and no routes.

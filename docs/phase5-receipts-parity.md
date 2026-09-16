# Phase 5 — Non-PO receipts (รับเข้าอื่น / รับจาก CUP): parity and decisions

Base commit `19230a7` · parity executed **2026-09-16 14:09 (+07:00)** against live `DESKTOP-BVH8F8L.INV` (read-only).
Data map (keys, relationships, semantics): `docs/phase5-receipts-data-map.md`. Tests: `ReceiptParityTests` (integration),
`ReceiptTests` / `ReceiptSqlTests` (unit). Values are point-in-time evidence, re-derived on every test run.

## Module boundary
`/Receipts`, `/Receipts/Detail/{receiveNo}`, `/Receipts/Print/{receiveNo}` read only `OTH_IVO`, `OTH_IVOC`, `RCV_TYPE`
(+ `COMPANY` for the source name, `INV_MD` for item names). Unit tests assert the SQL never references `MS_PO*`/`MS_IVO*`;
the page states that these receipts do not reference a purchase order and links to the PO module for real POs.

## Key rules (from the data map)
| Rule | Value |
|---|---|
| Header key / detail route | `OTH_IVO.RECEIVE_NO` (1–10 letters/digits/`-`/`_`, else 404) |
| Lines | `OTH_IVOC.RECEIVE_NO = OTH_IVO.RECEIVE_NO` (1 018/1 018, 0 orphans, 0 headers without lines) |
| Type | `RTRIM(OTH_IVO.RCV_TYPE)` LEFT JOIN `RCV_TYPE.RCV_TYPE_CODE`; missing → "ไม่พบชื่อประเภท"; 0 orphans, 0 duplicate codes |
| Source | `OTH_IVO.DPT_CODE` → `COMPANY` via `OUTER APPLY TOP 1 … ORDER BY RECORD_NUMBER` (COMPANY_CODE not unique) |
| Fiscal year | Thai FY of `DATE_RECEIVE` via shared `ThaiFiscalYear`; SQL window `DATE_RECEIVE >= @From AND < @To` (typed datetimes, 1 Oct → 1 Oct exclusive); PO prefix rule **not** used |
| Default period | current FY if it has receipts (2569 ✔), else latest FY with data (+ note), else current FY (+ note) |
| Type filter | `type` → two alphanumerics or none ("all"/blank/invalid → all); options loaded from `RCV_TYPE` |
| Search | RECEIVE_NO, INVOICE_NO, RCV_NAME, COMPANY_NAME, DPT_CODE — trimmed, ≤ 50, nvarchar param, `%`/`_`/`[` literal |
| Packs | `QTY_ORDER / PACK_RATIO` (null when ratio 0/NULL) |
| Line value | `UNIT_VALUE × QTY_ORDER / PACK_RATIO` (UNIT_VALUE = price per pack; identity with header holds 110/110) |
| Reconciliation | `TOTAL_ITEM` vs line count, `TOTAL_VALUE` vs Σ line value — both identities hold for every header today |
| Not used | `TOTAL_COST`, `BUY_UNIT_COST` (always 0), `PROCESS`, `COMBINE_*`, `ERROR_*`, `USERID` (UNRESOLVED) |

## Pages
`/Receipts`: fiscal-year selector (years with data), type selector (RCV_TYPE), search, cards (headers, lines, type count,
Σ TOTAL_VALUE with a note that valueless types record 0), type breakdown (clickable → filter), result count, table
(receipt no., date, type, source, reference, lines, quantity, value), empty/error states, data-origin explainer.
`/Receipts/Detail/{no}`: cards (type, date + FY, lines/quantity, value), header facts (source, reference no./date, note),
line table (code → Inventory detail when the item exists, name or "ไม่พบชื่อรายการ", quantity, pack ratio, packs, price/pack,
value, lot, expiry — expired rows highlighted, location), reconciliation footer. `/Receipts/Print/{no}`: layout-less A4 view.

## Parity C10a–C10j — executed 2026-09-16 14:09 +07:00, FY 2569 (1 Oct 2025 – 30 Sep 2026)
| Check | Raw SQL | New | Result |
|---|---|---|---|
| C10a header count | FY 81; all years 110 | 81; Σ years 110 | **PASS** |
| C10b line count | FY 575; all 1 018 | 575; Σ years 1 018 | **PASS** |
| C10c header ↔ line set | O6900084 (type 09, 1 line), O6900048 (type 11, 43 lines), O6900085 (type 10, 2 lines): raw line sets | identical field-by-field (code, qty, pack, unit value, lot, expiry, location); list LineCount = detail count | **PASS** |
| C10d headers by type | 01:4 · 05:7 · 08:1 · 09:42 · 10:10 · 11:15 · 12:1 · 13:1 | identical (+ Σ TOTAL_VALUE per type identical: 05 8 400.15, 10 83 013.16, 11 260 389.53, 12 941.40) | **PASS** |
| C10e lines by type | 13 · 22 · 1 · 117 · 130 · 278 · 4 · 10 | identical | **PASS** |
| C10f fiscal-year grouping | FY2569 81 (1 Oct 2025 … 15 Sep 2026), FY2568 29 (20 Aug … 25 Sep 2025) | identical; every header's FY = its year; boundary unit tests 30 Sep/1 Oct | **PASS** |
| C10g representative headers | O6900085 (10, CUB001), O6900082 (11, CUB001), O6900084 (09, CUB001), O6900065 (05, PT001 รพ.สต.บ้านป่าตาล) — 12 fields each | identical | **PASS** |
| C10h representative lines | 12 headers sampled; value identity 81/81, item-count identity 81/81 | packs/derived names consistent; 0 inconsistent reconciliations | **PASS** |
| C10i data-loss guards | orphan type codes 0; lines without INV_MD item 0; duplicated source codes 0; VALUES-based shape: missing item keeps the row with null name | unknown/malformed keys → null/404 | **PASS** |
| C10j screen/detail consistency | — | detail header record equals list row (record equality) for 4 headers | **PASS** |
Unexplained deltas: **none**.

## Performance
List (81 headers, joins + line aggregate): 32 ms server-side first run (compile), sub-ms after; RCV_TYPE 0 ms; header 0 ms;
43 lines with item lookup 8 ms; distinct dates 0 ms. Per request: list 3 statements, detail 2. List HTML ≈ 98 KB.
No pagination, cache or indexes (theoretical `OTH_IVOC(RECEIVE_NO)` index documented only).

## Intentional decisions
1. Header value shown is `TOTAL_VALUE` (proven) — never `TOTAL_COST`. 2. Type/source lookups cannot drop headers.
3. Invalid `type`/`fy` normalise to "all"/default. 4. Search added (no legacy equivalent). 5. No dashboard cards (Phase 6).

## UNRESOLVED
`OTH_IVO.PROCESS` (A/C), `COMBINE_NO/COMBINE_STATUS`, `ERROR_*`, `USERID`, `OTH_IVOC.PROCESS/SUB_PROCESS`, `RCV_TYPE.RCV_TYPE_DATASET`;
`RECEIVE_NO` uniqueness is observed (110/110), not constrained.

## Tests
Unit 268 (was 215): key validation, FY derivation and 30 Sep/1 Oct boundary, FY/type parsing, packs, line value, report
aggregation/type filter, display fallbacks, reconciliation, default period, SQL guard + relationship + FY window.
Integration 39 (was 32): C10a–C10j.

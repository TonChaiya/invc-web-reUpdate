# Phase 2 — Inventory Status: review, parity and hardening

Base commit `27481db` · executed **2026-09-16 10:58 (+07:00)** against live `DESKTOP-BVH8F8L.INV` (read-only).
All figures below are point-in-time evidence, **not** acceptance constants — the integration tests re-derive
them from the database on every run (`tests/Invc.IntegrationTests/InventoryPhase2ParityTests.cs`).

## 1. Review of the Phase 1 prototype (Step 1)

| # | Deviation from legacy INV_Status.asp | Verdict | Reasoning |
|---|---|---|---|
| 1 | Explicit column list instead of `SELECT INV_MD.*` | **Keep** — improves safety, no visible change | The page only ever displayed 8 columns; `INV_MD.MEMO_NOTE` is `nvarchar(max)` and would be fetched for every row. A5 verifies each displayed field equals the legacy row source. |
| 2 | VCAR/BORROW N+1 folded into one LEFT JOIN | **Keep, with one documented semantic change** | Legacy opened a recordset per item and used only the *first* open claim (unordered). The new query sums all open claims per item. For 0 or 1 open claim the result is identical; for >1 the legacy result was undefined. `CLOSE_STATUS <> 'C'` is retained verbatim, so a NULL close status excludes the claim exactly as before. Verified by A7 (production = 0 everywhere; synthetic table-value rows exercise 0/1/many/closed/NULL cases). |
| 3 | Sort `LEN(WORKING_CODE), WORKING_CODE` instead of `CAST(WORKING_CODE AS INT)` | **Adjusted** | The Phase 1 form diverges from the cast for leading-zero codes (`0100` vs `100`). Replaced by a zero-padded key for digit-only codes (`RIGHT(REPLICATE('0',20)+code,20)`) which orders exactly like the integer cast including leading zeros, puts any non-numeric code after the numeric ones, and cannot throw. Secondary key `DRUG_NAME COLLATE Thai_CI_AS` added to match the legacy ORDER BY. Proven by A9 over the whole list (267 codes, all 7-digit numeric today). |
| 4 | HIS name lookup (`Med_inv`) omitted | **Keep** | Table does not exist in INV; the legacy call was silently failing under `On Error Resume Next`. `HOSP_CODE` is NULL for all 316 items, so the "(HOSP_CODE)" suffix is shown only when present. |
| 5 | Parameterised search | **Keep** | Removes the SQL-injection path; A8 shows identical counts for the legacy LIKE shape. Input is trimmed and bounded to 50 chars (`SearchKeyword.Normalize`; longest searchable column is nvarchar(50)). |
| 6 | `%`, `_`, `[` escaped in LIKE | **Keep — intentional behaviour change** | Legacy treated them as wildcards (a bare `%` returned everything). New behaviour: literal match. Unit tests pin the escaping; A8 asserts `%` no longer matches everything and `50%` finds 1 real item. |

Also reviewed and retained unchanged: `InventoryRules.MonthsOfStock` (VBScript `round(Q/A,2)`, banker's rounding,
`"N/A"` for rate 0/NULL), `ReadOnlySqlConnectionFactory` (refuses `sa`, forces `ApplicationIntent=ReadOnly`).

## 2. Data model (Step 2)

`InventoryItem` exposes exactly the legacy report fields: WORKING_CODE, DRUG_NAME, HOSP_CODE, VEN, QTY_ON_HAND,
SALE_UNIT, LOCATION, RATE_PER_MONTH, borrowable quantity, months of stock. Null handling verified against production:
VEN is NULL for all 267 active items (rendered blank, no placeholder); RATE_PER_MONTH NULL for 25 (→ "N/A");
HOSP_CODE NULL for all (suffix omitted); QTY_ON_HAND/TOTAL_VALUE never NULL but coalesced to 0 defensively.

## 3. Summary totals (Step 3)

Filter: **`INV_MD.NOUSE IS NULL`** ("active items") — the same filter as the legacy status list. One statement
(`SummaryByEdNed`) groups active items by `ED_NED` with a LEFT JOIN to `TBLED_NED`; the headline cards are the sums of
the groups, so cards and breakdown cannot disagree. This is *not* the dashboard definition
(default.asp additionally excluded OUT_OF_LIST / PO_INDIVIDUAL and used INNER JOIN); at this site both give 267/173/8/86.

## 4–7. Search, sort, months of stock, borrowable — see §1 and the parity table.

## 8. Item detail / lots (Step 8)

Route `/Inventory/Detail/{workingCode}` (code validated: 1–7 letters/digits, else 404; unknown code → 404).
Two statements per request: header (`INV_MD` ⟕ `TBLED_NED` + borrow join) and lots (`INV_MD_C`).
Lot columns follow the legacy chkstock.asp lot table: trade name (`DRUG_VN.TRADE_NAME` on WORKING_CODE+PACK_RATIO+VENDOR_CODE+MANUFAC_CODE),
packs (`QTY_ON_HAND / PACK_RATIO`, null instead of the legacy "reuse previous row" bug when ratio = 0), quantity, pack ratio,
expiry (rows past today highlighted), lot no., location, vendor and manufacturer names (`COMPANY` by code) — plus `LOT_VALUE`
and a header-vs-lots reconciliation footer.
Because `COMPANY.COMPANY_CODE` (`MED015` ×2) and the DRUG_VN key are **not unique** in production, lookups use
`OUTER APPLY (SELECT TOP 1 … ORDER BY RECORD_NUMBER)` so a lot row is never multiplied (verified: lot count on the detail
page equals `COUNT(*)` from `INV_MD_C` for sampled items).
**UNRESOLVED / not displayed:** `INV_MD_C.RECORD_STATUS` ('' or NULL), `DISP_FIRST` (all NULL), `BAR_CODE`, `LOT_COST`.

## 11. Pagination decision

Not implemented. Active list = 267 rows; server-side execution time of the full list query is **5 ms** (SET STATISTICS TIME),
summary 3 ms, lot totals 1 ms; the HTML for 267 rows is ~120 KB. A single result set is faster and simpler for users than paging.
Revisit if a deployment exceeds ~2 000 active items.

## 12. Performance review

Per request: Status page = 2 statements (summary + list), Detail page = 2 statements (header + lots), Health = 2.
No N+1, no `SELECT *`, no per-lot queries from the list. All joins are on the business keys the legacy code used.
Indexes that would theoretically help (documented only — **no database change**): `INV_MD_C(WORKING_CODE)` for lot lookups,
`VCAR(WORKING_CODE, CLOSE_STATUS)` if claims ever grow. Unnecessary at current volume.

## 13. Parity matrix A1–A11 — executed 2026-09-16 10:58:49 +07:00

| Check | Legacy / raw SQL | New implementation | Raw value | New value | Result |
|---|---|---|---|---|---|
| A1 active row count | `SELECT COUNT(*) FROM INV_MD WHERE [NOUSE] IS NULL` | `GetStatusAsync(null).Count` / `Summary.ActiveItemCount` | 267 | 267 | **PASS** Δ0 |
| A2 quantity total | `SUM(QTY_ON_HAND)` same filter | `Summary.TotalQtyOnHand` | 154 357 | 154 357 | **PASS** Δ0 |
| A3 value total | `SUM(TOTAL_VALUE)` same filter | `Summary.TotalValue` | 104 692.54 | 104 692.54 | **PASS** Δ0 |
| A4 ED/NED groups | `GROUP BY ED_NED` same filter | `Summary.Groups` | ED 173 / 142 684 / 73 638.64 · NED 8 / 3 016 / 461.08 · MES 86 / 8 657 / 30 592.82 | identical | **PASS** Δ0 |
| A5 10 representative items | legacy row source ordered by `CAST(WORKING_CODE AS int), DRUG_NAME COLLATE Thai_CI_AS` | rows #0,1,2,66,89,133,178,264,265,266 of `GetStatusAsync(null)` | e.g. 1000010 Acyclovir 400 mg tab 100/100 → 1; 1001590 HYDRALAZINE 500/333 → 1.5; 1001100 0/7 → 0; 3000850 2/NULL → N/A | all 8 fields + months equal | **PASS** |
| A6 months-of-stock rule | VBScript `round(Q/A,2)`, "N/A" if A=0 | `InventoryRules.MonthsOfStock` | 14 unit cases incl. 0.125→0.12 / 0.135→0.14 (banker's), 0.5, negatives | equal | **PASS** |
| A7 borrowable qty | per-item VCAR−Σ BORROW, `CLOSE_STATUS<>'C'` | single LEFT JOIN | open claims 0 → all 0; synthetic rows A=11, B=NULL (NULL status excluded), C=NULL (qty 0 excluded) | equal | **PASS** |
| A8 search | legacy LIKE over 4 columns | parameterised, escaped | "10"→187 · "ยา"→24 · "mg"→116 · "  tab  "→95 · "50%"→1 | identical | **PASS** (intentional: `%` literal) |
| A9 sort | legacy CAST order, full sequence | zero-padded key | first 1000010,1000020,1000030 · mid 1001590 · last 3000840,3000850,3000860; non-numeric codes: 0 | identical (267/267) | **PASS** |
| A10 lot reconciliation | `COUNT/SUM` from INV_MD_C per active item vs INV_MD | `GetLotTotalsAsync` + `LotReconciliation` | 267 items, 191 with lots, 249 lots; qty mismatches 0, value mismatches 0 | identical | **PASS** |
| A11 sub-store value | `SUM(SUBSTOCK.TOTAL_VALUE) BY DEPT_ID` | not in scope — measured only | 224 292.18 / 3 depts (Phase 0) | – | **Documented, not implemented** |

Unexplained deltas: **none**.

## 15–16. Tests and guard

Unit 96 (was 52): search normalisation/bounding, LIKE escaping, working-code validation, packs-on-hand, reconciliation,
summary-from-groups, months-of-stock edges, SQL guard over every Phase 2 statement (incl. `static readonly` ones) and the
borrow template with table-value constructors. Integration 14 (was 7): A2–A10, detail parity, invalid codes.
`ReadOnlySql.Ensure` was **not** weakened; all new SQL (CASE, RIGHT/REPLICATE, OUTER APPLY, CAST … AS bit, VALUES) passes as-is.

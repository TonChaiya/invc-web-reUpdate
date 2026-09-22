# Borrow ("ยายืมจากหน่วยงานอื่น") — data map, mirror schema and reconciliation

Checkpoint 1 (2026-09-17); live facts re-verified 2026-09-22 (unchanged: 45 bills / 121 lines / Σ 177,132; CUB001 44 bills latest 2026-08-24, PAO001 1 bill; 0 duplicate RECEIVE_NO; all quantities integers 1–10,000, packs 1–1,000). Return workflow: `docs/borrow-workflow.md`. Source of truth: **SQL Server `INV` — STRICTLY READ-ONLY** (SELECT only). Application-owned store:
**MySQL `invc_web`** (Laragon MySQL 8.4.3, writable) holding a mirror of the type-09 receipts. Live proof: `.work/borrow-foundation/live-proof.txt`.

## 1. Source tables and keys (INV, verified via `sys.columns` 2026-09-17)
| Table | Role | Key | Live facts |
|---|---|---|---|
| `dbo.RCV_TYPE` | receipt types | `RCV_TYPE_CODE nvarchar(2)` | `09` = ยายืมจากหน่วยงานอื่น (dataset 4); 14 types |
| `dbo.OTH_IVO` | receipt header | PK `RECORD_NUMBER int`; `RECEIVE_NO nvarchar(10)`, `RCV_TYPE nchar(2)`, `DPT_CODE nvarchar(6)`, `INVOICE_NO nvarchar(20)`, `INVOICE_DATE`, `DATE_RECEIVE`, `SYSDATE` | 110 rows total; **45 with `RTRIM(RCV_TYPE)='09'`**; `RECEIVE_NO` **unique across all 110** (0 duplicates, 0 null/blank); type-09: no NULL DPT_CODE/INVOICE_NO/DATE_RECEIVE/SYSDATE; max RECEIVE_NO length 8 |
| `dbo.OTH_IVOC` | receipt line | PK `RECORD_NUMBER int`; `RECEIVE_NO nvarchar(10)`, `WORKING_CODE nvarchar(7)`, `QTY_ORDER decimal(14,0)`, `PACK_RATIO decimal(7,0)`, `LOTNO nvarchar(20)`, `MANUFAC_CODE`/`VENDORC nvarchar(6)`, `UNIT_VALUE`, `EXPIRED_DATE`, `LOCATION`, `PO_NO` | 1,018 rows; **121 belong to type-09 headers**; 0 orphan lines (any type); every type-09 line maps to exactly one header; no NULL qty, no 0/NULL pack; every working code exists in INV_MD |
| `dbo.COMPANY` | facility name | `COMPANY_CODE` (**not unique**: `MED015` ×2) | `DPT_CODE → COMPANY_CODE`, resolved with `OUTER APPLY (SELECT TOP 1 … ORDER BY RECORD_NUMBER)` |
| `dbo.INV_MD` | drug name | `WORKING_CODE` (unique index) | resolved with the same OUTER APPLY TOP 1 shape |

Facilities today: `CUB001` ศูนย์สาธารณสุขและการแพทย์ตำบลทรายมูล (44 bills, latest 2026-08-24) · `PAO001` รพ.สต.บ้านต้นเปา (1 bill, 2026-03-18). Σ QTY_ORDER = 177,132.
Sample `O6900084`: header 113, CUB001, invoice 24082569 (2026-08-24), received 2026-08-24, SYSDATE 2026-09-11 09:34:52; line 1036 `1001440` AMILORIDE + HCTZ (5+50MG) MODURETIC TAB, qty 250, pack 250, lot `808640.`.

## 2. Exact joins (`src/Invc.Infrastructure/Borrow/BorrowSourceRepository.cs`, both statements pass `ReadOnlySql.Ensure`)
```
headers: FROM dbo.OTH_IVO h OUTER APPLY (SELECT TOP 1 x.COMPANY_NAME FROM dbo.COMPANY x WHERE x.COMPANY_CODE = h.DPT_CODE ORDER BY x.RECORD_NUMBER) co
         WHERE RTRIM(h.RCV_TYPE) = @TypeCode          (@TypeCode = '09', nvarchar parameter)
lines:   FROM dbo.OTH_IVOC c JOIN dbo.OTH_IVO h ON h.RECEIVE_NO = c.RECEIVE_NO
         OUTER APPLY (SELECT TOP 1 m.DRUG_NAME FROM dbo.INV_MD m WHERE m.WORKING_CODE = c.WORKING_CODE ORDER BY m.RECORD_NUMBER) md
         WHERE RTRIM(h.RCV_TYPE) = @TypeCode
```
The line → header relationship is `RECEIVE_NO` only (INVOICE_NO is not used; it is identical on both sides for all 121 lines anyway).
`UNIT_VALUE`, `EXPIRED_DATE`, `LOCATION`, `PO_NO` are not mirrored in checkpoint 1.

## 3. MySQL mirror schema (`db/mysql/001_borrow_foundation.sql`, applied by `scripts/mysql-migrate.ps1`)
| Table | Columns | Keys |
|---|---|---|
| `schema_version` | version, description, applied_at | PK version |
| `borrow_source_bill` | source_record_number INT PK (= OTH_IVO.RECORD_NUMBER), receive_no VARCHAR(20) NOT NULL, invoice_no VARCHAR(40), invoice_date, date_receive, facility_code VARCHAR(12), facility_name VARCHAR(255), source_sysdate, source_hash CHAR(64), last_synced_at | **UNIQUE(receive_no)** (safe: proven unique), KEY facility_code, KEY date_receive |
| `borrow_source_item` | source_record_number INT PK (= OTH_IVOC.RECORD_NUMBER), source_bill_record_number INT NOT NULL, receive_no VARCHAR(20), working_code VARCHAR(14), drug_name VARCHAR(255), qty_order DECIMAL(14,0), pack_ratio DECIMAL(7,0), lot_no VARCHAR(40), manufac_code/vendor_code VARCHAR(12), source_hash CHAR(64), last_synced_at | FK → borrow_source_bill ON DELETE CASCADE; KEY receive_no, working_code, source_bill_record_number |
Column widths follow the live INV types (wider than the INVC manual) so no value can be truncated. utf8mb4. Migrations are idempotent (`IF NOT EXISTS`, version row) and never drop.
Setup: start Laragon MySQL → `.\scripts\mysql-migrate.ps1` (password via `MYSQL_PWD` env only; Laragon's local root has none) → connection string in the untracked `src/Invc.Web/appsettings.Development.local.json` (`AppDatabase:ConnectionString`) or `AppDatabase__ConnectionString`.

## 4. Reconciliation (`BorrowSyncService`, runs on every `/Borrow` and `/Borrow/Facility/{code}` request)
1. Read the **complete** type-09 snapshot from INV (headers + lines). Failure ⇒ stop; mirror untouched; page shows the last mirror with a warning.
2. Read stored hashes from the mirror; build `BorrowReconciliationPlan` in memory: insert (unknown id), update (hash differs), delete (id absent from snapshot), unchanged.
3. Apply in **one MySQL transaction**: upsert bills, upsert items, delete stale items, delete stale bills; any error ⇒ rollback.
4. Result: inserted / updated / deleted / unchanged counts + timestamp ("ซิงก์ล่าสุด … ใหม่ X · แก้ไข X · ลบ X").
Hashes: SHA-256 over trimmed source fields (NULL ≠ empty), never including `last_synced_at`. No "last RECEIVE_NO" incremental logic — edited old INVC bills are always reconciled.

## 5. Read-only boundary
INV access goes only through `ISqlConnectionFactory` (ApplicationIntent=ReadOnly, `sa` refused) and `ReadOnlySql.Ensure`. The only DML in the
application is `BorrowMirrorSql` (INSERT … ON DUPLICATE KEY UPDATE / DELETE on `borrow_source_*`), executed only through
`IAppDbConnectionFactory` (MySqlConnector), whose connection string is refused if it names database `INV`, uses SQL Server keywords
(`Initial Catalog`, `Integrated Security`, `Data Source`, `ApplicationIntent`) or the login `sa`. Unit tests pin all of this.

## 6. Implemented 2026-09-22 (migration 002, `docs/borrow-workflow.md`)
1. Return workflow per item / per bill with partial returns, derived statuses (ค้างคืน / คืนบางส่วน / คืนครบ / ต้องตรวจสอบ) — `borrow_return_event`, append-only.
2. Incoming non-borrow receipt advisory ("มีการรับเข้าหลังยืม") — INV read-only, set-based, informational only.
3. Return history / audit with corrections (reversal events), actor = authenticated name or `anonymous:<ip>` fallback.
4. `/Health` reports MySQL `invc_web` (state, schema version, last mirror sync) independently of INV.
Additional live fact used by the advisory: `RCV_TYPE` codes with headers today — 01 (4), 05 (26), 08 (1), 09 (45), 10 (11), 11 (16), 12 (5), 13 (2); 375 non-09 receipt lines match an outstanding borrow line's WORKING_CODE on/after its borrow date (119 of 121 lines).

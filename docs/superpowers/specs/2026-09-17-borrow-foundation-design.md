# Borrow foundation — design (checkpoint 1: MySQL mirror + automatic reconciliation + minimal UI)

**Goal.** A new "ยายืมจากหน่วยงานอื่น" module that shows INV type-09 receipts (drugs borrowed from other facilities) per facility, backed by
an application-owned MySQL mirror that is reconciled automatically from SQL Server INV on every visit. Legacy pages are reference only;
the UI is new (modern minimal, table-first). Owner intent and data rules: see the checkpoint brief; verified facts: `docs/borrow-data-map.md`.

**Non-goals (later checkpoints).** Return confirmation workflow, incoming non-borrow receipt alert, return history/audit, any write to INV,
any deployment/IIS change, deleting legacy code.

## Architecture
```
Invc.Core/Borrow        BorrowModels (BorrowSourceBill/Item, BorrowSourceHash, BorrowReconciliationPlan, BorrowSyncResult, BorrowOverview)
                        IBorrowSourceRepository (INV, SELECT) · IBorrowMirrorRepository (MySQL) · BorrowSyncService
Invc.Infrastructure/AppData   AppDatabaseOptions ("AppDatabase" section) · IAppDbConnectionFactory · MySqlAppDbConnectionFactory (MySqlConnector)
Invc.Infrastructure/Borrow    BorrowSourceRepository (Dapper on ISqlConnectionFactory, ReadOnlySql.Ensure) · BorrowMirrorRepository (Dapper on MySQL, transaction)
Invc.Web/Pages/Borrow         Index (/Borrow) · Facility (/Borrow/Facility/{facilityCode}) · nav "ยายืม"
db/mysql/001_borrow_foundation.sql + scripts/mysql-migrate.ps1   (explicit, versioned, idempotent; never at startup)
```
Two databases, two providers, two option classes, two factories. `MySqlAppDbConnectionFactory` refuses connection strings that name `INV`,
use SQL Server keywords, or the login `sa`. `ReadOnlySql.Ensure` is unchanged and still guards every INV statement. The mirror repository is
the only DML in the solution and is scoped to `borrow_source_bill` / `borrow_source_item` (unit-tested).

## Reconciliation semantics
Full snapshot from INV first (headers + lines, type 09 by parameter) → stored hashes from MySQL → in-memory plan (insert / update / delete /
unchanged by source `RECORD_NUMBER` and SHA-256 content hash) → one MySQL transaction (upsert bills, upsert items, delete stale items, delete
stale bills) → result with counts and timestamp. Source read failure ⇒ nothing in the mirror changes (service never reaches the mirror);
MySQL failure ⇒ rollback. No incremental "last RECEIVE_NO" logic. Dataset is small (45 bills / 121 lines); correctness over optimisation.

## UI (design language: compact, table-first, colour only for meaning)
- `/Borrow`: h4 title + one muted line; sync line right ("ซิงก์ล่าสุด … ใหม่ X · แก้ไข X · ลบ X", warning text if INV unreachable);
  summary pills `[สถานบริการ N] [บิล N] [รายการ N] [จำนวนรวม N]`; one search box (facility code/name, receive no, invoice no, working code, drug);
  facility table (name primary, code muted secondary, bills, items, total qty, latest date; latest date folds under the name below `sm`);
  `<details>` data provenance at the bottom. No cards.
- `/Borrow/Facility/{code}`: breadcrumb, title `ชื่อ (CODE)`, pills `บิล · รายการ · จำนวนรวม`, then one compact section per bill newest first
  (`O6900084 · 24 ส.ค. 2569 · เอกสาร 24082569 · n รายการ`) with a table ยา / รหัส / จำนวน / pack / lot (code and lot fold under the drug name below `md`/`sm`).
- No returned/not-returned controls in checkpoint 1.

## Configuration
`AppDatabase:ConnectionString` (MySqlConnector format). Development: untracked `src/Invc.Web/appsettings.Development.local.json` (git-ignored,
loaded by `Program.cs`) or `AppDatabase__ConnectionString`. Tracked `appsettings.Development.json` carries an empty placeholder only. Production
supplies it through IIS environment variables like `InvDatabase__ConnectionString` (not part of this checkpoint).

## Testing
Unit: hash determinism/sensitivity, plan classification, overview aggregation, sync ordering/failure, INV SQL passes `ReadOnlySql.Ensure` and has
no DML, mirror DML scoped to borrow tables and refused by the INV guard, factory refuses INV/SQL Server/sa, migration file has no DROP/TRUNCATE.
Web (WebApplicationFactory + fakes): `/Borrow` 200 with summary/table/search, facility page, 404s, mirror shown when INV fails.
Integration (skippable): type-09 parity against live INV (counts, ids, quantities, `O6900084` sample); reconciliation against an isolated
`invc_web_test` database (created from the migration, dropped afterwards): insert → unchanged → tampered/stale rows repaired/removed → FK rollback → facility filter.

# Borrow foundation — plan / execution record (checkpoint 1, 2026-09-17)

Base `3b78edf` with uncommitted Reorder UI work preserved (5 modified + 2 untracked files recorded before any edit).
Boundaries: SQL Server INV SELECT only · MySQL `invc_web` writable · no IIS/deploy/certificate/firewall · no legacy deletion · no commit/push.

| # | Step | Outcome |
|---|---|---|
| 1 | `git status --short` captured; Reorder files confirmed | ✔ |
| 2 | Live read-only proof (`.work/borrow-foundation/live-proof.txt`): 45 type-09 headers, 121 lines, RECEIVE_NO unique (all 110), 0 orphans, 1:1 line→header, O6900084 present | ✔ safe to implement |
| 3 | Laragon MySQL 8.4.3 inspected; not running → started `mysqld` with its own `my.ini`/data dir (process only, no service); default local root (no password) works | ✔ |
| 4 | Core models + failing tests (hash, plan, overview, sync) → green | ✔ 6 tests |
| 5 | Infrastructure: `AppData` factory (MySqlConnector), `BorrowSourceRepository` (INV), `BorrowMirrorRepository` (MySQL), DI `AddInvcAppData` | ✔ |
| 6 | `db/mysql/001_borrow_foundation.sql` + `scripts/mysql-migrate.ps1`; database `invc_web` created, version 1 applied | ✔ |
| 7 | Infrastructure guard tests (read-only guard, DML scoping, factory refusals, migration content) | ✔ 5 tests |
| 8 | Web: failing page tests → `Program.cs` (AppData + local config), `/Borrow`, `/Borrow/Facility/{code}`, nav "ยายืม", scoped CSS | ✔ 4 tests |
| 9 | Integration: INV parity + isolated `invc_web_test` reconciliation (insert/unchanged/update/delete/rollback/filter) | ✔ 2 tests |
| 10 | Docs: `docs/borrow-data-map.md`, this plan, design spec; `CLAUDE.md` boundary clarification | ✔ |
| 11 | `scripts/verify.ps1`, preview via `scripts/dev.ps1`, 375 px overflow check, screenshots under `.work/borrow-foundation-preview/` | see final report |

Deferred to later checkpoints: return confirmation workflow, incoming non-borrow receipt alert, return history/audit.

# Borrow ("ยายืมจากหน่วยงานอื่น") — return workflow: operations guide

Completed 2026-09-22 (migration 002). Companion of `docs/borrow-data-map.md` (source facts, mirror schema, reconciliation).

## 1. Source of truth and boundaries
| Fact | Where it lives | Writable by INVC Web? |
|---|---|---|
| What was borrowed (bill, item, quantity, lot, pack, facility) | SQL Server **INV** `OTH_IVO` / `OTH_IVOC`, `RTRIM(RCV_TYPE) = '09'` | **No — SELECT only, ever** |
| Mirror of the above (for fast reads and stable keys) | MySQL `invc_web.borrow_source_bill` / `borrow_source_item` (migration 001) | yes, only by the reconciliation |
| What was returned, by whom, when, and corrections | MySQL `invc_web.borrow_return_event` (migration 002) | yes, **append-only** |
`BorrowedQty` always comes from INV (`QTY_ORDER`). Nothing in the workflow ever changes INV; the receipt-after-borrow advisory reads INV read-only.

## 2. Mirror behaviour (unchanged from checkpoint 1)
Every visit to `/Borrow` or `/Borrow/Facility/{code}` reads the complete type-09 snapshot from INV, diffs it against the mirror by content hash and applies inserts/updates/deletes in one MySQL transaction. INV read failure ⇒ mirror untouched, page shows the last mirrored state with "ไม่สามารถซิงก์ข้อมูลจาก INV ได้ในขณะนี้". MySQL write failure ⇒ rollback, nothing partial. **The event table is not part of the mirror**: it has no foreign key to the mirror tables (deliberately — see §5) and reconciliation never touches it.

## 3. Return event model (`borrow_return_event`)
One row per event, never updated or deleted by the application:
`id · source_item_record_number (OTH_IVOC.RECORD_NUMBER) · source_bill_record_number (OTH_IVO.RECORD_NUMBER) · receive_no · working_code · drug_name · facility_code · facility_name` (snapshots) · `event_type` `RETURN` (quantity_delta > 0) or `CORRECTION` (quantity_delta < 0, `corrects_event_id` = the RETURN it reverses) · `event_at` (when the return happened, default = server time now) · `actor` · `note` · `client_request_id` (form token, UNIQUE) · `created_at`.
CHECK constraints keep the sign/type/link consistent. Indexes cover per-item balance, per-bill, per-facility+time, working code, actor, time.

## 4. Derived quantities and statuses (never stored)
- `ReturnedQty(item) = Σ quantity_delta` of its events; `OutstandingQty = max(0, BorrowedQty − ReturnedQty)`.
- Item: **ค้างคืน (OPEN)** ReturnedQty = 0 · **คืนบางส่วน (PARTIAL)** 0 < Returned < Borrowed · **คืนครบ (RETURNED)** Outstanding = 0.
- Bill: OPEN when no item has any return; RETURNED when nothing outstanding; otherwise PARTIAL. Facility and overall totals are sums of their items; a facility is "outstanding" when any bill is not RETURNED.
- **ต้องตรวจสอบ (conflict)**: `ReturnedQty > BorrowedQty` — only possible when INV later lowered `QTY_ORDER` below what was already returned (or a bill line was moved). The item is flagged on every screen, further returns are refused, nothing is auto-corrected; a human reviews and, if needed, records a CORRECTION with a reason. Conflicts count under the "คงค้าง" filter, never under "คืนครบ".
- No "overdue": INV has no due date for borrows.
- Rules are in `Invc.Core.Borrow.BorrowReturnRules` and used identically by the pages, the MySQL repository (inside the transaction) and the tests.

## 5. Recording a return (`/Borrow/Return/{item}` · `/Borrow/ReturnBill/{bill}`)
- POST only, antiforgery, Post/Redirect/Get. The form carries a random `ClientRequestId`; the repository stores it (UNIQUE) so a refresh or double-click after submit is answered "บันทึกไว้แล้ว (ไม่บันทึกซ้ำ)" instead of writing twice.
- Server-side, inside one MySQL transaction (`REPEATABLE READ`): `SELECT … FOR UPDATE` on the mirrored item row (row lock — concurrent returns of the same item serialise), read `Σ quantity_delta`, validate (`qty > 0`, integer, `qty ≤ outstanding`, item exists, item belongs to the posted bill, no conflict), insert, commit. Integration test proves 20 simultaneous returns against 60 outstanding yield exactly 12 successes.
- "คืนครบรายการ" submits the **current** outstanding quantity re-read on the server (the browser's number is ignored). "คืนครบทั้งบิล" needs an explicit confirmation checkbox and writes one RETURN per open item of the bill in one transaction — any failure rolls the whole bill back.
- Date/time of the return is editable (default now, not in the future); actor and creation time are server-side.

## 6. Corrections (`/Borrow/Correct/{event}` from ประวัติ)
A mistake is fixed by a new **CORRECTION** event referencing the original RETURN: quantity ≤ the RETURN's remaining reversible quantity (`quantity_delta + Σ its corrections`), reason required, actor/time recorded; the item's net returned quantity can never go below 0; a correction cannot be corrected (record a new RETURN instead). The original row is never edited or deleted. Corrections still work when the source row has disappeared from the mirror (the event's own snapshot columns are used).

## 7. Receipt-after-borrow advisory ("มีการรับเข้าหลังยืม")
For every item still outstanding, the facility page shows non-borrow receipts (`RCV_TYPE ≠ '09'`) of the **same WORKING_CODE** received on/after the borrow date — receive no, type, date, quantity, lot — from INV, read-only, in one set-based query per facility (`BorrowReceiptAdvisoryRepository`). It is a hint for staff ("อาจพิจารณาคืน"); it never marks anything returned and never says "ต้องคืน". Live data: 119 of 121 borrow lines have at least one candidate, so the list is collapsed behind a disclosure. If INV is unreachable the hint is simply omitted with a note.

## 8. Audit identity
`actor` = `HttpContext.User.Identity.Name` when the request is authenticated (IIS Windows Authentication, the owner-approved model). The current side-by-side site still runs with anonymous authentication, so the documented server-side fallback `anonymous:<client IP>` is recorded (`BorrowReturnRules.ResolveActor`). The browser can never supply the actor. Enabling Windows Authentication on the `invc-web` site (no code change) makes real user names appear.

## 9. Screens
`/Borrow` work board (filter คงค้าง · คืนบางส่วน · คืนครบ · ทั้งหมด, default คงค้าง; totals สถานบริการ · บิล · ยืม · คืนแล้ว · คงค้าง) → `/Borrow/Facility/{code}` (bills with status/totals, items with ยืม/คืนแล้ว/คงเหลือ, status chips, บันทึกคืน, คืนครบทั้งบิล…, advisory hints, item/bill history links; filters by status and keyword incl. lot/receive/invoice) → `/Borrow/Return/{item}`, `/Borrow/ReturnBill/{bill}` → `/Borrow/History` (facility / receive no / item / date / actor; newest first; ย้อนกลับ… on reversible RETURNs) → `/Borrow/Correct/{event}`. Drug names A–Z inside a bill (owner rule).

The Borrow module has two top-level views: **สถานบริการ** (`/Borrow`, the operational board above) and **สรุปคงค้าง**
(`/Borrow/Outstanding`, review only). สรุปคงค้าง aggregates the work that is still open by **facility × WORKING_CODE**
instead of by bill, because the same medicine is usually borrowed on several bills: one row per medicine with bill
count, ยืม, คืนแล้ว and the primary **ต้องคืน**, a native `<details>` drill-down to the source bills (RECEIVE_NO,
receive date, per-bill figures), per-facility totals (จำนวนรายการยา = distinct working codes, not source lines) and a
compact overall strip. Only items with `OutstandingQty > 0` take part, so fully returned and conflict items never
appear; search (facility code/name, working code, drug name, receive/invoice number) filters **before** aggregation so
the totals always describe the displayed rows. Facilities are ordered by outstanding quantity (largest first),
medicines A–Z. It is derived in memory from the same work board (`BorrowOutstandingSummary.Build` in Core) — no extra
query, no new table — and is **read-only in both hosting modes**: returns are recorded on the facility screen.

## 10. Failure behaviour
- MySQL unreachable or `AppDatabase__ConnectionString` missing: every Borrow screen returns HTTP 200 with the in-page message "ไม่สามารถอ่านข้อมูลยายืมจากฐานข้อมูลของเว็บได้ … สถานะระบบ"; `/Health` shows **ฐานข้อมูลของเว็บ (ยายืม): ล้มเหลว / ยังไม่ตั้งค่า** while the INV status stays readable; all non-Borrow pages are unaffected. The MySQL factory validates lazily (never during DI activation).
- INV unreachable: mirror kept, sync warning; advisory omitted with a note.
- Configuration programming errors (wrong database name, SQL Server keywords, `sa`) are still raised on first use with their explicit messages — not hidden.

## 11. Migration and production configuration
- Schema: `db/mysql/001_borrow_foundation.sql` (v1), `db/mysql/002_borrow_return_workflow.sql` (v2) — idempotent, no DROP, applied with `scripts/mysql-migrate.ps1` against `invc_web` only (refuses `INV`); never at application startup. `schema_version` = 2 after 002. Check: `.\scripts\mysql-migrate.ps1 -Status`.
- Production setting `AppDatabase__ConnectionString` is an IIS `environmentVariable` in the release `web.config`, next to `InvDatabase__ConnectionString`, `AllowedHosts`, `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_HTTPS_PORT`; the deploy script carries all of them into every new release and adds the MySQL one from a local operator file when absent. Never in `appsettings*.json`, never in git; `appsettings.*.local.json` is excluded from publish and the artifact audit fails on it.
- MySQL must be running for Borrow: today it is Laragon's MySQL 8.4 (`C:\laragon`, port 3306, data `C:\laragon\data\mysql-8.4\invc_web`) started manually — installing it as a Windows service and creating a dedicated MySQL user for the site are recommended follow-ups.

## 12. Backup expectations
`invc_web` is the only place the return trail exists. Back it up with the rest of the server (e.g. `mysqldump invc_web` daily, kept with the INV backups). Losing it loses return history, not borrow data (INV is the source of what was borrowed; the mirror rebuilds itself on the next visit).

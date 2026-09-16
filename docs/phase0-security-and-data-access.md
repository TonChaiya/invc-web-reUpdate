# Phase 0 — Security Audit & New-Application Data Access Recommendation

2026-09-16. Findings only; nothing was changed. Secret values are deliberately omitted.

## Part 1 — Security audit of the legacy web application

### 1.1 Credentials and infrastructure exposure

| # | Finding | Location | Severity |
|---|---|---|---|
| S1 | **`sa` password hard-coded in plain text** in the connection string | `Connections/INVFlood.asp` line ~12 | Critical |
| S2 | A second (DSN) password is hard-coded in a commented line | `Connections/INVFlood.asp` | High |
| S3 | The same `sa` credential is embedded in every Access frontend's linked-table `Connect` string (`C:\INVC\*.mdb`) and in the 2017 `Web.rar` archive | outside the web root but on the same host | Critical |
| S4 | `sa` is the **only** SQL login on the instance; the app, Access and admins all share it | SQL Server `sys.server_principals` | Critical |
| S5 | Server name, IIS layout and other DB hosts (`RXTEST`, `172.16.1.15`, `IPISSSRV`, `DBAPPSRV`, logins `homc`, `dbappsrv`) are discoverable from source/Access files | `.asp`, `.mdb` | Medium |
| S6 | `web.config` sets `httpErrors errorMode="Detailed"` → stack traces and SQL text shown to any client | `web.config` | High |
| S7 | `_mmServerScripts/MMHTTPDB.asp` accepts `ConnectionString`, `UserName`, `Password` and SQL over HTTP and executes them (Dreamweaver remote script) — a **remote SQL proxy** if the folder is served | `_mmServerScripts/` | Critical |
| S8 | SQL Server 2022 **Enterprise Evaluation** edition — licence expires 180 days after install (installed ~2026-07-27) | instance | High (availability) |
| S9 | Orphaned DB user `PHAROPD` with no login | `INV` | Low |

### 1.2 SQL injection / unsafe concatenation

All 60+ queries are built by string concatenation. Only `INV_Status.asp` (keyword) and `chkstock.asp`/`ChkConfirm.asp` (barcode)
apply `Replace(x, "'", "''")`. Unescaped request values flow directly into SQL in at least:

| Page | Parameter(s) |
|---|---|
| `PO_Search.asp`, `PO.asp`, `POforIPISS.asp` | `POstatus`, `YYY`, `FiscalYear` |
| `PODetail.asp`, `ProcessTime.asp` | `RPO` (REAL_PO) |
| `table_ipiss.asp`, `ipiss_*.asp`, `table_*.asp` | `FiscalYear`, `SelectedDate`, `WORKING_CODE`, `Budget` |
| `Dashboard.asp` | `BudgetYear` |
| `chkstock.asp`, `seesubstock.asp`, `searchItem.asp`, `EOC.asp` | `code`, `DEPT`, `SearchKey`, `keyword` |
| `login.asp` | `username` (password is MD5-hashed first, but `username` is raw) |
| `ShelfList.asp`, `pending.asp` | session values originally taken from querystring |
| `SaveChecked.asp`, `SavePrintSlip.asp` | `SUB_PO_NO`, `WORKING_CODE` — **write paths** |

Because the connection runs as `sa`, any injection point yields full server control (xp_cmdshell, other DBs).

### 1.3 Authentication / session

* Passwords stored as unsalted **MD5 hex** in `USER.password` (4 users). Login compares the hash in SQL.
* No CSRF protection on `SaveChecked.asp`/`SavePrintSlip.asp`; authorization relies on `Session("UserID")` only.
* Most report pages have **no login check at all** (e.g. `INV_Status.asp`, `PO_Search.asp`, `Dashboard.asp`).
* `On Error Resume Next` in `Connections/HOMC.asp` suppresses errors on every page that includes it.
* Pages emit `windows-874`; user input is echoed without `Server.HTMLEncode` in several places (`INV_Status.asp` keyword echo, PO pages) → reflected XSS.

### 1.4 Remediation recommendations (for Phase 1+, not applied)

1. Create a dedicated SQL login (e.g. `invc_web_ro`) with `db_datareader` on `INV` only, `DENY` on write, and use it for the new app.
   Rotate the `sa` password immediately after the legacy app and Access frontends are re-pointed (Access will need its own least-privilege login — separate task, outside the web scope).
2. Remove `_mmServerScripts/`, `_notes/`, archives (`*.rar`, `*.zip`), and the `login-form-v4/` template from the served root.
3. Set `httpErrors errorMode="DetailedLocalOnly"` (or remove) in `web.config`; add `<customErrors>` equivalent for ASP.
4. All new data access via parameterised queries (Dapper `@param`), never string concatenation.
5. Store the connection string in `appsettings` + environment/User Secrets, never in the repository; add `.gitignore` before the first commit.
6. Move to ASP.NET Core Identity (or Windows/AD auth) with hashed passwords (PBKDF2/Argon2); do not port MD5.
7. Plan the SQL Server licence: Evaluation → Express/Standard before the 180-day expiry.

## Part 2 — Data access recommendation for the new application (Step 10)

### 2.1 Verified constraints

* One database, one schema (`dbo`), 116 tables, **no views/procedures/FKs**; compatibility level 100 (no `STRING_AGG`, `TRY_CAST` is OK ≥ 2012 engine but avoid features that fail at level 100 such as `STRING_AGG`, `OPENJSON` — use plain T-SQL).
* Collation `Thai_CI_AS`; all text is `nvarchar` → use `NVARCHAR` parameters (Dapper default for .NET strings is fine, `DbString` with `IsAnsi=false`).
* Small data volume at this site (largest operational tables: CARD 2.6 k, MNTH_SUM 3.9 k, Vaccine* 165–190 k). Even the heaviest legacy query is sub-second.
* Business keys are strings without FKs; lookups are joined by code. Access writes to the same tables concurrently during the day.

### 2.2 Recommended design (default stack accepted)

* **ASP.NET Core 10, Razor Pages**, `Microsoft.Data.SqlClient` + **Dapper**, `TrustServerCertificate` decided per environment, `ApplicationIntent=ReadOnly` (harmless on a standalone instance, self-documenting).
* Dedicated **read-only SQL login** (`db_datareader` + explicit `DENY INSERT, UPDATE, DELETE, EXECUTE` on `dbo`). No `sa`.
* Repository-per-report with hand-written SQL that **reproduces the legacy queries verbatim first** (parity), then optionally improves
  (LEFT JOIN with "unknown code" surfacing, set-based reorder status instead of VBScript loops, single query instead of N+1 for borrow/HOMC).
* Read-only means the two legacy write screens (`SaveChecked.asp`, `SavePrintSlip.asp`) are **out of scope** for the read-only account; if they must be ported, they need a separate, write-scoped login and are a later phase.
* Query timeouts 30 s, `NOLOCK` **not** recommended (Access commits are small; use default READ COMMITTED; consider `READ_COMMITTED_SNAPSHOT` only after DBA agreement — it is a DB-level change and outside Phase 0).
* Caching: in-memory cache of lookup tables (TblPOStatus, BDG_TYPE, TBLBUY, TBLED_NED, DEPT_ID, LOCATION) for 5–15 min; no caching of stock figures.

### 2.3 Direct queries vs. reporting snapshot

Direct read-only queries are **reasonable and sufficient** for this site: data volume is tiny, the legacy pages already run
ad-hoc SQL against the live DB during business hours, and Access already produces month-end snapshots (`MNTH_SUM`, `MBS_RE_M/Y`)
that the reports can use for historical views. A snapshot/reporting database would only be justified if (a) the same code base is
deployed to a large hospital instance (hundreds of thousands of CARD rows), (b) HIS/CRHBACK cross-database joins are reintroduced,
or (c) the Access users report lock contention. None applies today. **Not part of Phase 0.**

### 2.4 Inputs Phase 1 needs from the owner

1. Confirmation that `DESKTOP-BVH8F8L.INV` is the production target (evidence says yes) and a hostname/DNS plan if the server is renamed.
2. Creation of the read-only login by the DBA/owner (script can be provided; **not executed by the assistant**).
3. Decision on whether non-PO receipts (`OTH_IVO`) must appear in the "purchasing" reports.
4. Decision on authentication (reuse `USER` table read-only vs. Windows auth vs. new identity store).
5. Whether the sub-store checking write screens are in scope.

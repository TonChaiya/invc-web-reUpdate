# IIS deployment design — INVC Web (Phase 9 specification; NOTHING in this document has been executed)

Status: **DESIGN — awaiting owner review.** Written in Phase 8 from the read-only discovery in `docs/phase8-environment-discovery.md`.
Every command-like fragment below is illustrative and marked **FUTURE — REQUIRES EXPLICIT OWNER APPROVAL**; none was run.
Values that discovery did not prove are marked **OWNER DECISION REQUIRED** rather than guessed.

## 1. Goal
Run the Phase 7 release package of `Invc.Web` (ASP.NET Core 10, framework-dependent, read-only against `INV`) on IIS on `DESKTOP-BVH8F8L`,
side by side with the legacy Classic ASP site, behind **IIS Windows Authentication with no application login**, over HTTPS, with a
versioned folder layout that allows rollback to the previous release in minutes, and only then cut the normal URL over.

## 2. Non-goals
No application login page, no ASP.NET Core Identity/cookie authentication, no user/role tables, no password storage, no role-based
authorization (§17), no database writes, no schema/permission changes performed by the application or this project, no retirement or
deletion of the legacy ASP files, no change to Access frontends, no Laragon/Apache changes by this project, no CSP work (Phase 7 "future hardening").

## 3. Existing topology (OBSERVED / INFERRED — discovery §3–6, §11)
- Host `DESKTOP-BVH8F8L`: Windows 10 Pro 22H2, **WORKGROUP**, Wi-Fi, DHCP `192.168.1.10/24`, no DNS suffix, firewall disabled.
- IIS 10.0 (enabled 2026-09-15), `W3SVC` running; single site **Default Web Site** (ID 1) on HTTP :80, no HTTPS binding.
- Legacy INVC: IIS application **`/invc`** under Default Web Site, physical path **`C:\INVC\Web` (the repository working copy — INFERRED, byte-exact)**,
  Classic ASP working, anonymous access, database access via `sa` in `Connections/` (Phase 0).
- Port 443 held by Laragon Apache; 8081 Laragon HTTP.
- Port 8080 (`192.168.1.99:8080`, host header `invc-ksl.ddns.net`) is an **owner-confirmed intentional binding** on Default Web Site: a
  DDNS/port-forward the owner configured so external users can reach the **legacy** `/INVC` virtual directory from outside the LAN
  (confirmed 2026-09-17; see `docs/phase8-environment-discovery.md` §16). Phase 9 must not modify, remove, repoint, redirect, reuse this
  binding, or touch router/NAT/DDNS/firewall configuration; legacy external access must stay available throughout Phase 9. The new
  `INVC-Web` site is not exposed on this binding and gets no new external port-forward/DDNS entry during Phase 9. Final disposition of
  `invc-ksl.ddns.net` is an owner decision deferred to the cutover phase.
- SQL Server 2022 Enterprise Evaluation, default instance, **same machine**, TCP 1433, Mixed mode; `INV` ONLINE; logins: `sa`, the developer
  account (both sysadmin), service accounts; no reader principal.
- No ASP.NET Core Module, no machine-wide .NET runtime.

## 4. Target topology (RECOMMENDED — Option 1 of §11)
```
Clients (Windows PCs on 192.168.1.0/24)
   │  https://DESKTOP-BVH8F8L:<HTTPS port>/   (Windows Authentication, NTLM)
   ▼
IIS 10 on DESKTOP-BVH8F8L
   ├─ Default Web Site :80            (unchanged during acceptance)
   │     └─ /invc  → legacy Classic ASP   (rollback / fallback; later: redirect or re-point to a COPY)
   └─ Site "INVC-Web"  (new)          app pool "INVC-Web" (No Managed Code, Integrated, ApplicationPoolIdentity)
         physical path D:\Apps\InvcWeb\releases\<release-id>   (repointed per release)
         ANCM V2 in-process → Invc.Web.dll
              │  Integrated Security (IIS AppPool\INVC-Web), ApplicationIntent=ReadOnly, SELECT only
              ▼
         SQL Server 2022 (same machine, tcp 1433 or shared memory)  → INV (read-only role for the pool identity)
```

## 5. Windows Authentication design (owner-approved model)
- **Authentication happens in IIS**, not in the application. Target settings on the INVC-Web site: `windowsAuthentication enabled=true`
  (providers `Negotiate`, `NTLM`), `anonymousAuthentication enabled=false`. Kernel-mode authentication may stay at the IIS default.
- The application does not read credentials, never sees a password, and stores nothing about users. `HttpContext.User.Identity.Name`
  becomes available (§18) but is not persisted.
- **SSO feasibility in this environment (workgroup, no Kerberos):** browsers on Windows send the logged-on credentials automatically only
  for sites in the *Local intranet* zone (single-label host names such as `DESKTOP-BVH8F8L` normally qualify; IP addresses and `.local`
  names may not). NTLM pass-through then succeeds **only if the client user's Windows user name and password match a local account on
  `DESKTOP-BVH8F8L`**. Otherwise the browser shows a Windows credential prompt; after entering a valid local account of the host the user
  proceeds. Non-Windows or unmanaged clients will always be prompted.
- Consequences to record for the owner: (a) each INVC user needs a **local Windows account on the host** (or a matching one) — created by
  the administrator, not by the application; (b) prompts are an infrastructure/client-policy behaviour, **not** a reason to create an INVC
  login form; (c) a domain join (or a small AD) would make SSO transparent — **OWNER DECISION**, outside this project.
- "ไม่มีหน้า Login ของ INVC" = the application has no login page. It does **not** mean authentication is disabled.

## 6. No-login application requirement (binding for Phase 9 and later)
- `Program.cs` keeps **no** `AddAuthentication`, `AddIdentity`, cookie schemes, login/logout/account pages, password reset or registration.
- IIS handles the 401 challenge; the app simply receives an authenticated `WindowsPrincipal` from ANCM (`forwardWindowsAuthToken` default true).
- Optional later: `builder.Services.AddAuthorization(o => o.FallbackPolicy = RequireAuthenticatedUser)` as defense in depth — **not required**
  while IIS denies anonymous, and **not part of Phase 9** (no source change planned).

## 7. Database identity design
Two identities, kept distinct:
- **User identity** — the Windows user authenticated by IIS. Used for access control at IIS and (optionally) display. **Never** forwarded to SQL Server;
  no impersonation, no per-user SQL logins (no business requirement; it would also break under NTLM double-hop rules).
- **Application database identity** — one controlled read-only principal used by the worker process.

| Option | Fit to evidence | Verdict |
|---|---|---|
| **A. ApplicationPoolIdentity + Windows Integrated Security** (`IIS AppPool\INVC-Web` login → `INV` user → `db_datareader`) | IIS and SQL Server on the **same machine**; virtual account needs no password, is per-pool, cannot be used from elsewhere | **RECOMMENDED** |
| B. Dedicated domain/service identity | no domain exists; a local service account would need a stored password and manual rotation | not applicable (workgroup) |
| C. Dedicated SQL login (`invc_web_ro`) | Mixed mode is enabled, so it works; but the password must live in an IIS environment variable (encrypted in `applicationHost.config` only if configured) and be rotated | acceptable **fallback** if the DBA prefers SQL logins |

**Current state: BLOCKER — DBA/ADMIN ACTION REQUIRED.** Neither `IIS AppPool\INVC-Web` nor any reader principal exists (discovery §11–12).
Nothing was provisioned in Phase 8.

## 8. Database permission requirement (minimal)
`SELECT` on the `dbo` tables the application reads (`docs/data-source-map.md`; simplest correct grant = `db_datareader` on `INV`, or explicit
`SELECT` per table if the DBA prefers), plus `CONNECT`. Explicit `DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo` is recommended as
defense in depth. The application independently enforces `ApplicationIntent=ReadOnly`, refuses `sa`, and passes every statement through
`ReadOnlySql.Ensure`. Example for DBA review only:

```sql
-- DO NOT RUN IN PHASE 8 — DBA-reviewed FUTURE example (Option A). Requires explicit owner approval.
-- CREATE LOGIN [IIS AppPool\INVC-Web] FROM WINDOWS;
-- USE INV; CREATE USER [IIS AppPool\INVC-Web] FOR LOGIN [IIS AppPool\INVC-Web];
-- ALTER ROLE db_datareader ADD MEMBER [IIS AppPool\INVC-Web];
-- DENY INSERT, UPDATE, DELETE, EXECUTE ON SCHEMA::dbo TO [IIS AppPool\INVC-Web];
```
The pool (and therefore the virtual account) must exist before the login can be created — ordering matters in Phase 9.

## 9. Hostname / AllowedHosts
- Evidence supports **`DESKTOP-BVH8F8L`** (already the legacy URL host; Local-intranet-zone friendly) with `desktop-bvh8f8l.local` as a secondary.
- Proposed `AllowedHosts=DESKTOP-BVH8F8L;desktop-bvh8f8l.local` — **conditional on OWNER DECISIONS**: keep the machine name; give the host a
  DHCP reservation or static address; optionally publish a friendly DNS alias (e.g. on the router) which would then be added.
- Raw IP is **not** recommended (DHCP; an old certificate shows `192.168.1.99`, so the address has changed before). `*` is refused by the app in Production.
- Nothing set yet.

## 10. HTTPS
- **EXTERNAL PREREQUISITE.** No usable certificate (only a SAN-less self-issued one for `192.168.1.99`); no IIS HTTPS binding; **443 is held by Laragon Apache**.
- Owner decisions: (1) certificate: internal CA or a self-signed certificate with SAN `DESKTOP-BVH8F8L`, `desktop-bvh8f8l.local` distributed to
  client Trusted Roots (no public CA can issue for a non-public name); (2) port: free 443 (re-port/stop Laragon SSL — not this project) or
  bind INVC-Web HTTPS on **8443**. The app enforces HTTPS redirection + HSTS once the HTTPS binding exists; an HTTP binding is kept only for redirection.
- If 8443 is chosen, `HTTPS_PORT`/`ASPNETCORE_HTTPS_PORT=8443` must be provided so the redirect targets the right port.

## 11. Site topology options compared
| Option | Description | Pros | Cons | Verdict |
|---|---|---|---|---|
| **1. New IIS site `INVC-Web`** with its own bindings (HTTP :8090 for acceptance → HTTPS :8443 or :443), dedicated pool | full isolation from Default Web Site; own auth settings, own HTTPS binding, own logs (`W3SVC<id>`); cutover by redirect/binding is reversible | one more port for users until the cutover redirect exists | **RECOMMENDED** |
| 2. New application `/invc-web` under Default Web Site, dedicated pool | same host/port :80 as legacy; per-application auth works | HTTPS binding would be site-wide (also affects `/invc`); shares the site's bindings and limits; harder to isolate | viable fallback |
| 3. Re-point `/invc` to the new package | one URL | destroys the fallback; legacy pool settings; Classic ASP and ANCM in one app | **rejected** |

## 12. Versioned release layout (D: exists — 30 GB, 29 GB free; package ≈ 26 MB)
```
D:\Apps\InvcWeb\
  releases\
    2026MMDD-HHmm_<sha7>\        ← audited publish output + release-manifest.json (read/execute for the pool identity)
    2026MMDD-HHmm_<sha7>\        ← previous release kept for rollback (keep at least two)
  logs\                          ← only folder with write permission for the pool identity, only if stdout/file logging is ever enabled
  RELEASES.md                    ← operator log: release-id, commit, manifest hash, who/when, live/previous
```
The site's physical path points at one `releases\<id>` folder and is **repointed** per release (or a `current` junction is retargeted —
either is a single reversible IIS/filesystem step). Never `C:\INVC\Web`, never the legacy folder, never `C:\inetpub\wwwroot`.
Nothing under `D:\Apps` exists today and nothing was created in Phase 8.

## 13. Side-by-side acceptance sequence (FUTURE — REQUIRES EXPLICIT OWNER APPROVAL — do not execute)
0. Elevated **read-only** confirmation of sites/pools/auth (`Get-Website`, `Get-WebApplication`, `IIS:\AppPools`, authentication GETs); backup `applicationHost.config`.
1. Administrator installs the **.NET 10 Hosting Bundle**; verify `%windir%\System32\inetsrv\aspnetcorev2.dll` and `dotnet --list-runtimes`.
2. Build/audit/manifest inside the project (`scripts/verify.ps1` → `publish-iis.ps1` → `test-release-artifact.ps1` → `new-release-manifest.ps1`); record the SHA.
3. Copy the audited package to `D:\Apps\InvcWeb\releases\<id>\` (new folder; never overwrite).
4. Create pool `INVC-Web` (§16). DBA creates the login/user/role for `IIS AppPool\INVC-Web` (§8).
5. Create site `INVC-Web` → physical path `releases\<id>`, HTTP binding on the acceptance port (e.g. 8090) with host `DESKTOP-BVH8F8L`.
6. Set environment variables on the site/pool (§15): `ASPNETCORE_ENVIRONMENT=Production`, `AllowedHosts`, `InvDatabase__ConnectionString`.
7. Enable Windows Authentication, disable Anonymous Authentication on the INVC-Web site.
8. Add the HTTPS binding + certificate (port per §10); keep HTTP only for the redirect.
9. Verify `/Health` → 200, `INVC Web · Production`, no technical details; an unauthenticated request → 401 challenge; forged `Host` → 400.
10. Functional smoke: `/`, `/Inventory/Status`, `/Inventory/Detail/{code}`, `/Reorder`, `/Reorder/Print`, `/PurchaseOrders`, `/Receipts`, print pages, Thai text, headers.
11. Compare key figures with the legacy `/invc` reports (parity matrices A–D) on the same day.
12. Owner/user acceptance from at least two client PCs (SSO behaviour recorded per client).
13. Only then: cutover decision (§14).

## 14. Cutover (reversible; FUTURE — REQUIRES EXPLICIT OWNER APPROVAL)
Preferred: **HTTP redirect at the legacy entry point** — on Default Web Site `/invc` set `httpRedirect` (feature installed) to
`https://DESKTOP-BVH8F8L:<port>/` (302, exact destination off), so `http://DESKTOP-BVH8F8L/invc/...` lands on the new site. Legacy files stay
in place and can be served again by removing the redirect. Alternative: add the :80 host binding to INVC-Web after removing it from Default
Web Site (binding switch). Rejected: overwriting `/invc` or deleting legacy files. Separately (owner safety decision): re-point `/invc` to a
**copy** of the legacy folder so the working copy is no longer a web root.

## 15. External configuration model (no secrets in Git, `appsettings.Production.json`, the package, or committed `web.config`)
| Variable | Value shape | Where |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | IIS `environmentVariables` on the INVC-Web pool/site (`system.webServer/aspNetCore`) |
| `AllowedHosts` | `DESKTOP-BVH8F8L;desktop-bvh8f8l.local` (pending §9 decisions) | same |
| `InvDatabase__ConnectionString` | Option A: `Server=DESKTOP-BVH8F8L;Initial Catalog=INV;Integrated Security=True;ApplicationIntent=ReadOnly;Encrypt=True;TrustServerCertificate=True;Application Name=Invc.Web` — no password. (`TrustServerCertificate` because SQL uses a self-generated certificate; replaceable by a trusted server certificate later.) Option C would add `User ID=invc_web_ro;Password=…` supplied only in IIS | same |
| `ASPNETCORE_HTTPS_PORT` | only if the HTTPS port is not 443 (e.g. `8443`) | same |
The factory adds/overrides `ApplicationIntent=ReadOnly`, disables MARS and refuses `sa`; `ProductionConfigurationValidator` fails startup on a missing/invalid value.

## 16. Application pool design
`INVC-Web`: **No Managed Code**, **Integrated** pipeline, identity **ApplicationPoolIdentity** (`IIS AppPool\INVC-Web`), 32-bit disabled,
Start Mode `AlwaysRunning` optional (AppWarmUp is installed), idle timeout per owner preference, one worker process. Separate from the
legacy pool (whatever it is). Filesystem: read/execute on `D:\Apps\InvcWeb\releases\<id>`; no write anywhere except `logs\` if ever enabled.

## 17. Authorization scope
Phase 9 introduces **no roles**. Every Windows account that IIS authenticates (and that network/host policy admits) gets the same read-only
INVC Web capability. Restricting to a Windows group (IIS URL Authorization / `<authorization>` rules) or application roles is a **separate future feature**.

## 18. User identity visibility — OPTIONAL UX ENHANCEMENT
The current app does not display the user. A future one-line addition could show "ผู้ใช้งาน: DESKTOP-BVH8F8L\name" from
`HttpContext.User.Identity.Name` in the navbar/footer (no storage, no lookup). Not a login feature; not implemented in Phase 8.

## 19. Logging / monitoring
Keep IIS request logs (`C:\inetpub\logs\LogFiles\W3SVC<id>`) and the Windows Application event log (ANCM writes start-up failures there).
Leave `stdoutLogEnabled=false` in the generated `web.config`; enable it **temporarily** into `D:\Apps\InvcWeb\logs\` only when diagnosing
start-up failures. The app logs through the built-in providers (EventLog could be added later without code change via configuration).
Smoke/monitoring: `/Health` (200/503; redacted in Production). No logging infrastructure change in Phase 8.

## 20. Network requirements
- Client → IIS: TCP `<HTTPS port>` (and `:80`/acceptance port during transition) on `192.168.1.0/24`.
- IIS worker → SQL Server: local (shared memory or `tcp/1433` on the same host) — no remote SQL endpoint involved.
- Firewall is currently disabled on all profiles (observed); if enabled later, allow the ports above inbound. No firewall change in Phase 8.

## 21. Security boundaries (carried from Phases 0–7)
Read-only SQL guard, `sa` refusal, `ApplicationIntent=ReadOnly`, fail-fast configuration, redacted `/Health`, security headers, no secrets in
the package, publish path guard (`scripts/project-path-guard.ps1`), legacy ASP excluded by construction. New in this design: production
web root ≠ repository; legacy `/invc` must stop serving the working copy (owner action); no application login surface exists to attack.

## 22. Prerequisites, owner decisions and blockers
| # | Item | Type | Owner |
|---|---|---|---|
| P1 | .NET 10 Hosting Bundle (ANCM V2 + ASP.NET Core Runtime 10.0.x) | **BLOCKER** | administrator |
| P2 | Read-only DB principal for `IIS AppPool\INVC-Web` (or fallback SQL login) | **BLOCKER — DBA/ADMIN ACTION REQUIRED** | DBA |
| P3 | Certificate for the chosen hostname + HTTPS port decision (443 vs 8443; Laragon conflict) | **BLOCKER / OWNER DECISION** | owner/admin |
| P4 | Hostname + address stability (keep `DESKTOP-BVH8F8L`, DHCP reservation, optional DNS alias) → final `AllowedHosts` | OWNER DECISION REQUIRED | owner |
| P5 | Local Windows accounts for INVC users on the host (or domain join) — SSO expectations | OWNER DECISION REQUIRED | owner/admin |
| P6 | Isolate legacy `/invc` from the repository working copy (copy folder, re-point or remove) | OWNER DECISION REQUIRED (safety) | owner/admin |
| P7 | Acceptance port (proposed 8090) and cutover method (proposed redirect) | OWNER DECISION | owner |
| P8 | Elevated read-only confirmation of current IIS configuration + `applicationHost.config` backup | prerequisite | administrator |
| P9 | Accept Windows 10 Pro/laptop/Wi-Fi/Evaluation-edition risks or plan a server host | OWNER DECISION | owner |

## 23. Exact scope allowed for Phase 9 (once the owner approves this spec and P1–P3 are resolved by the administrator/DBA)
Allowed, each step individually confirmed and logged: create folder tree under `D:\Apps\InvcWeb`; copy the audited package there; create
app pool `INVC-Web`; create site `INVC-Web` with the approved bindings; set the three environment variables; enable Windows / disable Anonymous
authentication **on the new site only**; add the HTTPS binding with the owner-provided certificate; run the acceptance checks of §13.
Still forbidden in Phase 9: any change to Default Web Site or `/invc` (until the separate cutover approval), any SQL DDL/DCL by this project,
installing the Hosting Bundle or certificates (administrator tasks), touching `C:\INVC\*.mdb`, Laragon, firewall, registry, deleting legacy files.

## 24. Rollback strategy (concrete checkpoints)
| Checkpoint | Restore action | Verification |
|---|---|---|
| R0 before anything | `applicationHost.config` backup taken; current `/invc` behaviour recorded (200 anonymous) | — |
| R1 after new site/pool created | stop/remove only the new site and pool; legacy untouched | `/invc` still 200 |
| R2 after a new release folder is live | re-point INVC-Web physical path to the previous `releases\<id>`; recycle pool | `/Health` 200 on previous manifest SHA |
| R3 after auth changes on INVC-Web | restore anonymous/windows values from R0 backup **for the new site only** | 401/200 behaviour as before |
| R4 after HTTPS binding | remove the binding; certificate remains installed | HTTP acceptance URL works |
| R5 after cutover redirect | remove the `httpRedirect` on `/invc` (or move the :80 binding back) | `http://DESKTOP-BVH8F8L/invc/` serves legacy again |
No database rollback is ever needed (the application is read-only). Legacy files are never deleted until the owner separately approves retirement.

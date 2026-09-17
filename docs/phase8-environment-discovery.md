# Phase 8 — Environment discovery (READ-ONLY, 2026-09-16)

Base commit `37ee094`. Every value below was collected without changing anything: file reads, registry reads, `Get-Cim*`,
`Get-Net*`, `netsh … show`, local HTTP GETs, and SQL `SELECT`/`SERVERPROPERTY`/`DATABASEPROPERTYEX`/catalog views over a
`ApplicationIntent=ReadOnly` Windows-authenticated connection. **Nothing was deployed, installed, created, started, stopped or granted.**

Labels: **OBSERVED** = read directly · **INFERRED** = derived from indirect evidence (stated) · **UNRESOLVED** = could not be read
(reason stated) · **OWNER DECISION REQUIRED** · **BLOCKER**.

Discovery limit: the session was **not elevated**. `Get-Website`, `Get-WebBinding`, `Get-ChildItem IIS:\AppPools`, `appcmd list …` and
`Get-WindowsOptionalFeature` all refused (`redirection.config` / `applicationHost.config`: insufficient permissions). Site, pool and
per-site authentication values are therefore INFERRED and must be confirmed by an elevated **read-only** pass at the start of Phase 9.

## 1. Host
| Item | Value | Label |
|---|---|---|
| Computer name | `DESKTOP-BVH8F8L` (Lenovo model 80NJ — a laptop/desktop-class machine, not server hardware) | OBSERVED |
| Windows | Windows 10 **Pro** 22H2, 10.0.19045, 64-bit; installed 2026-07-27; last boot 2026-09-10 | OBSERVED |
| Logged-on user during discovery | local account `DESKTOP-BVH8F8L\Mainam8888` (member of local `Administrators`; session not elevated) | OBSERVED |
| Network | single active adapter **Wi-Fi** (Intel Dual Band Wireless-AC 3160), `192.168.1.10/24` **DHCP**, gateway `192.168.1.1`, DNS `1.1.1.1`, `192.168.1.1`, `fe80::1`; profile name `RPST_Bansankhong_CAT_5G` | OBSERVED |
| DNS suffix | none (`USERDNSDOMAIN` empty, suffix search list empty, no connection-specific suffix) | OBSERVED |
| Other drives | `D:` "New Volume" 30 GB (29.1 GB free); `G:`/`H:` are Google Drive mounts (not suitable for hosting) | OBSERVED |
| Firewall | Windows Firewall **disabled** on Domain, Private and Public profiles | OBSERVED (security observation, not changed) |

Implications: a Wi-Fi, DHCP, Windows 10 *Pro* client machine is the IIS and SQL host. Client IIS is limited (10 concurrent
connections per site by design) and the address can change on DHCP renewal. Both are recorded as deployment risks (§14), not fixed here.

## 2. Domain / workgroup
| Item | Value | Label |
|---|---|---|
| Membership | **WORKGROUP** (`PartOfDomain=False`, `DomainRole=0`, `LOGONSERVER=\\DESKTOP-BVH8F8L`) | OBSERVED |
| Active Directory / Kerberos | none available — no domain controller, no SPNs, no domain accounts | OBSERVED (consequence) |
| Name resolution of the host | `DESKTOP-BVH8F8L` and `desktop-bvh8f8l.local` resolve only via LLMNR/mDNS/NetBIOS (link-local IPv6 answer); no DNS A record; `invc`, `invc-web` do not resolve | OBSERVED |

Implication for the owner-approved model: IIS Windows Authentication will negotiate **NTLM** (no Kerberos in a workgroup). Transparent
SSO works only when the browser sends the current Windows credentials automatically **and** a matching local account (same user name
and password) exists on `DESKTOP-BVH8F8L`; otherwise the browser shows a Windows credential prompt. This is an infrastructure/client
property — it does **not** call for an INVC login page (see spec §5).

## 3. IIS
| Item | Value | Label |
|---|---|---|
| Installed | IIS **10.0** (`HKLM:\SOFTWARE\Microsoft\InetStp` VersionString "Version 10.0", `PathWWWRoot=C:\inetpub\wwwroot`); `C:\inetpub\wwwroot\iisstart.htm` dated **2026-09-15** → IIS was enabled the day before this phase | OBSERVED / INFERRED (date) |
| Services | `W3SVC` Running (Automatic); `WAS` Running (Manual) | OBSERVED |
| PowerShell modules | `WebAdministration` 1.0.0.0 and `IISAdministration` 1.1.0.0 present (GET operations require elevation) | OBSERVED |
| IIS components installed (`InetStp\Components` = 1) | W3SVC, StaticContent, DefaultDocument, DirectoryBrowse, HttpErrors, HttpRedirect, HttpLogging, RequestFiltering, RequestMonitor, HttpTracing, CustomLogging, ODBCLogging, **AnonymousAuthentication**, **WindowsAuthentication**, BasicAuthentication, DigestAuthentication, ClientCertificateMappingAuthentication, IISCertificateMappingAuthentication, Authorization, IPSecurity, **ASP** (Classic ASP), ASPNET, ASPNET45, ISAPIExtensions, ISAPIFilter, CGI, FastCgi, ServerSideInclude, WebSockets, AppWarmUp, compression, caching, **FTPSvc**, **WebDAV**, ManagementConsole, ManagementScriptingTools, PowerShellProvider, Metabase/LegacySnapin/WMICompatibility | OBSERVED |
| ASP.NET Core Module (ANCM) | **absent**: no `%windir%\System32\inetsrv\aspnetcorev2.dll`, no `C:\Program Files\IIS\Asp.Net Core Module`, no uninstall entry for a Hosting Bundle / ASP.NET Core runtime | OBSERVED |
| URL Rewrite module | absent (`rewrite.dll` not present) — not required by the app | OBSERVED |

## 4. Listeners, existing sites and bindings
| Port | Owner | Meaning | Label |
|---|---|---|---|
| 80 (all IPs) | http.sys (`System`, pid 4) → IIS | `http://localhost/` → 200 `Server: Microsoft-IIS/10.0`, IIS default page "IIS Windows" | OBSERVED |
| **443 (all IPs)** | **`httpd.exe` — Apache 2.4.66 from Laragon** (`C:\laragon\bin\apache\…`, `httpd-ssl.conf: Listen 443`) | IIS **cannot** bind 443 while Laragon Apache runs | OBSERVED |
| 8080 (all IPs) | http.sys, not an IIS site (`400 Microsoft-HTTPAPI/2.0`) | reserved by another Windows component | OBSERVED |
| 8081 | Laragon Apache HTTP (`Listen 8081`, `*.test` virtual hosts via `hosts` file) | unrelated developer stack on the same machine | OBSERVED |
| 1433 (all IPs) | `sqlservr.exe` | SQL Server default instance | OBSERVED |
| IIS HTTPS bindings | none (`netsh http show sslcert` lists only `0.0.0.0:8391`, a non-IIS system binding using the Root store) | OBSERVED |
| IIS sites | only log folder `C:\inetpub\logs\LogFiles\W3SVC1` exists → a single site with ID 1 (**Default Web Site**), HTTP :80, no host header (responds to `localhost` and `192.168.1.10`) | INFERRED (log folder + probes; config unreadable) |

## 5. Legacy INVC site — FOUND, and it serves the repository working copy
| Item | Value | Label |
|---|---|---|
| URL | `http://localhost/invc/` and `http://192.168.1.10/invc/` → **200**, Classic ASP executes (`default.asp` renders 15,699 bytes, Thai title in Windows-874; `INV_Status.asp` → 200 with 217 KB of report data; `Dashboard.asp` → **500**, matching the Phase 0 "Conn never initialised" defect) | OBSERVED |
| Site / application | IIS application (or virtual directory) **`/invc` under Default Web Site** (`C:\inetpub\wwwroot` contains no `invc` folder, so it is a mapped path) | INFERRED |
| **Physical path** | **`C:\INVC\Web` — this repository's working copy.** Evidence: `http://localhost/invc/global.json` → 200, **116 bytes** = `C:\INVC\Web\global.json` (116 bytes); `http://localhost/invc/src/Invc.Web/appsettings.Development.json` → 200, **445 bytes** = the repository file (445 bytes). `.md`/`.ps1`/`.slnx` return 404 only because IIS has no MIME mapping for them. `Connections/` and `_notes/` → 403 (directory listing off), i.e. present | INFERRED (byte-exact match; config unreadable) |
| Application pool | UNRESOLVED (elevation required; the `w3wp.exe` owner could not be read). Most likely `DefaultAppPool` | UNRESOLVED |
| Authentication on `/invc` | anonymous GET succeeds → **Anonymous Authentication enabled**; Windows Authentication state unknown | INFERRED / UNRESOLVED |
| Database identity used by the legacy ASP | the legacy `Connections/*.asp` files (Phase 0 finding S4: `sa`) — not printed here | OBSERVED (Phase 0) |
| Reachability | answered on the LAN address with the firewall disabled → the whole `RPST_Bansankhong_CAT_5G` network can browse the working copy | OBSERVED |

**Security observation (owner action — not performed by this phase):** the IIS `/invc` application exposes the *development working
copy* (source tree, `global.json`, `appsettings.Development.json` — which uses `Integrated Security`, no password — and, by extension,
anything committed or generated under `C:\INVC\Web` that has a served MIME type). Only IIS MIME/request-filtering rules limit it.
The Phase 9 design therefore (a) never uses `C:\INVC\Web` as a web root and (b) recommends re-pointing `/invc` to a **copy** of the legacy
files or removing the application after acceptance — an IIS change that requires explicit owner approval (spec §14, §21).

## 6. Application pools
UNRESOLVED without elevation. Known: `WAS` running; `w3wp.exe` started on demand when `/invc` was requested (pid observed, owner not
readable). No `IIS AppPool\…` login exists in SQL Server (§11), so **no app-pool identity currently has database access** — the legacy
site cannot be using Windows Integrated Security to INV; consistent with Phase 0 (`sa` in `Connections/`).
Design consequence: a **dedicated new pool** is required for the ASP.NET Core site regardless of the legacy pool's settings (spec §8).

## 7. Authentication features
| Item | Value | Label |
|---|---|---|
| IIS Windows Authentication feature | **installed** (`WindowsAuthentication` + `WindowsAuthenticationBinaries`, `inetsrv\authsspi.dll` present) | OBSERVED |
| IIS Anonymous Authentication feature | installed | OBSERVED |
| Basic / Digest / client-cert mapping | installed (not to be used) | OBSERVED |
| Current setting on Default Web Site / `/invc` | Anonymous **enabled** (INFERRED from 200 without credentials); Windows auth per-site state UNRESOLVED | INFERRED / UNRESOLVED |
| Owner-approved TARGET for the new INVC Web site (design only, not applied) | Windows Authentication **Enabled**, Anonymous **Disabled**, application login page **NONE** | TARGET |

## 8. Hostname / AllowedHosts candidates
| Candidate | Evidence | Resolvable today | HTTPS suitability | Note |
|---|---|---|---|---|
| `DESKTOP-BVH8F8L` | computer name; already the URL users must use for `/invc` | LLMNR/NetBIOS only (no DNS record) | needs a certificate with this SAN; single-label names are usually treated as **Local intranet** zone by Windows browsers → IWA auto-negotiation | strongest candidate |
| `desktop-bvh8f8l.local` | mDNS | yes (mDNS) | as above | fallback for non-Windows clients |
| `192.168.1.10` | current DHCP lease | yes | poor (IP SANs, and the lease can change; an existing certificate names **192.168.1.99**, evidence that the address has already changed once) | not recommended in `AllowedHosts` |
| `invc`, `invc-web`, `invc.lan` (router DNS alias) | none — not resolvable, no internal DNS observed | no | would be ideal for a stable URL | requires router/DNS work by the owner |

**Recommendation:** `AllowedHosts=DESKTOP-BVH8F8L;desktop-bvh8f8l.local` is supported by evidence **if** the owner keeps this machine name
and gives it a DHCP reservation/static address; adding a friendly DNS alias is **OWNER DECISION REQUIRED**. `AllowedHosts` is not set anywhere yet.

## 9. HTTPS / certificates (metadata only — no private keys read or exported)
| Item | Value | Label |
|---|---|---|
| IIS HTTPS binding | none | OBSERVED |
| Port 443 | occupied by Laragon Apache (§4) | OBSERVED |
| `Cert:\LocalMachine\My` | one certificate: Subject `CN=192.168.1.99, CN=desktop-bvh8f8l`, **no SAN**, no EKU, self-issued, expires 2036-07-27, thumbprint `F3CC79A9E18FF4877C8792B87B9EF02CD2FCA100`, private key present | OBSERVED |
| Usability | not usable for browsers (no SAN → rejected by Edge/Chrome; wrong IP; not trusted by clients) | INFERRED |
| `Cert:\LocalMachine\WebHosting` | empty | OBSERVED |

**Classification: HTTPS = EXTERNAL DEPLOYMENT PREREQUISITE**, with two owner decisions: (1) certificate source (internal CA / self-signed
distributed to clients / public CA if a real DNS name exists) covering the chosen hostname; (2) port — free 443 (stop or re-port Laragon SSL)
or use an alternate HTTPS port such as 8443. No self-signed certificate was created in Phase 8.

## 10. Hosting Bundle / ANCM / .NET runtime
| Item | Value | Label |
|---|---|---|
| ASP.NET Core Module V2 | absent | OBSERVED |
| Machine-wide `dotnet` | `C:\Program Files\dotnet` absent; `dotnet` not on PATH; no `Microsoft.NETCore.App` / `Microsoft.AspNetCore.App` shared runtime | OBSERVED |
| Hosting Bundle uninstall entry | none | OBSERVED |
| Project-local SDK `C:\INVC\Web\.work\dotnet` (10.0.401) | exists, **irrelevant to IIS** — it is not registered with IIS and must never be referenced by production | OBSERVED |

**Classification: BLOCKED — .NET 10 HOSTING BUNDLE REQUIRED** (ASP.NET Core Runtime 10.0.x + ANCM V2), installed by an administrator
with a `W3SVC` restart afterwards (Microsoft installer behaviour) — outside this project.

## 11. SQL Server topology (SELECT-only)
| Item | Value | Label |
|---|---|---|
| Instance | `DESKTOP-BVH8F8L` default instance (`MSSQL16.MSSQLSERVER`), SQL Server 2022 **Enterprise Evaluation** 16.0.1000.6, service `NT Service\MSSQLSERVER` Running; SQL Browser disabled | OBSERVED |
| Location relative to IIS | **same machine** (IIS and SQL Server both on `DESKTOP-BVH8F8L`); discovery connection used Shared Memory + NTLM | OBSERVED |
| Network | TCP enabled, all IPs, static port **1433**, `ForceEncryption=0` (client `Encrypt=True` + `TrustServerCertificate` works) | OBSERVED (registry) |
| Authentication mode | **Mixed** (`LoginMode=2`, `IsIntegratedSecurityOnly=0`) | OBSERVED |
| `INV` | ONLINE, READ_WRITE, MULTI_USER, compatibility 100, FULL recovery, not read-only, not standby | OBSERVED |
| Server logins | `sa` (SQL, **sysadmin**), `DESKTOP-BVH8F8L\Mainam8888` (Windows, **sysadmin**), `NT AUTHORITY\NETWORK SERVICE`, `NT AUTHORITY\SYSTEM`, `NT Service\MSSQLSERVER`, `NT SERVICE\SQLSERVERAGENT`, `NT SERVICE\SQLTELEMETRY`, `NT SERVICE\SQLWriter`, `NT SERVICE\Winmgmt`. **No `IIS AppPool\…` login, no read-only application login** | OBSERVED |
| `INV` database users | `dbo` (mapped to `NT AUTHORITY\SYSTEM`, db_owner) and orphaned `PHAROPD` (no login). No reader principal | OBSERVED |
| Certificate for TDS | none configured (self-generated at start-up) → clients need `TrustServerCertificate=True` or a trusted certificate | INFERRED |

## 12. Current identities relevant to deployment
| Identity | Exists | Role today | Future role |
|---|---|---|---|
| `DESKTOP-BVH8F8L\Mainam8888` | yes (local admin, SQL sysadmin) | developer / operator | operator only — never the app-pool identity |
| `sa` | yes | used by legacy ASP and Access (Phase 0) | **never** used by INVC Web (factory refuses it) |
| `IIS AppPool\INVC-Web` (virtual account) | **does not exist yet** (created automatically when the pool is created) | — | proposed application DB identity (spec §7) |
| `NT AUTHORITY\NETWORK SERVICE` | SQL login exists, no INV user | — | possible alternative pool identity; less isolated than a per-pool virtual account |

## 13. Release package prerequisites / tooling status
| Item | Value |
|---|---|
| Release scripts present | `scripts/verify.ps1`, `publish-iis.ps1`, `test-release-artifact.ps1`, `new-release-manifest.ps1`, `project-path-guard.ps1`, `dev.ps1`, `dotnet-local.ps1` — OBSERVED |
| Last package (not rebuilt in Phase 8) | `.work/release/publish`, manifest for commit `bd5fb51`: **236 files, 25.6 MB**, framework-dependent (`Microsoft.NETCore.App 10.0.0; Microsoft.AspNetCore.App 10.0.0`), audit PASSED in the Phase 7 corrective pass |
| Source changes in Phase 8 | none (documentation only) |

## 14. Blockers (deployment cannot proceed until resolved — none are application defects)
1. **BLOCKER — HOSTING BUNDLE REQUIRED**: no ANCM V2 / ASP.NET Core 10 runtime on the host (§10).
2. **BLOCKER — DBA/ADMIN ACTION REQUIRED — read-only database identity**: no reader principal exists in SQL Server / INV (§11–12).
3. **BLOCKER — HTTPS**: no usable certificate; port 443 held by Laragon Apache (§9).
4. **BLOCKER (safety) — `/invc` serves `C:\INVC\Web`**: the legacy IIS application points at the repository working copy on a LAN-reachable
   host with the firewall disabled (§5). Phase 9 must not touch `C:\INVC\Web` as a web root; the owner must decide how to isolate the legacy site.
5. **Elevated read-only confirmation** of sites/pools/auth settings is outstanding (§ Discovery limit).

## 15. Unknowns
| Unknown | Why | How Phase 9 resolves it (read-only) |
|---|---|---|
| Exact site/app/pool/auth configuration | not elevated | elevated `Get-Website`, `Get-WebApplication`, `Get-ChildItem IIS:\AppPools`, `Get-WebConfigurationProperty -Filter system.webServer/security/authentication/*` (GET only) |
| Which client machines/accounts will use the site and whether their Windows accounts can match local accounts on the host | not observable from the host | OWNER DECISION REQUIRED (spec §5) |
| Whether the machine keeps this name / gets a fixed address / a DNS alias | infrastructure | OWNER DECISION REQUIRED (spec §9) |
| Who administers Laragon Apache on 443 | not observable | OWNER DECISION REQUIRED (spec §10) |
| Windows 10 Pro as a long-term server (10-connection IIS limit, laptop hardware, Wi-Fi, Evaluation SQL edition expiring ~2027-01) | outside scope | recorded as risk; OWNER DECISION |

## 16. Phase 9 addendum (elevated re-check, 2026-09-17)

Elevated `Get-Website`/raw `applicationHost.config` read (Phase 9 Checkpoint 0) resolved several Phase 8 UNRESOLVED items:

| Item | Value | Label |
|---|---|---|
| Legacy INVC | virtual directory **`/INVC`** under Default Web Site's root application (`app path=/`), **not** a separate IIS application — inherits **`DefaultAppPool`** | OBSERVED (elevated) |
| Physical path | `C:\INVC\Web`, confirmed via raw `applicationHost.config` | OBSERVED (elevated) |
| Default Web Site bindings | `*:80:` and `192.168.1.99:8080:invc-ksl.ddns.net` | OBSERVED (elevated) |
| `invc-ksl.ddns.net` binding | **OWNER-CONFIRMED (2026-09-17): intentional.** The owner configured this DDNS name/port-forward so external users can reach the **legacy** `/INVC` site from outside the LAN. This is a pre-existing, deliberate legacy access path — not a Phase 9 artifact and not a misconfiguration. | OWNER-CONFIRMED |

**Constraints this places on Phase 9** (owner-directed):
- Do not modify, remove, repoint, redirect, or reuse the `invc-ksl.ddns.net` binding or the `192.168.1.99:8080` binding.
- Do not touch router, NAT, DDNS, or firewall configuration.
- Legacy external access via `invc-ksl.ddns.net` must remain available and unaffected throughout Phase 9.
- The new side-by-side `INVC-Web` acceptance site is bound only to `DESKTOP-BVH8F8L:8090` / `:8443` and must **not** be exposed externally during Phase 9 (no new port-forward/DDNS entry for it).
- Final disposition of `invc-ksl.ddns.net` (kept, redirected, retired) is deferred to the cutover phase and requires a separate owner decision — out of scope for Phase 9.

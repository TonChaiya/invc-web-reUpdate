# Phase 8 plan — Read-only environment discovery + deployment / cutover design (NO deployment)

Owner-approved. Base `37ee094` (== origin/main, clean, 391 tests green). This phase is **discovery and design only**: read files,
query Windows/IIS/SQL Server state read-only, write documentation under `C:\INVC\Web`, commit and push. Nothing is created, changed,
installed, started, stopped, granted or deployed. INV stays strictly read-only (SELECT / SERVERPROPERTY / DATABASEPROPERTYEX / catalog views only).

Owner-approved access model recorded for the design: **IIS Windows Authentication; no INVC login page; no ASP.NET Core login/Identity;
Windows/IIS identity is the access-control boundary; Integrated Windows Authentication / SSO preferred.** A browser credential prompt
where IWA cannot negotiate is an infrastructure/client-policy matter, never a reason to add an application login form.

| # | Task | Method (read-only) | Output |
|---|---|---|---|
| 0 | Pre-flight: HEAD/origin/clean, `scripts/verify.ps1` = 391 | git, verify | ✔ (340 unit + 51 integration) |
| 1 | This plan | — | this file |
| 2 | Host identity: name, edition/build, architecture, domain/workgroup, DNS suffix, IP origin | `hostname`, `whoami`, `Win32_ComputerSystem`, `Win32_OperatingSystem`, `Get-NetIPConfiguration`, `Get-DnsClient*` | discovery §1–2 |
| 3 | IIS presence/version, W3SVC/WAS state, PowerShell modules, ANCM | `HKLM:\SOFTWARE\Microsoft\InetStp`, `Get-Service`, `Get-Module -ListAvailable`, file presence in `inetsrv` | discovery §3 |
| 4 | Existing sites/bindings | `Get-Website`/`appcmd list` (GET only) — **need elevation, unavailable**; fallback: `Get-NetTCPConnection`, `netsh http show sslcert/urlacl`, IIS log folders, local HTTP GET probes | discovery §4–5 |
| 5 | App pools | same limitation; process owner probe | discovery §6 |
| 6 | Windows Authentication feature and current auth settings | `InetStp\Components` registry, anonymous GET behaviour | discovery §7 |
| 7 | SSO / no-login feasibility | domain state, name resolution, client assumptions | spec §5 |
| 8 | Hostname / AllowedHosts candidates | computer name, DNS suffix, `Resolve-DnsName`, cert subjects, bindings | discovery §8, spec §9 |
| 9 | HTTPS: bindings + certificate metadata (no private keys) | `netsh http show sslcert`, `Cert:\LocalMachine\My` metadata | discovery §9 |
| 10 | Hosting Bundle / ANCM / machine-wide .NET | `Program Files\IIS`, `inetsrv\aspnetcorev2.dll`, `Program Files\dotnet`, uninstall registry | discovery §10 |
| 11 | Release tooling present; package status from existing manifest | file listing, `.work/release/manifest` | discovery §13 |
| 12 | Deployment folder design (no folder created) | `Win32_LogicalDisk`, `Test-Path` | spec §12 |
| 13 | SQL topology | `SERVERPROPERTY`, `DATABASEPROPERTYEX`, `sys.server_principals`, `sys.database_principals`, `sys.dm_exec_connections`, SQL registry (TCP/LoginMode) | discovery §11 |
| 14–16 | DB identity, permission requirement, app-pool design | analysis | spec §7–8, §10 |
| 17–20 | Site topology, side-by-side acceptance, cutover, rollback | analysis | spec §11, §13–15 |
| 21–26 | External configuration, Windows-auth target, authorization scope, identity display, logging, network | analysis | spec §6, §9, §16–19 |
| 27 | `docs/phase8-environment-discovery.md` (OBSERVED / INFERRED / OWNER DECISION REQUIRED labels) | — | ✔ |
| 28 | `docs/superpowers/specs/2026-09-16-iis-deployment-design.md` (Phase 9 design spec) | — | ✔ |
| 29 | Update `docs/iis-deployment-runbook.md` with discovered facts + approved auth model; every infrastructure step marked FUTURE | — | ✔ |
| 30 | Self-review (no TBD without reason, no login, no DB writes, no `sa`, no `C:\INVC\Web` as web root, concrete rollback, explicit blockers) | — | ✔ |
| 31–32 | `scripts/verify.ps1`, `git diff --check`, commit "Phase 8: document IIS deployment environment", fast-forward push | — | report |

Discovery limits (recorded, not worked around): the session is not elevated, so `WebAdministration`/`IISAdministration`/`appcmd list`
cannot read `applicationHost.config`/`redirection.config`, and `Get-WindowsOptionalFeature` is refused. Site, application-pool and
per-site authentication values are therefore **INFERRED** from HTTP behaviour, http.sys state, log folders and the IIS component registry,
and are marked as such. An elevated **read-only** `Get-Website` / `Get-WebBinding` / `Get-ChildItem IIS:\AppPools` /
`Get-WebConfigurationProperty` pass is listed as the first Phase 9 prerequisite.

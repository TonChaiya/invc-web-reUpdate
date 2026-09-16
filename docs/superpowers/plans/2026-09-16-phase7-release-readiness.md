# Phase 7 plan — Release readiness + security hardening (package only, no deployment)

Owner-approved. Base `f0427fd` (== origin/main, clean, 358 tests green). Boundaries: modify only `C:\INVC\Web`; INV strictly read-only;
no IIS/inetpub/Windows/SQL Server/Access changes; no appcmd/msdeploy/IIS cmdlets; all artifacts under `.work/release/`.

| # | Task | Output |
|---|---|---|
| 0 | Pre-flight (HEAD/origin/clean/358) | ✔ |
| 1 | This plan | this file |
| 2 | Audit Program.cs, csproj, appsettings*, Error/Health pages, Data/, scripts, .gitignore; baseline `dotnet publish` of `src/Invc.Web` to `.work\phase7-baseline-publish\`; record artifact contents | notes in readiness doc |
| 3–4 | Publish isolation: exclude `appsettings.Development.json` and `*.pdb` from publish via csproj; prove no legacy/source files enter the package | csproj + audit script + tests |
| 5–7 | `appsettings.Production.json` (non-secret logging defaults); `ProductionConfigurationValidator` (connection string present/valid, no `sa`, `AllowedHosts` not `*` in Production) with fail-fast at startup; document `InvDatabase__ConnectionString` + `AllowedHosts` as deployment inputs | Core/Web + unit tests |
| 8–9 | Error page review (no details in Production, request id kept); `/Health` redaction in Production (status, elapsed, environment; 503 on failure) with Development details retained | Web + tests |
| 10–11 | Security headers middleware (nosniff, Referrer-Policy, X-Frame-Options, Permissions-Policy); CSP documented as future; HTTPS/HSTS preserved and documented as IIS prerequisite | Web + tests |
| 12–13 | Document authentication decision (none in app; owner decision) and DB identity blocker (never `sa`; no provisioning performed) | docs |
| 14–16 | Release `.pdb` exclusion; verify generated ASP.NET Core `web.config`; artifacts under `.work/release/{publish,manifest,smoke,logs}` (git-ignored) | csproj + scripts |
| 17–21 | `scripts/dev.ps1`, `scripts/verify.ps1`, `scripts/publish-iis.ps1` (package-only, output-path guard), `scripts/test-release-artifact.ps1`, `scripts/new-release-manifest.ps1` | scripts + tests of guard logic |
| 22–25 | Published Production smoke (AllowedHosts local, process-local env), negative smoke against a non-listening endpoint (503, no disclosure), functional checks (static, Thai, print CSS, env, no dev config, no `.asp`) | evidence in readiness doc |
| 26–29 | New-app security source review; release-level read-only SQL assertion test; Release-mode dashboard performance recheck; logging review | tests + doc |
| 30–34 | `docs/phase7-release-readiness.md` (checklist READY/BLOCKED/OWNER DECISION), `docs/iis-deployment-runbook.md` (incl. rollback, infrastructure risks) | docs |
| 35–37 | Tests; full regression; final pipeline: verify → publish → audit → smoke → negative smoke → manifest → `git diff --check` | evidence |
| 38–39 | Out-of-project write audit; commit "Phase 7: prepare secure release package"; fast-forward push | checkpoint |

# Audits a publish output of src/Invc.Web. Fails (exit 1) when forbidden artifacts appear, required files are missing,
# or the shipped configuration contains a populated connection secret. Audits the NEW artifact only — never the legacy root.
# Usage: .\scripts\test-release-artifact.ps1 [-PublishPath <project>\.work\release\publish]
param([string]$PublishPath)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'project-path-guard.ps1')
$root = Get-NormalizedProjectPath (Split-Path -Parent $PSScriptRoot)
if (-not $PublishPath) { $PublishPath = Join-Path $root '.work\release\publish' }
# Release-pipeline invariant: only a package inside this project is ever audited (exact-boundary check, reparse points refused).
Assert-PathInsideProject -Candidate $PublishPath -ProjectRoot $root -Purpose 'publish path' -RequireDescendant
$PublishPath = Get-NormalizedProjectPath $PublishPath
if (-not (Test-Path -LiteralPath $PublishPath)) { Write-Host "publish path not found: $PublishPath" -ForegroundColor Red; exit 1 }

$failures = New-Object System.Collections.Generic.List[string]
$files = Get-ChildItem -Recurse -File $PublishPath
$dirs = Get-ChildItem -Recurse -Directory $PublishPath

# 1. Forbidden file patterns (legacy evidence, source, symbols, developer config)
$forbiddenPatterns = '*.asp','*.mdb','*.accdb','*.rar','*.zip','*.7z','*.cs','*.csproj','*.sln','*.slnx','*.pdb','appsettings.Development.json','appsettings.*.local.json','appsettings.Local.json','secrets.json','*.mno','*.inc'
foreach ($p in $forbiddenPatterns) {
    $hits = $files | Where-Object { $_.Name -like $p }
    foreach ($h in $hits) { $failures.Add("forbidden artifact: $($h.FullName.Substring($PublishPath.Length))") }
}
foreach ($d in 'Connections','_mmServerScripts','_notes','tests','docs','scripts','Login_v4','login-form-v4') {
    if ($dirs | Where-Object { $_.Name -ieq $d }) { $failures.Add("forbidden directory: $d") }
}

# 2. Required files
foreach ($r in 'Invc.Web.dll','Invc.Core.dll','Invc.Infrastructure.dll','appsettings.json','appsettings.Production.json','web.config','Invc.Web.runtimeconfig.json') {
    if (-not (Test-Path (Join-Path $PublishPath $r))) { $failures.Add("missing required file: $r") }
}
foreach ($r in 'wwwroot\css\site.css','wwwroot\css\print.css','wwwroot\lib\bootstrap\dist\css\bootstrap.min.css') {
    if (-not (Test-Path (Join-Path $PublishPath $r))) { $failures.Add("missing static asset: $r") }
}

# 3. Generated web.config must be the ASP.NET Core hosting config, not the legacy root one
$wc = Get-Content (Join-Path $PublishPath 'web.config') -Raw -ErrorAction SilentlyContinue
if ($wc) {
    if ($wc -notmatch 'AspNetCoreModuleV2' -or $wc -notmatch 'Invc\.Web\.dll') { $failures.Add("web.config is not the ASP.NET Core hosting configuration for Invc.Web") }
    foreach ($bad in 'httpErrors','_mmServerScripts','password=','pwd=','Provider=SQLOLEDB') {
        if ($wc -match [regex]::Escape($bad)) { $failures.Add("web.config contains forbidden content: $bad") }
    }
}

# 4. Shipped configuration must not carry a populated connection secret
foreach ($cfg in 'appsettings.json','appsettings.Production.json') {
    $path = Join-Path $PublishPath $cfg
    if (Test-Path $path) {
        $json = Get-Content $path -Raw | ConvertFrom-Json
        $cs = $null
        if ($json.PSObject.Properties['InvDatabase']) { $cs = $json.InvDatabase.ConnectionString }
        if ($cs -and $cs.Trim().Length -gt 0) { $failures.Add("$cfg ships a populated InvDatabase:ConnectionString") }
        $raw = Get-Content $path -Raw
        if ($raw -match '(?i)password\s*=|pwd\s*=|User ID\s*=\s*sa') { $failures.Add("$cfg contains credential-like text") }
    }
}

# 5. Report
Write-Host ("artifact: {0} files, {1} directories" -f $files.Count, $dirs.Count)
if ($failures.Count -gt 0) {
    Write-Host "RELEASE ARTIFACT AUDIT FAILED:" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
Write-Host "RELEASE ARTIFACT AUDIT PASSED (no legacy ASP, no source, no symbols, no dev config, no shipped secret)" -ForegroundColor Green
exit 0

# PACKAGE ONLY — prepares an IIS-compatible, framework-dependent Release publish of src/Invc.Web inside the project.
# This script NEVER deploys: it does not call appcmd/msdeploy/IIS cmdlets and refuses any output path outside C:\INVC\Web
# (exact path-boundary check plus reparse-point protection; see project-path-guard.ps1).
# Usage: .\scripts\publish-iis.ps1 [-OutputPath <path under the project>] [-GuardOnly]
#   default OutputPath = <project>/.work/release/publish
#   -GuardOnly         validates the output path and exits without publishing (used by tests).
param(
    [string]$OutputPath,
    [switch]$GuardOnly
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'project-path-guard.ps1')
$root = Get-NormalizedProjectPath (Split-Path -Parent $PSScriptRoot)
if (-not $OutputPath) { $OutputPath = Join-Path $root '.work\release\publish' }

# Containment is asserted BEFORE anything is created or removed. The output must be strictly below the project root.
Assert-PathInsideProject -Candidate $OutputPath -ProjectRoot $root -Purpose 'publish output path' -RequireDescendant
$resolvedOut = Get-NormalizedProjectPath $OutputPath
Write-Host "Output path accepted: $resolvedOut"
if ($GuardOnly) { exit 0 }

$dotnetLocal = Join-Path $PSScriptRoot 'dotnet-local.ps1'
$project = Join-Path $root 'src\Invc.Web\Invc.Web.csproj'
New-Item -ItemType Directory -Force -Path (Split-Path $resolvedOut -Parent) | Out-Null
# Re-check after the parent exists (a junction could have appeared), then clear the previous package.
Assert-PathInsideProject -Candidate $resolvedOut -ProjectRoot $root -Purpose 'publish output path' -RequireDescendant
if (Test-Path -LiteralPath $resolvedOut) { Remove-Item -LiteralPath $resolvedOut -Recurse -Force }

Write-Host "== restore" -ForegroundColor Cyan
& $dotnetLocal restore $project --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "== publish (Release, framework-dependent) -> $resolvedOut" -ForegroundColor Cyan
& $dotnetLocal publish $project -c Release --no-restore -o $resolvedOut --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$count = (Get-ChildItem -Recurse -File -LiteralPath $resolvedOut).Count
Write-Host "PACKAGE READY: $count files in $resolvedOut (not deployed)" -ForegroundColor Green
exit 0

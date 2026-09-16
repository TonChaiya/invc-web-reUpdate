# PACKAGE ONLY — prepares an IIS-compatible, framework-dependent Release publish of src/Invc.Web inside the project.
# This script NEVER deploys: it does not call appcmd/msdeploy/IIS cmdlets and refuses any output path outside C:\INVC\Web.
# Usage: .\scripts\publish-iis.ps1 [-OutputPath <path under the project>] [-GuardOnly]
#   default OutputPath = <project>\.work\release\publish
#   -GuardOnly         validates the output path and exits without publishing (used by tests).
param(
    [string]$OutputPath,
    [switch]$GuardOnly
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Split-Path -Parent $PSScriptRoot)).Path.TrimEnd('\')
if (-not $OutputPath) { $OutputPath = Join-Path $root '.work\release\publish' }

function Test-PathInsideProject([string]$candidate, [string]$projectRoot) {
    # Normalise without requiring the path to exist; reject anything that does not stay under the project root.
    $full = [System.IO.Path]::GetFullPath($candidate).TrimEnd('\')
    $prefix = $projectRoot.TrimEnd('\') + '\'
    return $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase) -and -not ($full -match '\\(inetpub|Windows)\\')
}

if (-not (Test-PathInsideProject $OutputPath $root)) {
    Write-Host "REFUSED: output path '$OutputPath' is outside the project root '$root'. This script only packages inside the project; deployment to IIS is a separate, owner-approved step." -ForegroundColor Red
    exit 2
}
$resolvedOut = [System.IO.Path]::GetFullPath($OutputPath)
Write-Host "Output path accepted: $resolvedOut"
if ($GuardOnly) { exit 0 }

$dotnetLocal = Join-Path $PSScriptRoot 'dotnet-local.ps1'
$project = Join-Path $root 'src\Invc.Web\Invc.Web.csproj'
New-Item -ItemType Directory -Force -Path (Split-Path $resolvedOut -Parent) | Out-Null
if (Test-Path $resolvedOut) { Remove-Item -Recurse -Force $resolvedOut }

Write-Host "== restore" -ForegroundColor Cyan
& $dotnetLocal restore $project --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "== publish (Release, framework-dependent) -> $resolvedOut" -ForegroundColor Cyan
& $dotnetLocal publish $project -c Release --no-restore -o $resolvedOut --nologo -v q
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$count = (Get-ChildItem -Recurse -File $resolvedOut).Count
Write-Host "PACKAGE READY: $count files in $resolvedOut (not deployed)" -ForegroundColor Green
exit 0

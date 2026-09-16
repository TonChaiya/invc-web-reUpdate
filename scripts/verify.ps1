# Repeatable project verification with the project-local SDK: dotnet --info, restore, build, test, git diff --check.
# Usage: .\scripts\verify.ps1 [-SkipIntegration] [-Configuration Release]
#   -SkipIntegration  runs unit tests only (integration tests need the read-only INV database; they skip themselves when it is unreachable).
# Exit code is non-zero on the first failing step. No database writes are performed by any step.
param(
    [switch]$SkipIntegration,
    [string]$Configuration = "Release"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dotnetLocal = Join-Path $PSScriptRoot 'dotnet-local.ps1'
$sln = Join-Path $root 'Invc.slnx'

function Step([string]$name, [scriptblock]$action) {
    Write-Host "== $name" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -ne 0) { Write-Host "FAILED: $name (exit $LASTEXITCODE)" -ForegroundColor Red; exit $LASTEXITCODE }
}

Push-Location $root
try {
    Step "dotnet --info"   { & $dotnetLocal --info | Select-String 'Version:|Base Path|RID' | ForEach-Object { $_.Line.Trim() } }
    Step "restore"         { & $dotnetLocal restore $sln --nologo -v q }
    Step "build ($Configuration)" { & $dotnetLocal build $sln -c $Configuration --no-restore --nologo -v q }
    if ($SkipIntegration) {
        Step "test (unit only)" { & $dotnetLocal test (Join-Path $root 'tests\Invc.UnitTests\Invc.UnitTests.csproj') -c $Configuration --no-build --nologo }
    } else {
        Step "test (all)"  { & $dotnetLocal test $sln -c $Configuration --no-build --nologo }
    }
    # git prints line-ending notices on stderr; PowerShell 5.1 would treat them as errors, so merge streams via cmd.
    Step "git diff --check" { cmd /c "git diff --check 2>&1"; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
    Write-Host "VERIFY OK" -ForegroundColor Green
    exit 0
}
finally { Pop-Location }

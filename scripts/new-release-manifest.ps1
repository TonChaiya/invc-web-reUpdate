# Writes a release manifest (JSON) for a publish output: commit SHA, timestamps, framework, publish mode, per-file SHA-256.
# Both paths must be inside the project (exact-boundary check, reparse points refused); the manifest must be a descendant
# file path, never the project root. Nothing is created before the guard passes.
# Usage: .\scripts\new-release-manifest.ps1 [-PublishPath ...] [-ManifestPath ...] [-GuardOnly]
#   defaults: <project>/.work/release/publish  and  <project>/.work/release/manifest/release-manifest.json
param([string]$PublishPath, [string]$ManifestPath, [switch]$GuardOnly)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'project-path-guard.ps1')
$root = Get-NormalizedProjectPath (Split-Path -Parent $PSScriptRoot)
if (-not $PublishPath) { $PublishPath = Join-Path $root '.work\release\publish' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $root '.work\release\manifest\release-manifest.json' }

Assert-PathInsideProject -Candidate $PublishPath -ProjectRoot $root -Purpose 'publish path' -RequireDescendant
Assert-PathInsideProject -Candidate $ManifestPath -ProjectRoot $root -Purpose 'manifest path' -RequireDescendant
$PublishPath = Get-NormalizedProjectPath $PublishPath
$ManifestPath = Get-NormalizedProjectPath $ManifestPath
if ((Test-Path -LiteralPath $ManifestPath) -and (Get-Item -LiteralPath $ManifestPath).PSIsContainer) {
    Write-Host "REFUSED: manifest path '$ManifestPath' is a directory; a file path is required." -ForegroundColor Red; exit 2
}
Write-Host "Paths accepted: publish=$PublishPath manifest=$ManifestPath"
if ($GuardOnly) { exit 0 }
if (-not (Test-Path -LiteralPath $PublishPath)) { Write-Host "publish path not found: $PublishPath" -ForegroundColor Red; exit 1 }

Push-Location $root
try {
    $sha = (git rev-parse HEAD).Trim()
    $dirty = (git status --porcelain -- . ':!.work' | Measure-Object -Line).Lines -gt 0
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $PublishPath 'Invc.Web.runtimeconfig.json') -Raw | ConvertFrom-Json
    $files = Get-ChildItem -Recurse -File -LiteralPath $PublishPath | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            path   = $_.FullName.Substring($PublishPath.Length + 1).Replace('\', '/')
            bytes  = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $manifest = [pscustomobject]@{
        application      = 'Invc.Web'
        commit           = $sha
        workingTreeDirty = $dirty
        generatedUtc     = (Get-Date).ToUniversalTime().ToString('o')
        generatedLocal   = (Get-Date).ToString('o')
        targetFramework  = $runtimeConfig.runtimeOptions.tfm
        runtimeFramework = ($runtimeConfig.runtimeOptions.frameworks | ForEach-Object { "$($_.name) $($_.version)" }) -join '; '
        publishMode      = 'framework-dependent, Release, portable (no .pdb, no appsettings.Development.json)'
        fileCount        = $files.Count
        totalBytes       = ($files | Measure-Object bytes -Sum).Sum
        files            = $files
    }
    $manifestDir = Split-Path $ManifestPath -Parent
    Assert-PathInsideProject -Candidate $manifestDir -ProjectRoot $root -Purpose 'manifest directory' -RequireDescendant
    New-Item -ItemType Directory -Force -Path $manifestDir | Out-Null
    # Final re-check after the directory exists (guards against a junction created in between), then write.
    Assert-PathInsideProject -Candidate $ManifestPath -ProjectRoot $root -Purpose 'manifest path' -RequireDescendant
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ManifestPath -Encoding utf8
    Write-Host ("MANIFEST WRITTEN: {0} (commit {1}, {2} files)" -f $ManifestPath, $sha.Substring(0, 7), $files.Count) -ForegroundColor Green
    exit 0
}
finally { Pop-Location }

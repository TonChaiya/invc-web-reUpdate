# Writes a release manifest (JSON) for a publish output: commit SHA, timestamps, framework, publish mode, per-file SHA-256.
# Output stays under the project: <project>\.work\release\manifest\release-manifest.json (git-ignored).
# Usage: .\scripts\new-release-manifest.ps1 [-PublishPath ...] [-ManifestPath ...]
param([string]$PublishPath, [string]$ManifestPath)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $PublishPath) { $PublishPath = Join-Path $root '.work\release\publish' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $root '.work\release\manifest\release-manifest.json' }
if (-not (Test-Path $PublishPath)) { Write-Host "publish path not found: $PublishPath" -ForegroundColor Red; exit 1 }
if (-not ([System.IO.Path]::GetFullPath($ManifestPath)).StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) { Write-Host "manifest path must be inside the project" -ForegroundColor Red; exit 2 }

Push-Location $root
try {
    $sha = (git rev-parse HEAD).Trim()
    $dirty = (git status --porcelain -- . ':!.work' | Measure-Object -Line).Lines -gt 0
    $runtimeConfig = Get-Content (Join-Path $PublishPath 'Invc.Web.runtimeconfig.json') -Raw | ConvertFrom-Json
    $files = Get-ChildItem -Recurse -File $PublishPath | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            path   = $_.FullName.Substring($PublishPath.TrimEnd('\').Length + 1).Replace('\', '/')
            bytes  = $_.Length
            sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
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
    New-Item -ItemType Directory -Force -Path (Split-Path $ManifestPath -Parent) | Out-Null
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -Path $ManifestPath -Encoding utf8
    Write-Host ("MANIFEST WRITTEN: {0} (commit {1}, {2} files)" -f $ManifestPath, $sha.Substring(0, 7), $files.Count) -ForegroundColor Green
    exit 0
}
finally { Pop-Location }

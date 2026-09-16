# Runs the project-local .NET SDK with all caches/temp state confined to C:\INVC\Web\.work.
# Usage:  .\scripts\dotnet-local.ps1 build Invc.sln
# Nothing here changes machine-wide or user-wide environment; variables are set for this process only.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot          # C:\INVC\Web
$work = Join-Path $root '.work'
foreach ($d in 'dotnet','dotnet-home','nuget-packages','nuget-http-cache','nuget-scratch','temp') {
    New-Item -ItemType Directory -Force -Path (Join-Path $work $d) | Out-Null
}
$env:DOTNET_ROOT                        = Join-Path $work 'dotnet'
$env:DOTNET_CLI_HOME                    = Join-Path $work 'dotnet-home'
$env:NUGET_PACKAGES                     = Join-Path $work 'nuget-packages'
$env:NUGET_HTTP_CACHE_PATH              = Join-Path $work 'nuget-http-cache'
$env:NUGET_SCRATCH                      = Join-Path $work 'nuget-scratch'
$env:TEMP                               = Join-Path $work 'temp'
$env:TMP                                = Join-Path $work 'temp'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_TELEMETRY_OPTOUT        = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE  = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH    = '0'
$env:DOTNET_NOLOGO                      = '1'
$env:MSBUILDDISABLENODEREUSE            = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER      = '0'
$dotnet = Join-Path $env:DOTNET_ROOT 'dotnet.exe'
if (-not (Test-Path $dotnet)) { throw "Project-local SDK not found at $dotnet (see docs/development-setup.md)" }
& $dotnet @args
exit $LASTEXITCODE

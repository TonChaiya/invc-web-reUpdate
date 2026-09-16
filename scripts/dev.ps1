# Starts the new ASP.NET Core Web project with the project-local .NET SDK and project-local caches only.
# Usage: .\scripts\dev.ps1 [-Url http://127.0.0.1:5265] [-Environment Development]
# Nothing is installed or persisted globally; IIS is never touched.
param(
    [string]$Url = "http://127.0.0.1:5265",
    [string]$Environment = "Development"
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$env:ASPNETCORE_ENVIRONMENT = $Environment   # process-local only
& (Join-Path $PSScriptRoot 'dotnet-local.ps1') run --project (Join-Path $root 'src\Invc.Web\Invc.Web.csproj') --urls $Url
exit $LASTEXITCODE

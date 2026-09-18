# Applies the versioned MySQL scripts under db/mysql to the APPLICATION-OWNED database (default `invc_web`).
# Never touches SQL Server INV. Never drops or resets anything: each script is idempotent and records its version.
# Usage: .\scripts\mysql-migrate.ps1 [-Database invc_web] [-User root] [-Server 127.0.0.1] [-Port 3306] [-MySqlExe <path>] [-Status]
#   Password: set the environment variable MYSQL_PWD for the process (mysql.exe reads it) — never pass it on the command line,
#   never write it into a tracked file. Laragon's default local root has no password.
#   -Status only prints the applied versions.
param(
    [string]$Database = 'invc_web',
    [string]$User = 'root',
    [string]$Server = '127.0.0.1',
    [int]$Port = 3306,
    [string]$MySqlExe = 'C:\laragon\bin\mysql\mysql-8.4.3-winx64\bin\mysql.exe',
    [switch]$Status
)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dir = Join-Path $root 'db\mysql'
if ($Database -ieq 'INV') { Write-Host 'REFUSED: the application database must not be named INV.' -ForegroundColor Red; exit 2 }
if (-not (Test-Path $MySqlExe)) { Write-Host "mysql.exe not found: $MySqlExe" -ForegroundColor Red; exit 1 }
$common = @('--protocol=tcp', "--host=$Server", "--port=$Port", "--user=$User", '--default-character-set=utf8mb4', '--batch', '--silent')

function Invoke-MySql([string]$sql, [string]$db) {
    $args = $common + @()
    if ($db) { $args += "--database=$db" }
    $out = $sql | & $MySqlExe @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "mysql failed: $out" }
    return $out
}

$version = Invoke-MySql 'SELECT VERSION();' $null
Write-Host "MySQL $version at ${Server}:$Port (user $User)"
Invoke-MySql "CREATE DATABASE IF NOT EXISTS ``$Database`` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;" $null | Out-Null
$hasVersionTable = Invoke-MySql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '$Database' AND table_name = 'schema_version';" $null
$applied = @()
if ([int]$hasVersionTable -gt 0) { $applied = @(Invoke-MySql 'SELECT version FROM schema_version ORDER BY version;' $Database | ForEach-Object { [int]$_ }) }
Write-Host "database ${Database}: applied versions = [$($applied -join ', ')]"
if ($Status) { exit 0 }

$scripts = Get-ChildItem (Join-Path $dir '*.sql') | Sort-Object Name
foreach ($s in $scripts) {
    if ($s.Name -notmatch '^(\d{3})_') { Write-Host "skipping unversioned file $($s.Name)"; continue }
    $v = [int]$Matches[1]
    if ($applied -contains $v) { Write-Host "  $($s.Name): already applied"; continue }
    Write-Host "  $($s.Name): applying..."
    $sql = Get-Content $s.FullName -Raw -Encoding UTF8
    Invoke-MySql $sql $Database | Out-Null
    Write-Host "  $($s.Name): done"
}
$after = @(Invoke-MySql 'SELECT version FROM schema_version ORDER BY version;' $Database)
Write-Host "applied versions now = [$($after -join ', ')]"
exit 0

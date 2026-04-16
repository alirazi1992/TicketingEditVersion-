<#
.SYNOPSIS
    Resets the TikQ SQL Server database (drop + create) for test environments only.

.DESCRIPTION
    Runs tools/sql/reset-tikq-db.sql via sqlcmd (if installed). Without -Force,
    only prints what would be done and exits. Use -Force to actually run.

.PARAMETER Server
    SQL Server instance (default: "." for local default instance).

.PARAMETER DatabaseName
    Database name to drop/create (default: TikQ). Must match the name used in the SQL script.

.PARAMETER UseTrusted
    Use Windows Integrated Security (default: true). If false, use -Username and -Password.

.PARAMETER Username
    SQL login (optional). Used when -UseTrusted is false.

.PARAMETER Password
    SQL password (optional). Used when -UseTrusted is false.

.PARAMETER Force
    Required to actually execute the reset. Without -Force, script only prints the planned action and exits.
#>

[CmdletBinding()]
param(
    [string] $Server = ".",
    [string] $DatabaseName = "TikQ",
    [bool]   $UseTrusted = $true,
    [string] $Username,
    [string] $Password,
    [switch] $Force
)

$ErrorActionPreference = "Stop"
$ScriptDir = $PSScriptRoot
$SqlPath   = Join-Path $ScriptDir "reset-tikq-db.sql"

if (-not (Test-Path $SqlPath)) {
    Write-Error "SQL script not found: $SqlPath"
    exit 1
}

# Resolve path for sqlcmd
$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if (-not $sqlcmd) {
    Write-Warning "sqlcmd not found in PATH. Install SQL Server Command Line Utilities or run the SQL script manually in SSMS."
    Write-Host "  SSMS: Open and execute: $SqlPath"
    exit 1
}

$conn = "Server=$Server"
if ($UseTrusted) {
    $conn += ";Integrated Security=true"
} else {
    if (-not $Username -or -not $Password) {
        Write-Error "When -UseTrusted is false, -Username and -Password are required."
        exit 1
    }
    $conn += ";User Id=$Username;Password=$Password"
}

$conn += ";TrustServerCertificate=true"
$what = "Run reset script: $SqlPath`n  Server: $Server | Database: $DatabaseName | Trusted: $UseTrusted"

if (-not $Force) {
    Write-Host "DRY RUN (no changes). To execute, pass -Force.`n"
    Write-Host $what
    Write-Host "`nCommand that would run:"
    if ($UseTrusted) {
        Write-Host "  sqlcmd -S `"$Server`" -E -d master -i `"$SqlPath`""
    } else {
        Write-Host "  sqlcmd -S `"$Server`" -U `"$Username`" -P **** -d master -i `"$SqlPath`""
    }
    exit 0
}

# Replace database name in SQL content so -DatabaseName is honored
$sqlContent = Get-Content -Raw -Path $SqlPath
$sqlContent = $sqlContent -replace '\[TikQ\]', "[$DatabaseName]"
$sqlContent = $sqlContent -replace "N'TikQ'", "N'$DatabaseName'"
$tempSql = Join-Path $env:TEMP "reset-tikq-db-$([Guid]::NewGuid().ToString('N')).sql"
try {
    Set-Content -Path $tempSql -Value $sqlContent -NoNewline
    if ($UseTrusted) {
        & sqlcmd -S $Server -E -d master -i $tempSql
    } else {
        & sqlcmd -S $Server -U $Username -P $Password -d master -i $tempSql
    }
    if ($LASTEXITCODE -ne 0) {
        Write-Error "sqlcmd exited with code $LASTEXITCODE"
        exit $LASTEXITCODE
    }
    Write-Host "Database '$DatabaseName' reset successfully on $Server."
} finally {
    if (Test-Path $tempSql) { Remove-Item $tempSql -Force }
}

<#
.SYNOPSIS
  Verifies TikQ SQL Server schema and migration history (SubcategoryFieldDefinitions fix).

.DESCRIPTION
  Prints: (1) current DB name and required columns check for SubcategoryFieldDefinitions,
  (2) migrations history tail, (3) success/failure messages.
  Use after applying the schema fix (EF migrations or fix-subcategory-field-definitions-schema.sql).

.PARAMETER ConnectionString
  SQL Server connection string to the TikQ database (e.g. "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;").

.EXAMPLE
  .\verify-schema-and-migrations.ps1 -ConnectionString "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;"
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " TikQ schema & migrations verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$dbName = $null
$columnsOk = $false
$columnsMissing = @()
$migrationTail = @()
$overallSuccess = $false
$conn = $null

try {
    Add-Type -AssemblyName "System.Data"
    $conn = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    $conn.Open()

    # 1) Current DB name
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT DB_NAME() AS CurrentDb"
    $dbName = $cmd.ExecuteScalar()
    Write-Host "[1] Current database: $dbName" -ForegroundColor White

    # 2) Required columns on SubcategoryFieldDefinitions
    $required = @("FieldKey", "SortOrder", "IsActive", "CreatedAt", "UpdatedAt")
    $cmd.CommandText = @"
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'SubcategoryFieldDefinitions'
  AND COLUMN_NAME IN (N'FieldKey', N'SortOrder', N'IsActive', N'CreatedAt', N'UpdatedAt')
"@
    $reader = $cmd.ExecuteReader()
    $found = [System.Collections.Generic.List[string]]::new()
    while ($reader.Read()) { $found.Add($reader.GetString(0)) }
    $reader.Close()

    $columnsMissing = $required | Where-Object { $_ -notin $found }
    $columnsOk = $columnsMissing.Count -eq 0

    if ($columnsOk) {
        Write-Host "[2] SubcategoryFieldDefinitions: required columns present (FieldKey, SortOrder, IsActive, CreatedAt, UpdatedAt)" -ForegroundColor Green
    } else {
        Write-Host "[2] SubcategoryFieldDefinitions: MISSING columns: $($columnsMissing -join ', ')" -ForegroundColor Red
    }

    # 3) Migration history tail
    $cmd.CommandText = "SELECT TOP 5 MigrationId, ProductVersion FROM [dbo].[__EFMigrationsHistory] ORDER BY MigrationId DESC"
    $reader = $cmd.ExecuteReader()
    $rows = [System.Collections.Generic.List[object]]::new()
    while ($reader.Read()) {
        $rows.Add([PSCustomObject]@{ MigrationId = $reader.GetString(0); ProductVersion = $reader.GetString(1) })
    }
    $reader.Close()
    $migrationTail = $rows

    Write-Host "[3] Migration history (tail):" -ForegroundColor White
    if ($migrationTail.Count -eq 0) {
        Write-Host "    (no rows in __EFMigrationsHistory)" -ForegroundColor Yellow
    } else {
        foreach ($r in $migrationTail) {
            Write-Host "    $($r.MigrationId)" -ForegroundColor Gray
        }
    }

    $conn.Close()
    $conn.Dispose()
    $conn = $null

    $overallSuccess = $columnsOk
}
catch {
    Write-Host "ERROR: $_" -ForegroundColor Red
    if ($conn -and $conn.State -eq 'Open') { try { $conn.Close() } catch {}; try { $conn.Dispose() } catch {} }
}

Write-Host ""
if ($overallSuccess) {
    Write-Host "Result: SUCCESS - Schema matches EF model; GetByIdWithIncludesAsync should work." -ForegroundColor Green
} else {
    Write-Host "Result: FAILURE - Fix schema (see docs/01_Runbook/SCHEMA_FIX_SUBCATEGORY_FIELD_DEFINITIONS.md)." -ForegroundColor Red
}
Write-Host ""
exit $(if ($overallSuccess) { 0 } else { 1 })

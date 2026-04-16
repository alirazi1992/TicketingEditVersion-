# PowerShell script to manually add DefaultValue column to SubcategoryFieldDefinitions table
# This is a fallback if migrations don't apply correctly
# Usage: Run from the backend/Ticketing.Backend directory:
# .\tools\add-defaultvalue-column.ps1

$ErrorActionPreference = "Stop"

$backendDir = (Get-Item -Path $PSScriptRoot).Parent.FullName
$dbPath = Join-Path $backendDir "App_Data\ticketing.db"

Write-Host "Attempting to add DefaultValue column to SubcategoryFieldDefinitions table..."

if (-not (Test-Path $dbPath)) {
    Write-Error "Database file not found at: $dbPath"
    exit 1
}

# Check if sqlite3 command is available
$sqlite3 = Get-Command sqlite3 -ErrorAction SilentlyContinue
if (-not $sqlite3) {
    Write-Warning "sqlite3 command not found. Please install SQLite command-line tools."
    Write-Host "Alternatively, you can use the backend's migration system by restarting the backend server."
    exit 1
}

# Check if column exists
Write-Host "Checking if DefaultValue column exists..."
$columnCheck = & sqlite3 $dbPath "SELECT COUNT(*) FROM pragma_table_info('SubcategoryFieldDefinitions') WHERE name = 'DefaultValue';"

if ($columnCheck -eq "1") {
    Write-Host "DefaultValue column already exists. No action needed."
    exit 0
}

# Add the column
Write-Host "Adding DefaultValue column..."
try {
    & sqlite3 $dbPath "ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN DefaultValue TEXT;"
    Write-Host "Successfully added DefaultValue column!"
} catch {
    Write-Error "Failed to add DefaultValue column: $_"
    exit 1
}

Write-Host "Done!"







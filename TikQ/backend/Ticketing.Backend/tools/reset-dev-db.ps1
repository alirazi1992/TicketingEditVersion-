# Reset Development Database Script
# This script safely backs up the current database and recreates it with fresh migrations
# WARNING: This will delete all data in the development database!

param(
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$dbPath = Join-Path $projectRoot "App_Data\ticketing.db"
$backupDir = Join-Path $projectRoot "App_Data\backup"

Write-Host "=== Development Database Reset Script ===" -ForegroundColor Cyan
Write-Host ""

# Check if database exists
if (-not (Test-Path $dbPath)) {
    Write-Host "Database file not found at: $dbPath" -ForegroundColor Yellow
    Write-Host "Nothing to reset. Exiting." -ForegroundColor Yellow
    exit 0
}

# Confirm action
if (-not $Force) {
    Write-Host "WARNING: This will DELETE all data in the development database!" -ForegroundColor Red
    Write-Host "Database location: $dbPath" -ForegroundColor Yellow
    Write-Host ""
    $confirm = Read-Host "Type 'YES' to continue (or press Enter to cancel)"
    
    if ($confirm -ne "YES") {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Check if backend is running
Write-Host "Checking for running backend processes..." -ForegroundColor Cyan
$backendProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Where-Object {
    $_.CommandLine -like "*Ticketing*" -or $_.Path -like "*Ticketing*"
}

if ($backendProcesses) {
    Write-Host "WARNING: Backend processes detected. Please stop the backend before resetting the database." -ForegroundColor Red
    Write-Host "Running processes:" -ForegroundColor Yellow
    $backendProcesses | ForEach-Object { Write-Host "  - PID $($_.Id): $($_.Path)" -ForegroundColor Yellow }
    Write-Host ""
    $continue = Read-Host "Continue anyway? (y/N)"
    if ($continue -ne "y" -and $continue -ne "Y") {
        Write-Host "Operation cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Create backup directory
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
    Write-Host "Created backup directory: $backupDir" -ForegroundColor Green
}

# Create timestamped backup
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupPath = Join-Path $backupDir "ticketing_$timestamp.db"

Write-Host "Creating backup..." -ForegroundColor Cyan
Copy-Item -Path $dbPath -Destination $backupPath -Force
Write-Host "Backup created: $backupPath" -ForegroundColor Green

# Delete original database
Write-Host "Deleting original database..." -ForegroundColor Cyan
Remove-Item -Path $dbPath -Force
Write-Host "Original database deleted." -ForegroundColor Green

# Instructions for user
Write-Host ""
Write-Host "=== Next Steps ===" -ForegroundColor Cyan
Write-Host "1. Start the backend server:" -ForegroundColor Yellow
Write-Host "   cd $projectRoot" -ForegroundColor White
Write-Host "   dotnet run" -ForegroundColor White
Write-Host ""
Write-Host "2. The database will be automatically recreated with migrations applied." -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Default users will be seeded:" -ForegroundColor Yellow
Write-Host "   - Admin: admin@test.com / Admin123!" -ForegroundColor White
Write-Host "   - Client: client1@test.com / Client123!" -ForegroundColor White
Write-Host ""
Write-Host "Backup location: $backupPath" -ForegroundColor Green
Write-Host ""
Write-Host "Database reset complete!" -ForegroundColor Green










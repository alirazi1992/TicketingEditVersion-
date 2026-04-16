# verify-backend.ps1
# Builds backend and verifies health endpoint
# This script ensures the backend can build and run correctly

param(
    [string]$ApiBaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

# Get script directory and repo root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$BackendPath = Join-Path $RepoRoot "backend\Ticketing.Backend"

Write-Host "=== Backend Verification ===" -ForegroundColor Cyan
Write-Host "Repo Root: $RepoRoot" -ForegroundColor Gray
Write-Host "Backend Path: $BackendPath" -ForegroundColor Gray
Write-Host ""

# Step 1: Verify .NET SDK
Write-Host "[1/3] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Host "✓ .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found" -ForegroundColor Red
    Write-Host "  Please install .NET 8 SDK" -ForegroundColor White
    exit 1
}

# Step 2: Build backend
Write-Host "[2/3] Building backend..." -ForegroundColor Yellow
Push-Location $BackendPath
try {
    Write-Host "  Running: dotnet clean" -ForegroundColor Gray
    dotnet clean --verbosity quiet 2>&1 | Out-Null
    
    Write-Host "  Running: dotnet build" -ForegroundColor Gray
    $buildOutput = dotnet build --no-restore 2>&1
    $buildSuccess = $LASTEXITCODE -eq 0
    
    if ($buildSuccess) {
        Write-Host "✓ Build succeeded" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        Pop-Location
        exit 1
    }
} catch {
    Write-Host "✗ Build error: $_" -ForegroundColor Red
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

# Step 3: Test health endpoint (if backend is running)
Write-Host "[3/3] Testing health endpoint..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/health" -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
    if ($healthResponse.StatusCode -eq 200) {
        $healthData = $healthResponse.Content | ConvertFrom-Json
        Write-Host "✓ Health check passed" -ForegroundColor Green
        Write-Host "  Status: $($healthData.status)" -ForegroundColor Gray
        Write-Host "  Environment: $($healthData.environment)" -ForegroundColor Gray
        if ($healthData.dbPath) {
            Write-Host "  DB Path: $($healthData.dbPath)" -ForegroundColor Gray
            $dbExists = Test-Path $healthData.dbPath
            Write-Host "  DB File Exists: $dbExists" -ForegroundColor $(if ($dbExists) { "Green" } else { "Yellow" })
        }
        if ($null -ne $healthData.canConnectToDb) {
            $dbStatus = if ($healthData.canConnectToDb) { "Connected" } else { "Disconnected" }
            $dbColor = if ($healthData.canConnectToDb) { "Green" } else { "Red" }
            Write-Host "  DB Connection: $dbStatus" -ForegroundColor $dbColor
        }
        Write-Host "  Timestamp: $($healthData.timestamp)" -ForegroundColor Gray
    } else {
        Write-Host "✗ Health check returned status: $($healthResponse.StatusCode)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "⚠ Health endpoint not reachable (backend may not be running)" -ForegroundColor Yellow
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  To start the backend, run:" -ForegroundColor White
    Write-Host "    .\tools\run-backend.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  Then run this script again to verify health endpoint" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Green
Write-Host "Backend builds successfully" -ForegroundColor Green
if ($healthResponse -and $healthResponse.StatusCode -eq 200) {
    Write-Host "Backend is running and healthy at: $ApiBaseUrl" -ForegroundColor Green
}
Write-Host ""



































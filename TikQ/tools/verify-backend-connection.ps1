# verify-backend-connection.ps1
# Quick verification script to check backend connectivity
# Uses repo-root relative paths (no hardcoded drive letters)

param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [switch]$SkipHealthCheck
)

$ErrorActionPreference = "Stop"

# Get script directory and repo root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

Write-Host "=== Backend Connection Verification ===" -ForegroundColor Cyan
Write-Host "Repo Root: $RepoRoot" -ForegroundColor Gray
Write-Host "API Base URL: $ApiBaseUrl" -ForegroundColor Gray
Write-Host ""

# Test 1: Health endpoint
if (-not $SkipHealthCheck) {
    Write-Host "[1/2] Testing health endpoint..." -ForegroundColor Yellow
    try {
        $healthResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/health" -TimeoutSec 5 -UseBasicParsing
        if ($healthResponse.StatusCode -eq 200) {
            $healthData = $healthResponse.Content | ConvertFrom-Json
            Write-Host "✓ Health check passed" -ForegroundColor Green
            Write-Host "  Status: $($healthData.status)" -ForegroundColor Gray
            Write-Host "  Environment: $($healthData.environment)" -ForegroundColor Gray
            if ($healthData.dbPath) {
                Write-Host "  DB Path: $($healthData.dbPath)" -ForegroundColor Gray
            }
            if ($null -ne $healthData.canConnectToDb) {
                $dbStatus = if ($healthData.canConnectToDb) { "Connected" } else { "Disconnected" }
                $dbColor = if ($healthData.canConnectToDb) { "Green" } else { "Red" }
                Write-Host "  DB Connection: $dbStatus" -ForegroundColor $dbColor
            }
            Write-Host "  Timestamp: $($healthData.timestamp)" -ForegroundColor Gray
        } else {
            Write-Host "✗ Health check failed with status: $($healthResponse.StatusCode)" -ForegroundColor Red
            exit 1
        }
    } catch {
        Write-Host "✗ Health check failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "" -ForegroundColor Red
        Write-Host "Troubleshooting:" -ForegroundColor Yellow
        Write-Host "  1. Ensure backend is running:" -ForegroundColor White
        Write-Host "     cd backend\Ticketing.Backend" -ForegroundColor Gray
        Write-Host "     dotnet run" -ForegroundColor Gray
        Write-Host "" -ForegroundColor White
        Write-Host "  2. Check if backend is listening on $ApiBaseUrl" -ForegroundColor White
        Write-Host "  3. Verify CORS is configured correctly" -ForegroundColor White
        exit 1
    }
} else {
    Write-Host "[1/2] Skipping health check" -ForegroundColor Gray
}

# Test 2: Ping endpoint (legacy)
Write-Host "[2/2] Testing ping endpoint..." -ForegroundColor Yellow
try {
    $pingResponse = Invoke-WebRequest -Uri "$ApiBaseUrl/api/ping" -TimeoutSec 5 -UseBasicParsing
    if ($pingResponse.StatusCode -eq 200) {
        $pingData = $pingResponse.Content | ConvertFrom-Json
        Write-Host "✓ Ping endpoint responded" -ForegroundColor Green
        Write-Host "  Message: $($pingData.message)" -ForegroundColor Gray
    } else {
        Write-Host "✗ Ping endpoint failed with status: $($pingResponse.StatusCode)" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Ping endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Green
Write-Host "Backend is accessible at: $ApiBaseUrl" -ForegroundColor Green
Write-Host "Swagger UI: $ApiBaseUrl/swagger" -ForegroundColor Gray
Write-Host "Health Check: $ApiBaseUrl/api/health" -ForegroundColor Gray
Write-Host ""


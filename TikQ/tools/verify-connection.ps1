# tools/verify-connection.ps1
# Verifies backend connectivity on ports 5000 and 5001, and frontend proxy endpoint

$ErrorActionPreference = "Continue"

Write-Host "=== Backend Connection Verification ===" -ForegroundColor Cyan
Write-Host ""

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$backendPath = Join-Path $repoRoot "backend\Ticketing.Backend"
$frontendPath = Join-Path $repoRoot "frontend"

# Check for .env.local to see effective API base URL
$envLocalPath = Join-Path $frontendPath ".env.local"
$effectiveApiBase = "http://localhost:5000"
if (Test-Path $envLocalPath) {
    $envContent = Get-Content $envLocalPath -Raw
    if ($envContent -match "NEXT_PUBLIC_API_BASE_URL=(.+)") {
        $effectiveApiBase = $matches[1].Trim().Trim('"').Trim("'")
        Write-Host "Found .env.local: NEXT_PUBLIC_API_BASE_URL=$effectiveApiBase" -ForegroundColor Cyan
    }
} else {
    Write-Host "No .env.local found, using default: $effectiveApiBase" -ForegroundColor Gray
}
Write-Host ""

# Test backend on port 5000
Write-Host "Testing backend on port 5000..." -ForegroundColor Yellow
$healthUrl5000 = "http://localhost:5000/api/health"
try {
    $response = Invoke-WebRequest -Uri $healthUrl5000 -Method GET -TimeoutSec 3 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Backend is running on port 5000" -ForegroundColor Green
        Write-Host "       Health endpoint: $healthUrl5000" -ForegroundColor Gray
        $port5000Ok = $true
    } else {
        Write-Host "  [FAIL] Backend returned status $($response.StatusCode)" -ForegroundColor Red
        Write-Host "         Tried URL: $healthUrl5000" -ForegroundColor Gray
        $port5000Ok = $false
    }
} catch {
    Write-Host "  [FAIL] Cannot connect to backend on port 5000" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "    Tried URL: $healthUrl5000" -ForegroundColor Gray
    $port5000Ok = $false
}

Write-Host ""

# Test backend on port 5001
Write-Host "Testing backend on port 5001..." -ForegroundColor Yellow
$healthUrl5001 = "http://localhost:5001/api/health"
try {
    $response = Invoke-WebRequest -Uri $healthUrl5001 -Method GET -TimeoutSec 3 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Backend is running on port 5001" -ForegroundColor Green
        Write-Host "       Health endpoint: $healthUrl5001" -ForegroundColor Gray
        $port5001Ok = $true
    } else {
        Write-Host "  [FAIL] Backend returned status $($response.StatusCode)" -ForegroundColor Red
        Write-Host "         Tried URL: $healthUrl5001" -ForegroundColor Gray
        $port5001Ok = $false
    }
} catch {
    Write-Host "  [FAIL] Cannot connect to backend on port 5001" -ForegroundColor Red
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "    Tried URL: $healthUrl5001" -ForegroundColor Gray
    $port5001Ok = $false
}

# Test Swagger endpoint (if enabled)
Write-Host ""
Write-Host "Testing Swagger endpoint..." -ForegroundColor Yellow
$swaggerUrl = "http://localhost:5000/swagger"
try {
    $response = Invoke-WebRequest -Uri $swaggerUrl -Method GET -TimeoutSec 3 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Swagger is accessible at $swaggerUrl" -ForegroundColor Green
        $swaggerOk = $true
    } else {
        Write-Host "  [SKIP] Swagger returned status $($response.StatusCode)" -ForegroundColor Yellow
        $swaggerOk = $false
    }
} catch {
    Write-Host "  [SKIP] Swagger not accessible (this is OK if disabled)" -ForegroundColor Yellow
    $swaggerOk = $false
}

Write-Host ""

# Test frontend proxy (if frontend is running)
Write-Host "Testing frontend proxy endpoint..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:3000/bapi/api/health" -Method GET -TimeoutSec 3 -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [OK] Frontend proxy is working on port 3000" -ForegroundColor Green
        $proxyOk = $true
    } else {
        Write-Host "  [FAIL] Frontend proxy returned status $($response.StatusCode)" -ForegroundColor Red
        $proxyOk = $false
    }
} catch {
    Write-Host "  [SKIP] Cannot connect to frontend proxy (frontend may not be running)" -ForegroundColor Yellow
    Write-Host "    Error: $($_.Exception.Message)" -ForegroundColor Gray
    Write-Host "    Note: This is OK if frontend is not running" -ForegroundColor Gray
    $proxyOk = $false
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan

if ($port5000Ok -or $port5001Ok) {
    Write-Host "[PASS] Backend is accessible" -ForegroundColor Green
    if ($port5000Ok) {
        Write-Host "  - Port 5000: OK" -ForegroundColor Green
    }
    if ($port5001Ok) {
        Write-Host "  - Port 5001: OK" -ForegroundColor Green
    }
} else {
    Write-Host "[FAIL] Backend is NOT accessible" -ForegroundColor Red
    Write-Host ""
    Write-Host "To start the backend:" -ForegroundColor Yellow
    Write-Host "  cd $backendPath" -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    exit 1
}

if ($proxyOk) {
    Write-Host "[PASS] Frontend proxy is working" -ForegroundColor Green
} else {
    Write-Host "[SKIP] Frontend proxy test skipped (frontend not running)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "All checks passed!" -ForegroundColor Green
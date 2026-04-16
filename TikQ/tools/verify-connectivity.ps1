# verify-connectivity.ps1
# Verifies frontend-backend connectivity by testing health endpoints on both ports

$ErrorActionPreference = "Continue"

Write-Host "=== TikQ Connectivity Verification ===" -ForegroundColor Cyan
Write-Host ""

# Test health endpoint on both ports
$ports = @(5000, 5001)
$results = @{}

foreach ($port in $ports) {
    $url = "http://localhost:$port/api/health"
    Write-Host "Testing: $url" -ForegroundColor Yellow
    
    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "  ✓ PASS: Port $port is responding (HTTP $($response.StatusCode))" -ForegroundColor Green
            $results[$port] = @{
                Status = "OK"
                StatusCode = $response.StatusCode
                Url = $url
            }
        } else {
            Write-Host "  ✗ FAIL: Port $port returned HTTP $($response.StatusCode)" -ForegroundColor Red
            $results[$port] = @{
                Status = "FAIL"
                StatusCode = $response.StatusCode
                Url = $url
            }
        }
    } catch {
        $errorMsg = $_.Exception.Message
        if ($_.Exception.Response) {
            $statusCode = $_.Exception.Response.StatusCode.value__
            Write-Host "  ✗ FAIL: Port $port returned HTTP $statusCode" -ForegroundColor Red
            $results[$port] = @{
                Status = "FAIL"
                StatusCode = $statusCode
                Url = $url
                Error = $errorMsg
            }
        } else {
            Write-Host "  ✗ FAIL: Port $port is not reachable ($errorMsg)" -ForegroundColor Red
            $results[$port] = @{
                Status = "FAIL"
                StatusCode = $null
                Url = $url
                Error = $errorMsg
            }
        }
    }
    Write-Host ""
}

# Also test 127.0.0.1 variants
Write-Host "Testing 127.0.0.1 variants..." -ForegroundColor Yellow
foreach ($port in $ports) {
    $url = "http://127.0.0.1:$port/api/health"
    Write-Host "Testing: $url" -ForegroundColor Yellow
    
    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "  ✓ PASS: 127.0.0.1:$port is responding (HTTP $($response.StatusCode))" -ForegroundColor Green
            if (-not $results[$port] -or $results[$port].Status -ne "OK") {
                $results[$port] = @{
                    Status = "OK"
                    StatusCode = $response.StatusCode
                    Url = $url
                }
            }
        }
    } catch {
        # Silently continue - we already tested localhost
    }
    Write-Host ""
}

# Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
$anyWorking = $false
foreach ($port in $ports) {
    if ($results[$port] -and $results[$port].Status -eq "OK") {
        Write-Host "Port $port: ✓ WORKING" -ForegroundColor Green
        Write-Host "  URL: $($results[$port].Url)" -ForegroundColor Gray
        $anyWorking = $true
    } else {
        Write-Host "Port $port: ✗ NOT WORKING" -ForegroundColor Red
        if ($results[$port]) {
            if ($results[$port].StatusCode) {
                Write-Host "  HTTP Status: $($results[$port].StatusCode)" -ForegroundColor Gray
            }
            if ($results[$port].Error) {
                Write-Host "  Error: $($results[$port].Error)" -ForegroundColor Gray
            }
        }
    }
}

Write-Host ""

if ($anyWorking) {
    $workingPorts = $ports | Where-Object { $results[$_] -and $results[$_].Status -eq "OK" }
    $workingPort = $workingPorts[0]
    Write-Host "✓ PASS: At least one backend is responding" -ForegroundColor Green
    Write-Host ""
    Write-Host "Frontend should use:" -ForegroundColor Yellow
    Write-Host "  NEXT_PUBLIC_API_BASE_URL=http://localhost:$workingPort" -ForegroundColor White
    Write-Host ""
    Write-Host "Or run frontend with:" -ForegroundColor Yellow
    Write-Host "  .\tools\run-frontend.ps1 -ApiBaseUrl http://localhost:$workingPort" -ForegroundColor White
    exit 0
} else {
    Write-Host "✗ FAIL: No backend is responding on ports 5000 or 5001" -ForegroundColor Red
    Write-Host ""
    Write-Host "To fix:" -ForegroundColor Yellow
    Write-Host "  1. Start the backend:" -ForegroundColor White
    Write-Host "     .\tools\run-backend.ps1" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Or check if backend is running on a different port" -ForegroundColor White
    Write-Host ""
    exit 1
}
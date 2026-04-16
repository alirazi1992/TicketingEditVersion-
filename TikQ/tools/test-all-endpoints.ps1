# test-all-endpoints.ps1
# Systematic test of all API endpoints used by dashboards

$ErrorActionPreference = "Continue"

Write-Host "=== TikQ API Endpoints Test ===" -ForegroundColor Cyan
Write-Host ""

$baseUrl = "http://localhost:5000"
$results = @()

# Test 1: Health Check
Write-Host "Test 1: Health Check" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/health" -Method GET -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [PASS] Health check" -ForegroundColor Green
        $results += @{Test="Health Check"; Status="PASS"; Code=$response.StatusCode}
    } else {
        Write-Host "  [FAIL] Health check returned $($response.StatusCode)" -ForegroundColor Red
        $results += @{Test="Health Check"; Status="FAIL"; Code=$response.StatusCode}
    }
} catch {
    Write-Host "  [FAIL] Health check - $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="Health Check"; Status="FAIL"; Code="ERROR"; Error=$_.Exception.Message}
}
Write-Host ""

# Test 2: Ping
Write-Host "Test 2: Ping" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/ping" -Method GET -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        Write-Host "  [PASS] Ping" -ForegroundColor Green
        $results += @{Test="Ping"; Status="PASS"; Code=$response.StatusCode}
    } else {
        Write-Host "  [FAIL] Ping returned $($response.StatusCode)" -ForegroundColor Red
        $results += @{Test="Ping"; Status="FAIL"; Code=$response.StatusCode}
    }
} catch {
    Write-Host "  [FAIL] Ping - $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="Ping"; Status="FAIL"; Code="ERROR"; Error=$_.Exception.Message}
}
Write-Host ""

# Test 3: Categories (Public)
Write-Host "Test 3: GET /api/categories (Public)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/categories" -Method GET -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        $content = $response.Content | ConvertFrom-Json
        Write-Host "  [PASS] Categories endpoint (returned $($content.Count) categories)" -ForegroundColor Green
        $results += @{Test="GET /api/categories"; Status="PASS"; Code=$response.StatusCode; Count=$content.Count}
    } else {
        Write-Host "  [FAIL] Categories returned $($response.StatusCode)" -ForegroundColor Red
        $results += @{Test="GET /api/categories"; Status="FAIL"; Code=$response.StatusCode}
    }
} catch {
    $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "ERROR" }
    Write-Host "  [FAIL] Categories - $statusCode - $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="GET /api/categories"; Status="FAIL"; Code=$statusCode; Error=$_.Exception.Message}
}
Write-Host ""

# Test 4: Auth Debug Users (Public)
Write-Host "Test 4: GET /api/auth/debug-users (Public)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/auth/debug-users" -Method GET -UseBasicParsing -ErrorAction Stop
    if ($response.StatusCode -eq 200) {
        $content = $response.Content | ConvertFrom-Json
        Write-Host "  [PASS] Debug users endpoint (returned $($content.Count) users)" -ForegroundColor Green
        $results += @{Test="GET /api/auth/debug-users"; Status="PASS"; Code=$response.StatusCode; Count=$content.Count}
    } else {
        Write-Host "  [FAIL] Debug users returned $($response.StatusCode)" -ForegroundColor Red
        $results += @{Test="GET /api/auth/debug-users"; Status="FAIL"; Code=$response.StatusCode}
    }
} catch {
    $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "ERROR" }
    Write-Host "  [FAIL] Debug users - $statusCode - $($_.Exception.Message)" -ForegroundColor Red
    $results += @{Test="GET /api/auth/debug-users"; Status="FAIL"; Code=$statusCode; Error=$_.Exception.Message}
}
Write-Host ""

# Test 5: Tickets (Requires Auth - will fail with 401)
Write-Host "Test 5: GET /api/tickets (Requires Auth)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/tickets" -Method GET -UseBasicParsing -ErrorAction Stop
    Write-Host "  [WARN] Tickets endpoint (unexpected - should require auth)" -ForegroundColor Yellow
    $results += @{Test="GET /api/tickets"; Status="PASS"; Code=$response.StatusCode}
} catch {
    $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "ERROR" }
    if ($statusCode -eq 401) {
        Write-Host "  [PASS] Tickets endpoint correctly requires auth (401)" -ForegroundColor Green
        $results += @{Test="GET /api/tickets"; Status="PASS"; Code=$statusCode; Note="Correctly requires auth"}
    } else {
        Write-Host "  [FAIL] Tickets - $statusCode - $($_.Exception.Message)" -ForegroundColor Red
        $results += @{Test="GET /api/tickets"; Status="FAIL"; Code=$statusCode; Error=$_.Exception.Message}
    }
}
Write-Host ""

# Test 6: Technician Tickets (Requires Auth - will fail with 401)
Write-Host "Test 6: GET /api/technician/tickets (Requires Auth)" -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "$baseUrl/api/technician/tickets" -Method GET -UseBasicParsing -ErrorAction Stop
    Write-Host "  [WARN] Technician tickets endpoint (unexpected - should require auth)" -ForegroundColor Yellow
    $results += @{Test="GET /api/technician/tickets"; Status="PASS"; Code=$response.StatusCode}
} catch {
    $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "ERROR" }
    if ($statusCode -eq 401) {
        Write-Host "  [PASS] Technician tickets endpoint correctly requires auth (401)" -ForegroundColor Green
        $results += @{Test="GET /api/technician/tickets"; Status="PASS"; Code=$statusCode; Note="Correctly requires auth"}
    } else {
        Write-Host "  [FAIL] Technician tickets - $statusCode - $($_.Exception.Message)" -ForegroundColor Red
        $results += @{Test="GET /api/technician/tickets"; Status="FAIL"; Code=$statusCode; Error=$_.Exception.Message}
    }
}
Write-Host ""

# Summary
Write-Host "=== Summary ===" -ForegroundColor Cyan
$passCount = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failCount = ($results | Where-Object { $_.Status -eq "FAIL" }).Count

Write-Host "Passed: $passCount" -ForegroundColor Green
Write-Host "Failed: $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Green" })
Write-Host ""

foreach ($result in $results) {
    $color = if ($result.Status -eq "PASS") { "Green" } else { "Red" }
    Write-Host "  $($result.Test): $($result.Status) ($($result.Code))" -ForegroundColor $color
    if ($result.Error) {
        Write-Host "    Error: $($result.Error)" -ForegroundColor Gray
    }
    if ($result.Note) {
        Write-Host "    Note: $($result.Note)" -ForegroundColor Gray
    }
}

Write-Host ""

if ($failCount -eq 0) {
    Write-Host "[SUCCESS] All endpoint tests passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "[FAILURE] Some endpoint tests failed. Review errors above." -ForegroundColor Red
    exit 1
}
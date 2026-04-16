#!/usr/bin/env pwsh
# Test script for Supervisor API endpoints

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$Token = ""
)

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Supervisor API Endpoint Tester" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

if ([string]::IsNullOrEmpty($Token)) {
    Write-Host "ERROR: Token is required" -ForegroundColor Red
    Write-Host "Usage: .\test-supervisor-endpoints.ps1 -Token 'your-jwt-token'" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To get a token:" -ForegroundColor Yellow
    Write-Host "1. Login to the frontend" -ForegroundColor Yellow
    Write-Host "2. Open browser console" -ForegroundColor Yellow
    Write-Host "3. Run: localStorage.getItem('ticketing.auth.token')" -ForegroundColor Yellow
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type" = "application/json"
}

function Test-Endpoint {
    param(
        [string]$Method,
        [string]$Path,
        [string]$Description,
        [object]$Body = $null
    )
    
    $url = "$BaseUrl$Path"
    Write-Host "Testing: $Description" -ForegroundColor Yellow
    Write-Host "  $Method $url" -ForegroundColor Gray
    
    try {
        $params = @{
            Uri = $url
            Method = $Method
            Headers = $headers
            ErrorAction = "Stop"
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        Write-Host "  ✓ SUCCESS" -ForegroundColor Green
        Write-Host "  Response: $($response | ConvertTo-Json -Depth 2 -Compress)" -ForegroundColor Gray
        Write-Host ""
        return $true
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $statusText = $_.Exception.Response.StatusDescription
        Write-Host "  ✗ FAILED: $statusCode $statusText" -ForegroundColor Red
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "  Response: $responseBody" -ForegroundColor Gray
        }
        catch {
            Write-Host "  (Could not read response body)" -ForegroundColor Gray
        }
        
        Write-Host ""
        return $false
    }
}

# Test health endpoint first
Write-Host "Checking backend health..." -ForegroundColor Cyan
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method GET -ErrorAction Stop
    Write-Host "✓ Backend is running" -ForegroundColor Green
    Write-Host ""
}
catch {
    Write-Host "✗ Backend is not responding at $BaseUrl" -ForegroundColor Red
    Write-Host "Please start the backend with: .\tools\run-backend.ps1" -ForegroundColor Yellow
    exit 1
}

# Test supervisor endpoints
$results = @()

$results += Test-Endpoint -Method "GET" -Path "/api/supervisor/technicians" `
    -Description "Get list of managed technicians"

$results += Test-Endpoint -Method "GET" -Path "/api/supervisor/technicians/available" `
    -Description "Get available technicians to link"

$results += Test-Endpoint -Method "GET" -Path "/api/supervisor/tickets/available-to-assign" `
    -Description "Get tickets available for assignment"

# Summary
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
$passed = ($results | Where-Object { $_ -eq $true }).Count
$total = $results.Count
$failed = $total - $passed

Write-Host "Passed: $passed / $total" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
if ($failed -gt 0) {
    Write-Host "Failed: $failed / $total" -ForegroundColor Red
}
Write-Host ""

if ($failed -eq 0) {
    Write-Host "✓ All tests passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "✗ Some tests failed" -ForegroundColor Red
    Write-Host ""
    Write-Host "Common issues:" -ForegroundColor Yellow
    Write-Host "- 401 Unauthorized: Token is invalid or expired" -ForegroundColor Yellow
    Write-Host "- 403 Forbidden: User is not a supervisor" -ForegroundColor Yellow
    Write-Host "- 404 Not Found: Endpoint doesn't exist (controller not loaded?)" -ForegroundColor Yellow
    Write-Host "- 500 Server Error: Check backend logs for details" -ForegroundColor Yellow
    exit 1
}

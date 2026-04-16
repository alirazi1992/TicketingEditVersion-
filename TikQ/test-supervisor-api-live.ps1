#!/usr/bin/env pwsh
# Live test of supervisor API to see actual error

Write-Host "Testing Supervisor API Endpoints" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Test health first
Write-Host "1. Testing Health Endpoint..." -ForegroundColor Yellow
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/api/health" -Method GET -ErrorAction Stop
    Write-Host "   ✓ Backend is running" -ForegroundColor Green
    Write-Host "   Response: $($health | ConvertTo-Json -Compress)" -ForegroundColor Gray
} catch {
    Write-Host "   ✗ Backend not responding" -ForegroundColor Red
    Write-Host "   Start backend with: .\tools\run-backend.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "2. Testing /api/supervisor/technicians WITHOUT auth..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/supervisor/technicians" -Method GET -ErrorAction Stop
    Write-Host "   ✓ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Body: $($response.Content.Substring(0, [Math]::Min(200, $response.Content.Length)))" -ForegroundColor Gray
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "   ✗ Status: $statusCode" -ForegroundColor Red
    
    try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Body: $responseBody" -ForegroundColor Gray
    } catch {
        Write-Host "   (Could not read response body)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "3. Testing /api/supervisor/technicians/available WITHOUT auth..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/supervisor/technicians/available" -Method GET -ErrorAction Stop
    Write-Host "   ✓ Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Body: $($response.Content.Substring(0, [Math]::Min(200, $response.Content.Length)))" -ForegroundColor Gray
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "   ✗ Status: $statusCode" -ForegroundColor Red
    
    try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Body: $responseBody" -ForegroundColor Gray
    } catch {
        Write-Host "   (Could not read response body)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "=================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "1. If 401: Need to login and get token" -ForegroundColor Yellow
Write-Host "2. If 404: Controller/route not found" -ForegroundColor Yellow
Write-Host "3. If 500: Server error - check backend logs" -ForegroundColor Yellow
Write-Host ""
Write-Host "To test WITH auth:" -ForegroundColor Yellow
Write-Host "1. Login to frontend (http://localhost:3000)" -ForegroundColor Yellow
Write-Host "2. Open browser console" -ForegroundColor Yellow
Write-Host "3. Run: localStorage.getItem('ticketing.auth.token')" -ForegroundColor Yellow
Write-Host "4. Copy token and run:" -ForegroundColor Yellow
Write-Host "   .\test-supervisor-api-live.ps1 -Token 'YOUR_TOKEN'" -ForegroundColor Yellow

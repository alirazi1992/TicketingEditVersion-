#!/usr/bin/env pwsh
# Quick fix for 404 - Restart backend to load SupervisorController

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Fix 404 - Restart Backend" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop backend
Write-Host "Step 1: Stopping backend..." -ForegroundColor Yellow
& ".\tools\stop-backend.ps1"
Start-Sleep -Seconds 2

# Step 2: Start backend
Write-Host ""
Write-Host "Step 2: Starting backend..." -ForegroundColor Yellow
Write-Host ""
Write-Host "Backend will start in a new window." -ForegroundColor Gray
Write-Host "Watch for: 'Now listening on: http://localhost:5000'" -ForegroundColor Gray
Write-Host ""

# Start in new window so we can continue testing
Start-Process powershell -ArgumentList "-NoExit", "-Command", "& '.\tools\run-backend.ps1'"

Write-Host "Waiting for backend to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Step 3: Test endpoints
Write-Host ""
Write-Host "Step 3: Testing endpoints..." -ForegroundColor Yellow
Write-Host ""

# Test health
Write-Host "Testing health endpoint..." -ForegroundColor Gray
try {
    $health = Invoke-RestMethod -Uri "http://localhost:5000/api/health" -Method GET -ErrorAction Stop -TimeoutSec 5
    Write-Host "  ✓ Health OK" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Health failed - backend may still be starting" -ForegroundColor Yellow
    Write-Host "  Wait a few more seconds and try again" -ForegroundColor Yellow
}

Write-Host ""

# Test supervisor endpoint
Write-Host "Testing /api/supervisor/technicians..." -ForegroundColor Gray
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/supervisor/technicians" -Method GET -ErrorAction Stop -TimeoutSec 5
    Write-Host "  Status: $($response.StatusCode)" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    
    if ($statusCode -eq 401) {
        Write-Host "  ✓ Status: 401 Unauthorized (GOOD - endpoint exists!)" -ForegroundColor Green
        Write-Host "  ✓ 404 is FIXED!" -ForegroundColor Green
        Write-Host ""
        Write-Host "  Next step: Make user a supervisor" -ForegroundColor Yellow
        Write-Host "  Run: UPDATE Technicians SET IsSupervisor = 1 WHERE UserId = ..." -ForegroundColor Yellow
    } elseif ($statusCode -eq 404) {
        Write-Host "  ✗ Status: 404 Not Found (endpoint still not found)" -ForegroundColor Red
        Write-Host "  Backend may need more time to start, or there's another issue" -ForegroundColor Yellow
    } else {
        Write-Host "  Status: $statusCode" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Next Steps:" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. If you see 401 Unauthorized - SUCCESS!" -ForegroundColor Green
Write-Host "   The 404 is fixed. Now make user a supervisor:" -ForegroundColor Green
Write-Host ""
Write-Host "   UPDATE Technicians SET IsSupervisor = 1" -ForegroundColor White
Write-Host "   WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your@email.com')" -ForegroundColor White
Write-Host ""
Write-Host "2. If you still see 404:" -ForegroundColor Yellow
Write-Host "   - Wait a few more seconds for backend to fully start" -ForegroundColor Yellow
Write-Host "   - Check backend window for errors" -ForegroundColor Yellow
Write-Host "   - Run: .\test-supervisor-endpoints.ps1" -ForegroundColor Yellow
Write-Host ""
Write-Host "3. Test in browser:" -ForegroundColor Cyan
Write-Host "   - Open http://localhost:3000" -ForegroundColor Cyan
Write-Host "   - Navigate to supervisor page" -ForegroundColor Cyan
Write-Host "   - Check console for 401 (not 404)" -ForegroundColor Cyan

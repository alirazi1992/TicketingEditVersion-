#!/usr/bin/env pwsh
# Test Supervisor API Endpoints

param(
    [string]$Token = ""
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Supervisor API Endpoint Tester" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if backend is running
Write-Host "1. Testing Backend Health..." -ForegroundColor Yellow
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

# Test 2: Test without auth (should get 401)
Write-Host "2. Testing /api/supervisor/technicians WITHOUT auth..." -ForegroundColor Yellow
try {
    $response = Invoke-WebRequest -Uri "http://localhost:5000/api/supervisor/technicians" -Method GET -ErrorAction Stop
    Write-Host "   Status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "   Body: $($response.Content)" -ForegroundColor Gray
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    Write-Host "   Status: $statusCode" -ForegroundColor $(if ($statusCode -eq 401) { "Yellow" } else { "Red" })
    
    try {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "   Body: $responseBody" -ForegroundColor Gray
        
        if ($statusCode -eq 401) {
            Write-Host "   ✓ Expected 401 (endpoint requires auth)" -ForegroundColor Green
        }
    } catch {
        Write-Host "   (Could not read response body)" -ForegroundColor Gray
    }
}

Write-Host ""

# Test 3: Test with auth if token provided
if ($Token) {
    Write-Host "3. Testing /api/supervisor/technicians WITH auth..." -ForegroundColor Yellow
    
    $headers = @{
        "Authorization" = "Bearer $Token"
    }
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/supervisor/technicians" -Method GET -Headers $headers -ErrorAction Stop
        Write-Host "   ✓ Status: 200 OK" -ForegroundColor Green
        Write-Host "   Response: $($response | ConvertTo-Json -Compress)" -ForegroundColor Gray
        
        if ($response -is [array]) {
            Write-Host "   ✓ Returned array with $($response.Count) items" -ForegroundColor Green
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "   ✗ Status: $statusCode" -ForegroundColor Red
        
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $responseBody = $reader.ReadToEnd()
            Write-Host "   Body: $responseBody" -ForegroundColor Gray
            
            if ($statusCode -eq 401 -or $statusCode -eq 403) {
                Write-Host ""
                Write-Host "   User is not a supervisor. Run this SQL:" -ForegroundColor Yellow
                Write-Host "   UPDATE Technicians SET IsSupervisor = 1" -ForegroundColor Yellow
                Write-Host "   WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')" -ForegroundColor Yellow
            }
        } catch {
            Write-Host "   (Could not read response body)" -ForegroundColor Gray
        }
    }
    
    Write-Host ""
    
    # Test 4: Test available endpoint
    Write-Host "4. Testing /api/supervisor/technicians/available WITH auth..." -ForegroundColor Yellow
    
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:5000/api/supervisor/technicians/available" -Method GET -Headers $headers -ErrorAction Stop
        Write-Host "   ✓ Status: 200 OK" -ForegroundColor Green
        Write-Host "   Response: $($response | ConvertTo-Json -Compress)" -ForegroundColor Gray
        
        if ($response -is [array]) {
            Write-Host "   ✓ Returned array with $($response.Count) items" -ForegroundColor Green
        }
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
} else {
    Write-Host "3. Skipping auth tests (no token provided)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "To test with auth:" -ForegroundColor Yellow
    Write-Host "1. Login to frontend (http://localhost:3000)" -ForegroundColor Yellow
    Write-Host "2. Open browser console (F12)" -ForegroundColor Yellow
    Write-Host "3. Run: localStorage.getItem('ticketing.auth.token')" -ForegroundColor Yellow
    Write-Host "4. Copy token and run:" -ForegroundColor Yellow
    Write-Host "   .\test-supervisor-endpoints.ps1 -Token 'YOUR_TOKEN'" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backend Status: " -NoNewline
if ((Test-NetConnection -ComputerName localhost -Port 5000 -WarningAction SilentlyContinue).TcpTestSucceeded) {
    Write-Host "✓ Running" -ForegroundColor Green
} else {
    Write-Host "✗ Not Running" -ForegroundColor Red
}

Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Ensure backend is running" -ForegroundColor Yellow
Write-Host "2. Get auth token from frontend" -ForegroundColor Yellow
Write-Host "3. Run this script with -Token parameter" -ForegroundColor Yellow
Write-Host "4. If 401/403, make user a supervisor in database" -ForegroundColor Yellow
Write-Host "5. If 200 OK, check frontend Network tab" -ForegroundColor Yellow

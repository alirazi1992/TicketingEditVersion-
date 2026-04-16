# Comprehensive Smoke Test Runner
# Runs both backend and frontend smoke tests
# Exit codes: 0 = success, 1 = failure

param(
    [string]$BackendUrl = "http://localhost:5000",
    [string]$FrontendUrl = "http://localhost:3000",
    [bool]$StartBackend = $false,
    [bool]$StartFrontend = $false
)

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $PSScriptRoot
$backendStarted = $false
$frontendStarted = $false
$backendProcess = $null
$frontendProcess = $null

function Test-ServerRunning {
    param([string]$Url, [string]$Name)
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec 3 -ErrorAction Stop
        return $true
    } catch {
        return $false
    }
}

function Start-Backend {
    Write-Host "Starting backend server..." -ForegroundColor Cyan
    $backendPath = Join-Path $scriptRoot "backend\Ticketing.Backend"
    
    if (-not (Test-Path $backendPath)) {
        Write-Host "ERROR: Backend path not found: $backendPath" -ForegroundColor Red
        exit 1
    }
    
    Push-Location $backendPath
    try {
        $backendProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", ".\src\Ticketing.Api\Ticketing.Api.csproj" -PassThru -NoNewWindow
        Write-Host "Backend process started (PID: $($backendProcess.Id))" -ForegroundColor Green
        
        # Wait for backend to start
        Write-Host "Waiting for backend to start..." -ForegroundColor Cyan
        $maxWait = 30
        $waited = 0
        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds 2
            $waited += 2
            if (Test-ServerRunning -Url "$BackendUrl/swagger/index.html" -Name "Backend") {
                Write-Host "Backend is ready!" -ForegroundColor Green
                return $backendProcess
            }
            Write-Host "." -NoNewline -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "ERROR: Backend failed to start within $maxWait seconds" -ForegroundColor Red
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
        exit 1
    } finally {
        Pop-Location
    }
}

function Stop-Backend {
    param([System.Diagnostics.Process]$Process)
    
    if ($Process -and -not $Process.HasExited) {
        Write-Host "Stopping backend server..." -ForegroundColor Cyan
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "Backend stopped" -ForegroundColor Green
    }
}

function Start-Frontend {
    Write-Host "Starting frontend server..." -ForegroundColor Cyan
    $frontendPath = Join-Path $scriptRoot "frontend"
    
    if (-not (Test-Path $frontendPath)) {
        Write-Host "ERROR: Frontend path not found: $frontendPath" -ForegroundColor Red
        exit 1
    }
    
    Push-Location $frontendPath
    try {
        $frontendProcess = Start-Process -FilePath "npm" -ArgumentList "run", "dev" -PassThru -NoNewWindow
        Write-Host "Frontend process started (PID: $($frontendProcess.Id))" -ForegroundColor Green
        
        # Wait for frontend to start
        Write-Host "Waiting for frontend to start..." -ForegroundColor Cyan
        $maxWait = 60
        $waited = 0
        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds 2
            $waited += 2
            try {
                $response = Invoke-WebRequest -Uri $FrontendUrl -Method GET -TimeoutSec 2 -ErrorAction Stop
                Write-Host ""
                Write-Host "Frontend is ready!" -ForegroundColor Green
                return $frontendProcess
            } catch {
                Write-Host "." -NoNewline -ForegroundColor Gray
            }
        }
        Write-Host ""
        Write-Host "ERROR: Frontend failed to start within $maxWait seconds" -ForegroundColor Red
        Stop-Process -Id $frontendProcess.Id -Force -ErrorAction SilentlyContinue
        exit 1
    } finally {
        Pop-Location
    }
}

function Stop-Frontend {
    param([System.Diagnostics.Process]$Process)
    
    if ($Process -and -not $Process.HasExited) {
        Write-Host "Stopping frontend server..." -ForegroundColor Cyan
        Stop-Process -Id $Process.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "Frontend stopped" -ForegroundColor Green
    }
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "COMPREHENSIVE SMOKE TEST RUNNER" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

$exitCode = 0

try {
    # Check if servers are running
    $backendRunning = Test-ServerRunning -Url "$BackendUrl/swagger/index.html" -Name "Backend"
    $frontendRunning = Test-ServerRunning -Url $FrontendUrl -Name "Frontend"
    
    # Start backend if needed
    if (-not $backendRunning) {
        if ($StartBackend) {
            $backendProcess = Start-Backend
            $backendStarted = $true
        } else {
            Write-Host "ERROR: Backend is not running on $BackendUrl" -ForegroundColor Red
            Write-Host "Start it manually or use -StartBackend flag" -ForegroundColor Yellow
            exit 1
        }
    } else {
        Write-Host "✅ Backend is already running" -ForegroundColor Green
    }
    
    # Frontend is optional (Playwright auto-starts it)
    if (-not $frontendRunning -and $StartFrontend) {
        $frontendProcess = Start-Frontend
        $frontendStarted = $true
    } elseif ($frontendRunning) {
        Write-Host "✅ Frontend is already running" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "Running Backend Smoke Tests" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    
    # Run backend tests
    $backendTestScript = Join-Path $PSScriptRoot "run-smoke-tests.ps1"
    & $backendTestScript -BaseUrl $BackendUrl -StopOnFail $true
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "❌ Backend smoke tests failed" -ForegroundColor Red
        $exitCode = 1
    } else {
        Write-Host ""
        Write-Host "✅ Backend smoke tests passed" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "Running Frontend Smoke Tests" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    
    # Run frontend tests
    $frontendPath = Join-Path $scriptRoot "frontend"
    Push-Location $frontendPath
    try {
        # Set environment variable for API URL
        $env:NEXT_PUBLIC_API_BASE_URL = $BackendUrl
        
        & npx playwright test e2e/smoke.spec.ts --reporter=list
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "❌ Frontend smoke tests failed" -ForegroundColor Red
            $exitCode = 1
        } else {
            Write-Host ""
            Write-Host "✅ Frontend smoke tests passed" -ForegroundColor Green
        }
        
        # Append frontend results to report
        $reportPath = Join-Path $scriptRoot "RUNTIME_SMOKE_REPORT.md"
        $junitPath = Join-Path $frontendPath "test-results\junit.xml"
        
        $frontendReport = ""
        
        if (Test-Path $junitPath) {
            try {
                $junitContent = Get-Content $junitPath -Raw -ErrorAction Stop
                $testsRunMatch = [regex]::Match($junitContent, 'tests="(\d+)"')
                $failuresMatch = [regex]::Match($junitContent, 'failures="(\d+)"')
                $testsRun = if ($testsRunMatch.Success) { $testsRunMatch.Groups[1].Value } else { "N/A" }
                $failures = if ($failuresMatch.Success) { $failuresMatch.Groups[1].Value } else { "N/A" }
                
                $frontendReport = @"

## Frontend Smoke Test Results

**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Frontend URL**: $FrontendUrl

### Summary

- **Tests Run**: $testsRun
- **Failures**: $failures
- **Status**: $(if ($failures -eq "0" -or $failures -eq "N/A") { "✅ PASS" } else { "❌ FAIL" })

### Reports

- JUnit XML: ``frontend/test-results/junit.xml``
- HTML Report: ``frontend/playwright-report/index.html``

---
"@
            } catch {
                $frontendReport = @"

## Frontend Smoke Test Results

**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Frontend URL**: $FrontendUrl

### Summary

- **Status**: $(if ($LASTEXITCODE -eq 0) { "✅ PASS" } else { "❌ FAIL" })
- **Note**: Could not parse JUnit XML for detailed results

### Reports

- JUnit XML: ``frontend/test-results/junit.xml``
- HTML Report: ``frontend/playwright-report/index.html``

---
"@
            }
        } else {
            # No JUnit file, but we know the exit code
            $frontendReport = @"

## Frontend Smoke Test Results

**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Frontend URL**: $FrontendUrl

### Summary

- **Status**: $(if ($LASTEXITCODE -eq 0) { "✅ PASS" } else { "❌ FAIL" })
- **Note**: JUnit XML report not found. Check Playwright output for details.

### Reports

- HTML Report: ``frontend/playwright-report/index.html``

---
"@
        }
        
        if ($frontendReport) {
            Add-Content -Path $reportPath -Value $frontendReport -Encoding UTF8
        }
        
    } finally {
        Pop-Location
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "SUMMARY" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    
    if ($exitCode -eq 0) {
        Write-Host "✅ ALL TESTS PASSED" -ForegroundColor Green
    } else {
        Write-Host "❌ SOME TESTS FAILED" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Full report available at: RUNTIME_SMOKE_REPORT.md" -ForegroundColor Cyan
    Write-Host ""
    
} finally {
    # Cleanup: Stop servers we started
    if ($backendStarted -and $backendProcess) {
        Stop-Backend -Process $backendProcess
    }
    
    if ($frontendStarted -and $frontendProcess) {
        Stop-Frontend -Process $frontendProcess
    }
}

exit $exitCode


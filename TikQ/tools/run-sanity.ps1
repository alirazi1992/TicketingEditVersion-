# TikQ Sanity Check Script
# Runs comprehensive build and basic smoke tests for backend and frontend

param(
    [string]$BackendUrl = "http://localhost:5000",
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$script:BackendProcess = $null

function Write-Header {
    param([string]$Message)
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "✓ $Message" -ForegroundColor Green
}

function Write-Error {
    param([string]$Message)
    Write-Host "✗ $Message" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Message)
    Write-Host "⚠ $Message" -ForegroundColor Yellow
}

function Test-ServerRunning {
    param([string]$Url, [int]$TimeoutSeconds = 5)
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Method GET -TimeoutSec $TimeoutSeconds -UseBasicParsing -ErrorAction SilentlyContinue
        return $response.StatusCode -eq 200
    } catch {
        return $false
    }
}

function Start-Backend {
    Write-Header "Starting Backend Server"
    
    $backendPath = Join-Path $PSScriptRoot "..\backend\Ticketing.Backend"
    if (-not (Test-Path $backendPath)) {
        Write-Error "Backend path not found: $backendPath"
        exit 1
    }
    
    Push-Location $backendPath
    try {
        if ($Clean) {
            Write-Host "Cleaning backend build artifacts..." -ForegroundColor Yellow
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue bin, obj
        }
        
        Write-Host "Building backend..." -ForegroundColor Yellow
        $buildOutput = dotnet build Ticketing.Backend.csproj 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Backend build failed"
            $buildOutput | Write-Host
            exit 1
        }
        Write-Success "Backend build succeeded"
        
        Write-Host "Starting backend server..." -ForegroundColor Yellow
        $script:BackendProcess = Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "Ticketing.Backend.csproj" -PassThru -NoNewWindow -RedirectStandardOutput "backend-output.log" -RedirectStandardError "backend-error.log"
        
        # Wait for backend to start
        Write-Host "Waiting for backend to start (max 30 seconds)..." -ForegroundColor Yellow
        $maxWait = 30
        $waited = 0
        while ($waited -lt $maxWait) {
            Start-Sleep -Seconds 2
            $waited += 2
            if (Test-ServerRunning -Url "$BackendUrl/swagger/index.html") {
                Write-Success "Backend is ready at $BackendUrl"
                return $true
            }
            Write-Host "." -NoNewline -ForegroundColor Gray
        }
        Write-Host ""
        Write-Error "Backend failed to start within $maxWait seconds"
        Write-Host "Check backend-error.log for details" -ForegroundColor Yellow
        return $false
    } finally {
        Pop-Location
    }
}

function Stop-Backend {
    if ($script:BackendProcess -and -not $script:BackendProcess.HasExited) {
        Write-Host "`nStopping backend server..." -ForegroundColor Yellow
        Stop-Process -Id $script:BackendProcess.Id -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Success "Backend stopped"
    }
}

function Test-BackendEndpoints {
    Write-Header "Testing Backend Endpoints"
    
    # Note: These tests require an admin token
    # For now, we'll just check that Swagger loads
    Write-Host "Testing Swagger UI..." -ForegroundColor Yellow
    if (Test-ServerRunning -Url "$BackendUrl/swagger/index.html") {
        Write-Success "Swagger UI is accessible"
    } else {
        Write-Error "Swagger UI is not accessible"
        return $false
    }
    
    Write-Warning "Full endpoint tests require admin authentication token"
    Write-Host "To test field definitions endpoints manually:" -ForegroundColor Yellow
    Write-Host "  1. Login at $BackendUrl/api/auth/login" -ForegroundColor Gray
    Write-Host "  2. Use token in Authorization header" -ForegroundColor Gray
    Write-Host "  3. GET  $BackendUrl/api/admin/subcategories/1/fields" -ForegroundColor Gray
    Write-Host "  4. POST $BackendUrl/api/admin/subcategories/1/fields" -ForegroundColor Gray
    
    return $true
}

function Test-Frontend {
    Write-Header "Testing Frontend Build"
    
    $frontendPath = Join-Path $PSScriptRoot "..\frontend"
    if (-not (Test-Path $frontendPath)) {
        Write-Error "Frontend path not found: $frontendPath"
        return $false
    }
    
    Push-Location $frontendPath
    try {
        if ($Clean) {
            Write-Host "Cleaning frontend build artifacts..." -ForegroundColor Yellow
            Remove-Item -Recurse -Force -ErrorAction SilentlyContinue .next
        }
        
        Write-Host "Building frontend..." -ForegroundColor Yellow
        $buildOutput = npm run build 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Frontend build failed"
            $buildOutput | Select-Object -Last 50 | Write-Host
            return $false
        }
        Write-Success "Frontend build succeeded"
        
        # Check for common errors in output
        if ($buildOutput -match "error|Error|ERROR") {
            Write-Warning "Build completed but warnings/errors detected"
            $buildOutput | Select-String -Pattern "error|Error|ERROR" | Select-Object -First 10 | Write-Host
        }
        
        return $true
    } finally {
        Pop-Location
    }
}

# Main execution
Write-Header "TikQ Sanity Check"
Write-Host "Backend URL: $BackendUrl" -ForegroundColor Gray
Write-Host "Clean build: $Clean" -ForegroundColor Gray
Write-Host ""

$allPassed = $true

try {
    # Backend tests
    if (-not $SkipBackend) {
        if (Start-Backend) {
            if (-not (Test-BackendEndpoints)) {
                $allPassed = $false
            }
        } else {
            $allPassed = $false
        }
    } else {
        Write-Warning "Skipping backend tests"
    }
    
    # Frontend tests
    if (-not $SkipFrontend) {
        if (-not (Test-Frontend)) {
            $allPassed = $false
        }
    } else {
        Write-Warning "Skipping frontend tests"
    }
    
    # Summary
    Write-Header "Sanity Check Summary"
    if ($allPassed) {
        Write-Success "All checks passed!"
        exit 0
    } else {
        Write-Error "Some checks failed. Review output above."
        exit 1
    }
} finally {
    Stop-Backend
}







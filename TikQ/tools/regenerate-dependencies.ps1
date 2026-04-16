# regenerate-dependencies.ps1
# Regenerates all project dependencies

$ErrorActionPreference = "Stop"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$frontendPath = Join-Path $projectRoot "frontend"
$backendPath = Join-Path $projectRoot "backend\Ticketing.Backend"

Write-Host "🔄 Regenerating dependencies..." -ForegroundColor Cyan
Write-Host "="*60 -ForegroundColor Cyan

$results = @{
    Frontend = $null
    Backend = $null
}

# Frontend: pnpm install
Write-Host "`n📦 Installing frontend dependencies (pnpm)..." -ForegroundColor Yellow
Write-Host "Path: $frontendPath" -ForegroundColor Gray

if (-not (Test-Path $frontendPath)) {
    Write-Host "❌ Frontend path does not exist!" -ForegroundColor Red
    exit 1
}

Push-Location $frontendPath
try {
    # Check if pnpm is available
    $pnpmCheck = Get-Command pnpm -ErrorAction SilentlyContinue
    if ($null -eq $pnpmCheck) {
        Write-Host "⚠️  pnpm not found, trying npm..." -ForegroundColor Yellow
        npm install
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Frontend dependencies installed (npm)" -ForegroundColor Green
            $results.Frontend = "npm"
        } else {
            Write-Host "❌ Frontend dependency installation failed" -ForegroundColor Red
            $results.Frontend = "failed"
        }
    } else {
        $pnpmVersion = pnpm --version 2>&1
        Write-Host "Using pnpm version: $pnpmVersion" -ForegroundColor Gray
        pnpm install
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Frontend dependencies installed (pnpm)" -ForegroundColor Green
            $results.Frontend = "pnpm"
        } else {
            Write-Host "❌ Frontend dependency installation failed" -ForegroundColor Red
            $results.Frontend = "failed"
        }
    }
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    $results.Frontend = "error"
} finally {
    Pop-Location
}

# Backend: dotnet restore
Write-Host "`n📦 Restoring backend dependencies (dotnet)..." -ForegroundColor Yellow
Write-Host "Path: $backendPath" -ForegroundColor Gray

if (-not (Test-Path $backendPath)) {
    Write-Host "❌ Backend path does not exist!" -ForegroundColor Red
    exit 1
}

Push-Location $backendPath
try {
    # Check if dotnet is available
    $dotnetCheck = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -eq $dotnetCheck) {
        Write-Host "❌ dotnet CLI not found!" -ForegroundColor Red
        $results.Backend = "not_found"
    } else {
        $dotnetVersion = dotnet --version 2>&1
        Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Gray
        dotnet restore
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✅ Backend dependencies restored" -ForegroundColor Green
            $results.Backend = "success"
        } else {
            Write-Host "❌ Backend dependency restoration failed" -ForegroundColor Red
            $results.Backend = "failed"
        }
    }
} catch {
    Write-Host "❌ Error: $_" -ForegroundColor Red
    $results.Backend = "error"
} finally {
    Pop-Location
}

Write-Host "`n" + "="*60 -ForegroundColor Cyan
Write-Host "✅ Dependency regeneration complete!" -ForegroundColor Green
Write-Host "="*60 -ForegroundColor Cyan

return $results


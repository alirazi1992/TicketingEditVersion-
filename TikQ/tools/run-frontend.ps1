# run-frontend.ps1
# Starts the Next.js frontend development server
# Ensures consistent startup from repo root

param(
    [int]$Port = 3000,
    [string]$ApiBaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

# Get script directory and repo root (path-robust)
$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$repoRoot = Split-Path -Parent $scriptDir
$frontendPath = Join-Path $repoRoot "frontend"

Write-Host "=== TikQ Frontend Runner ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Verify frontend directory exists
if (-not (Test-Path $frontendPath)) {
    Write-Host "ERROR: Frontend path not found: $frontendPath" -ForegroundColor Red
    exit 1
}

# Step 2: Check if Node.js is available
try {
    $nodeVersion = node --version 2>&1
    Write-Host "Using Node.js: $nodeVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: Node.js not found. Please install Node.js 18+." -ForegroundColor Red
    exit 1
}

# Step 3: Check if dependencies are installed
$nodeModulesPath = Join-Path $frontendPath "node_modules"
if (-not (Test-Path $nodeModulesPath)) {
    Write-Host "Dependencies not found. Installing..." -ForegroundColor Yellow
    Push-Location $frontendPath
    try {
        npm install
        Write-Host "Dependencies installed successfully" -ForegroundColor Green
    } catch {
        Write-Host "ERROR: Failed to install dependencies: $_" -ForegroundColor Red
        Pop-Location
        exit 1
    } finally {
        Pop-Location
    }
}

# Step 4: Set environment variables
$env:PORT = $Port.ToString()

# Try to read backend URL from shared file (created by run-backend.ps1)
$devDir = Join-Path $repoRoot ".dev"
$backendUrlFile = Join-Path $devDir "backend-url.txt"
$detectedBackendUrl = $null

if (Test-Path $backendUrlFile) {
    try {
        $detectedBackendUrl = (Get-Content $backendUrlFile -Raw).Trim()
        if ($detectedBackendUrl) {
            Write-Host "Detected backend URL from .dev/backend-url.txt: $detectedBackendUrl" -ForegroundColor Green
        }
    } catch {
        Write-Host "Could not read backend URL file: $_" -ForegroundColor Yellow
    }
}

# Priority: explicit parameter > detected from file > environment variable > default
if ($ApiBaseUrl) {
    $env:NEXT_PUBLIC_API_BASE_URL = $ApiBaseUrl
    Write-Host "API Base URL (from parameter): $ApiBaseUrl" -ForegroundColor Gray
} elseif ($detectedBackendUrl) {
    $env:NEXT_PUBLIC_API_BASE_URL = $detectedBackendUrl
    Write-Host "API Base URL (auto-detected): $detectedBackendUrl" -ForegroundColor Gray
} elseif ($env:NEXT_PUBLIC_API_BASE_URL) {
    Write-Host "API Base URL (from environment): $($env:NEXT_PUBLIC_API_BASE_URL)" -ForegroundColor Gray
} else {
    Write-Host "API Base URL: (auto-detect: will try http://localhost:5000, then http://localhost:5001, etc.)" -ForegroundColor Yellow
}

# Step 5: Navigate to frontend and start
Push-Location $frontendPath

try {
    Write-Host ""
    Write-Host "Starting frontend development server..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Frontend directory: $frontendPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Frontend URLs ===" -ForegroundColor Cyan
    Write-Host "Frontend URL: http://localhost:$Port" -ForegroundColor White
    if ($env:NEXT_PUBLIC_API_BASE_URL) {
        Write-Host "API Base URL: $($env:NEXT_PUBLIC_API_BASE_URL)" -ForegroundColor White
    } else {
        Write-Host "API Base URL: (auto-detect: will try http://localhost:5000, then http://localhost:5001)" -ForegroundColor Yellow
    }
    Write-Host "=====================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
    Write-Host ""
    
    # Start Next.js dev server
    npm run dev
    
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to start frontend: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Ensure Node.js 18+ is installed" -ForegroundColor White
    Write-Host "  2. Run 'npm install' in the frontend directory" -ForegroundColor White
    Write-Host "  3. Check that port $Port is not in use" -ForegroundColor White
    if ($_.Exception.Message -match "EPERM|spawn") {
        Write-Host "  4. EPERM/spawn: Run this script from a normal PowerShell/CMD window (not inside sandbox)." -ForegroundColor White
        Write-Host "     Or: add an exception for Node in Windows Defender/antivirus; close other Next.js instances." -ForegroundColor White
    }
    exit 1
} finally {
    Pop-Location
}
































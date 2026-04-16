# run-backend.ps1
# Safely stops any running backend and starts a fresh instance
# Prevents MSB3027/MSB3021 file-lock errors on Windows

param(
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"

# Get script directory and project root (path-robust)
$scriptDir = $PSScriptRoot
if (-not $scriptDir) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
}
$repoRoot = Split-Path -Parent $scriptDir
$backendPath = Join-Path $repoRoot "backend\Ticketing.Backend"

Write-Host "=== TikQ Backend Runner ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Stop any running backend processes
Write-Host "Step 1: Stopping any running backend processes..." -ForegroundColor Yellow
Write-Host ""
& "$scriptDir\stop-backend.ps1"
Write-Host ""

# Step 2: Wait a moment for processes to fully stop
Start-Sleep -Milliseconds 500

# Step 3: Check if port is available, try to free it if it's our backend, or use fallback
$selectedPort = $Port
$portAvailable = $false

Write-Host "Step 2: Checking port availability..." -ForegroundColor Yellow

function Test-IsOurBackendProcess {
    param([int]$ProcessId)
    try {
        $proc = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if (-not $proc) { return $false }
        
        $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
        $cmdLine = if ($procInfo) { $procInfo.CommandLine } else { "" }
        $procPath = if ($proc.Path) { $proc.Path } elseif ($procInfo -and $procInfo.ExecutablePath) { $procInfo.ExecutablePath } else { "" }
        
        # Check if it's our backend
        if ($proc.ProcessName -eq "Ticketing.Backend" -or $proc.ProcessName -eq "Ticketing.Api" -or $proc.ProcessName -eq "dotnet") {
            if ($cmdLine -match "Ticketing\.(Backend|Api)" -or ($procPath -and $procPath -match "Ticketing\.(Backend|Api)")) {
                if ($procPath -and $procPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                    return $true
                }
            }
        }
        return $false
    } catch {
        return $false
    }
}

try {
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        $portAvailable = $true
        Write-Host "  Port $Port is available" -ForegroundColor Green
    } else {
        Write-Host "  Port $Port is in use" -ForegroundColor Yellow
        $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
        $isOurBackend = $false
        
        foreach ($pid in $pids) {
            if (Test-IsOurBackendProcess -ProcessId $pid) {
                $isOurBackend = $true
                Write-Host "  Detected our backend process (PID: $pid), attempting to stop it..." -ForegroundColor Yellow
                try {
                    Stop-Process -Id $pid -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 500
                    Write-Host "  Process stopped successfully" -ForegroundColor Green
                    $portAvailable = $true
                    break
                } catch {
                    Write-Host "  Could not stop process: $_" -ForegroundColor Red
                }
            }
        }
        
        if (-not $portAvailable) {
            if ($Port -eq 5000) {
                Write-Host "  Port $Port is in use by another process" -ForegroundColor Yellow
                Write-Host "  Trying fallback port 5001..." -ForegroundColor Yellow
                $connections5001 = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue
                if (-not $connections5001) {
                    $selectedPort = 5001
                    $portAvailable = $true
                    Write-Host "  Port 5001 is available (fallback)" -ForegroundColor Green
                    Write-Host "  WARNING: Frontend may need NEXT_PUBLIC_API_BASE_URL=http://localhost:5001" -ForegroundColor Yellow
                } else {
                    Write-Host "  ERROR: Both ports 5000 and 5001 are in use" -ForegroundColor Red
                    Write-Host "  Please stop the processes using these ports manually:" -ForegroundColor Red
                    Write-Host "    netstat -ano | findstr :5000" -ForegroundColor White
                    Write-Host "    taskkill /PID <pid> /F" -ForegroundColor White
                    exit 1
                }
            } else {
                Write-Host "  ERROR: Port $Port is in use" -ForegroundColor Red
                exit 1
            }
        }
    }
} catch {
    Write-Host "  Could not check port availability, proceeding anyway..." -ForegroundColor Yellow
    $portAvailable = $true
}

if (-not $portAvailable) {
    Write-Host ""
    Write-Host "ERROR: Cannot start backend - port conflict" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Step 4: Verify backend directory exists
if (-not (Test-Path $backendPath)) {
    Write-Host "ERROR: Backend path not found: $backendPath" -ForegroundColor Red
    exit 1
}

# Step 5: Check if .NET SDK is available
try {
    $dotnetVersion = dotnet --version 2>&1
    Write-Host "Using .NET SDK: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "ERROR: .NET SDK not found. Please install .NET 8 SDK." -ForegroundColor Red
    exit 1
}

# Step 6: Determine which project to run
# Note: Ticketing.Api.csproj is a library (controllers only), not runnable
# The actual runnable project is Ticketing.Backend.csproj in the root
$projectFile = Join-Path $backendPath "Ticketing.Backend.csproj"
if (-not (Test-Path $projectFile)) {
    Write-Host "ERROR: Project file not found: $projectFile" -ForegroundColor Red
    exit 1
}

# Navigate to backend directory
Push-Location $backendPath

try {
    Write-Host "Step 3: Starting backend server..." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Backend directory: $backendPath" -ForegroundColor Gray
    Write-Host "Project: Ticketing.Backend.csproj" -ForegroundColor Gray
    Write-Host ""
    Write-Host "=== Backend URLs ===" -ForegroundColor Cyan
    $apiBaseUrl = "http://localhost:$selectedPort"
    Write-Host "API Base URL: $apiBaseUrl" -ForegroundColor White
    if ($selectedPort -ne $Port) {
        Write-Host "  (Note: Port $Port was in use, using $selectedPort instead)" -ForegroundColor Yellow
        Write-Host "  Frontend should use: NEXT_PUBLIC_API_BASE_URL=$apiBaseUrl" -ForegroundColor Yellow
    }
    Write-Host "Swagger UI: $apiBaseUrl/swagger" -ForegroundColor White
    Write-Host "Health Check: $apiBaseUrl/api/health" -ForegroundColor White
    Write-Host "===================" -ForegroundColor Cyan
    Write-Host ""
    
    # Write backend URL to shared file for frontend script
    $devDir = Join-Path $repoRoot ".dev"
    if (-not (Test-Path $devDir)) {
        New-Item -ItemType Directory -Path $devDir -Force | Out-Null
    }
    $backendUrlFile = Join-Path $devDir "backend-url.txt"
    $apiBaseUrl | Out-File -FilePath $backendUrlFile -Encoding UTF8 -NoNewline
    Write-Host "Backend URL written to: $backendUrlFile" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
    Write-Host ""
    
    # Set environment variable for URL
    $env:ASPNETCORE_URLS = "http://127.0.0.1:$selectedPort"
    
    # Run the backend
    dotnet run --project $projectFile
    
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to start backend: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Troubleshooting:" -ForegroundColor Yellow
    Write-Host "  1. Ensure .NET 8 SDK is installed" -ForegroundColor White
    Write-Host "  2. Run 'dotnet restore' in the backend directory" -ForegroundColor White
    Write-Host "  3. Run '.\tools\stop-backend.ps1' to stop any stale processes" -ForegroundColor White
    Write-Host "  4. Check backend logs above for specific errors" -ForegroundColor White
    exit 1
} finally {
    Pop-Location
}
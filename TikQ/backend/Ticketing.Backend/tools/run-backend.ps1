# run-backend.ps1
# Safely stops stale backend processes on port 5000 and starts the API project
# Only stops processes that are confirmed to be THIS backend project

param(
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"

# Get the repo root path (this script is in backend/Ticketing.Backend/tools)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Split-Path -Parent $scriptDir
$repoRoot = Split-Path -Parent $backendDir
$repoRoot = Resolve-Path $repoRoot

Write-Host "=== Backend Runner ===" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host "Checking for processes on port $Port..." -ForegroundColor Cyan
Write-Host ""

# Find processes listening on port 5000
try {
    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    
    if ($connections) {
        $pidsToCheck = $connections | Select-Object -ExpandProperty OwningProcess -Unique
        
        foreach ($pid in $pidsToCheck) {
            try {
                $process = Get-Process -Id $pid -ErrorAction Stop
                $processInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $pid" -ErrorAction SilentlyContinue
                
                $commandLine = $processInfo.CommandLine ?? ""
                $executablePath = $processInfo.ExecutablePath ?? ""
                $processPath = $process.Path ?? ""
                
                Write-Host "Found process on port $Port:" -ForegroundColor Yellow
                Write-Host "  PID: $pid" -ForegroundColor Gray
                Write-Host "  Name: $($process.ProcessName)" -ForegroundColor Gray
                Write-Host "  Path: $processPath" -ForegroundColor Gray
                if ($commandLine) {
                    Write-Host "  Command: $commandLine" -ForegroundColor Gray
                }
                Write-Host ""
                
                # Check if this is our backend process
                $isOurBackend = $false
                $reason = ""
                
                # Check 1: Command line or path contains "dotnet" AND ("Ticketing.Api" OR "Ticketing.Backend")
                if (($commandLine -match "dotnet" -or $executablePath -match "dotnet") -and 
                    ($commandLine -match "Ticketing\.(Api|Backend)" -or $processPath -match "Ticketing\.(Api|Backend)")) {
                    $isOurBackend = $true
                    $reason = "Command line/path contains dotnet and Ticketing.Api/Backend"
                }
                
                # Check 2: Process path is in this repo directory
                if (-not $isOurBackend -and $processPath) {
                    $normalizedProcessPath = (Resolve-Path $processPath -ErrorAction SilentlyContinue).Path
                    if ($normalizedProcessPath -and $normalizedProcessPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $isOurBackend = $true
                        $reason = "Process is in this repository directory"
                    }
                }
                
                # Check 3: Process name is Ticketing.Backend or Ticketing.Api
                if (-not $isOurBackend -and 
                    ($process.ProcessName -eq "Ticketing.Backend" -or $process.ProcessName -eq "Ticketing.Api")) {
                    # Additional check: verify it's in our repo or has our command line
                    if ($processPath -and $processPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                        $isOurBackend = $true
                        $reason = "Process name matches and is in this repository"
                    }
                }
                
                if ($isOurBackend) {
                    Write-Host "  ✓ Confirmed: This is our backend process ($reason)" -ForegroundColor Green
                    Write-Host "  Stopping process $pid..." -ForegroundColor Yellow
                    Stop-Process -Id $pid -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 500
                    Write-Host "  ✓ Process stopped successfully" -ForegroundColor Green
                    Write-Host ""
                } else {
                    Write-Host "  ✗ WARNING: Port $Port is used by PID $pid, but it doesn't appear to be this backend project." -ForegroundColor Red
                    Write-Host "  Not stopping this process to avoid breaking other applications." -ForegroundColor Red
                    Write-Host ""
                    Write-Host "  To manually stop it, run:" -ForegroundColor Yellow
                    Write-Host "    taskkill /PID $pid /F" -ForegroundColor Gray
                    Write-Host ""
                    Write-Host "  Or find what's using the port:" -ForegroundColor Yellow
                    Write-Host "    netstat -ano | findstr :$Port" -ForegroundColor Gray
                    Write-Host ""
                    exit 1
                }
            } catch {
                Write-Host "  ⚠ Could not inspect process $pid: $_" -ForegroundColor Yellow
                Write-Host ""
            }
        }
    } else {
        Write-Host "No processes found listening on port $Port" -ForegroundColor Green
        Write-Host ""
    }
} catch {
    Write-Host "Could not check port $Port: $_" -ForegroundColor Yellow
    Write-Host "Continuing anyway..." -ForegroundColor Gray
    Write-Host ""
}

# Also check for Ticketing.Backend processes by name (as a safety net)
$backendProcesses = Get-Process -Name "Ticketing.Backend" -ErrorAction SilentlyContinue
if ($backendProcesses) {
    foreach ($proc in $backendProcesses) {
        try {
            $procPath = $proc.Path
            if ($procPath -and $procPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                Write-Host "Found Ticketing.Backend process (PID: $($proc.Id)) in this repo. Stopping..." -ForegroundColor Yellow
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                Start-Sleep -Milliseconds 500
                Write-Host "✓ Process stopped" -ForegroundColor Green
            }
        } catch {
            Write-Host "Could not stop process $($proc.Id): $_" -ForegroundColor Yellow
        }
    }
}

# Shutdown build server to release file locks
Write-Host "Shutting down build server to release file locks..." -ForegroundColor Cyan
dotnet build-server shutdown 2>&1 | Out-Null
Start-Sleep -Milliseconds 500

# Change to backend directory
Set-Location $backendDir

# Determine which project to run
# The root Ticketing.Backend.csproj is the actual runnable project
$projectPath = ".\Ticketing.Backend.csproj"
if (-not (Test-Path $projectPath)) {
    Write-Host "ERROR: Could not find Ticketing.Backend.csproj" -ForegroundColor Red
    exit 1
}

Write-Host "Starting backend..." -ForegroundColor Cyan
Write-Host "Project: $projectPath" -ForegroundColor Gray
Write-Host "URL: http://127.0.0.1:$Port" -ForegroundColor Gray
Write-Host "Swagger: http://127.0.0.1:$Port/swagger" -ForegroundColor Gray
Write-Host ""

# Run the backend project
try {
    dotnet run --project $projectPath --urls "http://127.0.0.1:$Port"
} catch {
    Write-Host ""
    Write-Host "ERROR: Failed to start backend" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}


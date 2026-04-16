# run-api.ps1
# Safely stops any running backend instance and starts the API project
# Prevents file-lock errors (MSB3027/MSB3026) when running dotnet run

param(
    [int]$Port = 5000
)

$ErrorActionPreference = "Stop"

Write-Host "Checking for running backend processes on port $Port..." -ForegroundColor Cyan

# Find process listening on port 5000
try {
    $netstatOutput = netstat -ano | Select-String ":$Port\s+.*LISTENING"
    if ($netstatOutput) {
        $pidMatch = $netstatOutput | Select-String -Pattern "\s+(\d+)$"
        if ($pidMatch) {
            $pid = [int]$pidMatch.Matches[0].Groups[1].Value
            $process = Get-Process -Id $pid -ErrorAction SilentlyContinue
            if ($process) {
                Write-Host "Found process '$($process.ProcessName)' (PID: $pid) listening on port $Port. Stopping..." -ForegroundColor Yellow
                Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 1
                Write-Host "Process stopped." -ForegroundColor Green
            }
        }
    }
} catch {
    Write-Host "Could not check port $Port (this is OK if no process is running)" -ForegroundColor Gray
}

# Also check for Ticketing.Backend process by name
$backendProcesses = Get-Process -Name "Ticketing.Backend" -ErrorAction SilentlyContinue
if ($backendProcesses) {
    Write-Host "Found $($backendProcesses.Count) Ticketing.Backend process(es). Stopping..." -ForegroundColor Yellow
    $backendProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 1
    Write-Host "Ticketing.Backend processes stopped." -ForegroundColor Green
}

# Shutdown build server to release file locks
Write-Host "Shutting down build server to release file locks..." -ForegroundColor Cyan
dotnet build-server shutdown 2>&1 | Out-Null
Start-Sleep -Seconds 1

# Change to the backend directory (script should be run from repo root or backend/Ticketing.Backend)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$backendDir = Split-Path -Parent $scriptDir
Set-Location $backendDir

Write-Host "Starting API project..." -ForegroundColor Cyan
Write-Host "Running: dotnet run --project .\src\Ticketing.Api\Ticketing.Api.csproj" -ForegroundColor Gray
Write-Host ""

# Run the API project
dotnet run --project .\src\Ticketing.Api\Ticketing.Api.csproj


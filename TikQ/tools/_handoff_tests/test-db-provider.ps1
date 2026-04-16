# test-db-provider.ps1
# Smoke test for config-driven database provider (Sqlite | SqlServer).
# Mode 1 (default): Assume backend is running with Sqlite; GET /api/health and print result.
# Mode 2: Start backend with SqlServer via env vars; GET /api/health and print result.
# Usage:
#   .\test-db-provider.ps1
#       Uses default (Sqlite). Backend must already be running, or use -StartBackend.
#   .\test-db-provider.ps1 -StartBackend
#       Starts backend with default (Sqlite), waits, then calls /api/health.
#   .\test-db-provider.ps1 -StartBackend -Provider SqlServer -ConnectionString "Server=.;Database=TikQ;Integrated Security=true;TrustServerCertificate=true;"
#       Starts backend with SqlServer, then calls /api/health.

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$StartBackend,
    [ValidateSet("Sqlite", "SqlServer")]
    [string]$Provider = "Sqlite",
    [string]$ConnectionString = "",
    [int]$TimeoutSeconds = 15,
    [int]$WaitForReadySeconds = 20
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Resolve-Path (Join-Path $scriptDir "..\..")
$backendPath = Join-Path $repoRoot "backend\Ticketing.Backend"
$stopScript = Join-Path (Join-Path $repoRoot "tools") "stop-backend.ps1"

function Get-Health {
    param([string]$Url)
    try {
        $r = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSeconds
        return @{ StatusCode = $r.StatusCode; Content = $r.Content }
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return @{ StatusCode = $code; Content = $_.Exception.Message }
    }
}

function Start-BackendWithProvider {
    param([string]$DbProvider, [string]$ConnStr)
    if (Test-Path $stopScript) { & $stopScript 2>$null; Start-Sleep -Seconds 2 }
    $env:Database__Provider = $DbProvider
    if (-not [string]::IsNullOrWhiteSpace($ConnStr)) { $env:ConnectionStrings__DefaultConnection = $ConnStr }
    Push-Location $backendPath
    try {
        $job = Start-Job -ScriptBlock {
            param($p) Set-Location $p; dotnet run
        } -ArgumentList $backendPath
        $elapsed = 0
        while ($elapsed -lt $WaitForReadySeconds) {
            Start-Sleep -Seconds 2
            $elapsed += 2
            $h = Get-Health -Url "$BaseUrl/api/health"
            if ($h.StatusCode -eq 200) {
                Stop-Job $job -ErrorAction SilentlyContinue
                Remove-Job $job -Force -ErrorAction SilentlyContinue
                return $true
            }
        }
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        return $false
    } finally {
        Pop-Location
        Remove-Item Env:Database__Provider -ErrorAction SilentlyContinue
        Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
    }
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DB Provider Smoke Test" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host " Provider: $Provider" -ForegroundColor Gray
if ($StartBackend) {
    Write-Host " StartBackend: yes" -ForegroundColor Gray
    if ($Provider -eq "SqlServer" -and [string]::IsNullOrWhiteSpace($ConnectionString)) {
        Write-Host " ERROR: -Provider SqlServer requires -ConnectionString." -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

if ($StartBackend) {
    Write-Host "Building backend..." -ForegroundColor Yellow
    Push-Location $backendPath
    try {
        dotnet build -nologo -v q
        if ($LASTEXITCODE -ne 0) { Write-Host "Build failed." -ForegroundColor Red; exit 1 }
    } finally { Pop-Location }
    Write-Host "Starting backend with Provider=$Provider ..." -ForegroundColor Yellow
    $started = Start-BackendWithProvider -DbProvider $Provider -ConnStr $ConnectionString
    if (-not $started) {
        Write-Host "Backend did not respond with 200 on /api/health within ${WaitForReadySeconds}s." -ForegroundColor Red
        exit 1
    }
}

$health = Get-Health -Url "$BaseUrl/api/health"
Write-Host "GET $BaseUrl/api/health => $($health.StatusCode)" -ForegroundColor $(if ($health.StatusCode -eq 200) { "Green" } else { "Red" })
if ($health.Content) {
    try {
        $obj = $health.Content | ConvertFrom-Json
        $obj | ConvertTo-Json -Depth 5
    } catch {
        Write-Host $health.Content
    }
}

if ($health.StatusCode -ne 200) {
    Write-Host "Smoke test FAILED: /api/health did not return 200." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Smoke test PASSED." -ForegroundColor Green
exit 0

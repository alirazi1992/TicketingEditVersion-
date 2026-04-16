<#
.SYNOPSIS
  GATE: Verifies that backend route mapping is working (controllers + /api/health).

.DESCRIPTION
  GET /api/health must return JSON 200. Optionally GET /swagger/v1/swagger.json
  returns 200 in dev (not required in prod). Prints PASS/FAIL and sets exit code.

.PARAMETER BaseUrl
  Backend base URL (e.g. http://localhost:5000 or https://tikq-api.contoso.com).

.PARAMETER CheckSwagger
  If set, also requires GET /swagger/v1/swagger.json to return 200 (optional in prod).

.PARAMETER TimeoutSeconds
  HTTP timeout in seconds. Default 10.

.EXAMPLE
  .\verify-routes.ps1 -BaseUrl "http://localhost:5000"

.EXAMPLE
  .\verify-routes.ps1 -BaseUrl "https://tikq-api.contoso.com" -CheckSwagger
#>

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$CheckSwagger,
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Stop"

# --- Health (required) ---
$healthUrl = "$BaseUrl/api/health"
$healthPass = $false
try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
    if ($response.StatusCode -eq 200) {
        $contentType = $response.Headers["Content-Type"]
        if ($contentType -and $contentType -match "json") {
            $healthPass = $true
        }
        # Accept 200 with JSON body; some servers send application/json without charset
        if (-not $healthPass -and $response.Content -match "^\s*\{") {
            $healthPass = $true
        }
    }
} catch {
    # fallthrough: $healthPass stays false
}

# --- Swagger (optional unless -CheckSwagger) ---
$swaggerPass = $true
if ($CheckSwagger) {
    $swaggerUrl = "$BaseUrl/swagger/v1/swagger.json"
    $swaggerPass = $false
    try {
        $swaggerResp = Invoke-WebRequest -Uri $swaggerUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
        if ($swaggerResp.StatusCode -eq 200) { $swaggerPass = $true }
    } catch { }
}

# --- Report ---
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Route mapping verification (GATE)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host " GET /api/health (JSON 200): $(if ($healthPass) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($healthPass) { 'Green' } else { 'Red' })
if ($CheckSwagger) {
    Write-Host " GET /swagger/v1/swagger.json (200): $(if ($swaggerPass) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($swaggerPass) { 'Green' } else { 'Red' })
}
Write-Host ""

$allPass = $healthPass -and $swaggerPass
if ($allPass) {
    Write-Host " PASS" -ForegroundColor Green
    exit 0
} else {
    Write-Host " FAIL" -ForegroundColor Red
    if (-not $healthPass) {
        Write-Host " Fix: Ensure Program.cs maps routes in all environments (MapControllers + MapGet /api/health). See docs/01_Runbook/ROUTES_404.md" -ForegroundColor Yellow
    }
    exit 1
}

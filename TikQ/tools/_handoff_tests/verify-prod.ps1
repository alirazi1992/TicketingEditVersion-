<#
.SYNOPSIS
  End-to-end verification script for IIS Production + SQL Server.

.DESCRIPTION
  Hits /api/health to verify provider, environment, DB connectivity and counts;
  optionally asserts provider is SqlServer; optionally tests login + /api/auth/whoami
  using provided credentials. Outputs clear PASS/FAIL. Never logs secrets; passwords
  are redacted in any output.
  For http://localhost:8080 to PASS with ExpectProvider=SqlServer, the backend must
  be running with Database__Provider=SqlServer (e.g. after running deploy-iis.ps1).

.PARAMETER BaseUrl
  Backend base URL (e.g. https://tikq-api.contoso.com or http://localhost:8080).

.PARAMETER ExpectProvider
  If set, asserts that /api/health reports this provider (e.g. SqlServer). Omit to skip assertion.

.PARAMETER LoginEmail
  Optional. If provided with LoginPassword, attempts POST /api/auth/login and GET /api/auth/whoami.

.PARAMETER LoginPassword
  Optional. Used only when LoginEmail is also provided. Never logged or printed.

.PARAMETER TimeoutSeconds
  HTTP timeout in seconds. Default 15.

.EXAMPLE
  .\verify-prod.ps1 -BaseUrl "https://tikq-api.contoso.com" -ExpectProvider SqlServer

.EXAMPLE
  .\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer -LoginEmail "admin@test.com" -LoginPassword "Admin123!"
#>

param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$ExpectProvider = "SqlServer",
    [string]$LoginEmail = "",
    [string]$LoginPassword = "",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

# Redact password in any string (for safe logging)
function Redact-Password {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    return $Text -replace '(?i)(password["\s:=]+)[^\s"\'',}\]]+', '$1***'
}

# --- 1. Health check ---
$healthUrl = "$BaseUrl/api/health"
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " TikQ Production Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host " ExpectProvider: $(if ($ExpectProvider) { $ExpectProvider } else { '(none)' })" -ForegroundColor Gray
Write-Host " Login test: $(if ($LoginEmail) { "yes ($($LoginEmail -replace '@.*','@***'))" } else { "no" })" -ForegroundColor Gray
Write-Host ""

$healthPass = $false
$providerPass = $true
$healthObj = $null

try {
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
        $healthPass = $true
        $healthObj = $response.Content | ConvertFrom-Json

        # Provider: primary = database.provider (camelCase or PascalCase); fallback = infer from database.path
        $db = $healthObj.database
        if ($null -eq $db) { $db = $healthObj.Database }
        $provider = $db.provider
        if ([string]::IsNullOrWhiteSpace($provider)) { $provider = $db.Provider }
        $inferredProvider = $false
        if ([string]::IsNullOrWhiteSpace($provider) -and $null -ne $db) {
            $p = $db.path; if ($null -eq $p) { $p = $db.Path }
            if ($null -ne $p -and $p -is [string]) {
                if ($p -match '\.db') { $provider = 'Sqlite'; $inferredProvider = $true }
                elseif ($p -match 'Server=|Database=') { $provider = 'SqlServer'; $inferredProvider = $true }
            }
        }
        if ($inferredProvider) {
            Write-Host "  (provider inferred from path; backend should expose database.provider from DbContext)" -ForegroundColor DarkGray
        }
        $envName = $healthObj.environment
        if ([string]::IsNullOrWhiteSpace($envName)) { $envName = $healthObj.Environment }
        $canConnect = $db.canConnect
        if ($null -eq $canConnect) { $canConnect = $db.CanConnect }
        $status = $healthObj.status
        if ([string]::IsNullOrWhiteSpace($status)) { $status = $healthObj.Status }
        $counts = $db.dataCounts
        if ($null -eq $counts) { $counts = $db.DataCounts }

        Write-Host "[HEALTH] GET $healthUrl => $($response.StatusCode)" -ForegroundColor Green
        Write-Host "  provider:    $(if ([string]::IsNullOrWhiteSpace($provider)) { '(missing)' } else { $provider })" -ForegroundColor $(if ([string]::IsNullOrWhiteSpace($provider)) { "Red" } else { "Gray" })
        Write-Host "  environment: $envName" -ForegroundColor Gray
        Write-Host "  db canConnect: $canConnect" -ForegroundColor $(if ($canConnect) { "Green" } else { "Red" })
        Write-Host "  status:      $status" -ForegroundColor Gray
        $catCount = if ($counts) { $counts.categories } else { $counts.Categories }; if ($null -eq $catCount) { $catCount = 0 }
        $ticketCount = if ($counts) { $counts.tickets } else { $counts.Tickets }; if ($null -eq $ticketCount) { $ticketCount = 0 }
        $userCount = if ($counts) { $counts.users } else { $counts.Users }; if ($null -eq $userCount) { $userCount = 0 }
        Write-Host "  counts:      categories=$catCount tickets=$ticketCount users=$userCount" -ForegroundColor Gray
        $dbError = $db.error; if ([string]::IsNullOrWhiteSpace($dbError)) { $dbError = $db.Error }
        if ($dbError) {
            Write-Host "  db error:    $dbError" -ForegroundColor Yellow
        }
        $pendingMig = $db.pendingMigrationsCount; if ($null -eq $pendingMig) { $pendingMig = $db.PendingMigrationsCount }
        $lastMig = $db.lastMigrationId; if ([string]::IsNullOrWhiteSpace($lastMig)) { $lastMig = $db.LastMigrationId }
        if ($null -ne $pendingMig -or $null -ne $lastMig) {
            Write-Host "  migrations:  pending=$pendingMig last=$lastMig" -ForegroundColor Gray
        }
        $envPresent = $healthObj.effectiveEnvVarsPresent; if ($null -eq $envPresent) { $envPresent = $healthObj.EffectiveEnvVarsPresent }
        if ($envPresent) {
            $pDb = $envPresent.Database__Provider
            $pConn = $envPresent.ConnectionStrings__DefaultConnection
            $pJwt = $envPresent.Jwt__Secret
            Write-Host "  env present: Database__Provider=$pDb ConnectionStrings__DefaultConnection=$pConn Jwt__Secret=$pJwt" -ForegroundColor Gray
        }

        # Clear PASS/FAIL for provider: missing or wrong
        if ([string]::IsNullOrWhiteSpace($provider)) {
            $providerPass = $false
            Write-Host "  PROVIDER: FAIL - database.provider is missing or empty (backend should set from DbContext.Database.ProviderName)" -ForegroundColor Red
        } elseif (-not [string]::IsNullOrWhiteSpace($ExpectProvider) -and $provider -ne $ExpectProvider) {
            $providerPass = $false
            Write-Host "  PROVIDER: FAIL - expected '$ExpectProvider' but got '$provider'" -ForegroundColor Red
        } else {
            Write-Host "  PROVIDER: PASS ($provider)" -ForegroundColor Green
        }
    } else {
        Write-Host "[HEALTH] GET $healthUrl => $($response.StatusCode)" -ForegroundColor Red
    }
} catch {
    $msg = $_.Exception.Message
    Write-Host "[HEALTH] GET $healthUrl => FAIL" -ForegroundColor Red
    Write-Host "  Error: $(Redact-Password $msg)" -ForegroundColor Red
}

# Health + provider summary
Write-Host ""
if ($healthPass -and $providerPass) {
    Write-Host "HEALTH: PASS (ok=$healthPass, provider check=$(if ($ExpectProvider) { $providerPass } else { 'skipped' }))" -ForegroundColor Green
} else {
    Write-Host "HEALTH: FAIL (healthOk=$healthPass, providerOk=$providerPass)" -ForegroundColor Red
}

# --- 2. Optional login + whoami ---
$loginPass = $true  # no test = pass
if ($LoginEmail -and $LoginPassword) {
    $loginPass = $false
    $loginUrl = "$BaseUrl/api/auth/login"
    $whoamiUrl = "$BaseUrl/api/auth/whoami"

    $body = @{ email = $LoginEmail; password = $LoginPassword } | ConvertTo-Json
    $session = $null
    try {
        $loginResp = Invoke-WebRequest -Uri $loginUrl -Method POST -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec $TimeoutSeconds -SessionVariable session
        if ($loginResp.StatusCode -ge 200 -and $loginResp.StatusCode -lt 300) {
            $whoamiResp = Invoke-WebRequest -Uri $whoamiUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds -WebSession $session
            if ($whoamiResp.StatusCode -eq 200) {
                $whoamiObj = $whoamiResp.Content | ConvertFrom-Json
                if ($whoamiObj.isAuthenticated -eq $true) {
                    $loginPass = $true
                    Write-Host "LOGIN:  PASS (login + whoami ok, role=$($whoamiObj.role), email=$($whoamiObj.email))" -ForegroundColor Green
                } else {
                    Write-Host "LOGIN:  FAIL (whoami returned isAuthenticated=false)" -ForegroundColor Red
                }
            } else {
                Write-Host "LOGIN:  FAIL (GET whoami => $($whoamiResp.StatusCode))" -ForegroundColor Red
            }
        } else {
            Write-Host "LOGIN:  FAIL (POST login => $($loginResp.StatusCode))" -ForegroundColor Red
        }
    } catch {
        $errMsg = $_.Exception.Message
        Write-Host "LOGIN:  FAIL ($(Redact-Password $errMsg))" -ForegroundColor Red
    }
    Write-Host ""
} elseif ($LoginEmail -or $LoginPassword) {
    Write-Host "LOGIN:  SKIP (provide both -LoginEmail and -LoginPassword to test login)" -ForegroundColor Yellow
    Write-Host ""
}

# --- 3. Optional: when expecting SqlServer, warn if key env not present ---
$envCheckPass = $true
$envPresentForCheck = $healthObj.effectiveEnvVarsPresent; if ($null -eq $envPresentForCheck) { $envPresentForCheck = $healthObj.EffectiveEnvVarsPresent }
if ($healthPass -and $ExpectProvider -eq "SqlServer" -and $envPresentForCheck) {
    if (-not $envPresentForCheck.Database__Provider) {
        Write-Host "ENV:    WARN Database__Provider not set in app (IIS env); provider may default incorrectly." -ForegroundColor Yellow
        $envCheckPass = $false
    }
    if (-not $envPresentForCheck.ConnectionStrings__DefaultConnection) {
        Write-Host "ENV:    WARN ConnectionStrings__DefaultConnection not set; SQL Server may be unreachable." -ForegroundColor Yellow
        $envCheckPass = $false
    }
    if ($envCheckPass) { Write-Host "ENV:    PASS (Database__Provider and ConnectionStrings__DefaultConnection present)" -ForegroundColor Green }
}

# --- 4. Final result ---
$overallPass = $healthPass -and $providerPass -and $loginPass -and $envCheckPass
Write-Host "========================================" -ForegroundColor Cyan
if ($overallPass) {
    Write-Host " OVERALL: PASS" -ForegroundColor Green
} else {
    Write-Host " OVERALL: FAIL" -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $overallPass) {
    exit 1
}
exit 0

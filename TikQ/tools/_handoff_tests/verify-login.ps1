<#
.SYNOPSIS
  Verifies bootstrap/login: POST /api/auth/login with seed creds, then GET /api/auth/whoami with cookie; asserts isAuthenticated true.

.DESCRIPTION
  Uses provided credentials to POST /api/auth/login, then GET /api/auth/whoami with the session cookie.
  Asserts whoami returns isAuthenticated = true. Never logs or prints passwords; redacts in any error output.
  Use after deploy with bootstrap to confirm first-run seeding and login work.

.PARAMETER BaseUrl
  Backend base URL (e.g. https://tikq-api.contoso.com or http://localhost:8080).

.PARAMETER LoginEmail
  Email for login (or set env TikQ_LOGIN_EMAIL).

.PARAMETER LoginPassword
  Password for login (or set env TikQ_LOGIN_PASSWORD). Never logged or printed.

.PARAMETER TimeoutSeconds
  HTTP timeout in seconds. Default 15.

.EXAMPLE
  .\verify-login.ps1 -BaseUrl "http://localhost:8080" -LoginEmail "admin@local" -LoginPassword "YourBootstrapPassword"

.EXAMPLE
  $env:TikQ_LOGIN_EMAIL = "admin@local"; $env:TikQ_LOGIN_PASSWORD = "secret"; .\verify-login.ps1 -BaseUrl "http://localhost:8080"
#>

param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$LoginEmail = "",
    [string]$LoginPassword = "",
    [int]$TimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"

# Resolve creds from params or env (never log password)
if ([string]::IsNullOrWhiteSpace($LoginEmail)) { $LoginEmail = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_EMAIL", "Process") }
if ([string]::IsNullOrWhiteSpace($LoginEmail)) { $LoginEmail = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_EMAIL", "User") }
if ([string]::IsNullOrWhiteSpace($LoginEmail)) { $LoginEmail = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_EMAIL", "Machine") }
if ([string]::IsNullOrWhiteSpace($LoginPassword)) { $LoginPassword = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_PASSWORD", "Process") }
if ([string]::IsNullOrWhiteSpace($LoginPassword)) { $LoginPassword = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_PASSWORD", "User") }
if ([string]::IsNullOrWhiteSpace($LoginPassword)) { $LoginPassword = [Environment]::GetEnvironmentVariable("TikQ_LOGIN_PASSWORD", "Machine") }

function Redact-Password {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return $Text }
    return $Text -replace '(?i)(password["\s:=]+)[^\s"\'',}\]]+', '$1***'
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " TikQ Login Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor Gray
Write-Host " Login:   $(if ($LoginEmail) { $LoginEmail -replace '@.*','@***' } else { '(not set)' })" -ForegroundColor Gray
Write-Host ""

$loginUrl = "$BaseUrl/api/auth/login"
$whoamiUrl = "$BaseUrl/api/auth/whoami"
$loginPass = $false

if ([string]::IsNullOrWhiteSpace($LoginEmail) -or [string]::IsNullOrWhiteSpace($LoginPassword)) {
    Write-Host "LOGIN: FAIL (provide -LoginEmail and -LoginPassword, or set TikQ_LOGIN_EMAIL and TikQ_LOGIN_PASSWORD)" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host " OVERALL: FAIL" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Cyan
    exit 1
}

try {
    $body = @{ email = $LoginEmail; password = $LoginPassword } | ConvertTo-Json
    $session = $null
    $loginResp = Invoke-WebRequest -Uri $loginUrl -Method POST -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec $TimeoutSeconds -SessionVariable session
    if ($loginResp.StatusCode -ge 200 -and $loginResp.StatusCode -lt 300) {
        # GATE: verify login resulted in auth cookie (session cookie container or Set-Cookie header)
        $uriForCookies = [System.Uri]$loginUrl
        $cookies = $session.Cookies.GetCookies($uriForCookies)
        $authCookie = $cookies | Where-Object { $_.Name -eq 'tikq_access' }
        $setCookieHeader = $loginResp.Headers['Set-Cookie']
        $cookieInResponse = ([bool]$authCookie) -or ($setCookieHeader -and $setCookieHeader -match 'tikq_access')
        if ($cookieInResponse) {
            Write-Host "[COOKIE] Set-Cookie present (tikq_access)" -ForegroundColor Green
        } else {
            Write-Host "[COOKIE] Set-Cookie missing or no tikq_access (check AuthCookies/SecurePolicy and forwarded headers)" -ForegroundColor Yellow
        }
        $whoamiResp = Invoke-WebRequest -Uri $whoamiUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds -WebSession $session
        if ($whoamiResp.StatusCode -eq 200) {
            $whoamiObj = $whoamiResp.Content | ConvertFrom-Json
            $isAuth = $whoamiObj.isAuthenticated
            if ($null -eq $isAuth) { $isAuth = $whoamiObj.IsAuthenticated }
            if ($isAuth -eq $true) {
                $loginPass = $true
                $role = $whoamiObj.role; if ($null -eq $role) { $role = $whoamiObj.Role }
                $email = $whoamiObj.email; if ($null -eq $email) { $email = $whoamiObj.Email }
                $xCookie = $whoamiResp.Headers['X-Auth-Cookie-Present']; if ($null -eq $xCookie) { $xCookie = $whoamiResp.Headers['X-Auth-Cookie-Present'] }
                Write-Host "[LOGIN]  POST $loginUrl => $($loginResp.StatusCode)" -ForegroundColor Green
                Write-Host "[WHOAMI] GET $whoamiUrl => 200, isAuthenticated=true, X-Auth-Cookie-Present=$xCookie" -ForegroundColor Green
                Write-Host "LOGIN:   PASS (role=$role, email=$($email -replace '@.*','@***'))" -ForegroundColor Green
            } else {
                Write-Host "LOGIN:   FAIL (whoami returned isAuthenticated=false)" -ForegroundColor Red
            }
        } else {
            Write-Host "LOGIN:   FAIL (GET whoami => $($whoamiResp.StatusCode))" -ForegroundColor Red
        }
    } else {
        Write-Host "LOGIN:   FAIL (POST login => $($loginResp.StatusCode))" -ForegroundColor Red
    }
} catch {
    $errMsg = $_.Exception.Message
    Write-Host "LOGIN:   FAIL ($(Redact-Password $errMsg))" -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
if ($loginPass) {
    Write-Host " OVERALL: PASS" -ForegroundColor Green
} else {
    Write-Host " OVERALL: FAIL" -ForegroundColor Red
}
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

if (-not $loginPass) { exit 1 }
exit 0

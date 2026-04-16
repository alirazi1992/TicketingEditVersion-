<#
.SYNOPSIS
  Quick test for POST /api/categories: create then duplicate (expect 201 then 409).

.DESCRIPTION
  1) Cookie-based login as admin (curl cookie jar: -c save, -b send).
  2) POST /api/categories with a unique name (timestamp) -> expect 201; print HTTP code + body.
  3) POST again with the SAME name -> expect 409 DUPLICATE_NAME; print HTTP code + body.
  Uses curl.exe with cookie jar (same approach as smoke-ticket-live.ps1). No WebSession.

.PARAMETER BaseUrl
  API base URL (no trailing slash). Default: http://localhost:8080 (frontend proxy).
  Use http://localhost:5000 for direct backend.

.PARAMETER Email
  Admin login email.

.PARAMETER Password
  Admin login password.

.EXAMPLE
  .\create-category.ps1
  .\create-category.ps1 -BaseUrl "http://localhost:5000" -Email "admin@example.com" -Password "Admin123!"
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = "admin@example.com",
    [string]$Password = "Admin123!"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

# Temp files for curl (cookie jar, headers, body). Cleaned up at end.
$tempDir = [System.IO.Path]::GetTempPath()
$cookieJar = Join-Path $tempDir "tikq_create_category_cookies_$([Guid]::NewGuid().ToString('N')).txt"
$headersFile = Join-Path $tempDir "tikq_create_category_headers_$([Guid]::NewGuid().ToString('N')).txt"
$bodyFile = Join-Path $tempDir "tikq_create_category_body_$([Guid]::NewGuid().ToString('N')).txt"

function Remove-TempFiles {
    foreach ($f in @($cookieJar, $headersFile, $bodyFile)) {
        if (Test-Path $f) { Remove-Item $f -Force -ErrorAction SilentlyContinue }
    }
}

# Run curl and return HTTP status code; optional paths to write response headers/body.
# Uses cookie jar: -c to save (login), -b to send on every request. Same pattern as smoke-ticket-live.ps1.
function Invoke-CurlSimple {
    param(
        [string]$Method,
        [string]$Url,
        [string]$CookieJar,
        [switch]$SaveCookies,
        [string]$Body = $null,
        [string]$OutHeaders = $null,
        [string]$OutBody = $null
    )
    $bodyTemp = $null
    $argList = @(
        "-s", "-S",
        "-X", $Method,
        "-H", "Content-Type: application/json",
        "-H", "Accept: application/json",
        "-w", "`n%{http_code}",
        "-o", $bodyFile,
        "-D", $headersFile
    )
    if ($CookieJar) {
        if ($SaveCookies) { $argList += "-c", $CookieJar }
        $argList += "-b", $CookieJar
    }
    if ($Body) {
        $bodyTemp = Join-Path $tempDir "tikq_create_category_req_$([Guid]::NewGuid().ToString('N')).txt"
        [System.IO.File]::WriteAllText($bodyTemp, $Body, [System.Text.UTF8Encoding]::new($false))
        $argList += "-d", "@$bodyTemp"
    }
    try {
        $stdout = & curl.exe $argList $Url 2>&1
    } finally {
        if ($bodyTemp -and (Test-Path $bodyTemp)) { Remove-Item $bodyTemp -Force -ErrorAction SilentlyContinue }
    }
    $codeStr = if ($stdout -is [array]) { $stdout[-1] } else { $stdout }
    $codeStr = ($codeStr -replace "`r`n", "`n") -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -match '^\d{3}$' } | Select-Object -Last 1
    if (-not $codeStr) { $codeStr = "000" }
    $code = [int]$codeStr
    $headersContent = ""
    $bodyContent = ""
    if (Test-Path $headersFile) { $headersContent = Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue }
    if (Test-Path $bodyFile) { $bodyContent = Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue }
    if ($OutHeaders) { Set-Content -Path $OutHeaders -Value $headersContent -NoNewline -ErrorAction SilentlyContinue }
    if ($OutBody) { Set-Content -Path $OutBody -Value $bodyContent -NoNewline -ErrorAction SilentlyContinue }
    return $code
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " POST /api/categories (201 then 409)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor White
Write-Host " Expected: [1] 200 login, [2] 201 created, [3] 409 duplicate" -ForegroundColor Gray
Write-Host ""

# 1) Login with curl; save cookies to jar; verify Set-Cookie and jar contains tikq_access
Write-Host "[1] POST $BaseUrl/api/auth/login ... " -NoNewline
$loginJson = (@{ email = $Email; password = $Password } | ConvertTo-Json -Compress)
$loginCode = Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/auth/login" -CookieJar $cookieJar -SaveCookies -Body $loginJson -OutHeaders $headersFile -OutBody $bodyFile
$h = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
$b = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }

if ($loginCode -eq 200) {
    Write-Host $loginCode -ForegroundColor Green
    $setCookie = $h -match 'Set-Cookie:\s*tikq_access'
    Write-Host "     Set-Cookie (tikq_access) in response: $setCookie" -ForegroundColor Gray
    $jarHasAccess = $false
    if (Test-Path $cookieJar) {
        $jarContent = Get-Content -Path $cookieJar -Raw -ErrorAction SilentlyContinue
        $jarHasAccess = $jarContent -match 'tikq_access'
    }
    Write-Host "     cookie jar contains tikq_access: $jarHasAccess" -ForegroundColor Gray
    if (-not $jarHasAccess) {
        Write-Host "     WARN: Cookie jar missing tikq_access; subsequent requests may get 401." -ForegroundColor Yellow
    }
} else {
    Write-Host $loginCode -ForegroundColor Red
    Write-Host "     Body: $b" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Result: FAIL - [1] expected 200 login." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 2) POST category with unique name -> expect 201
$categoryName = "Test Category $(Get-Date -Format 'yyyyMMdd-HHmmss')"
$catJson = (@{ name = $categoryName; description = "create-category.ps1"; isActive = $true } | ConvertTo-Json -Compress)

Write-Host "[2] POST $BaseUrl/api/categories (unique name) ... " -NoNewline
$createCode = Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/categories" -CookieJar $cookieJar -Body $catJson -OutHeaders $headersFile -OutBody $bodyFile
$b2 = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
$h2 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }

if ($createCode -eq 201) {
    Write-Host $createCode -ForegroundColor Green
    Write-Host "     Body: $b2" -ForegroundColor Gray
} else {
    Write-Host $createCode -ForegroundColor $(if ($createCode -eq 401) { "Red" } else { "Yellow" })
    Write-Host "     Body: $b2" -ForegroundColor Gray
    if ($createCode -eq 401) {
        $headerLines = ($h2 -split "`n")
        $first50 = $headerLines | Select-Object -First 50
        Write-Host "     Response headers (first 50 lines):" -ForegroundColor Yellow
        Write-Host ($first50 -join "`n") -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - [2] expected 201 created." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 3) POST again with SAME name -> expect 409
Write-Host "[3] POST $BaseUrl/api/categories (same name, expect 409) ... " -NoNewline
$dupCode = Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/categories" -CookieJar $cookieJar -Body $catJson -OutHeaders $headersFile -OutBody $bodyFile
$b3 = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
$h3 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }

if ($dupCode -eq 409) {
    Write-Host $dupCode -ForegroundColor Green
    Write-Host "     Body: $b3" -ForegroundColor Gray
} else {
    Write-Host $dupCode -ForegroundColor $(if ($dupCode -eq 401) { "Red" } else { "Yellow" })
    Write-Host "     Body: $b3" -ForegroundColor Gray
    if ($dupCode -eq 401) {
        $headerLines = ($h3 -split "`n")
        $first50 = $headerLines | Select-Object -First 50
        Write-Host "     Response headers (first 50 lines):" -ForegroundColor Yellow
        Write-Host ($first50 -join "`n") -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - [3] expected 409 duplicate." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

Remove-TempFiles
Write-Host ""
Write-Host "Result: SUCCESS (201 then 409)" -ForegroundColor Green
Write-Host ""

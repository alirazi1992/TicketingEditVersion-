<#
.SYNOPSIS
  Call GET /api/diag/build (Admin) to verify IIS is running the expected build stamp.

.DESCRIPTION
  Logs in as admin with curl cookie jar, then GET /api/diag/build.
  Prints HTTP code and body (e.g. {"build":"categories-fix-v2-2026-02-25"}).
  Use after republish to confirm the new backend is deployed.

.PARAMETER BaseUrl
  API base URL (no trailing slash). Default: http://localhost:8080

.PARAMETER Email
  Admin login email.

.PARAMETER Password
  Admin login password.

.EXAMPLE
  .\diag-build.ps1
  .\diag-build.ps1 -BaseUrl "http://localhost:5000" -Email "admin@example.com" -Password "Admin123!"
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = "admin@example.com",
    [string]$Password = "Admin123!"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')

$tempDir = [System.IO.Path]::GetTempPath()
$cookieJar = Join-Path $tempDir "tikq_diag_build_cookies_$([Guid]::NewGuid().ToString('N')).txt"
$headersFile = Join-Path $tempDir "tikq_diag_build_headers_$([Guid]::NewGuid().ToString('N')).txt"
$bodyFile = Join-Path $tempDir "tikq_diag_build_body_$([Guid]::NewGuid().ToString('N')).txt"

function Remove-TempFiles {
    foreach ($f in @($cookieJar, $headersFile, $bodyFile)) {
        if (Test-Path $f) { Remove-Item $f -Force -ErrorAction SilentlyContinue }
    }
}

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
        $bodyTemp = Join-Path $tempDir "tikq_diag_build_req_$([Guid]::NewGuid().ToString('N')).txt"
        [System.IO.File]::WriteAllText($bodyTemp, $Body, [System.Text.UTF8Encoding]::new($false))
        $argList += "-H", "Content-Type: application/json"
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
Write-Host "GET /api/diag/build (verify IIS deploy)" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor White
Write-Host ""

$loginJson = (@{ email = $Email; password = $Password } | ConvertTo-Json -Compress)
$loginCode = Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/auth/login" -CookieJar $cookieJar -SaveCookies -Body $loginJson -OutHeaders $headersFile -OutBody $bodyFile
if ($loginCode -ne 200) {
    $b = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
    Write-Host "Login failed: HTTP $loginCode" -ForegroundColor Red
    Write-Host " Body: $b" -ForegroundColor Gray
    Remove-TempFiles
    exit 1
}

$diagCode = Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/diag/build" -CookieJar $cookieJar -OutHeaders $headersFile -OutBody $bodyFile
$body = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }

Remove-TempFiles

Write-Host "HTTP $diagCode" -ForegroundColor $(if ($diagCode -eq 200) { "Green" } else { "Red" })
Write-Host "Body: $body" -ForegroundColor Gray
Write-Host ""

if ($diagCode -ne 200) {
    exit 1
}
exit 0

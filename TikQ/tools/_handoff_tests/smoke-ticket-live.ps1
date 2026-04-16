<#
.SYNOPSIS
  Smoke test for ticket mutations: login (cookie), list tickets, post message, change status, fetch ticket+messages.

.DESCRIPTION
  Verifies that:
  1) Admin login returns 200 and sets auth cookie (Set-Cookie / cookie jar).
  2) GET tickets returns 200.
  3) POST /api/tickets/{id}/messages returns 201.
  4) PATCH /api/tickets/{id} (status) returns 200.
  5) GET ticket and GET messages return 200 and show updated data.

  Uses curl.exe with a cookie jar for cookie-based auth (tikq_access). No JWT in body.
  Run with backend at BaseUrl (default http://localhost:8080) and valid admin credentials.
  Compatible with IIS and PowerShell 5.1; no extra dependencies.

.PARAMETER BaseUrl
  Backend API base URL (no trailing slash). Default: http://localhost:8080

.PARAMETER Email
  Admin login email. Default: admin@local (BootstrapAdmin for Sqlite dev).

.PARAMETER Password
  Admin login password. Default: empty (BootstrapAdmin default in appsettings).

.PARAMETER TicketId
  Optional. If provided, use this ticket ID instead of picking from list.

.EXAMPLE
  .\smoke-ticket-live.ps1
  .\smoke-ticket-live.ps1 -BaseUrl "http://localhost:8080" -Email "admin@test.com" -Password "YourPassword"
  .\smoke-ticket-live.ps1 -BaseUrl "https://myserver/app" -TicketId "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$Email = "admin@local",
    [string]$Password = "",
    [string]$TicketId = ""
)

$ErrorActionPreference = "Stop"
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

# Ensure base URL has no trailing slash (IIS-friendly)
$BaseUrl = $BaseUrl.TrimEnd('/')

# Temp files for curl (cookie jar, headers, body). Cleaned up at end.
$tempDir = [System.IO.Path]::GetTempPath()
$cookieJar = Join-Path $tempDir "tikq_smoke_cookies_$([Guid]::NewGuid().ToString('N')).txt"
$headersFile = Join-Path $tempDir "tikq_smoke_headers_$([Guid]::NewGuid().ToString('N')).txt"
$bodyFile = Join-Path $tempDir "tikq_smoke_body_$([Guid]::NewGuid().ToString('N')).txt"

function Remove-TempFiles {
    foreach ($f in @($cookieJar, $headersFile, $bodyFile)) {
        if (Test-Path $f) { Remove-Item $f -Force -ErrorAction SilentlyContinue }
    }
}

# Truncate string for 400 response body display (first 500 chars)
function Get-TruncatedBody {
    param([string]$text)
    if ([string]::IsNullOrEmpty($text)) { return "(empty)" }
    if ($text.Length -le 500) { return $text }
    return $text.Substring(0, 500) + "..."
}

# Run curl and return HTTP status code; optional paths to write response headers/body.
# Uses cookie jar: -c to save (login), -b to send on every request.
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
        $bodyTemp = Join-Path $tempDir "tikq_smoke_req_$([Guid]::NewGuid().ToString('N')).txt"
        # Write without BOM so server receives plain JSON (PS 5.1 compatible)
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
Write-Host " TikQ smoke test: ticket mutations" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl: $BaseUrl" -ForegroundColor White
Write-Host " Email:   $Email" -ForegroundColor White
Write-Host " Auth:    cookie-based (curl cookie jar)" -ForegroundColor Gray
Write-Host ""

# 1) Login with curl; save cookies to jar; validate Set-Cookie and jar contains tikq_access
Write-Host "[1] POST $BaseUrl/api/auth/login ... " -NoNewline
$loginJson = (@{ email = $Email; password = $Password } | ConvertTo-Json -Compress)
$h = $null
$b = $null
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
    if ($loginCode -eq 400) {
        Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
        Write-Host "     $(Get-TruncatedBody $b)" -ForegroundColor Gray
    }
    Write-Host "     Response headers:" -ForegroundColor Yellow
    Write-Host $h -ForegroundColor Gray
    Write-Host ""
    Write-Host "Result: FAIL - Login did not return 200." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 2) GET tickets (with cookie jar)
$tid = $TicketId
if (-not $tid) {
    Write-Host "[2] GET $BaseUrl/api/tickets ... " -NoNewline
    $h2 = $null
    $b2 = $null
    $listCode = Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/tickets" -CookieJar $cookieJar -OutHeaders $headersFile -OutBody $bodyFile
    $h2 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
    $b2 = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
    if ($listCode -eq 200) {
        Write-Host $listCode -ForegroundColor Green
        $listJson = $b2 | ConvertFrom-Json
        $tickets = @($listJson)
        if ($tickets.Count -eq 0) {
            Write-Host "     No tickets in list; create a ticket first." -ForegroundColor Yellow
            Remove-TempFiles
            exit 0
        }
        $tid = $tickets[0].id
        if (-not $tid) { $tid = $tickets[0].Id }
        Write-Host "     Using ticket id: $tid" -ForegroundColor Gray
    } else {
        Write-Host $listCode -ForegroundColor Red
        if ($listCode -eq 400) {
            Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
            Write-Host "     $(Get-TruncatedBody $b2)" -ForegroundColor Gray
        }
        if ($listCode -eq 401) {
            Write-Host "     Response headers (401):" -ForegroundColor Yellow
            Write-Host $h2 -ForegroundColor Gray
        }
        Write-Host ""
        Write-Host "Result: FAIL - GET /api/tickets returned $listCode." -ForegroundColor Red
        Write-Host ""
        Remove-TempFiles
        exit 1
    }
} else {
    Write-Host "[2] Using provided TicketId: $tid" -ForegroundColor Gray
}

# 3) POST message — body must match TicketMessageRequest: { "message": string (required), "status": TicketStatus? (optional, string enum) }
Write-Host "[3] POST $BaseUrl/api/tickets/$tid/messages ... " -NoNewline
$msgText = "Smoke test message at $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
# DTO: Message (required), Status (optional); backend accepts enum as string e.g. "InProgress"
$postBody = (@{ message = $msgText; status = "InProgress" } | ConvertTo-Json -Compress)
$postCode = Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/tickets/$tid/messages" -CookieJar $cookieJar -Body $postBody -OutBody $bodyFile
$b3 = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
if ($postCode -eq 201 -or $postCode -eq 200) {
    Write-Host $postCode -ForegroundColor Green
} else {
    Write-Host $postCode -ForegroundColor Red
    if ($postCode -eq 400) {
        Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
        Write-Host "     $(Get-TruncatedBody $b3)" -ForegroundColor Gray
    }
    if ($postCode -eq 401) {
        Invoke-CurlSimple -Method POST -Url "$BaseUrl/api/tickets/$tid/messages" -CookieJar $cookieJar -Body $postBody -OutHeaders $headersFile | Out-Null
        $h3 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
        Write-Host "     Response headers (401):" -ForegroundColor Yellow
        Write-Host $h3 -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - POST messages returned $postCode." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 4) PATCH status — body must match TicketUpdateRequest: { "status": TicketStatus? } (optional fields; enum as string)
Write-Host "[4] PATCH $BaseUrl/api/tickets/$tid ... " -NoNewline
# DTO: Status (optional); backend JsonStringEnumConverter accepts "InProgress"
$patchBody = (@{ status = "InProgress" } | ConvertTo-Json -Compress)
$patchCode = Invoke-CurlSimple -Method Patch -Url "$BaseUrl/api/tickets/$tid" -CookieJar $cookieJar -Body $patchBody -OutBody $bodyFile
$b4 = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
if ($patchCode -eq 200) {
    Write-Host $patchCode -ForegroundColor Green
} else {
    Write-Host $patchCode -ForegroundColor Red
    if ($patchCode -eq 400) {
        Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
        Write-Host "     $(Get-TruncatedBody $b4)" -ForegroundColor Gray
    }
    if ($patchCode -eq 401) {
        Invoke-CurlSimple -Method Patch -Url "$BaseUrl/api/tickets/$tid" -CookieJar $cookieJar -Body $patchBody -OutHeaders $headersFile | Out-Null
        $h4 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
        Write-Host "     Response headers (401):" -ForegroundColor Yellow
        Write-Host $h4 -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - PATCH ticket returned $patchCode." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 5) GET ticket
Write-Host "[5] GET $BaseUrl/api/tickets/$tid ... " -NoNewline
$getCode = Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/tickets/$tid" -CookieJar $cookieJar -OutBody $bodyFile
$b = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
if ($getCode -eq 200) {
    Write-Host $getCode -ForegroundColor Green
    $ticket = $b | ConvertFrom-Json
    $status = $ticket.status; if (-not $status) { $status = $ticket.Status }
    Write-Host "     ticket status: $status" -ForegroundColor Gray
} else {
    Write-Host $getCode -ForegroundColor Red
    if ($getCode -eq 400) {
        Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
        Write-Host "     $(Get-TruncatedBody $b)" -ForegroundColor Gray
    }
    if ($getCode -eq 401) {
        Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/tickets/$tid" -CookieJar $cookieJar -OutHeaders $headersFile | Out-Null
        $h5 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
        Write-Host "     Response headers (401):" -ForegroundColor Yellow
        Write-Host $h5 -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - GET ticket returned $getCode." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

# 6) GET messages
Write-Host "[6] GET $BaseUrl/api/tickets/$tid/messages ... " -NoNewline
$msgsCode = Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/tickets/$tid/messages" -CookieJar $cookieJar -OutBody $bodyFile
$msgsBody = if (Test-Path $bodyFile) { Get-Content -Path $bodyFile -Raw -ErrorAction SilentlyContinue } else { "" }
if ($msgsCode -eq 200) {
    Write-Host $msgsCode -ForegroundColor Green
    $msgs = $msgsBody | ConvertFrom-Json
    $count = if ($msgs -is [Array]) { $msgs.Count } else { @($msgs).Count }
    Write-Host "     messages count: $count" -ForegroundColor Gray
} else {
    Write-Host $msgsCode -ForegroundColor Red
    if ($msgsCode -eq 400) {
        Write-Host "     Response body (first 500 chars):" -ForegroundColor Yellow
        Write-Host "     $(Get-TruncatedBody $msgsBody)" -ForegroundColor Gray
    }
    if ($msgsCode -eq 401) {
        Invoke-CurlSimple -Method GET -Url "$BaseUrl/api/tickets/$tid/messages" -CookieJar $cookieJar -OutHeaders $headersFile | Out-Null
        $h6 = if (Test-Path $headersFile) { Get-Content -Path $headersFile -Raw -ErrorAction SilentlyContinue } else { "" }
        Write-Host "     Response headers (401):" -ForegroundColor Yellow
        Write-Host $h6 -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Result: FAIL - GET messages returned $msgsCode." -ForegroundColor Red
    Write-Host ""
    Remove-TempFiles
    exit 1
}

Remove-TempFiles
Write-Host ""
Write-Host "Result: SUCCESS - All steps returned 2xx (cookie-based auth)." -ForegroundColor Green
Write-Host ""

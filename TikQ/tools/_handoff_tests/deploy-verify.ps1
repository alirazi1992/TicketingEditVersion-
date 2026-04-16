<#
.SYNOPSIS
  Verify IIS deploy: physical path vs health contentRoot, diag/build endpoint, create-category test.

.DESCRIPTION
  Prints:
  - IIS site "TikQ" physical path (from WebAdministration or appcmd)
  - /api/health contentRoot (from running app)
  - Match: Yes/No (paths normalized)
  - GET /api/diag/build status: 404 (not deployed), 401 (exists, needs auth), 200 (OK)
  - diag-build.ps1: Pass/Fail (authenticated build stamp check)
  - create-category.ps1: Pass/Fail (201 then 409)

  Run from repo root or tools\_handoff_tests. Use after deploy to confirm IIS is serving the new build.

.PARAMETER BaseUrl
  Base URL of the app (default http://localhost:8080).

.PARAMETER SiteName
  IIS site name (default TikQ).

.PARAMETER SkipCreateCategory
  If set, do not run create-category.ps1 (faster run).

.PARAMETER Email
  Admin email for diag-build and create-category.

.PARAMETER Password
  Admin password for diag-build and create-category.

.EXAMPLE
  .\deploy-verify.ps1
  .\deploy-verify.ps1 -BaseUrl "http://localhost:8080" -SiteName "TikQ"
#>
param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$SiteName = "TikQ",
    [switch]$SkipCreateCategory,
    [string]$Email = "admin@example.com",
    [string]$Password = "Admin123!"
)

$ErrorActionPreference = "Stop"
$BaseUrl = $BaseUrl.TrimEnd('/')
$scriptDir = $PSScriptRoot
if (-not $scriptDir) { $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path }

function Normalize-PathForCompare($p) {
    if ([string]::IsNullOrWhiteSpace($p)) { return "" }
    $expanded = [Environment]::ExpandEnvironmentVariables($p.Trim())
    try {
        return [System.IO.Path]::GetFullPath($expanded)
    } catch {
        return $expanded.TrimEnd('\', '/')
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Deploy verify: IIS vs running app" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BaseUrl:  $BaseUrl" -ForegroundColor White
Write-Host " SiteName: $SiteName" -ForegroundColor White
Write-Host ""

# 1) IIS physical path
$iisPath = $null
try {
    if (Get-Command Get-WebFilePath -ErrorAction SilentlyContinue) {
        $fp = Get-WebFilePath -PSPath "IIS:\Sites\$SiteName" -ErrorAction Stop
        if ($fp -and $fp.FullName) { $iisPath = Normalize-PathForCompare($fp.FullName) }
    }
} catch { }
if (-not $iisPath) {
    try {
        if (Get-Command Get-Website -ErrorAction SilentlyContinue) {
            $site = Get-Website -Name $SiteName -ErrorAction Stop
            $raw = $site.PhysicalPath; if (-not $raw) { $raw = $site.physicalPath }
            $iisPath = Normalize-PathForCompare($raw)
        }
    } catch { }
}
if (-not $iisPath -and (Test-Path "$env:SystemRoot\System32\inetsrv\appcmd.exe")) {
    try {
        # Root vdir holds the site's physical path
        $out = & "$env:SystemRoot\System32\inetsrv\appcmd.exe" list vdir "$SiteName/" /text:physicalPath 2>&1
        if ($out -and ($out -is [string]) -and (Test-Path $out)) { $iisPath = Normalize-PathForCompare($out.Trim()) }
        if (-not $iisPath) {
            $out2 = & "$env:SystemRoot\System32\inetsrv\appcmd.exe" list vdir "$SiteName/" 2>&1 | Out-String
            if ($out2 -match 'physicalPath:"([^"]+)"') { $iisPath = Normalize-PathForCompare($Matches[1]) }
        }
    } catch { }
}

if (-not $iisPath) {
    Write-Host "IIS physical path: (could not read - run as Admin or check site name)" -ForegroundColor Yellow
} else {
    Write-Host "IIS physical path: $iisPath" -ForegroundColor White
}

# 2) /api/health contentRoot
$healthRoot = $null
try {
    $health = Invoke-RestMethod -Uri "$BaseUrl/api/health" -Method GET -TimeoutSec 10
    $healthRoot = $health.contentRoot
    if (-not $healthRoot) { $healthRoot = $health.ContentRoot }
    $healthRoot = Normalize-PathForCompare($healthRoot)
    Write-Host "Health contentRoot: $healthRoot" -ForegroundColor White
} catch {
    Write-Host "Health contentRoot: (request failed: $($_.Exception.Message))" -ForegroundColor Red
}

# 3) Match
if ($iisPath -and $healthRoot) {
    $match = ($iisPath -eq $healthRoot)
    Write-Host "Match (IIS path == contentRoot): $match" -ForegroundColor $(if ($match) { "Green" } else { "Yellow" })
} else {
    Write-Host "Match: (skip - missing IIS path or contentRoot)" -ForegroundColor Gray
}
Write-Host ""

# 4) GET /api/diag/build (no auth) - 404 = not deployed, 401 = exists, 200 = ok
Write-Host "GET /api/diag/build (no auth): " -NoNewline
$diagCode = $null
try {
    $r = Invoke-WebRequest -Uri "$BaseUrl/api/diag/build" -Method GET -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    $diagCode = $r.StatusCode
} catch {
    if ($_.Exception.Response) { $diagCode = [int]$_.Exception.Response.StatusCode }
}
if ($null -eq $diagCode) {
    Write-Host "request failed" -ForegroundColor Red
} elseif ($diagCode -eq 404) {
    Write-Host "404 (endpoint not found - deploy likely not updated)" -ForegroundColor Red
} elseif ($diagCode -eq 401) {
    Write-Host "401 (endpoint exists, requires Admin auth)" -ForegroundColor Green
} elseif ($diagCode -eq 200) {
    Write-Host "200 OK" -ForegroundColor Green
} else {
    Write-Host "$diagCode" -ForegroundColor Yellow
}

# 5) diag-build.ps1 (authenticated)
Write-Host "diag-build.ps1 (auth): " -NoNewline
$diagScript = Join-Path $scriptDir "diag-build.ps1"
if (-not (Test-Path $diagScript)) {
    Write-Host "script not found" -ForegroundColor Yellow
} else {
    try {
        & $diagScript -BaseUrl $BaseUrl -Email $Email -Password $Password 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Pass" -ForegroundColor Green
        } else {
            Write-Host "Fail (exit $LASTEXITCODE)" -ForegroundColor Red
        }
    } catch {
        Write-Host "Fail ($_)" -ForegroundColor Red
    }
}

# 6) create-category.ps1
if (-not $SkipCreateCategory) {
    Write-Host "create-category.ps1: " -NoNewline
    $catScript = Join-Path $scriptDir "create-category.ps1"
    if (-not (Test-Path $catScript)) {
        Write-Host "script not found" -ForegroundColor Yellow
    } else {
        try {
            & $catScript -BaseUrl $BaseUrl -Email $Email -Password $Password 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Pass" -ForegroundColor Green
            } else {
                Write-Host "Fail (exit $LASTEXITCODE)" -ForegroundColor Red
            }
        } catch {
            Write-Host "Fail ($_)" -ForegroundColor Red
        }
    }
} else {
    Write-Host "create-category.ps1: skipped (-SkipCreateCategory)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done." -ForegroundColor Cyan
Write-Host ""

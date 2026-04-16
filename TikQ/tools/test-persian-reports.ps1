#!/usr/bin/env pwsh
<#
.SYNOPSIS
Test Persian (fa-IR) reporting endpoints

.DESCRIPTION
Tests all report download endpoints to ensure:
- Persian calendar dates (۱۴۰۳/۱۱/۱۲)
- Persian digits (۰۱۲۳۴۵۶۷۸۹)
- Persian headers
- UTF-8 with BOM encoding
- Proper authorization checks

.PARAMETER Token
Bearer token for authentication (get from browser localStorage)

.PARAMETER TechnicianUserId
Technician user ID (GUID) for supervisor report test

.EXAMPLE
.\test-persian-reports.ps1 -Token "your-jwt-token" -TechnicianUserId "guid-here"
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$Token = "",
    
    [Parameter(Mandatory=$false)]
    [string]$TechnicianUserId = "",
    
    [Parameter(Mandatory=$false)]
    [string]$ApiBase = "http://localhost:5000"
)

$ErrorActionPreference = "Continue"
$testResults = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$ExpectedStatus,
        [hashtable]$Headers = @{},
        [string]$OutputFile = ""
    )
    
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "TEST: $Name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "URL: $Url"
    
    try {
        $response = Invoke-WebRequest -Uri $Url -Headers $Headers -Method GET -OutFile $OutputFile -PassThru -ErrorAction Stop
        
        $status = $response.StatusCode
        $contentType = $response.Headers["Content-Type"]
        
        Write-Host "✅ Status: $status" -ForegroundColor Green
        Write-Host "✅ Content-Type: $contentType" -ForegroundColor Green
        
        if ($OutputFile -and (Test-Path $OutputFile)) {
            $fileSize = (Get-Item $OutputFile).Length
            Write-Host "✅ File saved: $OutputFile ($fileSize bytes)" -ForegroundColor Green
            
            # Check for Persian content if CSV
            if ($OutputFile -match "\.csv$") {
                $content = Get-Content $OutputFile -Raw -Encoding UTF8
                
                # Check for Persian digits
                if ($content -match '[۰-۹]') {
                    Write-Host "✅ Contains Persian digits (۰-۹)" -ForegroundColor Green
                } else {
                    Write-Host "❌ No Persian digits found!" -ForegroundColor Red
                }
                
                # Check for Persian headers
                if ($content -match 'شناسه|عنوان|وضعیت|تاریخ') {
                    Write-Host "✅ Contains Persian headers" -ForegroundColor Green
                } else {
                    Write-Host "❌ No Persian headers found!" -ForegroundColor Red
                }
                
                # Show first line (header)
                $firstLine = ($content -split "`n")[0]
                Write-Host "`nFirst line (header):" -ForegroundColor Yellow
                Write-Host $firstLine
            }
        }
        
        $testResults += @{
            Name = $Name
            Status = "PASS"
            StatusCode = $status
        }
        
        return $true
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "❌ Failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Status Code: $statusCode" -ForegroundColor Red
        
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "✅ Expected status code $ExpectedStatus" -ForegroundColor Green
            $testResults += @{
                Name = $Name
                Status = "PASS (Expected Failure)"
                StatusCode = $statusCode
            }
            return $true
        }
        
        $testResults += @{
            Name = $Name
            Status = "FAIL"
            StatusCode = $statusCode
            Error = $_.Exception.Message
        }
        
        return $false
    }
}

# Create output directory
$outputDir = ".\test-reports-output"
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

Write-Host "=====================================" -ForegroundColor Magenta
Write-Host "Persian (fa-IR) Reports Testing" -ForegroundColor Magenta
Write-Host "=====================================" -ForegroundColor Magenta
Write-Host "API Base: $ApiBase"
Write-Host "Output Dir: $outputDir"
Write-Host ""

# Test 1: No Auth (should return 401)
Write-Host "`n[Test 1] Authorization Required" -ForegroundColor Yellow
if (-not $TechnicianUserId) {
    Write-Host "⚠️  Skipping: No TechnicianUserId provided" -ForegroundColor Yellow
} else {
    $url = "$ApiBase/api/supervisor/technicians/$TechnicianUserId/report?format=csv"
    Test-Endpoint -Name "No Auth (401)" -Url $url -ExpectedStatus 401
}

# Test 2: Supervisor Technician Report with Auth
if ($Token -and $TechnicianUserId) {
    Write-Host "`n[Test 2] Supervisor Technician Report (Persian)" -ForegroundColor Yellow
    $headers = @{
        "Authorization" = "Bearer $Token"
    }
    $outputFile = "$outputDir\technician-report-$(Get-Date -Format 'yyyyMMdd-HHmm').csv"
    $url = "$ApiBase/api/supervisor/technicians/$TechnicianUserId/report?format=csv"
    Test-Endpoint -Name "Supervisor Report (CSV)" -Url $url -ExpectedStatus 200 -Headers $headers -OutputFile $outputFile
} else {
    Write-Host "`n[Test 2] Supervisor Technician Report" -ForegroundColor Yellow
    Write-Host "⚠️  Skipping: Token or TechnicianUserId not provided" -ForegroundColor Yellow
    Write-Host "   Provide: -Token 'your-token' -TechnicianUserId 'guid'" -ForegroundColor Gray
}

# Test 3: Unsupported Format (should return 400)
if ($Token -and $TechnicianUserId) {
    Write-Host "`n[Test 3] Unsupported Format (400)" -ForegroundColor Yellow
    $headers = @{
        "Authorization" = "Bearer $Token"
    }
    $url = "$ApiBase/api/supervisor/technicians/$TechnicianUserId/report?format=pdf"
    Test-Endpoint -Name "Unsupported Format (PDF)" -Url $url -ExpectedStatus 400 -Headers $headers
}

# Test 4: Admin Basic Report
if ($Token) {
    Write-Host "`n[Test 4] Admin Basic Report (Persian)" -ForegroundColor Yellow
    $headers = @{
        "Authorization" = "Bearer $Token"
    }
    $outputFile = "$outputDir\basic-report-$(Get-Date -Format 'yyyyMMdd-HHmm').csv"
    $url = "$ApiBase/api/admin/reports/basic?range=1w&format=csv"
    Test-Endpoint -Name "Admin Basic Report (CSV)" -Url $url -ExpectedStatus 200 -Headers $headers -OutputFile $outputFile
}

# Test 5: Admin Analytic Report
if ($Token) {
    Write-Host "`n[Test 5] Admin Analytic Report (Persian ZIP)" -ForegroundColor Yellow
    $headers = @{
        "Authorization" = "Bearer $Token"
    }
    $outputFile = "$outputDir\analytic-report-$(Get-Date -Format 'yyyyMMdd-HHmm').zip"
    $url = "$ApiBase/api/admin/reports/analytic?range=1w&format=zip"
    Test-Endpoint -Name "Admin Analytic Report (ZIP)" -Url $url -ExpectedStatus 200 -Headers $headers -OutputFile $outputFile
    
    # Extract and check CSV files
    if (Test-Path $outputFile) {
        Write-Host "`nExtracting ZIP to check CSV files..." -ForegroundColor Yellow
        $extractDir = "$outputDir\analytic-extracted-$(Get-Date -Format 'yyyyMMdd-HHmm')"
        Expand-Archive -Path $outputFile -DestinationPath $extractDir -Force
        
        $csvFiles = Get-ChildItem $extractDir -Filter "*.csv"
        Write-Host "✅ Found $($csvFiles.Count) CSV files:" -ForegroundColor Green
        foreach ($csv in $csvFiles) {
            Write-Host "   - $($csv.Name) ($($csv.Length) bytes)" -ForegroundColor Cyan
            
            # Check first line of each CSV
            $firstLine = (Get-Content $csv.FullName -Raw -Encoding UTF8 -TotalCount 1)
            if ($firstLine -match '[۰-۹]' -or $firstLine -match 'شناسه|عنوان|وضعیت|تاریخ|دسته|مشتری') {
                Write-Host "     ✅ Contains Persian content" -ForegroundColor Green
            } else {
                Write-Host "     ⚠️  No Persian content detected" -ForegroundColor Yellow
            }
        }
    }
}

# Summary
Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "TEST SUMMARY" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta

$passCount = ($testResults | Where-Object { $_.Status -match "PASS" }).Count
$totalCount = $testResults.Count

foreach ($result in $testResults) {
    $color = if ($result.Status -match "PASS") { "Green" } else { "Red" }
    $icon = if ($result.Status -match "PASS") { "✅" } else { "❌" }
    Write-Host "$icon $($result.Name): $($result.Status) (HTTP $($result.StatusCode))" -ForegroundColor $color
}

Write-Host "`nTotal: $passCount / $totalCount tests passed" -ForegroundColor $(if ($passCount -eq $totalCount) { "Green" } else { "Yellow" })

if (Test-Path $outputDir) {
    Write-Host "`nOutput files saved to: $outputDir" -ForegroundColor Cyan
    Get-ChildItem $outputDir -Recurse -File | ForEach-Object {
        Write-Host "  - $($_.FullName)" -ForegroundColor Gray
    }
}

Write-Host "`n========================================" -ForegroundColor Magenta
Write-Host "NEXT STEPS" -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
Write-Host "1. Open any CSV file in Excel"
Write-Host "2. Verify headers are in Persian: شناسه تیکت, عنوان, وضعیت, etc."
Write-Host "3. Verify dates are Persian calendar: ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲"
Write-Host "4. Verify digits are Persian: ۰۱۲۳۴۵۶۷۸۹"
Write-Host "5. Verify status labels are Persian: در حال انجام, حل شده, etc."
Write-Host ""

# Instructions for getting token
if (-not $Token) {
    Write-Host "`n========================================" -ForegroundColor Yellow
    Write-Host "HOW TO GET TOKEN" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "1. Open http://localhost:3000 in browser"
    Write-Host "2. Login as supervisor or admin"
    Write-Host "3. Open browser console (F12)"
    Write-Host "4. Run: localStorage.getItem('ticketing.auth.token')"
    Write-Host "5. Copy the token (without quotes)"
    Write-Host "6. Run script again:"
    Write-Host '   .\test-persian-reports.ps1 -Token "YOUR_TOKEN" -TechnicianUserId "GUID"'
    Write-Host ""
}

Write-Host "✅ Testing complete!" -ForegroundColor Green

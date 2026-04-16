#!/usr/bin/env pwsh
# Verify field definitions endpoints work correctly
# Usage: .\tools\verify-fields.ps1 [admin-token]

param(
    [string]$Token = "",
    [string]$BaseUrl = "http://localhost:5000",
    [int]$SubcategoryId = 1
)

$ErrorActionPreference = "Stop"

Write-Host "=== Field Definitions Endpoint Verification ===" -ForegroundColor Cyan

# If no token provided, try to login
if ([string]::IsNullOrWhiteSpace($Token)) {
    Write-Host "No token provided, attempting login..." -ForegroundColor Yellow
    
    $loginBody = @{
        email = "admin@test.com"
        password = "Admin123!"
    } | ConvertTo-Json
    
    try {
        $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" `
            -Method POST `
            -ContentType "application/json" `
            -Body $loginBody
        
        $Token = $loginResponse.token
        Write-Host "Login successful!" -ForegroundColor Green
    }
    catch {
        Write-Host "Login failed: $_" -ForegroundColor Red
        exit 1
    }
}

$headers = @{
    "Authorization" = "Bearer $Token"
    "Content-Type" = "application/json"
}

Write-Host "`n1. Testing GET /api/admin/subcategories/$SubcategoryId/fields..." -ForegroundColor Cyan
try {
    $getResponse = Invoke-RestMethod -Uri "$BaseUrl/api/admin/subcategories/$SubcategoryId/fields" `
        -Method GET `
        -Headers $headers
    
    Write-Host "   ✓ GET Success: Found $($getResponse.Count) fields" -ForegroundColor Green
    if ($getResponse.Count -gt 0) {
        $getResponse | ForEach-Object {
            Write-Host "      - $($_.label) ($($_.key)): $($_.type)" -ForegroundColor Gray
        }
    }
}
catch {
    Write-Host "   ✗ GET Failed: $_" -ForegroundColor Red
    Write-Host "   Status: $($_.Exception.Response.StatusCode.value__)" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`n2. Testing POST /api/admin/subcategories/$SubcategoryId/fields..." -ForegroundColor Cyan
$testFieldKey = "test_field_$(Get-Date -Format 'yyyyMMddHHmmss')"
$createBody = @{
    name = $testFieldKey
    label = "Test Field"
    key = $testFieldKey
    type = "Text"
    isRequired = $false
    defaultValue = "test default"
} | ConvertTo-Json

try {
    $postResponse = Invoke-RestMethod -Uri "$BaseUrl/api/admin/subcategories/$SubcategoryId/fields" `
        -Method POST `
        -Headers $headers `
        -Body $createBody
    
    Write-Host "   ✓ POST Success: Created field ID $($postResponse.id)" -ForegroundColor Green
    $createdFieldId = $postResponse.id
}
catch {
    Write-Host "   ✗ POST Failed: $_" -ForegroundColor Red
    if ($_.ErrorDetails.Message) {
        Write-Host "   Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

Write-Host "`n3. Verifying field persists (GET again)..." -ForegroundColor Cyan
try {
    $verifyResponse = Invoke-RestMethod -Uri "$BaseUrl/api/admin/subcategories/$SubcategoryId/fields" `
        -Method GET `
        -Headers $headers
    
    $foundField = $verifyResponse | Where-Object { $_.id -eq $createdFieldId }
    if ($foundField) {
        Write-Host "   ✓ Field persisted: Found field '$($foundField.label)' in list" -ForegroundColor Green
    }
    else {
        Write-Host "   ✗ Field not found in list!" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "   ✗ Verification GET Failed: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n4. Cleaning up test field (DELETE)..." -ForegroundColor Cyan
try {
    Invoke-RestMethod -Uri "$BaseUrl/api/admin/subcategories/$SubcategoryId/fields/$createdFieldId" `
        -Method DELETE `
        -Headers $headers | Out-Null
    
    Write-Host "   ✓ DELETE Success: Test field removed" -ForegroundColor Green
}
catch {
    Write-Host "   ⚠ DELETE Failed (field may remain): $_" -ForegroundColor Yellow
}

Write-Host "`n=== All Tests Passed! ===" -ForegroundColor Green

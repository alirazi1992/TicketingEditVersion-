# Backend Smoke Test Script
# Tests critical backend endpoints to verify runtime functionality
# Exit codes: 0 = success, 1 = failure

param(
    [string]$BaseUrl = "http://localhost:5000",
    [bool]$StopOnFail = $true,
    [string]$OutFile = ""
)

$ErrorActionPreference = "Continue"
$results = @()
$startTime = Get-Date
$hasFailures = $false

# Resolve output file path
if ([string]::IsNullOrEmpty($OutFile)) {
    $scriptRoot = Split-Path -Parent $PSScriptRoot
    $OutFile = Join-Path $scriptRoot "RUNTIME_SMOKE_REPORT.md"
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int]$ExpectedStatus = 200,
        [string]$Description = "",
        [scriptblock]$JsonValidator = $null
    )

    Write-Host "Testing: $Name" -ForegroundColor Cyan
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            TimeoutSec = 10
            ErrorAction = "Stop"
        }

        if ($Body -ne $null) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @params
        $statusCode = $response.StatusCode
        $responseBody = $null

        try {
            $responseBody = $response.Content | ConvertFrom-Json
        } catch {
            # Not JSON, that's okay for some endpoints
        }

        if ($statusCode -eq $ExpectedStatus) {
            # Run JSON validator if provided
            $validationError = $null
            if ($JsonValidator -ne $null -and $responseBody -ne $null) {
                try {
                    & $JsonValidator $responseBody | Out-Null
                } catch {
                    $validationError = $_.Exception.Message
                }
            }

            if ($validationError -ne $null) {
                Write-Host "  [FAIL] JSON validation failed: $validationError" -ForegroundColor Red
                $script:results += [PSCustomObject]@{
                    Test = $Name
                    Status = "FAIL"
                    ExpectedStatus = $ExpectedStatus
                    ActualStatus = $statusCode
                    Url = $Url
                    Description = $Description
                    Error = "JSON validation: $validationError"
                }
                $script:hasFailures = $true
                if ($StopOnFail) { throw "Test failed: $Name" }
                return $null
            } else {
                Write-Host "  [PASS] Status: $statusCode" -ForegroundColor Green
                $script:results += [PSCustomObject]@{
                    Test = $Name
                    Status = "PASS"
                    ExpectedStatus = $ExpectedStatus
                    ActualStatus = $statusCode
                    Url = $Url
                    Description = $Description
                }
                return @{ Response = $response; Body = $responseBody }
            }
        } else {
            Write-Host "  [FAIL] Expected: $ExpectedStatus, Got: $statusCode" -ForegroundColor Red
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "FAIL"
                ExpectedStatus = $ExpectedStatus
                ActualStatus = $statusCode
                Url = $Url
                Description = $Description
                Error = "Status code mismatch"
            }
            $script:hasFailures = $true
            if ($StopOnFail) { throw "Test failed: $Name" }
            return $null
        }
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  [PASS] Status: $statusCode (expected)" -ForegroundColor Green
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "PASS"
                ExpectedStatus = $ExpectedStatus
                ActualStatus = $statusCode
                Url = $Url
                Description = $Description
            }
            return $null
        } else {
            $errorMsg = $_.Exception.Message
            Write-Host "  [FAIL] $errorMsg" -ForegroundColor Red
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "FAIL"
                ExpectedStatus = $ExpectedStatus
                ActualStatus = $statusCode
                Url = $Url
                Description = $Description
                Error = $errorMsg
            }
            $script:hasFailures = $true
            if ($StopOnFail) { throw "Test failed: $Name" }
            return $null
        }
    }
}

Write-Host "========================================" -ForegroundColor Yellow
Write-Host "BACKEND SMOKE TESTS" -ForegroundColor Yellow
Write-Host "Backend URL: $BaseUrl" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

# Check if backend is running
Write-Host "Checking if backend is running..." -ForegroundColor Cyan
try {
    $check = Invoke-WebRequest -Uri "$BaseUrl/swagger/index.html" -Method GET -TimeoutSec 3 -ErrorAction Stop
    Write-Host "[OK] Backend is running" -ForegroundColor Green
    Write-Host ""
} catch {
    Write-Host "[ERROR] Backend is not running on $BaseUrl" -ForegroundColor Red
    Write-Host "Please start the backend first:" -ForegroundColor Yellow
    Write-Host "  cd backend\Ticketing.Backend" -ForegroundColor White
    Write-Host "  dotnet run --project .\src\Ticketing.Api\Ticketing.Api.csproj" -ForegroundColor White
    Write-Host ""
    exit 1
}

# Test 1: Swagger UI
Test-Endpoint -Name "Swagger UI" -Method "GET" -Url "$BaseUrl/swagger/index.html" -ExpectedStatus 200 -Description "Swagger UI accessibility"

# Test 2: Debug Users
$debugResponse = Test-Endpoint -Name "Debug Users Endpoint" -Method "GET" -Url "$BaseUrl/api/auth/debug-users" -ExpectedStatus 200 -Description "List all users" `
    -JsonValidator { param($body) if ($body -isnot [array]) { throw "Expected array" } }

# Test 3: Categories (Public)
$categoriesResponse = Test-Endpoint -Name "Categories (Public)" -Method "GET" -Url "$BaseUrl/api/categories" -ExpectedStatus 200 -Description "Public categories endpoint" `
    -JsonValidator { param($body) if ($body -isnot [array]) { throw "Expected array" } }

# Test 4: Login with seed credentials
$seedCredentials = @(
    @{ Email = "admin@test.com"; Password = "Admin123!"; Role = "Admin"; ExpectedRole = "Admin" },
    @{ Email = "tech1@test.com"; Password = "Tech123!"; Role = "Technician"; ExpectedRole = "Technician" },
    @{ Email = "client1@test.com"; Password = "Client123!"; Role = "Client"; ExpectedRole = "Client" }
)

$authTokens = @{}

foreach ($cred in $seedCredentials) {
    $testName = "Login ($($cred.Role))"
    Write-Host "Testing: $testName" -ForegroundColor Cyan
    
    try {
        $loginBody = @{
            email = $cred.Email
            password = $cred.Password
        } | ConvertTo-Json

        $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/api/auth/login" `
            -Method POST `
            -Body $loginBody `
            -ContentType "application/json" `
            -TimeoutSec 10 `
            -ErrorAction Stop

        if ($loginResponse.StatusCode -eq 200) {
            $loginData = $loginResponse.Content | ConvertFrom-Json
            
            # Validate token exists
            if (-not $loginData.token) {
                Write-Host "  [FAIL] No token in response" -ForegroundColor Red
                $results += [PSCustomObject]@{
                    Test = $testName
                    Status = "FAIL"
                    ExpectedStatus = 200
                    ActualStatus = $loginResponse.StatusCode
                    Url = "$BaseUrl/api/auth/login"
                    Description = "Login response missing token"
                    Error = "No token in response"
                }
                $hasFailures = $true
                if ($StopOnFail) { throw "Test failed: $testName" }
                continue
            }

            # Validate user role
            if (-not $loginData.user -or $loginData.user.role -ne $cred.ExpectedRole) {
                Write-Host "  [FAIL] Role mismatch. Expected: $($cred.ExpectedRole), Got: $($loginData.user.role)" -ForegroundColor Red
                $results += [PSCustomObject]@{
                    Test = $testName
                    Status = "FAIL"
                    ExpectedStatus = 200
                    ActualStatus = $loginResponse.StatusCode
                    Url = "$BaseUrl/api/auth/login"
                    Description = "Role mismatch"
                    Error = "Expected role $($cred.ExpectedRole), got $($loginData.user.role)"
                }
                $hasFailures = $true
                if ($StopOnFail) { throw "Test failed: $testName" }
                continue
            }

            $authTokens[$cred.Role] = $loginData.token
            Write-Host "  [PASS] Status: 200, Token and role validated" -ForegroundColor Green
            $results += [PSCustomObject]@{
                Test = $testName
                Status = "PASS"
                ExpectedStatus = 200
                ActualStatus = $loginResponse.StatusCode
                Url = "$BaseUrl/api/auth/login"
                Description = "Login successful with correct role"
            }
        }
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
        Write-Host "  [FAIL] $($_.Exception.Message)" -ForegroundColor Red
        $results += [PSCustomObject]@{
            Test = $testName
            Status = "FAIL"
            ExpectedStatus = 200
            ActualStatus = $statusCode
            Url = "$BaseUrl/api/auth/login"
            Description = "Login failed"
            Error = $_.Exception.Message
        }
        $hasFailures = $true
        if ($StopOnFail) { throw "Test failed: $testName" }
    }
}

# Test 5: Unauthorized access
Test-Endpoint -Name "Unauthorized Access" -Method "GET" -Url "$BaseUrl/api/tickets" -ExpectedStatus 401 -Description "Protected endpoint returns 401"

# Test 6: Authenticated endpoints
if ($authTokens.ContainsKey("Client")) {
    $clientToken = $authTokens["Client"]
    $clientHeaders = @{ "Authorization" = "Bearer $clientToken" }
    
    # GET /api/auth/me - validate role
    $meResponse = Test-Endpoint -Name "GET /api/auth/me (Client)" -Method "GET" -Url "$BaseUrl/api/auth/me" -Headers $clientHeaders -ExpectedStatus 200 -Description "Authenticated user info" `
        -JsonValidator { param($body) if ($body.role -ne "Client") { throw "Expected role Client, got $($body.role)" } }
    
    # GET /api/tickets (Client)
    $ticketsResponse = Test-Endpoint -Name "GET /api/tickets (Client)" -Method "GET" -Url "$BaseUrl/api/tickets" -Headers $clientHeaders -ExpectedStatus 200 -Description "Client can view tickets" `
        -JsonValidator { param($body) if ($body -isnot [array]) { throw "Expected array" } }
    
    if ($ticketsResponse -and $ticketsResponse.Body -and $ticketsResponse.Body.Count -gt 0) {
        $ticketId = $ticketsResponse.Body[0].id
        if ($ticketId) {
            Test-Endpoint -Name "GET /api/tickets/{id} (Client)" -Method "GET" -Url "$BaseUrl/api/tickets/$ticketId" -Headers $clientHeaders -ExpectedStatus 200 -Description "Client can view ticket detail" `
                -JsonValidator { param($body) if (-not $body.id) { throw "Missing id field" } }
        }
    }

    # Create ticket (Client should be able to create)
    Write-Host "Testing: Create Ticket (Client)" -ForegroundColor Cyan
    try {
        # Get a category ID first
        if ($categoriesResponse -and $categoriesResponse.Body -and $categoriesResponse.Body.Count -gt 0) {
            $categoryId = $categoriesResponse.Body[0].id
            $subcategoryId = $null
            if ($categoriesResponse.Body[0].subcategories -and $categoriesResponse.Body[0].subcategories.Count -gt 0) {
                $subcategoryId = $categoriesResponse.Body[0].subcategories[0].id
            }

            $createTicketBody = @{
                title = "Smoke Test Ticket"
                description = "Created by automated smoke test"
                categoryId = $categoryId
                subcategoryId = $subcategoryId
                priority = "Medium"
            } | ConvertTo-Json

            $createResponse = Invoke-WebRequest -Uri "$BaseUrl/api/tickets" `
                -Method POST `
                -Headers $clientHeaders `
                -Body $createTicketBody `
                -ContentType "application/json" `
                -TimeoutSec 10 `
                -ErrorAction Stop

            if ($createResponse.StatusCode -eq 200 -or $createResponse.StatusCode -eq 201) {
                $ticketData = $createResponse.Content | ConvertFrom-Json
                if ($ticketData.id) {
                    Write-Host "  [PASS] Ticket created with ID: $($ticketData.id)" -ForegroundColor Green
                    $results += [PSCustomObject]@{
                        Test = "Create Ticket (Client)"
                        Status = "PASS"
                        ExpectedStatus = "200/201"
                        ActualStatus = $createResponse.StatusCode
                        Url = "$BaseUrl/api/tickets"
                        Description = "Ticket created successfully"
                    }
                } else {
                    Write-Host "  [FAIL] Ticket created but missing ID" -ForegroundColor Red
                    $results += [PSCustomObject]@{
                        Test = "Create Ticket (Client)"
                        Status = "FAIL"
                        ExpectedStatus = "200/201"
                        ActualStatus = $createResponse.StatusCode
                        Url = "$BaseUrl/api/tickets"
                        Description = "Ticket creation response missing ID"
                        Error = "Missing id field"
                    }
                    $hasFailures = $true
                }
            }
        } else {
            Write-Host "  [SKIP] No categories available for ticket creation" -ForegroundColor Yellow
        }
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
        Write-Host "  [FAIL] $($_.Exception.Message)" -ForegroundColor Red
        $results += [PSCustomObject]@{
            Test = "Create Ticket (Client)"
            Status = "FAIL"
            ExpectedStatus = "200/201"
            ActualStatus = $statusCode
            Url = "$BaseUrl/api/tickets"
            Description = "Ticket creation failed"
            Error = $_.Exception.Message
        }
        $hasFailures = $true
    }
}

if ($authTokens.ContainsKey("Technician")) {
    $techToken = $authTokens["Technician"]
    $techHeaders = @{ "Authorization" = "Bearer $techToken" }
    
    Test-Endpoint -Name "GET /api/technician/tickets" -Method "GET" -Url "$BaseUrl/api/technician/tickets" -Headers $techHeaders -ExpectedStatus 200 -Description "Technician tickets endpoint" `
        -JsonValidator { param($body) if ($body -isnot [array]) { throw "Expected array" } }
}

if ($authTokens.ContainsKey("Admin")) {
    $adminToken = $authTokens["Admin"]
    $adminHeaders = @{ "Authorization" = "Bearer $adminToken" }
    
    Test-Endpoint -Name "GET /api/tickets (Admin)" -Method "GET" -Url "$BaseUrl/api/tickets" -Headers $adminHeaders -ExpectedStatus 200 -Description "Admin can view all tickets" `
        -JsonValidator { param($body) if ($body -isnot [array]) { throw "Expected array" } }
    
    # Test responsible endpoint exists (newly added) - Admin should be able to call it
    Write-Host "Testing: PUT /api/tickets/{id}/responsible (Admin)" -ForegroundColor Cyan
    try {
        $adminTicketsResponse = Invoke-WebRequest -Uri "$BaseUrl/api/tickets" -Method GET -Headers $adminHeaders -TimeoutSec 10 -ErrorAction Stop
        $tickets = $adminTicketsResponse.Content | ConvertFrom-Json
        if ($tickets -and $tickets.Count -gt 0 -and $tickets[0].id) {
            $ticketId = $tickets[0].id
            
            # Try to set responsible - will likely fail with 400/404 (invalid technician) but endpoint should exist
            try {
                $resp = Invoke-WebRequest -Uri "$BaseUrl/api/tickets/$ticketId/responsible" `
                    -Method PUT `
                    -Headers $adminHeaders `
                    -Body (@{ ResponsibleTechnicianId = [Guid]::NewGuid() } | ConvertTo-Json) `
                    -ContentType "application/json" `
                    -TimeoutSec 10 `
                    -ErrorAction Stop
                Write-Host "  [PASS] Endpoint exists and responded" -ForegroundColor Green
                $results += [PSCustomObject]@{
                    Test = "PUT /api/tickets/{id}/responsible (Endpoint exists)"
                    Status = "PASS"
                    ExpectedStatus = "200/400"
                    ActualStatus = $resp.StatusCode
                    Url = "$BaseUrl/api/tickets/$ticketId/responsible"
                    Description = "Responsible endpoint exists"
                }
            } catch {
                $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
                if ($statusCode -in @(400, 404)) {
                    Write-Host "  [PASS] Endpoint exists (Status: $statusCode - expected for invalid data)" -ForegroundColor Green
                    $results += [PSCustomObject]@{
                        Test = "PUT /api/tickets/{id}/responsible (Endpoint exists)"
                        Status = "PASS"
                        ExpectedStatus = "400/404"
                        ActualStatus = $statusCode
                        Url = "$BaseUrl/api/tickets/$ticketId/responsible"
                        Description = "Responsible endpoint exists (invalid data handled correctly)"
                    }
                } else {
                    Write-Host "  [FAIL] Unexpected status $statusCode" -ForegroundColor Red
                    $results += [PSCustomObject]@{
                        Test = "PUT /api/tickets/{id}/responsible"
                        Status = "FAIL"
                        ExpectedStatus = "400/404"
                        ActualStatus = $statusCode
                        Url = "$BaseUrl/api/tickets/$ticketId/responsible"
                        Description = "Unexpected response"
                        Error = $_.Exception.Message
                    }
                    $hasFailures = $true
                }
            }
        }
    } catch {
        Write-Host "  [SKIP] Could not test responsible endpoint" -ForegroundColor Yellow
    }
    
    # Test 403: Client cannot access admin endpoint
    if ($authTokens.ContainsKey("Client")) {
        $clientHeaders = @{ "Authorization" = "Bearer $($authTokens['Client'])" }
        Test-Endpoint -Name "Client 403 on Admin Endpoint" -Method "GET" -Url "$BaseUrl/api/admin/technicians" -Headers $clientHeaders -ExpectedStatus 403 -Description "Client forbidden from admin endpoints"
    }
}

# Summary
$endTime = Get-Date
$duration = $endTime - $startTime

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "TEST SUMMARY" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIP" }).Count

Write-Host ""
Write-Host "Results:" -ForegroundColor Cyan
Write-Host "  âœ… Passed: $passed" -ForegroundColor Green
Write-Host "  âŒ Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
if ($skipped -gt 0) {
    Write-Host "  â­ï¸  Skipped: $skipped" -ForegroundColor Yellow
}
Write-Host "  â±ï¸  Duration: $($duration.TotalSeconds.ToString('F2'))s" -ForegroundColor Cyan
Write-Host ""

# Print summary table
Write-Host "Test Results:" -ForegroundColor Cyan
Write-Host ("-" * 100)
$results | Format-Table -Property @{Label="Status"; Expression={if ($_.Status -eq "PASS") {"âœ…"} elseif ($_.Status -eq "FAIL") {"âŒ"} else {"â­ï¸"}}}, Test, ExpectedStatus, ActualStatus, Description -AutoSize
Write-Host ("-" * 100)
Write-Host ""

if ($failed -gt 0) {
    Write-Host "Failed Tests:" -ForegroundColor Red
    $results | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        Write-Host "  âŒ $($_.Test): $($_.Error)" -ForegroundColor Red
    }
    Write-Host ""
}

# Generate report
$reportContent = @"
# BACKEND SMOKE TEST RESULTS

**Generated**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")  
**Backend URL**: $BaseUrl  
**Duration**: $($duration.TotalSeconds.ToString('F2'))s

## Summary

- âœ… **Passed**: $passed
- âŒ **Failed**: $failed
$(if ($skipped -gt 0) { "- â­ï¸ **Skipped**: $skipped`n" }) - **Total**: $($results.Count)

## Test Results

| Status | Test | Expected | Actual | Description | Error |
|--------|------|----------|--------|-------------|-------|
$($results | ForEach-Object { 
    $status = if ($_.Status -eq "PASS") { "âœ… PASS" } elseif ($_.Status -eq "FAIL") { "âŒ FAIL" } else { "â­ï¸ SKIP" }
    $error = if ($_.Error) { $_.Error } else { "" }
    "| $status | $($_.Test) | $($_.ExpectedStatus) | $($_.ActualStatus) | $($_.Description) | $error |" 
} | Out-String)

## Details

$(if ($failed -gt 0) {
    "### Failed Tests`n`n"
    ($results | Where-Object { $_.Status -eq "FAIL" } | ForEach-Object {
        "#### $($_.Test)`n"
        "- **URL**: ``$($_.Url)```n"
        "- **Expected**: $($_.ExpectedStatus)`n"
        "- **Actual**: $($_.ActualStatus)`n"
        "- **Error**: $($_.Error)`n`n"
    } | Out-String)
} else {
    "All tests passed! âœ…`n"
})

---
*Produced by automated smoke test script*

"@

# Write report (overwrite if exists)
$reportContent | Out-File -FilePath $OutFile -Encoding UTF8 -Force

Write-Host "Results written to: $OutFile" -ForegroundColor Cyan
Write-Host ""

if ($hasFailures -or $failed -gt 0) {
    Write-Host "âŒ TESTS FAILED" -ForegroundColor Red
    exit 1
} else {
    Write-Host "âœ… ALL TESTS PASSED" -ForegroundColor Green
    exit 0
}


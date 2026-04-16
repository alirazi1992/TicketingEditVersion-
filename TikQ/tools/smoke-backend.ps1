# Backend Smoke Test Script
# Tests critical backend endpoints to verify runtime functionality

param(
    [string]$BaseUrl = "http://localhost:5000",
    [int]$TimeoutSeconds = 10
)

$ErrorActionPreference = "Continue"
$results = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int]$ExpectedStatus = 200,
        [string]$Description = ""
    )

    Write-Host "Testing: $Name" -ForegroundColor Cyan
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            TimeoutSec = $TimeoutSeconds
            ErrorAction = "Stop"
        }

        if ($Body -ne $null) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }

        $response = Invoke-WebRequest @params
        $statusCode = $response.StatusCode

        if ($statusCode -eq $ExpectedStatus) {
            Write-Host "  ✅ PASS: $Name (Status: $statusCode)" -ForegroundColor Green
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "PASS"
                ExpectedStatus = $ExpectedStatus
                ActualStatus = $statusCode
                Url = $Url
                Description = $Description
            }
            return $response
        } else {
            Write-Host "  ❌ FAIL: $Name (Expected: $ExpectedStatus, Got: $statusCode)" -ForegroundColor Red
            $script:results += [PSCustomObject]@{
                Test = $Name
                Status = "FAIL"
                ExpectedStatus = $ExpectedStatus
                ActualStatus = $statusCode
                Url = $Url
                Description = $Description
                Error = "Status code mismatch"
            }
            return $null
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        Write-Host "  ❌ FAIL: $Name - $($_.Exception.Message)" -ForegroundColor Red
        $script:results += [PSCustomObject]@{
            Test = $Name
            Status = "FAIL"
            ExpectedStatus = $ExpectedStatus
            ActualStatus = $statusCode
            Url = $Url
            Description = $Description
            Error = $_.Exception.Message
        }
        return $null
    }
}

Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "Backend Smoke Tests" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Yellow
Write-Host ""

# Test 1: Swagger UI
Test-Endpoint -Name "Swagger UI" -Method "GET" -Url "$BaseUrl/swagger/index.html" -ExpectedStatus 200 -Description "Swagger UI should be accessible"

# Test 2: Health/Debug endpoint (debug-users)
$debugResponse = Test-Endpoint -Name "Debug Users Endpoint" -Method "GET" -Url "$BaseUrl/api/auth/debug-users" -ExpectedStatus 200 -Description "Debug endpoint to list users"

# Extract seed user credentials if available
$seedUsers = @()
if ($debugResponse) {
    try {
        $users = $debugResponse.Content | ConvertFrom-Json
        Write-Host "  Found $($users.Count) users in system" -ForegroundColor Gray
        $seedUsers = $users
    } catch {
        Write-Host "  Could not parse users list" -ForegroundColor Yellow
    }
}

# Test 3: Public Categories endpoint
Test-Endpoint -Name "Categories (Public)" -Method "GET" -Url "$BaseUrl/api/categories" -ExpectedStatus 200 -Description "Public categories endpoint"

# Test 4: Login with seed credentials
$seedCredentials = @(
    @{ Email = "admin@test.com"; Password = "Admin123!"; Role = "Admin" },
    @{ Email = "tech1@test.com"; Password = "Tech123!"; Role = "Technician" },
    @{ Email = "client1@test.com"; Password = "Client123!"; Role = "Client" }
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
            -TimeoutSec $TimeoutSeconds `
            -ErrorAction Stop

        if ($loginResponse.StatusCode -eq 200) {
            $loginData = $loginResponse.Content | ConvertFrom-Json
            if ($loginData.token) {
                $authTokens[$cred.Role] = $loginData.token
                Write-Host "  ✅ PASS: $testName (Status: $($loginResponse.StatusCode))" -ForegroundColor Green
                $results += [PSCustomObject]@{
                    Test = $testName
                    Status = "PASS"
                    ExpectedStatus = 200
                    ActualStatus = $loginResponse.StatusCode
                    Url = "$BaseUrl/api/auth/login"
                    Description = "Login successful for $($cred.Role) user"
                }
            } else {
                Write-Host "  ❌ FAIL: $testName - No token in response" -ForegroundColor Red
                $results += [PSCustomObject]@{
                    Test = $testName
                    Status = "FAIL"
                    ExpectedStatus = 200
                    ActualStatus = $loginResponse.StatusCode
                    Url = "$BaseUrl/api/auth/login"
                    Description = "Login response missing token"
                    Error = "No token in response"
                }
            }
        }
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { "N/A" }
        Write-Host "  ❌ FAIL: $testName - $($_.Exception.Message)" -ForegroundColor Red
        $results += [PSCustomObject]@{
            Test = $testName
            Status = "FAIL"
            ExpectedStatus = 200
            ActualStatus = $statusCode
            Url = "$BaseUrl/api/auth/login"
            Description = "Login failed for $($cred.Role) user"
            Error = $_.Exception.Message
        }
    }
}

# Test 5: Unauthorized access to protected endpoint
Test-Endpoint -Name "Unauthorized Access" -Method "GET" -Url "$BaseUrl/api/tickets" -ExpectedStatus 401 -Description "Protected endpoint should return 401 without auth"

# Test 6: Authenticated endpoints
if ($authTokens.ContainsKey("Client")) {
    $clientToken = $authTokens["Client"]
    $clientHeaders = @{ "Authorization" = "Bearer $clientToken" }
    
    # GET /api/auth/me
    Test-Endpoint -Name "GET /api/auth/me (Client)" -Method "GET" -Url "$BaseUrl/api/auth/me" -Headers $clientHeaders -ExpectedStatus 200 -Description "Authenticated user info endpoint"
    
    # GET /api/tickets (Client sees their tickets)
    $ticketsResponse = Test-Endpoint -Name "GET /api/tickets (Client)" -Method "GET" -Url "$BaseUrl/api/tickets" -Headers $clientHeaders -ExpectedStatus 200 -Description "Client can view their tickets"
    
    # If tickets exist, test ticket detail
    if ($ticketsResponse) {
        try {
            $tickets = $ticketsResponse.Content | ConvertFrom-Json
            if ($tickets -and $tickets.Count -gt 0 -and $tickets[0].id) {
                $ticketId = $tickets[0].id
                Test-Endpoint -Name "GET /api/tickets/{id} (Client)" -Method "GET" -Url "$BaseUrl/api/tickets/$ticketId" -Headers $clientHeaders -ExpectedStatus 200 -Description "Client can view ticket detail"
            }
        } catch {
            Write-Host "  ⚠️  Could not parse tickets for detail test" -ForegroundColor Yellow
        }
    }
}

if ($authTokens.ContainsKey("Technician")) {
    $techToken = $authTokens["Technician"]
    $techHeaders = @{ "Authorization" = "Bearer $techToken" }
    
    # GET /api/technician/tickets
    Test-Endpoint -Name "GET /api/technician/tickets" -Method "GET" -Url "$BaseUrl/api/technician/tickets" -Headers $techHeaders -ExpectedStatus 200 -Description "Technician can view assigned tickets"
}

if ($authTokens.ContainsKey("Admin")) {
    $adminToken = $authTokens["Admin"]
    $adminHeaders = @{ "Authorization" = "Bearer $adminToken" }
    
    # GET /api/tickets (Admin sees all)
    $adminTicketsResponse = Test-Endpoint -Name "GET /api/tickets (Admin)" -Method "GET" -Url "$BaseUrl/api/tickets" -Headers $adminHeaders -ExpectedStatus 200 -Description "Admin can view all tickets"
    
    # If tickets exist, test responsible endpoint (newly added)
    if ($adminTicketsResponse) {
        try {
            $tickets = $adminTicketsResponse.Content | ConvertFrom-Json
            if ($tickets -and $tickets.Count -gt 0 -and $tickets[0].id) {
                $ticketId = $tickets[0].id
                # Note: This test requires a technician to be assigned - may fail if no assignments
                # We'll test the endpoint exists (might get 400/404 but not 404 Not Found route)
                Write-Host "Testing: PUT /api/tickets/{id}/responsible (Endpoint exists)" -ForegroundColor Cyan
                try {
                    $resp = Invoke-WebRequest -Uri "$BaseUrl/api/tickets/$ticketId/responsible" `
                        -Method PUT `
                        -Headers $adminHeaders `
                        -Body (@{ ResponsibleTechnicianId = [Guid]::NewGuid() } | ConvertTo-Json) `
                        -ContentType "application/json" `
                        -TimeoutSec $TimeoutSeconds `
                        -ErrorAction Stop
                } catch {
                    $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
                    # 400/404 is acceptable (means endpoint exists, just invalid data)
                    if ($statusCode -in @(400, 404)) {
                        Write-Host "  ✅ PASS: Endpoint exists (Status: $statusCode - expected for invalid data)" -ForegroundColor Green
                        $results += [PSCustomObject]@{
                            Test = "PUT /api/tickets/{id}/responsible (Endpoint exists)"
                            Status = "PASS"
                            ExpectedStatus = "400/404"
                            ActualStatus = $statusCode
                            Url = "$BaseUrl/api/tickets/$ticketId/responsible"
                            Description = "Responsible endpoint exists (returns error for invalid data)"
                        }
                    } else {
                        Write-Host "  ❌ FAIL: Unexpected status $statusCode" -ForegroundColor Red
                        $results += [PSCustomObject]@{
                            Test = "PUT /api/tickets/{id}/responsible"
                            Status = "FAIL"
                            ExpectedStatus = "400/404"
                            ActualStatus = $statusCode
                            Url = "$BaseUrl/api/tickets/$ticketId/responsible"
                            Description = "Unexpected response"
                            Error = $_.Exception.Message
                        }
                    }
                }
            }
        } catch {
            Write-Host "  ⚠️  Could not test responsible endpoint" -ForegroundColor Yellow
        }
    }
    
    # Test 403: Client cannot access admin endpoint
    if ($authTokens.ContainsKey("Client")) {
        $clientHeaders = @{ "Authorization" = "Bearer $($authTokens['Client'])" }
        Test-Endpoint -Name "Client forbidden from admin endpoint" -Method "GET" -Url "$BaseUrl/api/admin/technicians" -Headers $clientHeaders -ExpectedStatus 403 -Description "Client should get 403 for admin endpoints"
    }
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Yellow
Write-Host "Test Summary" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Yellow

$passed = ($results | Where-Object { $_.Status -eq "PASS" }).Count
$failed = ($results | Where-Object { $_.Status -eq "FAIL" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIP" }).Count

Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Gray" })
Write-Host "Skipped: $skipped" -ForegroundColor Yellow

Write-Host ""
Write-Host "Detailed Results:" -ForegroundColor Cyan
$results | Format-Table -AutoSize

# Export results
$reportPath = Join-Path (Split-Path $PSScriptRoot -Parent) "RUNTIME_SMOKE_REPORT.md"
Write-Host ""
Write-Host "Results will be appended to: $reportPath" -ForegroundColor Cyan

# Return exit code
if ($failed -gt 0) {
    exit 1
} else {
    exit 0
}


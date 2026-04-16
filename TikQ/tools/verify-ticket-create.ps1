# tools/verify-ticket-create.ps1
# Verification script to test ticket creation and visibility across roles

$ErrorActionPreference = "Stop"

Write-Host "=== Ticket Creation Verification Script ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$API_BASE_URL = "http://localhost:5000"
$CLIENT_EMAIL = "client1@test.com"
$CLIENT_PASSWORD = "Client123!"
$TECH_EMAIL = "tech1@test.com"
$TECH_PASSWORD = "Tech123!"
$ADMIN_EMAIL = "admin@test.com"
$ADMIN_PASSWORD = "Admin123!"

# Helper function to make API requests
function Invoke-ApiRequest {
    param(
        [string]$Method,
        [string]$Url,
        [hashtable]$Headers = @{},
        [object]$Body = $null
    )
    
    $params = @{
        Method = $Method
        Uri = $Url
        Headers = $Headers
        ContentType = "application/json"
        ErrorAction = "Stop"
    }
    
    if ($Body) {
        $params.Body = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-RestMethod @params
        return @{
            Success = $true
            Data = $response
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $null
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd() | ConvertFrom-Json
        } catch {
            $errorBody = $_.Exception.Message
        }
        return @{
            Success = $false
            StatusCode = $statusCode
            Error = $errorBody
        }
    }
}

# Step 1: Login as client
Write-Host "Step 1: Logging in as client..." -ForegroundColor Yellow
$loginResult = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = $CLIENT_EMAIL
    password = $CLIENT_PASSWORD
}

if (-not $loginResult.Success) {
    Write-Host "FAILED: Client login failed" -ForegroundColor Red
    Write-Host "Error: $($loginResult.Error)" -ForegroundColor Red
    exit 1
}

$clientToken = $loginResult.Data.token
$clientUserId = $loginResult.Data.user.id
Write-Host "✓ Client logged in. UserId: $clientUserId" -ForegroundColor Green
Write-Host ""

# Step 2: Get categories
Write-Host "Step 2: Fetching categories..." -ForegroundColor Yellow
$categoriesResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/categories" -Headers @{
    Authorization = "Bearer $clientToken"
}

if (-not $categoriesResult.Success) {
    Write-Host "FAILED: Could not fetch categories" -ForegroundColor Red
    exit 1
}

$categories = $categoriesResult.Data
if ($categories.Count -eq 0) {
    Write-Host "FAILED: No categories found" -ForegroundColor Red
    exit 1
}

$category = $categories[0]
$subcategoryId = $null
if ($category.subcategories -and $category.subcategories.Count -gt 0) {
    $subcategoryId = $category.subcategories[0].id
}

Write-Host "✓ Found category: $($category.name) (ID: $($category.id))" -ForegroundColor Green
if ($subcategoryId) {
    Write-Host "✓ Using subcategory ID: $subcategoryId" -ForegroundColor Green
}
Write-Host ""

# Step 3: Create ticket
Write-Host "Step 3: Creating ticket..." -ForegroundColor Yellow
$ticketTitle = "Test Ticket $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$ticketPayload = @{
    title = $ticketTitle
    description = "This is a test ticket created by the verification script"
    categoryId = $category.id
    priority = "Medium"
}

if ($subcategoryId) {
    $ticketPayload.subcategoryId = $subcategoryId
}

$createResult = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/tickets" -Headers @{
    Authorization = "Bearer $clientToken"
} -Body $ticketPayload

if (-not $createResult.Success) {
    Write-Host "FAILED: Ticket creation failed" -ForegroundColor Red
    Write-Host "Status: $($createResult.StatusCode)" -ForegroundColor Red
    Write-Host "Error: $($createResult.Error | ConvertTo-Json -Depth 5)" -ForegroundColor Red
    exit 1
}

$createdTicket = $createResult.Data
$ticketId = $createdTicket.id
Write-Host "✓ Ticket created successfully!" -ForegroundColor Green
Write-Host "  Ticket ID: $ticketId" -ForegroundColor Cyan
Write-Host "  Title: $ticketTitle" -ForegroundColor Cyan
Write-Host "  Status: $($createdTicket.status)" -ForegroundColor Cyan
Write-Host "  CreatedByUserId: $($createdTicket.createdByUserId)" -ForegroundColor Cyan
Write-Host ""

# Step 4: Verify ticket appears in client's list
Write-Host "Step 4: Verifying ticket appears in client's ticket list..." -ForegroundColor Yellow
$clientTicketsResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{
    Authorization = "Bearer $clientToken"
}

if (-not $clientTicketsResult.Success) {
    Write-Host "FAILED: Could not fetch client tickets" -ForegroundColor Red
    exit 1
}

$clientTickets = $clientTicketsResult.Data
$foundInClientList = $clientTickets | Where-Object { $_.id -eq $ticketId }
if ($foundInClientList) {
    Write-Host "✓ Ticket found in client's list" -ForegroundColor Green
} else {
    Write-Host "FAILED: Ticket NOT found in client's list" -ForegroundColor Red
    Write-Host "  Client has $($clientTickets.Count) tickets total" -ForegroundColor Yellow
    Write-Host "  Looking for ticket ID: $ticketId" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 5: Login as technician and verify
Write-Host "Step 5: Logging in as technician and checking ticket visibility..." -ForegroundColor Yellow
$techLoginResult = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = $TECH_EMAIL
    password = $TECH_PASSWORD
}

if (-not $techLoginResult.Success) {
    Write-Host "WARNING: Could not login as technician (skipping technician check)" -ForegroundColor Yellow
} else {
    $techToken = $techLoginResult.Data.token
    $techTicketsResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{
        Authorization = "Bearer $techToken"
    }
    
    if ($techTicketsResult.Success) {
        $techTickets = $techTicketsResult.Data
        $foundInTechList = $techTickets | Where-Object { $_.id -eq $ticketId }
        if ($foundInTechList) {
            Write-Host "✓ Ticket found in technician's list (assigned or visible)" -ForegroundColor Green
        } else {
            Write-Host "⚠ Ticket NOT in technician's list (may be unassigned - this is OK if no auto-assignment)" -ForegroundColor Yellow
        }
    } else {
        Write-Host "WARNING: Could not fetch technician tickets" -ForegroundColor Yellow
    }
}
Write-Host ""

# Step 6: Login as admin and verify
Write-Host "Step 6: Logging in as admin and checking ticket visibility..." -ForegroundColor Yellow
$adminLoginResult = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = $ADMIN_EMAIL
    password = $ADMIN_PASSWORD
}

if (-not $adminLoginResult.Success) {
    Write-Host "FAILED: Could not login as admin" -ForegroundColor Red
    exit 1
}

$adminToken = $adminLoginResult.Data.token
$adminTicketsResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{
    Authorization = "Bearer $adminToken"
}

if (-not $adminTicketsResult.Success) {
    Write-Host "FAILED: Could not fetch admin tickets" -ForegroundColor Red
    exit 1
}

$adminTickets = $adminTicketsResult.Data
$foundInAdminList = $adminTickets | Where-Object { $_.id -eq $ticketId }
if ($foundInAdminList) {
    Write-Host "✓ Ticket found in admin's list" -ForegroundColor Green
} else {
    Write-Host "FAILED: Ticket NOT found in admin's list" -ForegroundColor Red
    Write-Host "  Admin has $($adminTickets.Count) tickets total" -ForegroundColor Yellow
    Write-Host "  Looking for ticket ID: $ticketId" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Summary
Write-Host "=== Verification Summary ===" -ForegroundColor Cyan
Write-Host "✓ Ticket created successfully" -ForegroundColor Green
Write-Host "✓ Ticket visible to client" -ForegroundColor Green
Write-Host "✓ Ticket visible to admin" -ForegroundColor Green
Write-Host ""
Write-Host "All checks passed! Ticket creation and visibility are working correctly." -ForegroundColor Green
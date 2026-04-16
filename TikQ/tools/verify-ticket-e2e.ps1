# tools/verify-ticket-e2e.ps1
# End-to-end verification script for ticket creation and listing

$ErrorActionPreference = "Stop"

Write-Host "=== Ticket E2E Verification Script ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$API_BASE_URL = "http://localhost:5000"
$CLIENT_EMAIL = "client1@test.com"
$CLIENT_PASSWORD = "Client123!"
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
            StatusCode = 200
        }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $null
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction SilentlyContinue
            if (-not $errorBody) {
                $errorBody = $reader.ReadToEnd()
            }
        } catch {
            $errorBody = $_.Exception.Message
        }
        return @{
            Success = $false
            StatusCode = $statusCode
            Error = $errorBody
            Exception = $_.Exception
        }
    }
}

# Step 1: Check health endpoint
Write-Host "Step 1: Checking backend health..." -ForegroundColor Yellow
$healthResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/health"

if (-not $healthResult.Success) {
    Write-Host "FAILED: Backend health check failed" -ForegroundColor Red
    Write-Host "Status: $($healthResult.StatusCode)" -ForegroundColor Red
    Write-Host "Error: $($healthResult.Error | ConvertTo-Json -Depth 3)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please ensure the backend is running:" -ForegroundColor Yellow
    Write-Host "  cd backend\Ticketing.Backend" -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    exit 1
}

Write-Host "✓ Backend is healthy" -ForegroundColor Green
if ($healthResult.Data.canConnectToDb) {
    Write-Host "✓ Database connection: OK" -ForegroundColor Green
} else {
    Write-Host "⚠ Database connection: FAILED" -ForegroundColor Yellow
}
Write-Host ""

# Step 2: Login as client
Write-Host "Step 2: Logging in as client..." -ForegroundColor Yellow
$loginResult = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = $CLIENT_EMAIL
    password = $CLIENT_PASSWORD
}

if (-not $loginResult.Success) {
    Write-Host "FAILED: Client login failed" -ForegroundColor Red
    Write-Host "Status: $($loginResult.StatusCode)" -ForegroundColor Red
    Write-Host "Error: $($loginResult.Error | ConvertTo-Json -Depth 3)" -ForegroundColor Red
    exit 1
}

$clientToken = $loginResult.Data.token
$clientUserId = $loginResult.Data.user.id
Write-Host "✓ Client logged in. UserId: $clientUserId" -ForegroundColor Green
Write-Host ""

# Step 3: Get categories
Write-Host "Step 3: Fetching categories..." -ForegroundColor Yellow
$categoriesResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/categories" -Headers @{
    Authorization = "Bearer $clientToken"
}

if (-not $categoriesResult.Success) {
    Write-Host "FAILED: Could not fetch categories" -ForegroundColor Red
    Write-Host "Status: $($categoriesResult.StatusCode)" -ForegroundColor Red
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

# Step 4: Get initial ticket count (as client)
Write-Host "Step 4: Getting initial ticket count (as client)..." -ForegroundColor Yellow
$initialTicketsResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{
    Authorization = "Bearer $clientToken"
}

if (-not $initialTicketsResult.Success) {
    Write-Host "FAILED: Could not fetch initial tickets" -ForegroundColor Red
    Write-Host "Status: $($initialTicketsResult.StatusCode)" -ForegroundColor Red
    Write-Host "Error: $($initialTicketsResult.Error | ConvertTo-Json -Depth 5)" -ForegroundColor Red
    Write-Host ""
    Write-Host "This indicates a problem with GET /api/tickets endpoint." -ForegroundColor Yellow
    Write-Host "Check backend logs for exceptions." -ForegroundColor Yellow
    exit 1
}

$initialTickets = $initialTicketsResult.Data
$initialCount = if ($initialTickets) { $initialTickets.Count } else { 0 }
Write-Host "✓ Initial ticket count: $initialCount" -ForegroundColor Green
Write-Host ""

# Step 5: Create ticket
Write-Host "Step 5: Creating ticket..." -ForegroundColor Yellow
$ticketTitle = "E2E Test Ticket $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$ticketPayload = @{
    title = $ticketTitle
    description = "This is a test ticket created by the E2E verification script"
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
    Write-Host ""
    Write-Host "This indicates a problem with POST /api/tickets endpoint." -ForegroundColor Yellow
    Write-Host "Check backend logs for exceptions during ticket creation." -ForegroundColor Yellow
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

# Step 6: Verify ticket appears in client's list
Write-Host "Step 6: Verifying ticket appears in client's ticket list..." -ForegroundColor Yellow
Start-Sleep -Seconds 1  # Small delay to ensure DB commit

$clientTicketsResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{
    Authorization = "Bearer $clientToken"
}

if (-not $clientTicketsResult.Success) {
    Write-Host "FAILED: Could not fetch client tickets" -ForegroundColor Red
    Write-Host "Status: $($clientTicketsResult.StatusCode)" -ForegroundColor Red
    exit 1
}

$clientTickets = $clientTicketsResult.Data
$foundInClientList = $clientTickets | Where-Object { $_.id -eq $ticketId }
if ($foundInClientList) {
    Write-Host "✓ Ticket found in client's list" -ForegroundColor Green
    Write-Host "  Client now has $($clientTickets.Count) tickets (was $initialCount)" -ForegroundColor Cyan
} else {
    Write-Host "FAILED: Ticket NOT found in client's list" -ForegroundColor Red
    Write-Host "  Client has $($clientTickets.Count) tickets total" -ForegroundColor Yellow
    Write-Host "  Looking for ticket ID: $ticketId" -ForegroundColor Yellow
    Write-Host "  Available ticket IDs: $($clientTickets.id -join ', ')" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 7: Login as admin and verify
Write-Host "Step 7: Logging in as admin and checking ticket visibility..." -ForegroundColor Yellow
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
    Write-Host "Status: $($adminTicketsResult.StatusCode)" -ForegroundColor Red
    Write-Host "Error: $($adminTicketsResult.Error | ConvertTo-Json -Depth 3)" -ForegroundColor Red
    exit 1
}

$adminTickets = $adminTicketsResult.Data
$foundInAdminList = $adminTickets | Where-Object { $_.id -eq $ticketId }
if ($foundInAdminList) {
    Write-Host "✓ Ticket found in admin's list" -ForegroundColor Green
    Write-Host "  Admin has $($adminTickets.Count) tickets total" -ForegroundColor Cyan
} else {
    Write-Host "FAILED: Ticket NOT found in admin list" -ForegroundColor Red
    Write-Host "  Admin has $($adminTickets.Count) tickets total" -ForegroundColor Yellow
    Write-Host "  Looking for ticket ID: $ticketId" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 8: Verify ticket count in database (optional diagnostic)
Write-Host "Step 8: Checking ticket count in database (diagnostic)..." -ForegroundColor Yellow
# Note: /api/debug/tickets/count is accessible without auth in dev mode
$countResult = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/debug/tickets/count"

if ($countResult.Success) {
    $dbCount = $countResult.Data.totalCount
    Write-Host "✓ Database contains $dbCount tickets total" -ForegroundColor Green
    if ($dbCount -gt 0) {
        Write-Host "  Status breakdown:" -ForegroundColor Cyan
        $countResult.Data.byStatus | ForEach-Object {
            Write-Host "    $($_.Status): $($_.Count)" -ForegroundColor Gray
        }
    }
} else {
    Write-Host "⚠ Could not fetch ticket count (diagnostic endpoint may not be available)" -ForegroundColor Yellow
}
Write-Host ""

# Summary
Write-Host "=== Verification Summary ===" -ForegroundColor Cyan
Write-Host "✓ Backend health check passed" -ForegroundColor Green
Write-Host "✓ Ticket created successfully (ID: $ticketId)" -ForegroundColor Green
Write-Host "✓ Ticket visible to client" -ForegroundColor Green
Write-Host "✓ Ticket visible to admin" -ForegroundColor Green
Write-Host ""
Write-Host "All checks passed! Ticket creation and listing are working correctly." -ForegroundColor Green
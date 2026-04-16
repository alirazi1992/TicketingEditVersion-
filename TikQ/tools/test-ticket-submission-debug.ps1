# verify-ticket-creation.ps1
# PHASE 4: End-to-end verification of ticket creation

$ErrorActionPreference = "Continue"

Write-Host "=== PHASE 4: Ticket Creation Verification ===" -ForegroundColor Cyan
Write-Host ""

$API_BASE_URL = "http://localhost:5000"

function Invoke-ApiRequest {
    param([string]$Method, [string]$Url, [hashtable]$Headers = @{}, [object]$Body = $null)
    try {
        $params = @{ Method = $Method; Uri = $Url; Headers = $Headers; ErrorAction = "Stop" }
        if ($Body) {
            $params.ContentType = "application/json"
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        $response = Invoke-RestMethod @params
        return @{ Success = $true; Data = $response; StatusCode = 200 }
    } catch {
        $statusCode = if ($_.Exception.Response) { $_.Exception.Response.StatusCode.value__ } else { 0 }
        $errorBody = $null
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd() | ConvertFrom-Json
        } catch {
            try {
                $errorBody = $_.Exception.Message
            } catch {
                $errorBody = "Unknown error"
            }
        }
        return @{ Success = $false; StatusCode = $statusCode; Error = $errorBody }
    }
}

# Step 1: Check backend
Write-Host "Step 1: Checking backend..." -ForegroundColor Yellow
$health = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/health"
if (-not $health.Success) {
    Write-Host "[FAIL] Backend not running" -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Backend is running" -ForegroundColor Green
Write-Host ""

# Step 2: Login as client
Write-Host "Step 2: Logging in as client..." -ForegroundColor Yellow
$login = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = "client1@test.com"
    password = "Client123!"
}
if (-not $login.Success) {
    Write-Host "[FAIL] Login failed: $($login.Error)" -ForegroundColor Red
    exit 1
}
$token = $login.Data.token
$userId = $login.Data.user.id
Write-Host "[OK] Logged in as $($login.Data.user.email) (ID: $userId)" -ForegroundColor Green
Write-Host ""

# Step 3: Get categories
Write-Host "Step 3: Fetching categories..." -ForegroundColor Yellow
$cats = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/categories" -Headers @{ Authorization = "Bearer $token" }
if (-not $cats.Success) {
    Write-Host "[FAIL] Could not fetch categories: $($cats.Error)" -ForegroundColor Red
    exit 1
}
$categories = $cats.Data
if ($categories.Count -eq 0) {
    Write-Host "[WARN] No categories found" -ForegroundColor Yellow
    exit 1
}
Write-Host "[OK] Found $($categories.Count) categories" -ForegroundColor Green

# Pick first category with subcategory if available
$category = $categories[0]
$subcategoryId = $null
if ($category.subcategories -and $category.subcategories.Count -gt 0) {
    $subcategoryId = $category.subcategories[0].id
    Write-Host "  Using category: $($category.name) (ID: $($category.id))" -ForegroundColor Gray
    Write-Host "  Using subcategory: $($category.subcategories[0].name) (ID: $subcategoryId)" -ForegroundColor Gray
} else {
    Write-Host "  Using category: $($category.name) (ID: $($category.id))" -ForegroundColor Gray
}
Write-Host ""

# Step 4: Get initial ticket count
Write-Host "Step 4: Getting initial ticket count..." -ForegroundColor Yellow
$ticketsBefore = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{ Authorization = "Bearer $token" }
if (-not $ticketsBefore.Success) {
    Write-Host "[WARN] Could not fetch tickets: $($ticketsBefore.Error)" -ForegroundColor Yellow
    $countBefore = 0
} else {
    $countBefore = $ticketsBefore.Data.Count
    Write-Host "[OK] Client has $countBefore tickets" -ForegroundColor Green
}
Write-Host ""

# Step 5: Create a test ticket
Write-Host "Step 5: Creating test ticket..." -ForegroundColor Yellow
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$ticketPayload = @{
    title = "Test Ticket $timestamp"
    description = "This is a test ticket created by the verification script at $timestamp"
    categoryId = $category.id
    priority = "Medium"
}
if ($subcategoryId) {
    $ticketPayload.subcategoryId = $subcategoryId
}

Write-Host "Payload:" -ForegroundColor Cyan
$ticketPayload | ConvertTo-Json | Write-Host -ForegroundColor Gray
Write-Host ""

$create = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/tickets" -Headers @{ Authorization = "Bearer $token" } -Body $ticketPayload

if (-not $create.Success) {
    Write-Host "[FAIL] Ticket creation failed" -ForegroundColor Red
    Write-Host "  Status Code: $($create.StatusCode)" -ForegroundColor Yellow
    Write-Host "  Error: $($create.Error | ConvertTo-Json -Depth 5)" -ForegroundColor Yellow
    exit 1
}

$createdTicket = $create.Data
Write-Host "[OK] Ticket created successfully!" -ForegroundColor Green
Write-Host "  Ticket ID: $($createdTicket.id)" -ForegroundColor Cyan
Write-Host "  Status: $($createdTicket.status)" -ForegroundColor Cyan
Write-Host "  Title: $($createdTicket.title)" -ForegroundColor Cyan
Write-Host "  Status Code: $($create.StatusCode)" -ForegroundColor Cyan
Write-Host ""

# Step 6: Verify ticket appears in list
Write-Host "Step 6: Verifying ticket appears in client list..." -ForegroundColor Yellow
Start-Sleep -Seconds 1
$ticketsAfter = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{ Authorization = "Bearer $token" }
if (-not $ticketsAfter.Success) {
    Write-Host "[FAIL] Could not fetch tickets after creation" -ForegroundColor Red
    exit 1
}
$countAfter = $ticketsAfter.Data.Count
Write-Host "[OK] Client now has $countAfter tickets (was $countBefore)" -ForegroundColor Green

$found = $ticketsAfter.Data | Where-Object { $_.id -eq $createdTicket.id }
if ($found) {
    Write-Host "[OK] Created ticket found in client list" -ForegroundColor Green
    Write-Host "  Ticket ID: $($found.id)" -ForegroundColor Cyan
    Write-Host "  Title: $($found.title)" -ForegroundColor Cyan
    Write-Host "  Status: $($found.status)" -ForegroundColor Cyan
} else {
    Write-Host "[FAIL] Created ticket NOT found in client list" -ForegroundColor Red
    Write-Host "  Looking for ID: $($createdTicket.id)" -ForegroundColor Yellow
    Write-Host "  Available IDs: $($ticketsAfter.Data.id -join ', ')" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 7: Verify ticket appears for admin
Write-Host "Step 7: Verifying ticket appears for admin..." -ForegroundColor Yellow
$adminLogin = Invoke-ApiRequest -Method "POST" -Url "$API_BASE_URL/api/auth/login" -Body @{
    email = "admin@test.com"
    password = "Admin123!"
}
if ($adminLogin.Success) {
    $adminToken = $adminLogin.Data.token
    $adminTickets = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/tickets" -Headers @{ Authorization = "Bearer $adminToken" }
    if ($adminTickets.Success) {
        $adminFound = $adminTickets.Data | Where-Object { $_.id -eq $createdTicket.id }
        if ($adminFound) {
            Write-Host "[OK] Created ticket found in admin list" -ForegroundColor Green
        } else {
            Write-Host "[WARN] Created ticket NOT found in admin list" -ForegroundColor Yellow
        }
    } else {
        Write-Host "[WARN] Could not fetch admin tickets" -ForegroundColor Yellow
    }
} else {
    Write-Host "[WARN] Could not login as admin" -ForegroundColor Yellow
}
Write-Host ""

# Step 8: Check database count (if debug endpoint exists)
Write-Host "Step 8: Checking database ticket count..." -ForegroundColor Yellow
$dbCount = Invoke-ApiRequest -Method "GET" -Url "$API_BASE_URL/api/debug/tickets/count"
if ($dbCount.Success) {
    Write-Host "[OK] Database ticket count:" -ForegroundColor Green
    $dbCount.Data | ConvertTo-Json | Write-Host -ForegroundColor Gray
} else {
    Write-Host "[INFO] Debug endpoint not available (expected in production)" -ForegroundColor Gray
}
Write-Host ""

Write-Host "=== Verification Complete ===" -ForegroundColor Cyan
Write-Host "[PASS] Ticket creation and persistence verified" -ForegroundColor Green
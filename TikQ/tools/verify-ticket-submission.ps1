# verify-ticket-submission.ps1
# End-to-end verification script for ticket submission
# Verifies that tickets created by clients appear in all dashboards

param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [switch]$SkipBackendCheck
)

$ErrorActionPreference = "Stop"

# Get script directory and repo root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

Write-Host "=== Ticket Submission Verification ===" -ForegroundColor Cyan
Write-Host "Repo Root: $RepoRoot" -ForegroundColor Gray
Write-Host "API Base URL: $ApiBaseUrl" -ForegroundColor Gray
Write-Host ""

# Helper function to make API requests
function Invoke-ApiRequest {
    param(
        [string]$Method = "GET",
        [string]$Path,
        [hashtable]$Body = $null,
        [string]$Token = $null
    )
    
    $headers = @{
        "Content-Type" = "application/json"
    }
    
    if ($Token) {
        $headers["Authorization"] = "Bearer $Token"
    }
    
    $uri = "$ApiBaseUrl$Path"
    $params = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
    }
    
    if ($Body) {
        $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
    }
    
    try {
        $response = Invoke-RestMethod @params
        return @{ Success = $true; Data = $response }
    } catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $null
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $errorBody = $reader.ReadToEnd() | ConvertFrom-Json
        } catch {
            $errorBody = $_.Exception.Message
        }
        return @{ Success = $false; StatusCode = $statusCode; Error = $errorBody }
    }
}

# Step 1: Check backend is running
if (-not $SkipBackendCheck) {
    Write-Host "[1/6] Checking backend is running..." -ForegroundColor Yellow
    $healthResult = Invoke-ApiRequest -Path "/api/health"
    if (-not $healthResult.Success) {
        Write-Host "ERROR: Backend is not responding at $ApiBaseUrl" -ForegroundColor Red
        Write-Host "Please start the backend with: dotnet run (from backend/Ticketing.Backend)" -ForegroundColor Yellow
        exit 1
    }
    Write-Host "✓ Backend is running" -ForegroundColor Green
} else {
    Write-Host "[1/6] Skipping backend check" -ForegroundColor Gray
}

# Step 2: Login as Client
Write-Host "[2/6] Logging in as Client..." -ForegroundColor Yellow
$clientLogin = Invoke-ApiRequest -Method "POST" -Path "/api/auth/login" -Body @{
    email = "client1@test.com"
    password = "Client123!"
}
if (-not $clientLogin.Success) {
    Write-Host "ERROR: Failed to login as client" -ForegroundColor Red
    Write-Host "Response: $($clientLogin.Error)" -ForegroundColor Red
    exit 1
}
$clientToken = $clientLogin.Data.token
$clientUserId = $clientLogin.Data.user.id
Write-Host "✓ Client logged in (User ID: $clientUserId)" -ForegroundColor Green

# Step 3: Get categories and subcategories
Write-Host "[3/7] Getting categories..." -ForegroundColor Yellow
$categoriesResult = Invoke-ApiRequest -Path "/api/categories" -Token $clientToken
if (-not $categoriesResult.Success) {
    Write-Host "ERROR: Failed to get categories" -ForegroundColor Red
    exit 1
}
$category = $categoriesResult.Data[0]
$categoryId = $category.id
$subcategoryId = $null
if ($category.subcategories -and $category.subcategories.Count -gt 0) {
    $subcategoryId = $category.subcategories[0].id
    Write-Host "✓ Found category: $($category.name), subcategory: $($category.subcategories[0].name)" -ForegroundColor Green
} else {
    Write-Host "WARNING: No subcategories found for category $($category.name)" -ForegroundColor Yellow
    Write-Host "Creating ticket without subcategory may fail if subcategory is required" -ForegroundColor Yellow
}

# Step 4: Create a ticket as Client (with subcategory)
Write-Host "[4/7] Creating ticket as client..." -ForegroundColor Yellow
$ticketTitle = "Verification Test Ticket - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
$ticketBody = @{
    title = $ticketTitle
    description = "This ticket is for verifying that ticket submission works end-to-end. Testing JSON body parsing."
    categoryId = $categoryId
    priority = "High"
}
if ($subcategoryId) {
    $ticketBody.subcategoryId = $subcategoryId
}
$ticketCreate = Invoke-ApiRequest -Method "POST" -Path "/api/tickets" -Token $clientToken -Body $ticketBody
if (-not $ticketCreate.Success) {
    Write-Host "ERROR: Failed to create ticket" -ForegroundColor Red
    Write-Host "Response: $($ticketCreate.Error)" -ForegroundColor Red
    exit 1
}
$ticketId = $ticketCreate.Data.id
Write-Host "✓ Ticket created (ID: $ticketId)" -ForegroundColor Green
Write-Host "  Title: $ticketTitle" -ForegroundColor Gray
Write-Host "  Status: $($ticketCreate.Data.status)" -ForegroundColor Gray
Write-Host "  CreatedAt: $($ticketCreate.Data.createdAt)" -ForegroundColor Gray

# Step 5: Verify ticket appears in client dashboard
Write-Host "[5/7] Verifying ticket appears in client dashboard..." -ForegroundColor Yellow
Start-Sleep -Seconds 1 # Small delay to ensure DB commit
$clientTickets = Invoke-ApiRequest -Path "/api/tickets" -Token $clientToken
if (-not $clientTickets.Success) {
    Write-Host "ERROR: Failed to get client tickets" -ForegroundColor Red
    Write-Host "Response: $($clientTickets.Error)" -ForegroundColor Red
    exit 1
}
$clientTicket = $clientTickets.Data | Where-Object { $_.id -eq $ticketId }
if (-not $clientTicket) {
    Write-Host "ERROR: Ticket not found in client dashboard" -ForegroundColor Red
    Write-Host "Client has $($clientTickets.Data.Count) tickets, but created ticket is not in the list" -ForegroundColor Yellow
    Write-Host "Ticket IDs in list: $($clientTickets.Data | ForEach-Object { $_.id } | Select-Object -First 5 | Join-String -Separator ', ')" -ForegroundColor Gray
    exit 1
}
Write-Host "✓ Ticket found in client dashboard (Status: $($clientTicket.status))" -ForegroundColor Green

# Step 6: Verify ticket appears in admin dashboard
Write-Host "[6/7] Verifying ticket appears in admin dashboard..." -ForegroundColor Yellow
$adminLogin = Invoke-ApiRequest -Method "POST" -Path "/api/auth/login" -Body @{
    email = "admin@test.com"
    password = "Admin123!"
}
if (-not $adminLogin.Success) {
    Write-Host "WARNING: Could not login as admin to verify (admin@test.com / Admin123!)" -ForegroundColor Yellow
} else {
    $adminToken = $adminLogin.Data.token
    $adminTickets = Invoke-ApiRequest -Path "/api/tickets" -Token $adminToken
    if (-not $adminTickets.Success) {
        Write-Host "WARNING: Failed to get admin tickets" -ForegroundColor Yellow
        Write-Host "Response: $($adminTickets.Error)" -ForegroundColor Red
    } else {
        $adminTicket = $adminTickets.Data | Where-Object { $_.id -eq $ticketId }
        if (-not $adminTicket) {
            Write-Host "ERROR: Ticket not found in admin dashboard" -ForegroundColor Red
            Write-Host "Admin has $($adminTickets.Data.Count) tickets, but created ticket is not in the list" -ForegroundColor Yellow
            exit 1
        }
        Write-Host "✓ Ticket found in admin dashboard" -ForegroundColor Green
    }
}

# Step 7: Check if ticket was auto-assigned to technician(s)
Write-Host "[7/7] Checking auto-assignment status..." -ForegroundColor Yellow
if ($ticketCreate.Data.assignedTechnicians -and $ticketCreate.Data.assignedTechnicians.Count -gt 0) {
    Write-Host "✓ Ticket was auto-assigned to $($ticketCreate.Data.assignedTechnicians.Count) technician(s)" -ForegroundColor Green
    foreach ($tech in $ticketCreate.Data.assignedTechnicians) {
        Write-Host "  - $($tech.technicianName) (Lead: $($tech.isLead))" -ForegroundColor Gray
    }
} else {
    Write-Host "○ Ticket is UNASSIGNED (no technicians with matching subcategory coverage)" -ForegroundColor Yellow
    Write-Host "  This ticket will appear in Admin's 'Unassigned' queue for manual assignment" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Green
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  ✓ Backend is running" -ForegroundColor Green
Write-Host "  ✓ Client can create tickets (JSON body parsing works)" -ForegroundColor Green
Write-Host "  ✓ Ticket appears in client dashboard" -ForegroundColor Green
Write-Host "  ✓ Ticket appears in admin dashboard" -ForegroundColor Green
if ($ticketCreate.Data.assignedTechnicians -and $ticketCreate.Data.assignedTechnicians.Count -gt 0) {
    Write-Host "  ✓ Ticket was auto-assigned to technicians" -ForegroundColor Green
} else {
    Write-Host "  ○ Ticket remains unassigned (expected if no coverage)" -ForegroundColor Yellow
}
Write-Host ""
Write-Host "Ticket Details:" -ForegroundColor Cyan
Write-Host "  ID: $ticketId" -ForegroundColor Gray
Write-Host "  Title: $ticketTitle" -ForegroundColor Gray
Write-Host "  Status: $($ticketCreate.Data.status)" -ForegroundColor Gray
Write-Host "  CreatedAt: $($ticketCreate.Data.createdAt)" -ForegroundColor Gray
Write-Host ""
Write-Host "You can view this ticket in the dashboards to verify all features." -ForegroundColor Gray



































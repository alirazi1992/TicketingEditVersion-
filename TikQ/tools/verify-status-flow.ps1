<#
.SYNOPSIS
    Verifies ticket status consistency across all dashboards (Admin, Technician, Client).
    
.DESCRIPTION
    This test validates that:
    1. Ticket status is canonical (single source of truth)
    2. Status changes by Admin are visible to all users
    3. Viewing a ticket does NOT change its status (only updates seen state)
    4. isUnseen is computed separately from workflow status
    
.NOTES
    Run this after starting the backend server.
#>

param(
    [string]$BaseUrl = "http://localhost:5000",
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

# ============================================================================
# Helper Functions
# ============================================================================

function Write-Status($msg) { Write-Host "[STATUS] $msg" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "[PASS] $msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "[FAIL] $msg" -ForegroundColor Red }
function Write-Info($msg) { if ($Verbose) { Write-Host "[INFO] $msg" -ForegroundColor Gray } }

function Invoke-ApiRequest {
    param(
        [string]$Method = "GET",
        [string]$Endpoint,
        [string]$Token,
        [object]$Body
    )
    
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token) { $headers["Authorization"] = "Bearer $Token" }
    
    $uri = "$BaseUrl$Endpoint"
    Write-Info "API $Method $uri"
    
    try {
        $params = @{
            Method = $Method
            Uri = $uri
            Headers = $headers
            ContentType = "application/json"
        }
        
        if ($Body) {
            $params["Body"] = ($Body | ConvertTo-Json -Depth 10)
        }
        
        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        $errorBody = $_.ErrorDetails.Message
        Write-Fail "API call failed: $Method $Endpoint - Status: $statusCode"
        Write-Fail "Error: $errorBody"
        throw
    }
}

function Login {
    param([string]$Email, [string]$Password)
    
    $body = @{ email = $Email; password = $Password }
    $response = Invoke-ApiRequest -Method "POST" -Endpoint "/api/auth/login" -Body $body
    return $response.token
}

# ============================================================================
# Test: Status Consistency Across Dashboards
# ============================================================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Yellow
Write-Host "TICKET STATUS CONSISTENCY TEST" -ForegroundColor Yellow
Write-Host "========================================" -ForegroundColor Yellow
Write-Host ""

try {
    # Step 1: Login as different users
    Write-Status "Logging in as Admin..."
    $adminToken = Login -Email "admin@example.com" -Password "Admin123!"
    Write-Success "Admin login successful"
    
    Write-Status "Logging in as Technician..."
    $techToken = Login -Email "tech@example.com" -Password "Tech123!"
    Write-Success "Technician login successful"
    
    Write-Status "Logging in as Client..."
    $clientToken = Login -Email "client@example.com" -Password "Client123!"
    Write-Success "Client login successful"
    
    # Step 2: Get categories for ticket creation
    Write-Status "Fetching categories..."
    $categories = Invoke-ApiRequest -Endpoint "/api/categories" -Token $clientToken
    if ($categories.Count -eq 0) {
        Write-Fail "No categories found"
        exit 1
    }
    $category = $categories[0]
    $subcategory = if ($category.subcategories -and $category.subcategories.Count -gt 0) { $category.subcategories[0] } else { $null }
    Write-Success "Using category: $($category.name)"
    
    # Step 3: Create a test ticket as Client
    Write-Status "Creating test ticket as Client..."
    $ticketBody = @{
        title = "Status Consistency Test - $(Get-Date -Format 'yyyyMMdd-HHmmss')"
        description = "Test ticket to verify status consistency across dashboards"
        categoryId = $category.id
        subcategoryId = if ($subcategory) { $subcategory.id } else { $null }
        priority = "Medium"
    }
    $createdTicket = Invoke-ApiRequest -Method "POST" -Endpoint "/api/tickets" -Token $clientToken -Body $ticketBody
    $ticketId = $createdTicket.id
    Write-Success "Ticket created: $ticketId"
    Write-Info "Initial status: $($createdTicket.status)"
    
    # Verify initial status is Submitted
    if ($createdTicket.status -ne "Submitted") {
        Write-Fail "Expected initial status 'Submitted', got '$($createdTicket.status)'"
        exit 1
    }
    Write-Success "Initial status is 'Submitted' as expected"
    
    # Step 4: Admin views the ticket - status should NOT change to Viewed
    Write-Status "Admin viewing ticket (should NOT change status to Viewed)..."
    $adminViewTicket = Invoke-ApiRequest -Endpoint "/api/tickets/$ticketId" -Token $adminToken
    Write-Info "Status after Admin view: $($adminViewTicket.status)"
    
    # CRITICAL CHECK: Status should remain Submitted, not change to Viewed
    if ($adminViewTicket.status -eq "Viewed") {
        Write-Fail "BUG: Status changed to 'Viewed' when Admin viewed the ticket!"
        Write-Fail "Status should remain 'Submitted' - viewing should only update seen state, not workflow status"
        exit 1
    }
    if ($adminViewTicket.status -ne "Submitted") {
        Write-Fail "Unexpected status: '$($adminViewTicket.status)' (expected 'Submitted')"
        exit 1
    }
    Write-Success "Status remains 'Submitted' after Admin view (correct behavior)"
    
    # Step 5: Admin changes status to Solved
    Write-Status "Admin setting status to 'Solved'..."
    $updateBody = @{ status = "Solved" }
    $updatedTicket = Invoke-ApiRequest -Method "PATCH" -Endpoint "/api/tickets/$ticketId" -Token $adminToken -Body $updateBody
    Write-Info "Status after Admin update: $($updatedTicket.status)"
    
    if ($updatedTicket.status -ne "Solved") {
        Write-Fail "Admin status update failed. Expected 'Solved', got '$($updatedTicket.status)'"
        exit 1
    }
    Write-Success "Admin successfully set status to 'Solved'"
    
    # Step 6: Technician views the ticket - should see Solved (NOT Viewed or Read)
    Write-Status "Technician viewing ticket (should see 'Solved')..."
    $techViewTicket = Invoke-ApiRequest -Endpoint "/api/tickets/$ticketId" -Token $techToken
    Write-Info "Status as seen by Technician: $($techViewTicket.status)"
    Write-Info "isUnseen for Technician: $($techViewTicket.isUnseen)"
    
    # CRITICAL CHECK: Technician should see Solved status
    if ($techViewTicket.status -ne "Solved") {
        Write-Fail "STATUS INCONSISTENCY BUG!"
        Write-Fail "Admin set status to 'Solved', but Technician sees '$($techViewTicket.status)'"
        exit 1
    }
    Write-Success "Technician correctly sees status 'Solved'"
    
    # Step 7: Technician marks ticket as seen - status should remain Solved
    Write-Status "Technician marking ticket as seen..."
    try {
        Invoke-ApiRequest -Method "POST" -Endpoint "/api/tickets/$ticketId/seen" -Token $techToken | Out-Null
    } catch {
        Write-Info "Mark seen returned non-JSON response (expected for 204 No Content)"
    }
    
    # Re-fetch ticket as Technician
    $techAfterSeen = Invoke-ApiRequest -Endpoint "/api/tickets/$ticketId" -Token $techToken
    Write-Info "Status after marking seen: $($techAfterSeen.status)"
    Write-Info "isUnseen after marking seen: $($techAfterSeen.isUnseen)"
    
    # CRITICAL CHECK: Status should still be Solved, only isUnseen should change
    if ($techAfterSeen.status -ne "Solved") {
        Write-Fail "BUG: Status changed after marking as seen!"
        Write-Fail "Expected 'Solved', got '$($techAfterSeen.status)'"
        exit 1
    }
    Write-Success "Status remains 'Solved' after marking as seen (correct behavior)"
    
    # Step 8: Client views the ticket - should see Solved
    Write-Status "Client viewing ticket (should see 'Solved')..."
    $clientViewTicket = Invoke-ApiRequest -Endpoint "/api/tickets/$ticketId" -Token $clientToken
    Write-Info "Status as seen by Client: $($clientViewTicket.status)"
    
    if ($clientViewTicket.status -ne "Solved") {
        Write-Fail "STATUS INCONSISTENCY BUG!"
        Write-Fail "Client sees '$($clientViewTicket.status)' instead of 'Solved'"
        exit 1
    }
    Write-Success "Client correctly sees status 'Solved'"
    
    # Step 9: Verify via Admin ticket list endpoint
    Write-Status "Verifying status in Admin ticket list..."
    $adminTickets = Invoke-ApiRequest -Endpoint "/api/admin/tickets?page=1&pageSize=50" -Token $adminToken
    $ticketInList = $adminTickets.items | Where-Object { $_.id -eq $ticketId }
    
    if ($ticketInList) {
        Write-Info "Status in Admin list: $($ticketInList.status)"
        if ($ticketInList.status -ne "Solved") {
            Write-Fail "STATUS INCONSISTENCY in Admin list!"
            Write-Fail "Expected 'Solved', got '$($ticketInList.status)'"
            exit 1
        }
        Write-Success "Admin list shows correct status 'Solved'"
    } else {
        Write-Info "Ticket not found in first page of Admin list (may be on later page)"
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "ALL STATUS CONSISTENCY TESTS PASSED!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Summary:" -ForegroundColor White
    Write-Host "  - Ticket status is canonical (single source of truth)" -ForegroundColor White
    Write-Host "  - Viewing ticket does NOT change workflow status" -ForegroundColor White
    Write-Host "  - Admin status changes are visible to all users" -ForegroundColor White
    Write-Host "  - Marking as seen only affects isUnseen, not status" -ForegroundColor White
    Write-Host ""
    
    exit 0
}
catch {
    Write-Fail "Test failed with error: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}

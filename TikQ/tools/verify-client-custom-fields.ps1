# verify-client-custom-fields.ps1
# Verification script for client custom fields sync feature
# Tests: Admin creates field → Client sees it → Client submits ticket with field value → Value persists

param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [int]$SubcategoryId = 1
)

$ErrorActionPreference = "Stop"

Write-Host "=== Client Custom Fields Sync Verification ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Login as Admin
Write-Host "Step 1: Logging in as Admin..." -ForegroundColor Yellow
$adminLoginBody = (@{
    email = "admin@test.com"
    password = "Admin123!"
} | ConvertTo-Json)

try {
    $adminLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body $adminLoginBody
    $adminToken = $adminLogin.token
    Write-Host "✓ Admin logged in" -ForegroundColor Green
} catch {
    Write-Host "✗ Admin login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 2: Create a test field
Write-Host "Step 2: Creating test field..." -ForegroundColor Yellow
$fieldBody = (@{
    name = "TestDeviceSerial"
    label = "شماره سریال دستگاه"
    key = "deviceSerial"
    type = "Text"
    isRequired = $true
    defaultValue = ""
} | ConvertTo-Json)

try {
    $headers = @{
        Authorization = "Bearer $adminToken"
        "Content-Type" = "application/json"
    }
    $createdField = Invoke-RestMethod -Uri "$ApiBaseUrl/api/admin/subcategories/$SubcategoryId/fields" -Method POST -Headers $headers -Body $fieldBody
    $fieldId = $createdField.id
    Write-Host "✓ Field created with ID: $fieldId" -ForegroundColor Green
} catch {
    Write-Host "✗ Field creation failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Fetch fields via client endpoint
Write-Host "Step 3: Fetching fields via client endpoint..." -ForegroundColor Yellow
try {
    $clientFields = Invoke-RestMethod -Uri "$ApiBaseUrl/api/subcategories/$SubcategoryId/fields" -Method GET -Headers $headers
    $testField = $clientFields | Where-Object { $_.id -eq $fieldId }
    if ($testField) {
        Write-Host "✓ Client endpoint returns the new field" -ForegroundColor Green
        Write-Host "  Field: $($testField.label) (Key: $($testField.key), Required: $($testField.isRequired))" -ForegroundColor Gray
    } else {
        Write-Host "✗ Client endpoint does not return the new field" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Client endpoint failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 4: Login as Client
Write-Host "Step 4: Logging in as Client..." -ForegroundColor Yellow
$clientLoginBody = (@{
    email = "client@test.com"
    password = "Client123!"
} | ConvertTo-Json)

try {
    $clientLogin = Invoke-RestMethod -Uri "$ApiBaseUrl/api/auth/login" -Method POST -ContentType "application/json" -Body $clientLoginBody
    $clientToken = $clientLogin.token
    Write-Host "✓ Client logged in" -ForegroundColor Green
} catch {
    Write-Host "✗ Client login failed: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "  Note: You may need to create a client user first" -ForegroundColor Yellow
    exit 1
}

# Step 5: Create ticket with field value
Write-Host "Step 5: Creating ticket with custom field value..." -ForegroundColor Yellow
$ticketBody = (@{
    title = "Test Ticket with Custom Field"
    description = "This ticket tests custom field submission"
    categoryId = 1
    subcategoryId = $SubcategoryId
    priority = 0
    dynamicFields = @(
        @{
            fieldDefinitionId = $fieldId
            value = "SN123456789"
        }
    )
} | ConvertTo-Json -Depth 3)

try {
    $clientHeaders = @{
        Authorization = "Bearer $clientToken"
        "Content-Type" = "application/json"
    }
    $createdTicket = Invoke-RestMethod -Uri "$ApiBaseUrl/api/tickets" -Method POST -Headers $clientHeaders -Body $ticketBody
    $ticketId = $createdTicket.id
    Write-Host "✓ Ticket created with ID: $ticketId" -ForegroundColor Green
} catch {
    Write-Host "✗ Ticket creation failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "  Response: $responseBody" -ForegroundColor Gray
    }
    exit 1
}

# Step 6: Verify field value persisted
Write-Host "Step 6: Verifying field value persisted..." -ForegroundColor Yellow
try {
    $ticket = Invoke-RestMethod -Uri "$ApiBaseUrl/api/tickets/$ticketId" -Method GET -Headers $clientHeaders
    if ($ticket.dynamicFields -and $ticket.dynamicFields.Count -gt 0) {
        $fieldValue = $ticket.dynamicFields | Where-Object { $_.fieldDefinitionId -eq $fieldId }
        if ($fieldValue -and $fieldValue.value -eq "SN123456789") {
            Write-Host "✓ Field value persisted correctly: $($fieldValue.value)" -ForegroundColor Green
        } else {
            Write-Host "✗ Field value not found or incorrect" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "✗ No dynamic fields in ticket response" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "✗ Failed to retrieve ticket: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== All Tests Passed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  ✓ Admin can create custom fields"
Write-Host "  ✓ Client endpoint returns active fields"
Write-Host "  ✓ Client can submit tickets with field values"
Write-Host "  ✓ Field values persist correctly"
Write-Host ""
Write-Host "Feature is working correctly!" -ForegroundColor Green


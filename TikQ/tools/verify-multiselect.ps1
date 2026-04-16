# verify-multiselect.ps1
# Verification script for MultiSelect field type support
# Tests: Admin creates MultiSelect field → Client sees it → Client submits ticket with multiple values → Values persist

param(
    [string]$ApiBaseUrl = "http://localhost:5000",
    [int]$SubcategoryId = 1
)

$ErrorActionPreference = "Stop"

Write-Host "=== MultiSelect Field Type Verification ===" -ForegroundColor Cyan
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

# Step 2: Create a MultiSelect field
Write-Host "Step 2: Creating MultiSelect field..." -ForegroundColor Yellow
$fieldBody = (@{
    name = "TestMultiSelect"
    label = "انتخاب چندگانه تست"
    key = "testMultiSelect"
    type = "MultiSelect"
    isRequired = $true
    defaultValue = ""
    options = @(
        @{ value = "option1"; label = "گزینه 1" },
        @{ value = "option2"; label = "گزینه 2" },
        @{ value = "option3"; label = "گزینه 3" }
    )
} | ConvertTo-Json -Depth 3)

try {
    $headers = @{
        Authorization = "Bearer $adminToken"
        "Content-Type" = "application/json"
    }
    $createdField = Invoke-RestMethod -Uri "$ApiBaseUrl/api/admin/subcategories/$SubcategoryId/fields" -Method POST -Headers $headers -Body $fieldBody
    $fieldId = $createdField.id
    Write-Host "✓ MultiSelect field created with ID: $fieldId" -ForegroundColor Green
    Write-Host "  Type: $($createdField.type), Label: $($createdField.label)" -ForegroundColor Gray
} catch {
    Write-Host "✗ Field creation failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "  Response: $responseBody" -ForegroundColor Gray
    }
    exit 1
}

# Step 3: Fetch fields via client endpoint
Write-Host "Step 3: Fetching fields via client endpoint..." -ForegroundColor Yellow
try {
    $clientFields = Invoke-RestMethod -Uri "$ApiBaseUrl/api/subcategories/$SubcategoryId/fields" -Method GET -Headers $headers
    $testField = $clientFields | Where-Object { $_.id -eq $fieldId }
    if ($testField -and $testField.type -eq "MultiSelect") {
        Write-Host "✓ Client endpoint returns MultiSelect field" -ForegroundColor Green
        Write-Host "  Type: $($testField.type), Options: $($testField.options.Count)" -ForegroundColor Gray
    } else {
        Write-Host "✗ Client endpoint does not return MultiSelect field correctly" -ForegroundColor Red
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

# Step 5: Create ticket with MultiSelect field values (comma-separated)
Write-Host "Step 5: Creating ticket with MultiSelect field values..." -ForegroundColor Yellow
$ticketBody = (@{
    title = "Test Ticket with MultiSelect Field"
    description = "This ticket tests MultiSelect field submission"
    categoryId = 1
    subcategoryId = $SubcategoryId
    priority = 0
    dynamicFields = @(
        @{
            fieldDefinitionId = $fieldId
            value = "option1,option3"  # Multiple values as comma-separated string
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

# Step 6: Verify field values persisted
Write-Host "Step 6: Verifying MultiSelect field values persisted..." -ForegroundColor Yellow
try {
    $ticket = Invoke-RestMethod -Uri "$ApiBaseUrl/api/tickets/$ticketId" -Method GET -Headers $clientHeaders
    if ($ticket.dynamicFields -and $ticket.dynamicFields.Count -gt 0) {
        $fieldValue = $ticket.dynamicFields | Where-Object { $_.fieldDefinitionId -eq $fieldId }
        if ($fieldValue) {
            $values = $fieldValue.value -split ","
            if ($values -contains "option1" -and $values -contains "option3") {
                Write-Host "✓ MultiSelect field values persisted correctly: $($fieldValue.value)" -ForegroundColor Green
                Write-Host "  Parsed values: $($values -join ', ')" -ForegroundColor Gray
            } else {
                Write-Host "✗ MultiSelect field values incorrect" -ForegroundColor Red
                Write-Host "  Expected: option1,option3" -ForegroundColor Gray
                Write-Host "  Got: $($fieldValue.value)" -ForegroundColor Gray
                exit 1
            }
        } else {
            Write-Host "✗ MultiSelect field value not found in ticket" -ForegroundColor Red
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
Write-Host "  ✓ MultiSelect added to FieldType enum"
Write-Host "  ✓ Admin can create MultiSelect fields"
Write-Host "  ✓ Client endpoint returns MultiSelect fields"
Write-Host "  ✓ Client can submit tickets with multiple values"
Write-Host "  ✓ MultiSelect values persist correctly (comma-separated)"
Write-Host ""
Write-Host "MultiSelect field type is working correctly!" -ForegroundColor Green



































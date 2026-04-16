# verify-category-crud.ps1
# Verification script for Category/Subcategory CRUD operations
# Run this after starting the backend to verify endpoints work correctly

param(
    [string]$BaseUrl = "http://localhost:5000",
    [string]$AdminEmail = "admin@example.com",
    [string]$AdminPassword = "Admin123!"
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "Category CRUD Verification Script" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

# Step 1: Login as Admin
Write-Host "[1/6] Logging in as Admin..." -ForegroundColor Yellow
try {
    $loginBody = @{
        email = $AdminEmail
        password = $AdminPassword
    } | ConvertTo-Json

    $loginResponse = Invoke-RestMethod -Uri "$BaseUrl/api/auth/login" -Method POST -Body $loginBody -ContentType "application/json"
    $token = $loginResponse.token
    Write-Host "    SUCCESS: Logged in as $($loginResponse.user.fullName)" -ForegroundColor Green
} catch {
    Write-Host "    FAILED: Could not login - $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "    Make sure the backend is running and admin user exists." -ForegroundColor Yellow
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $token"
    "Content-Type" = "application/json"
}

# Step 2: Get current categories count
Write-Host "`n[2/6] Getting current categories..." -ForegroundColor Yellow
try {
    $categoriesBefore = Invoke-RestMethod -Uri "$BaseUrl/api/categories" -Method GET -Headers $headers
    $countBefore = $categoriesBefore.Count
    Write-Host "    SUCCESS: Found $countBefore existing categories" -ForegroundColor Green
} catch {
    Write-Host "    FAILED: Could not get categories - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 3: Create a new test category
$testCategoryName = "Test Category $(Get-Date -Format 'HHmmss')"
Write-Host "`n[3/6] Creating test category '$testCategoryName'..." -ForegroundColor Yellow
try {
    $categoryBody = @{
        name = $testCategoryName
        description = "Created by verification script"
        isActive = $true
    } | ConvertTo-Json

    $createdCategory = Invoke-RestMethod -Uri "$BaseUrl/api/categories" -Method POST -Body $categoryBody -Headers $headers
    Write-Host "    SUCCESS: Created category with ID $($createdCategory.id)" -ForegroundColor Green
    Write-Host "    - Name: $($createdCategory.name)" -ForegroundColor Gray
    Write-Host "    - IsActive: $($createdCategory.isActive)" -ForegroundColor Gray
} catch {
    Write-Host "    FAILED: Could not create category - $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "    Response: $responseBody" -ForegroundColor Red
    }
    exit 1
}

# Step 4: Verify category exists in GET /api/categories
Write-Host "`n[4/6] Verifying category appears in GET /api/categories..." -ForegroundColor Yellow
try {
    $categoriesAfter = Invoke-RestMethod -Uri "$BaseUrl/api/categories" -Method GET -Headers $headers
    $foundCategory = $categoriesAfter | Where-Object { $_.id -eq $createdCategory.id }
    
    if ($foundCategory) {
        Write-Host "    SUCCESS: Category found in public endpoint" -ForegroundColor Green
    } else {
        Write-Host "    WARNING: Category NOT found in public endpoint (might be inactive?)" -ForegroundColor Yellow
        # Check admin endpoint
        $adminCategories = Invoke-RestMethod -Uri "$BaseUrl/api/categories/admin" -Method GET -Headers $headers
        $foundInAdmin = $adminCategories.items | Where-Object { $_.id -eq $createdCategory.id }
        if ($foundInAdmin) {
            Write-Host "    - But found in admin endpoint" -ForegroundColor Yellow
        } else {
            Write-Host "    FAILED: Category not found anywhere!" -ForegroundColor Red
            exit 1
        }
    }
} catch {
    Write-Host "    FAILED: Could not verify category - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Step 5: Create a subcategory under the test category
$testSubcategoryName = "Test Subcategory $(Get-Date -Format 'HHmmss')"
Write-Host "`n[5/6] Creating subcategory '$testSubcategoryName' under category ID $($createdCategory.id)..." -ForegroundColor Yellow
try {
    $subcategoryBody = @{
        name = $testSubcategoryName
        description = "Created by verification script"
        isActive = $true
    } | ConvertTo-Json

    $createdSubcategory = Invoke-RestMethod -Uri "$BaseUrl/api/categories/$($createdCategory.id)/subcategories" -Method POST -Body $subcategoryBody -Headers $headers
    Write-Host "    SUCCESS: Created subcategory with ID $($createdSubcategory.id)" -ForegroundColor Green
    Write-Host "    - Name: $($createdSubcategory.name)" -ForegroundColor Gray
    Write-Host "    - CategoryId: $($createdSubcategory.categoryId)" -ForegroundColor Gray
} catch {
    Write-Host "    FAILED: Could not create subcategory - $($_.Exception.Message)" -ForegroundColor Red
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $responseBody = $reader.ReadToEnd()
        Write-Host "    Response: $responseBody" -ForegroundColor Red
    }
    exit 1
}

# Step 6: Verify subcategory exists
Write-Host "`n[6/6] Verifying subcategory appears in GET /api/categories/{id}/subcategories..." -ForegroundColor Yellow
try {
    $subcategories = Invoke-RestMethod -Uri "$BaseUrl/api/categories/$($createdCategory.id)/subcategories" -Method GET -Headers $headers
    $foundSubcategory = $subcategories | Where-Object { $_.id -eq $createdSubcategory.id }
    
    if ($foundSubcategory) {
        Write-Host "    SUCCESS: Subcategory found!" -ForegroundColor Green
    } else {
        Write-Host "    FAILED: Subcategory NOT found!" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "    FAILED: Could not verify subcategory - $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# Summary
Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "VERIFICATION COMPLETE - ALL TESTS PASSED" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "`nCreated resources:" -ForegroundColor White
Write-Host "  - Category ID: $($createdCategory.id) ($testCategoryName)" -ForegroundColor Gray
Write-Host "  - Subcategory ID: $($createdSubcategory.id) ($testSubcategoryName)" -ForegroundColor Gray
Write-Host "`nThese test resources will remain in the database." -ForegroundColor Yellow
Write-Host "You can delete them from the Admin panel if needed." -ForegroundColor Yellow

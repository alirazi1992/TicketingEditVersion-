# verify-notification-contract.ps1
# Verifies that INotificationService has all required methods and backend compiles

$ErrorActionPreference = "Stop"

# Get script directory and repo root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir
$BackendPath = Join-Path $RepoRoot "backend\Ticketing.Backend"

Write-Host "=== Notification Contract Verification ===" -ForegroundColor Cyan
Write-Host "Backend Path: $BackendPath" -ForegroundColor Gray
Write-Host ""

# Step 1: Check that INotificationService interface has required methods
Write-Host "[1/3] Checking INotificationService interface..." -ForegroundColor Yellow
$interfaceFile = Join-Path $BackendPath "Application\Services\NotificationService.cs"
if (-not (Test-Path $interfaceFile)) {
    Write-Host "✗ Interface file not found: $interfaceFile" -ForegroundColor Red
    exit 1
}

$interfaceContent = Get-Content $interfaceFile -Raw
$requiredMethods = @(
    "NotifyTicketAssignedAsync",
    "NotifyTicketCreatedAsync"
)

$missingMethods = @()
foreach ($method in $requiredMethods) {
    if ($interfaceContent -notmatch $method) {
        $missingMethods += $method
    }
}

if ($missingMethods.Count -gt 0) {
    Write-Host "✗ Missing methods in INotificationService:" -ForegroundColor Red
    foreach ($method in $missingMethods) {
        Write-Host "  - $method" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "✓ All required methods found in interface" -ForegroundColor Green
}

# Step 2: Check that NotificationService implements the methods
Write-Host "[2/3] Checking NotificationService implementation..." -ForegroundColor Yellow
$implementationMethods = @(
    "public async Task NotifyTicketAssignedAsync",
    "public async Task NotifyTicketCreatedAsync"
)

$missingImplementations = @()
foreach ($method in $implementationMethods) {
    if ($interfaceContent -notmatch [regex]::Escape($method)) {
        $missingImplementations += $method
    }
}

if ($missingImplementations.Count -gt 0) {
    Write-Host "✗ Missing implementations:" -ForegroundColor Red
    foreach ($method in $missingImplementations) {
        Write-Host "  - $method" -ForegroundColor Red
    }
    exit 1
} else {
    Write-Host "✓ All methods implemented" -ForegroundColor Green
}

# Step 3: Build backend to verify compilation
Write-Host "[3/3] Building backend to verify compilation..." -ForegroundColor Yellow
Push-Location $BackendPath
try {
    $buildOutput = dotnet build --no-restore 2>&1
    $buildSuccess = $LASTEXITCODE -eq 0
    
    if ($buildSuccess) {
        Write-Host "✓ Build succeeded - notification contract is valid" -ForegroundColor Green
    } else {
        Write-Host "✗ Build failed" -ForegroundColor Red
        Write-Host $buildOutput -ForegroundColor Red
        Pop-Location
        exit 1
    }
} catch {
    Write-Host "✗ Build error: $_" -ForegroundColor Red
    Pop-Location
    exit 1
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Verification Complete ===" -ForegroundColor Green
Write-Host "INotificationService contract is valid and backend compiles successfully" -ForegroundColor Green
Write-Host ""



































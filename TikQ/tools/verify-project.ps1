# verify-project.ps1
# Verifies project integrity after cleanup

$ErrorActionPreference = "Continue"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot
$frontendPath = Join-Path $projectRoot "frontend"
$backendPath = Join-Path $projectRoot "backend\Ticketing.Backend"

Write-Host "ðŸ” Verifying project integrity..." -ForegroundColor Cyan
$separator = "=" * 60
Write-Host $separator -ForegroundColor Cyan

$errors = @()
$warnings = @()
$success = @()

# Check git status
Write-Host "`nðŸ“‹ Checking git status..." -ForegroundColor Yellow
Push-Location $projectRoot
try {
    $gitStatus = git status --porcelain 2>&1
    if ($LASTEXITCODE -eq 0) {
        if ($gitStatus) {
            $modifiedCount = ($gitStatus -split "`n" | Where-Object { $_ -match "^ M" }).Count
            $untrackedCount = ($gitStatus -split "`n" | Where-Object { $_ -match "^\?\?" }).Count
            Write-Host "âš ï¸  Uncommitted changes detected:" -ForegroundColor Yellow
            Write-Host "  Modified: $modifiedCount files" -ForegroundColor Gray
            Write-Host "  Untracked: $untrackedCount files" -ForegroundColor Gray
            $warnings += "Git working directory has uncommitted changes"
        } else {
            Write-Host "âœ… Git working directory is clean" -ForegroundColor Green
            $success += "Git status clean"
        }
    } else {
        Write-Host "âš ï¸  Git check failed (may not be a git repo)" -ForegroundColor Yellow
        $warnings += "Git check failed"
    }
} catch {
    Write-Host "âš ï¸  Git check error: $_" -ForegroundColor Yellow
    $warnings += "Git check error"
}
Pop-Location

# Verify frontend dependencies
Write-Host "`nðŸ“¦ Verifying frontend dependencies..." -ForegroundColor Yellow
if (Test-Path $frontendPath) {
    Push-Location $frontendPath
    try {
        if (Test-Path "node_modules") {
            $pkgCount = (Get-ChildItem node_modules -Directory -ErrorAction SilentlyContinue).Count
            if ($pkgCount -gt 0) {
                Write-Host "âœ… node_modules exists with $pkgCount package directories" -ForegroundColor Green
                $success += "Frontend: node_modules present"
            } else {
                Write-Host "âš ï¸  node_modules exists but appears empty" -ForegroundColor Yellow
                $warnings += "Frontend: node_modules empty"
            }
        } else {
            Write-Host "âŒ node_modules missing!" -ForegroundColor Red
            $errors += "Frontend: node_modules missing"
        }
        
        # Check package.json
        if (Test-Path "package.json") {
            Write-Host "âœ… package.json exists" -ForegroundColor Green
        } else {
            Write-Host "âŒ package.json missing!" -ForegroundColor Red
            $errors += "Frontend: package.json missing"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "âŒ Frontend path does not exist!" -ForegroundColor Red
    $errors += "Frontend: path missing"
}

# Verify backend can restore
Write-Host "`nðŸ“¦ Verifying backend project..." -ForegroundColor Yellow
if (Test-Path $backendPath) {
    Push-Location $backendPath
    try {
        if (Test-Path "Ticketing.Backend.csproj") {
            Write-Host "âœ… Project file exists" -ForegroundColor Green
            
            # Check if dotnet is available
            $dotnetCheck = Get-Command dotnet -ErrorAction SilentlyContinue
            if ($null -ne $dotnetCheck) {
                Write-Host "  Running dotnet restore (dry-run)..." -ForegroundColor Gray
                dotnet restore --no-build 2>&1 | Out-Null
                if ($LASTEXITCODE -eq 0) {
                    Write-Host "âœ… Backend project valid" -ForegroundColor Green
                    $success += "Backend: project valid"
                } else {
                    Write-Host "âš ï¸  Backend restore had issues (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
                    $warnings += "Backend: restore issues"
                }
            } else {
                Write-Host "âš ï¸  dotnet CLI not found (skipping restore check)" -ForegroundColor Yellow
                $warnings += "Backend: dotnet CLI not found"
            }
        } else {
            Write-Host "âŒ Project file missing!" -ForegroundColor Red
            $errors += "Backend: csproj missing"
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "âŒ Backend path does not exist!" -ForegroundColor Red
    $errors += "Backend: path missing"
}

# Check VS Code settings
Write-Host "`nâš™ï¸  Checking editor settings..." -ForegroundColor Yellow
$vscodeSettings = Join-Path $projectRoot ".vscode\settings.json"
if (Test-Path $vscodeSettings) {
    Write-Host "âœ… .vscode/settings.json exists" -ForegroundColor Green
    $success += "VS Code settings configured"
} else {
    Write-Host "âš ï¸  .vscode/settings.json not found" -ForegroundColor Yellow
    $warnings += "VS Code settings missing"
}

# Summary
$separator = "=" * 60
Write-Host "`n$separator" -ForegroundColor Cyan
Write-Host "VERIFICATION SUMMARY" -ForegroundColor Cyan
Write-Host $separator -ForegroundColor Cyan

if ($success.Count -gt 0) {
    Write-Host "`nâœ… Success ($($success.Count)):" -ForegroundColor Green
    foreach ($item in $success) {
        Write-Host "  âœ“ $item" -ForegroundColor Gray
    }
}

if ($warnings.Count -gt 0) {
    Write-Host "`nâš ï¸  Warnings ($($warnings.Count)):" -ForegroundColor Yellow
    foreach ($item in $warnings) {
        Write-Host "  âš  $item" -ForegroundColor Gray
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`nâŒ Errors ($($errors.Count)):" -ForegroundColor Red
    foreach ($item in $errors) {
        Write-Host "  âœ— $item" -ForegroundColor Gray
    }
    Write-Host "`nâŒ Verification failed!" -ForegroundColor Red
    exit 1
} else {
    Write-Host "`nâœ… All critical checks passed!" -ForegroundColor Green
    if ($warnings.Count -gt 0) {
        Write-Host "WARNING: Some warnings present - review above" -ForegroundColor Yellow
    }
}
Write-Host $separator -ForegroundColor Cyan

return @{
    Success = $success
    Warnings = $warnings
    Errors = $errors
}


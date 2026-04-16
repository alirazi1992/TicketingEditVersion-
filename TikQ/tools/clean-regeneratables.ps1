# clean-regeneratables.ps1
# Safely deletes all regeneratable folders

param(
    [switch]$WhatIf
)

$ErrorActionPreference = "Continue"
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptRoot

Write-Host "🧹 Cleaning regeneratable folders..." -ForegroundColor Cyan
Write-Host "Project root: $projectRoot" -ForegroundColor Gray

$deleted = @()
$failed = @()

# Node.js folders
$nodeFolders = @(
    "node_modules",
    "frontend\node_modules",
    "frontend\.next"
)

# .NET folders
$dotnetFolders = @(
    "backend\Ticketing.Backend\bin",
    "backend\Ticketing.Backend\obj",
    "backend\obj"
)

# Additional build artifacts
$buildArtifacts = @(
    "frontend\tsconfig.tsbuildinfo",
    "frontend\dist",
    "frontend\build",
    "frontend\.vite"
)

$allFolders = $nodeFolders + $dotnetFolders + $buildArtifacts

foreach ($folder in $allFolders) {
    $fullPath = Join-Path $projectRoot $folder
    if (Test-Path $fullPath) {
        $relativePath = $folder
        Write-Host "  Found: $relativePath" -ForegroundColor Yellow
        
        if ($WhatIf) {
            Write-Host "    [WHAT-IF] Would delete" -ForegroundColor Cyan
        } else {
            try {
                Remove-Item -Path $fullPath -Recurse -Force -ErrorAction Stop
                if (-not (Test-Path $fullPath)) {
                    $deleted += $relativePath
                    Write-Host "    ✓ Deleted" -ForegroundColor Green
                } else {
                    $failed += $relativePath
                    Write-Host "    ✗ Failed to delete (path still exists)" -ForegroundColor Red
                }
            } catch {
                $failed += $relativePath
                Write-Host "    ✗ Error: $_" -ForegroundColor Red
            }
        }
    }
}

# Delete old src bin/obj folders recursively
Write-Host "`n  Checking old src structure..." -ForegroundColor Yellow
$srcPath = Join-Path $projectRoot "backend\Ticketing.Backend\src"
if (Test-Path $srcPath) {
    $oldBinFolders = Get-ChildItem -Path $srcPath -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue
    $oldObjFolders = Get-ChildItem -Path $srcPath -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue
    
    foreach ($folder in ($oldBinFolders + $oldObjFolders)) {
        $relativePath = $folder.FullName.Replace($projectRoot + "\", "")
        Write-Host "  Found: $relativePath" -ForegroundColor Yellow
        
        if ($WhatIf) {
            Write-Host "    [WHAT-IF] Would delete" -ForegroundColor Cyan
        } else {
            try {
                Remove-Item -Path $folder.FullName -Recurse -Force -ErrorAction Stop
                if (-not (Test-Path $folder.FullName)) {
                    $deleted += $relativePath
                    Write-Host "    ✓ Deleted" -ForegroundColor Green
                } else {
                    $failed += $relativePath
                    Write-Host "    ✗ Failed to delete (path still exists)" -ForegroundColor Red
                }
            } catch {
                $failed += $relativePath
                Write-Host "    ✗ Error: $_" -ForegroundColor Red
            }
        }
    }
}

Write-Host "`n" + "="*60 -ForegroundColor Cyan
if ($WhatIf) {
    Write-Host "WHAT-IF MODE: No files were actually deleted" -ForegroundColor Yellow
} else {
    Write-Host "✅ Cleanup complete!" -ForegroundColor Green
    Write-Host "Deleted: $($deleted.Count) items" -ForegroundColor Cyan
    if ($failed.Count -gt 0) {
        Write-Host "Failed: $($failed.Count) items" -ForegroundColor Red
        foreach ($item in $failed) {
            Write-Host "  - $item" -ForegroundColor Red
        }
    }
}
Write-Host "="*60 -ForegroundColor Cyan

# Return results
return @{
    Deleted = $deleted
    Failed = $failed
}


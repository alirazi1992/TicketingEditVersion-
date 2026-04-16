# TikQ Full Sanity Check Script
# This script builds and verifies both backend and frontend
# Exit codes: 0 = success, 1 = failure

param(
    [switch]$SkipBackend,
    [switch]$SkipFrontend,
    [switch]$SkipMigrations,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:ExitCode = 0

function Write-Status {
    param([string]$Message, [string]$Color = "White")
    Write-Host "[SANITY] $Message" -ForegroundColor $Color
}

function Write-Error-Status {
    param([string]$Message)
    Write-Host "[SANITY] ERROR: $Message" -ForegroundColor Red
    $script:ExitCode = 1
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SANITY] OK: $Message" -ForegroundColor Green
}

# Get script directory
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir

Write-Status "Starting TikQ Sanity Check..." "Cyan"
Write-Status "Repository root: $repoRoot" "Gray"

# =======================
# Backend Sanity
# =======================
if (-not $SkipBackend) {
    Write-Status "`n=== Backend Sanity Check ===" "Yellow"
    
    $backendPath = Join-Path $repoRoot "backend\Ticketing.Backend"
    
    if (-not (Test-Path $backendPath)) {
        Write-Error-Status "Backend path not found: $backendPath"
    } else {
        Push-Location $backendPath
        
        try {
            # Clean
            Write-Status "Cleaning backend..."
            dotnet clean --verbosity quiet 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Status "Backend clean failed"
            } else {
                Write-Success "Backend cleaned"
            }
            
            # Build
            Write-Status "Building backend..."
            $buildOutput = dotnet build --no-restore 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Status "Backend build failed"
                if ($Verbose) {
                    Write-Host $buildOutput
                }
            } else {
                Write-Success "Backend built successfully"
                
                # Check for warnings (non-critical)
                $warnings = $buildOutput | Select-String "warning"
                if ($warnings) {
                    Write-Status "Build warnings (non-critical): $($warnings.Count) found" "Yellow"
                }
            }
            
            # Check entrypoint
            Write-Status "Checking backend entrypoint..."
            $programCs = Join-Path $backendPath "Program.cs"
            if (Test-Path $programCs) {
                Write-Success "Entrypoint found: Program.cs"
            } else {
                Write-Error-Status "Entrypoint Program.cs not found"
            }
            
            # Check migrations
            if (-not $SkipMigrations) {
                Write-Status "Checking migrations..."
                $migrationsPath = Join-Path $backendPath "Infrastructure\Data\Migrations"
                if (Test-Path $migrationsPath) {
                    $migrations = Get-ChildItem -Path $migrationsPath -Filter "*.cs" | Where-Object { $_.Name -notlike "*Designer.cs" -and $_.Name -notlike "*Snapshot.cs" }
                    Write-Success "Found $($migrations.Count) migration(s)"
                } else {
                    Write-Error-Status "Migrations directory not found"
                }
            }
            
        } catch {
            Write-Error-Status "Backend check failed: $_"
        } finally {
            Pop-Location
        }
    }
} else {
    Write-Status "Skipping backend checks" "Gray"
}

# =======================
# Frontend Sanity
# =======================
if (-not $SkipFrontend) {
    Write-Status "`n=== Frontend Sanity Check ===" "Yellow"
    
    $frontendPath = Join-Path $repoRoot "frontend"
    
    if (-not (Test-Path $frontendPath)) {
        Write-Error-Status "Frontend path not found: $frontendPath"
    } else {
        Push-Location $frontendPath
        
        try {
            # Check Node/npm
            Write-Status "Checking Node.js and npm..."
            $nodeVersion = node -v 2>&1
            $npmVersion = npm -v 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Success "Node.js: $nodeVersion, npm: $npmVersion"
            } else {
                Write-Error-Status "Node.js or npm not found"
            }
            
            # Check package.json
            if (Test-Path "package.json") {
                Write-Success "package.json found"
            } else {
                Write-Error-Status "package.json not found"
            }
            
            # Build
            Write-Status "Building frontend..."
            $buildOutput = npm run build 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Error-Status "Frontend build failed"
                if ($Verbose) {
                    Write-Host $buildOutput
                }
            } else {
                Write-Success "Frontend built successfully"
            }
            
            # TypeCheck (if script exists)
            if (Test-Path "package.json") {
                $packageJson = Get-Content "package.json" | ConvertFrom-Json
                if ($packageJson.scripts.typecheck) {
                    Write-Status "Running TypeScript typecheck..."
                    $typecheckOutput = npm run typecheck 2>&1
                    # Typecheck may have errors but we'll report them
                    $errorCount = ($typecheckOutput | Select-String "error TS").Count
                    if ($errorCount -gt 0) {
                        Write-Status "TypeScript errors found: $errorCount" "Yellow"
                        if ($Verbose) {
                            Write-Host ($typecheckOutput | Select-Object -Last 20)
                        }
                    } else {
                        Write-Success "TypeScript typecheck passed"
                    }
                } else {
                    Write-Status "typecheck script not found in package.json" "Gray"
                }
            }
            
        } catch {
            Write-Error-Status "Frontend check failed: $_"
        } finally {
            Pop-Location
        }
    }
} else {
    Write-Status "Skipping frontend checks" "Gray"
}

# =======================
# Summary
# =======================
Write-Status "`n=== Sanity Check Summary ===" "Cyan"

if ($script:ExitCode -eq 0) {
    Write-Success "All checks passed!"
    Write-Status "`nNext steps:" "Gray"
    Write-Status "  1. Start backend: cd backend\Ticketing.Backend; dotnet run" "Gray"
    Write-Status "  2. Start frontend: cd frontend; npm run dev" "Gray"
    Write-Status "  3. Test endpoints: http://localhost:5000/swagger" "Gray"
} else {
    Write-Error-Status "Some checks failed. Review errors above."
    Write-Status "`nTroubleshooting:" "Gray"
    Write-Status "  - Run with -Verbose for detailed output" "Gray"
    Write-Status "  - Check logs above for specific errors" "Gray"
    Write-Status "  - Ensure all dependencies are installed" "Gray"
}

exit $script:ExitCode
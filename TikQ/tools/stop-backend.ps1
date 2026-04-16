# stop-backend.ps1
# Safely stops any running backend processes to prevent file-lock errors
# Only stops processes clearly related to this Ticketing backend project

$ErrorActionPreference = "Stop"

# Get repo root path (script is in tools/, repo root is parent)
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Split-Path -Parent $scriptDir)

Write-Host "=== Stopping Backend Processes ===" -ForegroundColor Cyan
Write-Host "Repository: $repoRoot" -ForegroundColor Gray
Write-Host ""

$stoppedCount = 0

# Function to check if a process is our backend
function Test-IsOurBackend {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$CommandLine,
        [string]$ProcessPath
    )
    
    # Check 1: Process name matches
    if ($Process.ProcessName -eq "Ticketing.Backend" -or $Process.ProcessName -eq "Ticketing.Api") {
        # Additional verification: path should be in our repo
        if ($ProcessPath -and $ProcessPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }
    
    # Check 2: Command line contains our DLLs
    if ($CommandLine) {
        if (($CommandLine -match "Ticketing\.Api\.dll") -or 
            ($CommandLine -match "Ticketing\.Backend\.dll") -or
            ($CommandLine -match "Ticketing\.Api\.exe") -or
            ($CommandLine -match "Ticketing\.Backend\.exe")) {
            # Verify it's in our repo path
            if ($CommandLine -match [regex]::Escape($repoRoot)) {
                return $true
            }
        }
    }
    
    # Check 3: Process path is in our repo and matches our project
    if ($ProcessPath) {
        if ($ProcessPath.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            if ($ProcessPath -match "Ticketing\.(Backend|Api)") {
                return $true
            }
        }
    }
    
    return $false
}

# Stop processes on port 5000
Write-Host "Checking port 5000..." -ForegroundColor Yellow
try {
    $connections5000 = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
    if ($connections5000) {
        $pids5000 = $connections5000 | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($processId in $pids5000) {
            try {
                $proc = Get-Process -Id $processId -ErrorAction Stop
                $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue
                if ($procInfo) {
                    $cmdLine = $procInfo.CommandLine
                } else {
                    $cmdLine = ""
                }
                if ($proc.Path) {
                    $procPath = $proc.Path
                } elseif ($procInfo -and $procInfo.ExecutablePath) {
                    $procPath = $procInfo.ExecutablePath
                } else {
                    $procPath = ""
                }
                
                if (Test-IsOurBackend -Process $proc -CommandLine $cmdLine -ProcessPath $procPath) {
                    Write-Host "  Stopping process on port 5000:" -ForegroundColor Yellow
                    Write-Host "    PID: $processId" -ForegroundColor Gray
                    Write-Host "    Name: $($proc.ProcessName)" -ForegroundColor Gray
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 300
                    Write-Host "    Stopped" -ForegroundColor Green
                    $stoppedCount++
                }
            } catch {
                # Process might have already exited
            }
        }
    }
} catch {
    Write-Host "  Could not check port 5000: $_" -ForegroundColor Yellow
}

# Stop processes on port 5001 (HTTPS)
Write-Host "Checking port 5001..." -ForegroundColor Yellow
try {
    $connections5001 = Get-NetTCPConnection -LocalPort 5001 -State Listen -ErrorAction SilentlyContinue
    if ($connections5001) {
        $pids5001 = $connections5001 | Select-Object -ExpandProperty OwningProcess -Unique
        foreach ($processId in $pids5001) {
            try {
                $proc = Get-Process -Id $processId -ErrorAction Stop
                $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $processId" -ErrorAction SilentlyContinue
                if ($procInfo) {
                    $cmdLine = $procInfo.CommandLine
                } else {
                    $cmdLine = ""
                }
                if ($proc.Path) {
                    $procPath = $proc.Path
                } elseif ($procInfo -and $procInfo.ExecutablePath) {
                    $procPath = $procInfo.ExecutablePath
                } else {
                    $procPath = ""
                }
                
                if (Test-IsOurBackend -Process $proc -CommandLine $cmdLine -ProcessPath $procPath) {
                    Write-Host "  Stopping process on port 5001:" -ForegroundColor Yellow
                    Write-Host "    PID: $processId" -ForegroundColor Gray
                    Write-Host "    Name: $($proc.ProcessName)" -ForegroundColor Gray
                    Stop-Process -Id $processId -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 300
                    Write-Host "    Stopped" -ForegroundColor Green
                    $stoppedCount++
                }
            } catch {
                # Process might have already exited
            }
        }
    }
} catch {
    Write-Host "  Could not check port 5001: $_" -ForegroundColor Yellow
}

# Stop processes by name (Ticketing.Backend or Ticketing.Api)
Write-Host "Checking for Ticketing.Backend processes..." -ForegroundColor Yellow
try {
    $backendProcs = Get-Process -Name "Ticketing.Backend" -ErrorAction SilentlyContinue
    foreach ($proc in $backendProcs) {
        try {
            $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)" -ErrorAction SilentlyContinue
            if ($procInfo) {
                $cmdLine = $procInfo.CommandLine
            } else {
                $cmdLine = ""
            }
            if ($proc.Path) {
                $procPath = $proc.Path
            } elseif ($procInfo -and $procInfo.ExecutablePath) {
                $procPath = $procInfo.ExecutablePath
            } else {
                $procPath = ""
            }
            
            if (Test-IsOurBackend -Process $proc -CommandLine $cmdLine -ProcessPath $procPath) {
                Write-Host "  Stopping Ticketing.Backend process:" -ForegroundColor Yellow
                Write-Host "    PID: $($proc.Id)" -ForegroundColor Gray
                Write-Host "    Path: $procPath" -ForegroundColor Gray
                Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                Start-Sleep -Milliseconds 300
                Write-Host "    Stopped" -ForegroundColor Green
                $stoppedCount++
            }
        } catch {
            # Process might have already exited
        }
    }
} catch {
    # No processes found, that's OK
}

# Stop dotnet processes running our backend DLLs
Write-Host "Checking for dotnet processes with Ticketing DLLs..." -ForegroundColor Yellow
try {
    $dotnetProcs = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
    foreach ($proc in $dotnetProcs) {
        try {
            $procInfo = Get-CimInstance Win32_Process -Filter "ProcessId = $($proc.Id)" -ErrorAction SilentlyContinue
            if ($procInfo) {
                $cmdLine = $procInfo.CommandLine
            } else {
                $cmdLine = ""
            }
            
            if ($cmdLine -match "Ticketing\.(Api|Backend)\.(dll|exe)") {
                # Verify it's in our repo
                if ($cmdLine -match [regex]::Escape($repoRoot)) {
                    Write-Host "  Stopping dotnet process running Ticketing backend:" -ForegroundColor Yellow
                    Write-Host "    PID: $($proc.Id)" -ForegroundColor Gray
                    $cmdPreview = $cmdLine.Substring(0, [Math]::Min(80, $cmdLine.Length))
                    Write-Host "    Command: $cmdPreview..." -ForegroundColor Gray
                    Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                    Start-Sleep -Milliseconds 300
                    Write-Host "    Stopped" -ForegroundColor Green
                    $stoppedCount++
                }
            }
        } catch {
            # Process might have already exited or we don't have permission
        }
    }
} catch {
    # No dotnet processes found, that's OK
}

# Shutdown build server to release file locks
Write-Host "Shutting down build server..." -ForegroundColor Yellow
try {
    dotnet build-server shutdown 2>&1 | Out-Null
    Start-Sleep -Milliseconds 300
    Write-Host "  Build server shut down" -ForegroundColor Green
} catch {
    Write-Host "  Could not shut down build server: $_" -ForegroundColor Yellow
}

Write-Host ""

if ($stoppedCount -eq 0) {
    Write-Host "Nothing to stop. No backend processes found." -ForegroundColor Green
} else {
    Write-Host "Stopped $stoppedCount backend process(es)." -ForegroundColor Green
}

Write-Host ""

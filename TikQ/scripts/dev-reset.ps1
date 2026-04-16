$ErrorActionPreference = "Stop"

param(
    [int]$Pid
)

function Stop-BackendProcesses {
    param(
        [int]$TargetPid
    )

    if ($TargetPid) {
        try {
            Write-Host "[dev-reset] Stopping PID $TargetPid..."
            Stop-Process -Id $TargetPid -Force -ErrorAction Stop
        } catch {
            Write-Host "[dev-reset] PID $TargetPid not running or not accessible."
        }
    }

    $byName = Get-Process -Name "Ticketing.Backend" -ErrorAction SilentlyContinue
    foreach ($proc in $byName) {
        Write-Host "[dev-reset] Stopping Ticketing.Backend process PID $($proc.Id)..."
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    try {
        $dotnetProcs = Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
            Where-Object {
                $_.CommandLine -and $_.CommandLine -match "Ticketing.Backend\.dll"
            }
        foreach ($proc in $dotnetProcs) {
            Write-Host "[dev-reset] Stopping dotnet process PID $($proc.ProcessId) (Ticketing.Backend.dll)..."
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Host "[dev-reset] Skipped CIM process scan: $($_.Exception.Message)"
    }
}

function Clean-BinObj {
    param(
        [string]$RootPath
    )

    if (-not (Test-Path $RootPath)) {
        throw "Path not found: $RootPath"
    }

    Write-Host "[dev-reset] Cleaning bin/obj under $RootPath..."
    Get-ChildItem -Path $RootPath -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @("bin", "obj") } |
        ForEach-Object {
            try {
                Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction Stop
                Write-Host "[dev-reset] Removed $($_.FullName)"
            } catch {
                Write-Host "[dev-reset] Failed to remove $($_.FullName): $($_.Exception.Message)"
            }
        }
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$backendPath = Join-Path $repoRoot "backend\\Ticketing.Backend"

Stop-BackendProcesses -TargetPid $Pid
Clean-BinObj -RootPath $backendPath

Write-Host "[dev-reset] Running dotnet clean..."
dotnet clean "$backendPath\\Ticketing.Backend.sln"

Write-Host "[dev-reset] Running dotnet build..."
dotnet build "$backendPath\\Ticketing.Backend.sln"

Write-Host "[dev-reset] Starting backend..."
dotnet run --project "$backendPath\\Ticketing.Backend.csproj"

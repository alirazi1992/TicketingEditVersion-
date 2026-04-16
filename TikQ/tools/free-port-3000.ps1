# Find and optionally stop the process using port 3000 (e.g. stale Next.js dev server).
# Usage: .\free-port-3000.ps1
#        .\free-port-3000.ps1 -Kill   # Actually stop the process (only if it looks like node/Next)

param([switch]$Kill)

$port = 3000
Write-Host "Checking what is using port $port..." -ForegroundColor Cyan

$lines = netstat -ano | findstr ":$port "
if (-not $lines) {
    Write-Host "Nothing is listening on port $port." -ForegroundColor Green
    exit 0
}

$pids = @()
foreach ($line in $lines) {
    if ($line -match '\s+(\d+)\s*$') {
        $processId = $matches[1]
        if ($processId -ne '0' -and $pids -notcontains $processId) { $pids += $processId }
    }
}

if ($pids.Count -eq 0) {
    Write-Host "No PID found for port $port." -ForegroundColor Yellow
    exit 0
}

foreach ($processId in $pids) {
    $proc = Get-Process -Id $processId -ErrorAction SilentlyContinue
    $name = if ($proc) { $proc.ProcessName } else { "unknown" }
    Write-Host "  PID $processId : $name" -ForegroundColor White

    if ($Kill) {
        if ($name -match 'node|next') {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "  Stopped PID $processId ($name)." -ForegroundColor Green
        } else {
            Write-Host "  Skipped PID $processId (not node/next). Use Task Manager to stop if needed." -ForegroundColor Yellow
        }
    } else {
        Write-Host "  To stop this process run: .\free-port-3000.ps1 -Kill" -ForegroundColor Gray
    }
}

Write-Host "Done." -ForegroundColor Cyan

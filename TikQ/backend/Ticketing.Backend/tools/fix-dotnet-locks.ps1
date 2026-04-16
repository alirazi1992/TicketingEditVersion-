# fix-dotnet-locks.ps1
# Safely stops build servers and kills orphaned dotnet/msbuild processes

dotnet build-server shutdown 2>&1 | Out-Null
Get-Process | Where-Object {$_.ProcessName -match "msbuild|dotnet|VBCSCompiler"} | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
Write-Host "Build server processes stopped"

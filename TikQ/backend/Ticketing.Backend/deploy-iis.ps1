#Requires -Version 5.1
# TikQ Backend - IIS deployment script (production-safe, idempotent, atomic)
# Run from: same folder as Ticketing.Backend.csproj (or any folder; uses $PSScriptRoot for project path)
#
# Stdout/app logs: written to <PublishDir>\logs (see step 13; check there if health check fails).
#
# Atomic deployment (avoids file locks):
#   1. Publish to a new versioned folder: C:\publish\tikq-backend-<timestamp>
#   2. Configure and set permissions on that folder only (no in-place overwrite)
#   3. Switch IIS site physical path to the new folder (single atomic switch)
#   4. Recycle application pool so the app loads from the new path
#   5. Optionally keep last N versioned folders and remove older ones (cleanup)
param(
    [int]$KeepLastNFolders = 5
)

$ErrorActionPreference = "Stop"
# KeepLastNFolders: after successful deploy, keep this many tikq-backend-* folders in C:\publish and remove older ones. Set to 0 to skip cleanup.

# --- Config (safe variable names; no secrets) ---
$AppPoolName     = "TikQ"
$SiteName        = "TikQ"
$PublishRoot     = "C:\publish"
$SitePort        = 8080
$HealthUrl       = "http://localhost:8080/api/health"
$JwtSecretEnvVar = "TikQ_JWT_SECRET"
$MinSecretLength = 32

$ProjectPath     = Join-Path $PSScriptRoot "Ticketing.Backend.csproj"
$Timestamp       = Get-Date -Format "yyyyMMdd-HHmmss"
$PublishDir      = Join-Path $PublishRoot "tikq-backend-$Timestamp"

# --- Helpers ---
function Write-Step { param([string]$Message) 
$ErrorActionPreference = "Stop"
Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $Message" }
function Ensure-Dir  { param([string]$Path) 
$ErrorActionPreference = "Stop"
if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path -Force | Out-Null; Write-Step "Created: $Path" } }

# Redact password in connection strings for safe logging (never print secrets).
function Redact-ConnectionString {
    param([string]$Value)
    
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrEmpty($Value)) { return "" }
    if ($Value -match 'Password\s*=\s*[^;]+') { return $Value -replace 'Password\s*=\s*[^;]+', 'Password=***' }
    return $Value
}

function Grant-AppPoolFullControl {
    param([string]$TargetPath, [string]$AppPoolName)
    
$ErrorActionPreference = "Stop"
$principal = "IIS AppPool\${AppPoolName}"
    # Use -f to avoid PowerShell parsing "$principal:(OI)(CI)F" as invalid variable scope
    $grantArg = "{0}:(OI)(CI)F" -f $principal
    icacls $TargetPath /grant $grantArg
}

# --- 1. Validate JWT secret (must exist, >= 32 chars; never log value) ---
Write-Step "Validating $JwtSecretEnvVar ..."
$secret = [Environment]::GetEnvironmentVariable($JwtSecretEnvVar, "Process")
if (-not $secret) { $secret = [Environment]::GetEnvironmentVariable($JwtSecretEnvVar, "Machine") }
if (-not $secret) { $secret = [Environment]::GetEnvironmentVariable($JwtSecretEnvVar, "User") }
if (-not $secret) {
    Write-Host "ERROR: Environment variable '$JwtSecretEnvVar' is not set. Set it before deploying (e.g. [Environment]::SetEnvironmentVariable('$JwtSecretEnvVar','your-secret','Machine'))." -ForegroundColor Red
    throw "Validation failed: $JwtSecretEnvVar is missing."
}
if ($secret.Length -lt $MinSecretLength) {
    Write-Host "ERROR: $JwtSecretEnvVar must be at least $MinSecretLength characters (current length: $($secret.Length))." -ForegroundColor Red
    throw "Validation failed: $JwtSecretEnvVar too short."
}
Write-Step "JWT secret validated (length >= $MinSecretLength)."

# --- 1b. Bootstrap env vars (optional; if password set, validate and collect for injection) ---
$MinBootstrapPasswordLength = 8

# Admin
$BootstrapAdminPasswordEnvVar = "TikQ_BOOTSTRAP_ADMIN_PASSWORD"
$BootstrapAdminEmailEnvVar    = "TikQ_BOOTSTRAP_ADMIN_EMAIL"
$bootstrapAdminPassword = [Environment]::GetEnvironmentVariable($BootstrapAdminPasswordEnvVar, "Process")
if (-not $bootstrapAdminPassword) { $bootstrapAdminPassword = [Environment]::GetEnvironmentVariable($BootstrapAdminPasswordEnvVar, "Machine") }
if (-not $bootstrapAdminPassword) { $bootstrapAdminPassword = [Environment]::GetEnvironmentVariable($BootstrapAdminPasswordEnvVar, "User") }
$bootstrapAdminEmail = [Environment]::GetEnvironmentVariable($BootstrapAdminEmailEnvVar, "Process")
if (-not $bootstrapAdminEmail) { $bootstrapAdminEmail = [Environment]::GetEnvironmentVariable($BootstrapAdminEmailEnvVar, "Machine") }
if (-not $bootstrapAdminEmail) { $bootstrapAdminEmail = [Environment]::GetEnvironmentVariable($BootstrapAdminEmailEnvVar, "User") }
if ($bootstrapAdminPassword) {
    if ($bootstrapAdminPassword.Length -lt $MinBootstrapPasswordLength) {
        Write-Host "ERROR: $BootstrapAdminPasswordEnvVar is set but must be at least $MinBootstrapPasswordLength characters (current length: $($bootstrapAdminPassword.Length))." -ForegroundColor Red
        throw "Validation failed: $BootstrapAdminPasswordEnvVar too short."
    }
    if ([string]::IsNullOrWhiteSpace($bootstrapAdminEmail)) { $bootstrapAdminEmail = "admin@local" }
    else { $bootstrapAdminEmail = $bootstrapAdminEmail.Trim() }
    Write-Step "Bootstrap admin env vars present; will inject (password length >= $MinBootstrapPasswordLength, email set)."
}

# Client
$BootstrapClientPasswordEnvVar = "TikQ_BOOTSTRAP_CLIENT_PASSWORD"
$BootstrapClientEmailEnvVar    = "TikQ_BOOTSTRAP_CLIENT_EMAIL"
$bootstrapClientPassword = [Environment]::GetEnvironmentVariable($BootstrapClientPasswordEnvVar, "Process")
if (-not $bootstrapClientPassword) { $bootstrapClientPassword = [Environment]::GetEnvironmentVariable($BootstrapClientPasswordEnvVar, "Machine") }
if (-not $bootstrapClientPassword) { $bootstrapClientPassword = [Environment]::GetEnvironmentVariable($BootstrapClientPasswordEnvVar, "User") }
$bootstrapClientEmail = [Environment]::GetEnvironmentVariable($BootstrapClientEmailEnvVar, "Process")
if (-not $bootstrapClientEmail) { $bootstrapClientEmail = [Environment]::GetEnvironmentVariable($BootstrapClientEmailEnvVar, "Machine") }
if (-not $bootstrapClientEmail) { $bootstrapClientEmail = [Environment]::GetEnvironmentVariable($BootstrapClientEmailEnvVar, "User") }
if ($bootstrapClientPassword) {
    if ($bootstrapClientPassword.Length -lt $MinBootstrapPasswordLength) {
        Write-Host "ERROR: $BootstrapClientPasswordEnvVar is set but must be at least $MinBootstrapPasswordLength characters (current length: $($bootstrapClientPassword.Length))." -ForegroundColor Red
        throw "Validation failed: $BootstrapClientPasswordEnvVar too short."
    }
    if ([string]::IsNullOrWhiteSpace($bootstrapClientEmail)) { $bootstrapClientEmail = "client@local" }
    else { $bootstrapClientEmail = $bootstrapClientEmail.Trim() }
    Write-Step "Bootstrap client env vars present; will inject (password length >= $MinBootstrapPasswordLength, email set)."
}

# Technician
$BootstrapTechPasswordEnvVar = "TikQ_BOOTSTRAP_TECH_PASSWORD"
$BootstrapTechEmailEnvVar    = "TikQ_BOOTSTRAP_TECH_EMAIL"
$bootstrapTechPassword = [Environment]::GetEnvironmentVariable($BootstrapTechPasswordEnvVar, "Process")
if (-not $bootstrapTechPassword) { $bootstrapTechPassword = [Environment]::GetEnvironmentVariable($BootstrapTechPasswordEnvVar, "Machine") }
if (-not $bootstrapTechPassword) { $bootstrapTechPassword = [Environment]::GetEnvironmentVariable($BootstrapTechPasswordEnvVar, "User") }
$bootstrapTechEmail = [Environment]::GetEnvironmentVariable($BootstrapTechEmailEnvVar, "Process")
if (-not $bootstrapTechEmail) { $bootstrapTechEmail = [Environment]::GetEnvironmentVariable($BootstrapTechEmailEnvVar, "Machine") }
if (-not $bootstrapTechEmail) { $bootstrapTechEmail = [Environment]::GetEnvironmentVariable($BootstrapTechEmailEnvVar, "User") }
if ($bootstrapTechPassword) {
    if ($bootstrapTechPassword.Length -lt $MinBootstrapPasswordLength) {
        Write-Host "ERROR: $BootstrapTechPasswordEnvVar is set but must be at least $MinBootstrapPasswordLength characters (current length: $($bootstrapTechPassword.Length))." -ForegroundColor Red
        throw "Validation failed: $BootstrapTechPasswordEnvVar too short."
    }
    if ([string]::IsNullOrWhiteSpace($bootstrapTechEmail)) { $bootstrapTechEmail = "tech@local" }
    else { $bootstrapTechEmail = $bootstrapTechEmail.Trim() }
    Write-Step "Bootstrap technician env vars present; will inject (password length >= $MinBootstrapPasswordLength, email set)."
}

# Supervisor
$BootstrapSupervisorPasswordEnvVar = "TikQ_BOOTSTRAP_SUPERVISOR_PASSWORD"
$BootstrapSupervisorEmailEnvVar    = "TikQ_BOOTSTRAP_SUPERVISOR_EMAIL"
$bootstrapSupervisorPassword = [Environment]::GetEnvironmentVariable($BootstrapSupervisorPasswordEnvVar, "Process")
if (-not $bootstrapSupervisorPassword) { $bootstrapSupervisorPassword = [Environment]::GetEnvironmentVariable($BootstrapSupervisorPasswordEnvVar, "Machine") }
if (-not $bootstrapSupervisorPassword) { $bootstrapSupervisorPassword = [Environment]::GetEnvironmentVariable($BootstrapSupervisorPasswordEnvVar, "User") }
$bootstrapSupervisorEmail = [Environment]::GetEnvironmentVariable($BootstrapSupervisorEmailEnvVar, "Process")
if (-not $bootstrapSupervisorEmail) { $bootstrapSupervisorEmail = [Environment]::GetEnvironmentVariable($BootstrapSupervisorEmailEnvVar, "Machine") }
if (-not $bootstrapSupervisorEmail) { $bootstrapSupervisorEmail = [Environment]::GetEnvironmentVariable($BootstrapSupervisorEmailEnvVar, "User") }
if ($bootstrapSupervisorPassword) {
    if ($bootstrapSupervisorPassword.Length -lt $MinBootstrapPasswordLength) {
        Write-Host "ERROR: $BootstrapSupervisorPasswordEnvVar is set but must be at least $MinBootstrapPasswordLength characters (current length: $($bootstrapSupervisorPassword.Length))." -ForegroundColor Red
        throw "Validation failed: $BootstrapSupervisorPasswordEnvVar too short."
    }
    if ([string]::IsNullOrWhiteSpace($bootstrapSupervisorEmail)) { $bootstrapSupervisorEmail = "supervisor@local" }
    else { $bootstrapSupervisorEmail = $bootstrapSupervisorEmail.Trim() }
    Write-Step "Bootstrap supervisor env vars present; will inject (password length >= $MinBootstrapPasswordLength, email set)."
}

# --- 1c. Database provider and connection (required for IIS; read from Process/Machine/User) ---
$DatabaseProviderEnvVar       = "Database__Provider"
$ConnectionStringEnvVar       = "ConnectionStrings__DefaultConnection"
$AutoMigrateOnStartupEnvVar   = "Database__AutoMigrateOnStartup"

function Get-EnvVar {
    param(
        [Parameter(Mandatory=$true)][string]$Name,
        [string[]]$Scopes = @("Process","Machine","User")
    )

    foreach ($scope in $Scopes) {
        $v = [Environment]::GetEnvironmentVariable($Name, $scope)
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v.Trim() }
    }
    return $null
}

$databaseProvider = Get-EnvVar -Name $DatabaseProviderEnvVar
if ([string]::IsNullOrWhiteSpace($databaseProvider)) { $databaseProvider = "SqlServer" }

$databaseProvider = $databaseProvider.Trim()

$connectionString = Get-EnvVar -Name $ConnectionStringEnvVar
$autoMigrateOnStartup = Get-EnvVar -Name $AutoMigrateOnStartupEnvVar
# When SqlServer and not set, default to true so first deploy runs migrations
if ($databaseProvider -eq "SqlServer" -and [string]::IsNullOrWhiteSpace($autoMigrateOnStartup)) {
    $autoMigrateOnStartup = "true"
}

if ($databaseProvider -eq "SqlServer" -and [string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Host "ERROR: Database__Provider is 'SqlServer' but ConnectionStrings__DefaultConnection is not set. Set the connection string (e.g. [Environment]::SetEnvironmentVariable('ConnectionStrings__DefaultConnection','Server=...;Database=TikQ;...','Machine'))." -ForegroundColor Red
    throw "Validation failed: SqlServer requires ConnectionStrings__DefaultConnection."
}
Write-Step "Database provider: $databaseProvider (connection string present: $(-not [string]::IsNullOrWhiteSpace($connectionString)), AutoMigrateOnStartup: $autoMigrateOnStartup)."
if (-not [string]::IsNullOrWhiteSpace($connectionString)) {
    Write-Step "Connection string (redacted): $(Redact-ConnectionString $connectionString)"
}

# --- 2. Ensure publish root ---
Ensure-Dir $PublishRoot

# --- 3. Publish ASP.NET Core 8 (Release) ---
Write-Step "Publishing to $PublishDir ..."
if (-not (Test-Path $ProjectPath)) { throw "Project not found: $ProjectPath" }
dotnet publish $ProjectPath -c Release -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
Write-Step "Publish completed."

# --- 4. Ensure required folders in publish dir ---
Ensure-Dir (Join-Path $PublishDir "logs")
Ensure-Dir (Join-Path $PublishDir "App_Data")
Ensure-Dir (Join-Path $PublishDir "App_Data\keys")

# --- 5. Inject environment variables into web.config (idempotent; no secrets in logs) ---
$WebConfigPath = Join-Path $PublishDir "web.config"
if (-not (Test-Path $WebConfigPath)) { throw "web.config not found after publish: $WebConfigPath" }
[xml]$webConfig = Get-Content $WebConfigPath -Encoding UTF8
$aspNetCore = $webConfig.configuration.location.'system.webServer'.aspNetCore
if (-not $aspNetCore) { $aspNetCore = $webConfig.configuration.'system.webServer'.aspNetCore }
if (-not $aspNetCore) { throw "Could not find aspNetCore node in web.config" }

# Build hashtable of name -> value to inject (never log secret values)
$varsToInject = @{ "Jwt__Secret" = $secret }

# Database: provider, connection string, auto-migrate (required for reliable SQL Server on IIS)
$varsToInject["Database__Provider"] = $databaseProvider
if (-not [string]::IsNullOrWhiteSpace($connectionString)) {
    $varsToInject["ConnectionStrings__DefaultConnection"] = $connectionString
}
$varsToInject["Database__AutoMigrateOnStartup"] = $autoMigrateOnStartup

# Bootstrap: inject Bootstrap__* so app's BootstrapOptions see them (production-safe, idempotent when users empty)
if ($bootstrapAdminPassword) {
    $varsToInject["Bootstrap__Enabled"] = "true"
    $varsToInject["Bootstrap__AdminEmail"] = $bootstrapAdminEmail
    $varsToInject["Bootstrap__AdminPassword"] = $bootstrapAdminPassword
}
if ($bootstrapClientPassword) {
    $varsToInject["Bootstrap__TestClientEmail"] = $bootstrapClientEmail
    $varsToInject["Bootstrap__TestClientPassword"] = $bootstrapClientPassword
}
if ($bootstrapTechPassword) {
    $varsToInject["Bootstrap__TestTechEmail"] = $bootstrapTechEmail
    $varsToInject["Bootstrap__TestTechPassword"] = $bootstrapTechPassword
}
if ($bootstrapSupervisorPassword) {
    $varsToInject["Bootstrap__TestSupervisorEmail"] = $bootstrapSupervisorEmail
    $varsToInject["Bootstrap__TestSupervisorPassword"] = $bootstrapSupervisorPassword
}

# Ensure <environmentVariables> exists under <aspNetCore>
$envVars = $aspNetCore.environmentVariables
if (-not $envVars) {
    $envVars = $webConfig.CreateElement("environmentVariables")
    [void]$aspNetCore.AppendChild($envVars)
}

# Update or add each variable (idempotent: replace existing entries by name)
foreach ($varName in $varsToInject.Keys) {
    $value = $varsToInject[$varName]
    $existing = $envVars.environmentVariable | Where-Object { $_.name -eq $varName }
    if ($existing) {
        $existing.value = $value
    } else {
        $el = $webConfig.CreateElement("environmentVariable")
        $el.SetAttribute("name", $varName)
        $el.SetAttribute("value", $value)
        [void]$envVars.AppendChild($el)
    }
}

$webConfig.Save($WebConfigPath)
# Validate: never log secret values (only keys and redacted connection string)
$injectedNames = $varsToInject.Keys -join ", "
$secretKeyNames = @("Jwt__Secret", "ConnectionStrings__DefaultConnection", "Bootstrap__AdminPassword", "Bootstrap__TestClientPassword", "Bootstrap__TestTechPassword", "Bootstrap__TestSupervisorPassword")
$hasSecrets = ($varsToInject.Keys | Where-Object { $_ -in $secretKeyNames }).Count -gt 0
Write-Step "Injected into web.config: $injectedNames" + $(if ($hasSecrets) { " (secret keys present; values redacted, never logged)." } else { "." })

# --- 6. Grant full control to IIS AppPool identity ---
Write-Step "Granting permissions to IIS AppPool\${AppPoolName} on $PublishDir ..."
Grant-AppPoolFullControl -TargetPath $PublishDir -AppPoolName $AppPoolName
foreach ($sub in @("logs", "App_Data", "App_Data\keys")) {
    $subPath = Join-Path $PublishDir $sub
    if (Test-Path $subPath) { Grant-AppPoolFullControl -TargetPath $subPath -AppPoolName $AppPoolName }
}
Write-Step "Permissions set."

# --- 7. Ensure IIS module loaded ---
if (-not (Get-Module -ListAvailable -Name WebAdministration)) {
    Write-Host "WARNING: WebAdministration module not found. Install IIS management tools or run from a machine with IIS." -ForegroundColor Yellow
}
Import-Module WebAdministration -ErrorAction Stop

# --- 8. Create or update site: switch physical path to new folder (atomic; no file locks) ---
$sitePath = "IIS:\Sites\$SiteName"
if (-not (Test-Path $sitePath)) {
    Write-Step "Creating site ${SiteName} on port $SitePort ..."
    New-Website -Name $SiteName -PhysicalPath $PublishDir -Port $SitePort -ApplicationPool $AppPoolName
} else {
    Write-Step "Switching site ${SiteName} physical path to $PublishDir (atomic) ..."
    Set-ItemProperty -Path $sitePath -Name physicalPath -Value $PublishDir
}
# Ensure app pool exists and is assigned
$poolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path $poolPath)) {
    Write-Step "Creating application pool ${AppPoolName} ..."
    New-WebAppPool -Name $AppPoolName
}
Set-ItemProperty -Path $sitePath -Name applicationPool -Value $AppPoolName -ErrorAction SilentlyContinue
Write-Step "IIS site physical path set to $PublishDir."

# --- 9. Restart AppPool ---
Write-Step "Recycling application pool ${AppPoolName} ..."
Restart-WebAppPool -Name $AppPoolName

# --- 10. Restart IIS (optional; recycle often enough; uncomment if required) ---
Write-Step "Restarting IIS ..."
iisreset

# --- 11. Preflight / Verification: GET /api/health and assert provider when SqlServer ---
Write-Step "Waiting 5s for app to start..."
Start-Sleep -Seconds 5
Write-Step "Preflight: GET $HealthUrl ..."
$healthPass = $false
$providerPass = $true
try {
    $response = Invoke-WebRequest -Uri $HealthUrl -UseBasicParsing -TimeoutSec 15
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 300) {
        $healthPass = $true
        $json = $response.Content | ConvertFrom-Json
        if ($databaseProvider -eq "SqlServer") {
            $reportedProvider = $json.database.provider
            if ($reportedProvider -eq "SqlServer") {
                Write-Host "  Provider check: reported provider is SqlServer." -ForegroundColor Green
            } else {
                $providerPass = $false
                Write-Host "  Provider check: expected SqlServer but /api/health reported: $reportedProvider" -ForegroundColor Red
            }
        }
    }
} catch {
    Write-Host "  Request failed: $_" -ForegroundColor Red
}

if (-not ($healthPass -and $providerPass)) {
    # When SqlServer and health failed, check stdout log for SQL login error 18456
    $logsDir = Join-Path $PublishDir "logs"
    $latestLog = Get-ChildItem -Path $logsDir -Filter "stdout*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $latestLog) { $latestLog = Get-ChildItem -Path $logsDir -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
    $logContent = ""
    if ($latestLog) { $logContent = Get-Content $latestLog.FullName -Raw -ErrorAction SilentlyContinue }
    $is18456 = $databaseProvider -eq "SqlServer" -and $logContent -and ($logContent -match "18456" -or $logContent -match "Login failed for user 'IIS APPPOOL")
    if ($is18456) {
        Write-Host ""
        Write-Host "SQL login missing for IIS APPPOOL\$AppPoolName. Run the provided SQL script." -ForegroundColor Red
        Write-Host "  1. Open tools\_handoff_tests\sqlserver-permissions.sql in SSMS and execute (or run sqlserver-permissions.ps1 -DatabaseName TikQ -AppPoolName $AppPoolName and execute the output)." -ForegroundColor Yellow
        Write-Host "  2. Recycle the Application Pool: Restart-WebAppPool -Name $AppPoolName" -ForegroundColor Yellow
        Write-Host "  3. See docs/01_Runbook/IIS_SQLSERVER_PERMISSIONS.md and docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md (SQL Server permissions section)." -ForegroundColor Yellow
        Write-Host "  Stdout log: $($latestLog.FullName)" -ForegroundColor Gray
        Write-Host ""
    }
}

if ($healthPass -and $providerPass) {
    Write-Host "Verification: PASS (health OK" + $(if ($databaseProvider -eq "SqlServer") { ", provider SqlServer" } else { "" }) + ")." -ForegroundColor Green
} else {
    Write-Host "Verification: FAIL (health=$healthPass, providerCheck=$providerPass)." -ForegroundColor Red
    throw "Verification failed: health=$healthPass, providerCheck=$providerPass."
}

# --- 12. Optional cleanup: keep last N versioned folders, remove older ones ---
if ($KeepLastNFolders -gt 0) {
    Write-Step "Cleanup: keeping last $KeepLastNFolders tikq-backend-* folders in $PublishRoot ..."
    $versionedFolders = Get-ChildItem -Path $PublishRoot -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^tikq-backend-\d{8}-\d{6}$' } | Sort-Object Name -Descending
    $toRemove = $versionedFolders | Select-Object -Skip $KeepLastNFolders
    foreach ($dir in $toRemove) {
        Write-Step "Removing old folder: $($dir.FullName)"
        Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    if ($toRemove.Count -gt 0) { Write-Step "Removed $($toRemove.Count) old folder(s)." } else { Write-Step "No old folders to remove." }
}

# --- 13. Show latest stdout log (first 200 lines); logs folder is where the app writes stdout ---
$logsDir = Join-Path $PublishDir "logs"
$latestLog = Get-ChildItem -Path $logsDir -Filter "stdout*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $latestLog) { $latestLog = Get-ChildItem -Path $logsDir -Filter "*.log" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 1 }
if ($latestLog) {
    Write-Step "Latest log (first 200 lines): $($latestLog.FullName)"
    Get-Content $latestLog.FullName -TotalCount 200 -ErrorAction SilentlyContinue
} else {
    Write-Step "No stdout log found in $logsDir yet (app may still be starting)."
}

Write-Host ""
Write-Step "Deploy completed. Publish folder: $PublishDir"









<#
.SYNOPSIS
  Prints SQL script to create SQL Server login, database, and user for IIS App Pool identity.

.DESCRIPTION
  Use when deploy fails with "Login failed for user 'IIS APPPOOL\TikQ'" (18456).
  Output can be run in SSMS or piped to sqlcmd. Database and App Pool names are configurable.

.PARAMETER DatabaseName
  Database name (default: TikQ).

.PARAMETER AppPoolName
  IIS Application Pool name (default: TikQ). The Windows identity is IIS APPPOOL\<AppPoolName>.

.EXAMPLE
  .\sqlserver-permissions.ps1
  .\sqlserver-permissions.ps1 -DatabaseName TikQ -AppPoolName TikQ
  .\sqlserver-permissions.ps1 -DatabaseName MyDb -AppPoolName MyPool | Set-Content -Path out.sql
#>
param(
    [string]$DatabaseName = "TikQ",
    [string]$AppPoolName = "TikQ"
)

$loginName = "IIS APPPOOL\$AppPoolName"

$sql = @"
-- Generated for Database=$DatabaseName, AppPool=$AppPoolName (login: $loginName)
USE master;
GO
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'$($loginName -replace "'","''")')
BEGIN
    CREATE LOGIN [$loginName] FROM WINDOWS;
    PRINT 'Created login: $loginName';
END
ELSE
    PRINT 'Login $loginName already exists.';
GO
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'$($DatabaseName -replace "'","''")')
BEGIN
    CREATE DATABASE [$DatabaseName];
    PRINT 'Created database: $DatabaseName';
END
ELSE
    PRINT 'Database $DatabaseName already exists.';
GO
USE [$DatabaseName];
GO
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'$($loginName -replace "'","''")')
BEGIN
    CREATE USER [$loginName] FOR LOGIN [$loginName];
    PRINT 'Created user $loginName in database $DatabaseName.';
END
ELSE
    PRINT 'User $loginName already exists in database $DatabaseName.';
GO
IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals dp
    INNER JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
    INNER JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
    WHERE dp.name = N'$($loginName -replace "'","''")' AND r.name = N'db_owner'
)
BEGIN
    ALTER ROLE db_owner ADD MEMBER [$loginName];
    PRINT 'Added $loginName to db_owner in $DatabaseName.';
END
ELSE
    PRINT 'User already in db_owner.';
GO
PRINT 'Done. Recycle the IIS Application Pool ($AppPoolName), then GET /api/health';
"@

Write-Output $sql

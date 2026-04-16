-- =============================================================================
-- TikQ — SQL Server: Create DB (if missing), Windows Login, and DB User for IIS
-- =============================================================================
--
-- Use this when the app fails to start with:
--   "Login failed for user 'IIS APPPOOL\TikQ'" (SqlException 18456)
--
-- Run in SQL Server Management Studio (SSMS) connected to your instance
-- (e.g. localhost or .), as a principal that can create logins and databases
-- (e.g. sa or a login with sysadmin / securityadmin + dbcreator).
--
-- Default: Database name = TikQ, Application Pool name = TikQ.
-- For different names, run: .\sqlserver-permissions.ps1 -DatabaseName MyDb -AppPoolName MyPool
-- and use the printed SQL.
--
-- After running this script, recycle the IIS Application Pool (TikQ), then
-- GET /api/health should return 200 and database.provider = SqlServer.
--
-- =============================================================================

USE master;
GO

-- 1) Create Windows login for the IIS Application Pool identity
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = N'IIS APPPOOL\TikQ')
BEGIN
    CREATE LOGIN [IIS APPPOOL\TikQ] FROM WINDOWS;
    PRINT 'Created login: IIS APPPOOL\TikQ';
END
ELSE
    PRINT 'Login IIS APPPOOL\TikQ already exists.';
GO

-- 2) Create database if it does not exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = N'TikQ')
BEGIN
    CREATE DATABASE [TikQ];
    PRINT 'Created database: TikQ';
END
ELSE
    PRINT 'Database TikQ already exists.';
GO

-- 3) In the TikQ database: create user from login and grant db_owner (for migrations)
USE [TikQ];
GO

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = N'IIS APPPOOL\TikQ')
BEGIN
    CREATE USER [IIS APPPOOL\TikQ] FOR LOGIN [IIS APPPOOL\TikQ];
    PRINT 'Created user IIS APPPOOL\TikQ in database TikQ.';
END
ELSE
    PRINT 'User IIS APPPOOL\TikQ already exists in database TikQ.';
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.database_principals dp
    INNER JOIN sys.database_role_members drm ON dp.principal_id = drm.member_principal_id
    INNER JOIN sys.database_principals r ON r.principal_id = drm.role_principal_id
    WHERE dp.name = N'IIS APPPOOL\TikQ' AND r.name = N'db_owner'
)
BEGIN
    ALTER ROLE db_owner ADD MEMBER [IIS APPPOOL\TikQ];
    PRINT 'Added IIS APPPOOL\TikQ to db_owner in TikQ.';
END
ELSE
    PRINT 'IIS APPPOOL\TikQ is already in db_owner.';
GO

PRINT 'Done. Recycle the IIS Application Pool (TikQ), then verify: GET http://localhost:8080/api/health';

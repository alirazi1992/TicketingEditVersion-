-- =============================================================================
-- TikQ Database Reset (SQL Server) - TEST ENVIRONMENTS ONLY
-- =============================================================================
--
-- HOW TO RUN:
--   - In SSMS: Connect to your SQL Server instance, open this file, and execute.
--   - Ensure you are connected to the server (not to the TikQ database).
--   - Or use the PowerShell helper: .\tools\sql\reset-tikq-db.ps1 -Force
--
-- WARNING: This script DESTROYS all data in the TikQ database. Use only in
--          test/development environments. Never run against production.
--
-- =============================================================================

USE master;
GO

IF DB_ID(N'TikQ') IS NOT NULL
BEGIN
    ALTER DATABASE [TikQ] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [TikQ];
END
GO

CREATE DATABASE [TikQ];
GO

-- =============================================================================
-- TikQ: Idempotent schema fix for SubcategoryFieldDefinitions (SQL Server)
-- =============================================================================
-- Fixes "Invalid column name CreatedAt/FieldKey/IsActive/SortOrder/UpdatedAt"
-- in TicketRepository.GetByIdWithIncludesAsync by aligning the table with the
-- EF Core model. Safe to run multiple times (no data loss).
--
-- Run from repo root with sqlcmd, e.g.:
--   sqlcmd -S . -d TikQ -E -i tools\sql\fix-subcategory-field-definitions-schema.sql
-- Or set -S, -U/-P, -d as needed. Use -E for Windows auth.
-- =============================================================================

SET NOCOUNT ON;
GO

-- -----------------------------------------------------------------------------
-- 1) Rename [Key] -> [FieldKey] if still present (migration 20260223100000)
-- -----------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubcategoryFieldDefinitions_SubcategoryId_Key' AND object_id = OBJECT_ID(N'dbo.SubcategoryFieldDefinitions'))
    DROP INDEX [IX_SubcategoryFieldDefinitions_SubcategoryId_Key] ON [dbo].[SubcategoryFieldDefinitions];
GO

IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'SubcategoryFieldDefinitions' AND COLUMN_NAME = N'Key')
BEGIN
    EXEC sp_rename N'dbo.SubcategoryFieldDefinitions.[Key]', N'FieldKey', N'COLUMN';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SubcategoryFieldDefinitions_SubcategoryId_FieldKey' AND object_id = OBJECT_ID(N'dbo.SubcategoryFieldDefinitions'))
BEGIN
    CREATE UNIQUE INDEX [IX_SubcategoryFieldDefinitions_SubcategoryId_FieldKey] ON [dbo].[SubcategoryFieldDefinitions] ([SubcategoryId], [FieldKey]);
END
GO

-- -----------------------------------------------------------------------------
-- 2) Add SortOrder, IsActive, CreatedAt, UpdatedAt (migration 20260224100000)
-- -----------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.SubcategoryFieldDefinitions', N'SortOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[SubcategoryFieldDefinitions] ADD [SortOrder] int NOT NULL CONSTRAINT [DF_SubcategoryFieldDefinitions_SortOrder] DEFAULT 0;
END
GO

IF COL_LENGTH(N'dbo.SubcategoryFieldDefinitions', N'IsActive') IS NULL
BEGIN
    ALTER TABLE [dbo].[SubcategoryFieldDefinitions] ADD [IsActive] bit NOT NULL CONSTRAINT [DF_SubcategoryFieldDefinitions_IsActive] DEFAULT 1;
END
GO

IF COL_LENGTH(N'dbo.SubcategoryFieldDefinitions', N'CreatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[SubcategoryFieldDefinitions] ADD [CreatedAt] datetime2(7) NOT NULL CONSTRAINT [DF_SubcategoryFieldDefinitions_CreatedAt] DEFAULT GETUTCDATE();
END
GO

IF COL_LENGTH(N'dbo.SubcategoryFieldDefinitions', N'UpdatedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[SubcategoryFieldDefinitions] ADD [UpdatedAt] datetime2(7) NULL;
END
GO

PRINT 'SubcategoryFieldDefinitions schema fix completed (idempotent).';
GO

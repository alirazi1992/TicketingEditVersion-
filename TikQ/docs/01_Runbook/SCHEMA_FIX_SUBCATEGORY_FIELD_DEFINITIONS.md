# Schema fix: SubcategoryFieldDefinitions (SQL Server)

**Purpose:** Fix SQL Server schema mismatch that causes `Invalid column name CreatedAt/FieldKey/IsActive/SortOrder/UpdatedAt` in `TicketRepository.GetByIdWithIncludesAsync`. The fix is done by applying the correct EF Core migrations (or an idempotent SQL script) to SQL Server—no change to business logic or EF model.

**Scope:** Table `SubcategoryFieldDefinitions`. Ensures columns: `FieldKey` (renamed from `Key`), `SortOrder`, `IsActive`, `CreatedAt`, `UpdatedAt`.

---

## 1. Option A: Apply via EF Core migrations (recommended)

From the **repository root** (or backend project directory):

```powershell
# 1) Restore and build
cd c:\Users\user\Desktop\42\TikQ
dotnet restore backend\Ticketing.Backend\Ticketing.Backend.csproj
dotnet build backend\Ticketing.Backend\Ticketing.Backend.csproj --no-restore

# 2) Apply all pending migrations to TikQ (SQL Server)
# Set connection string via env or appsettings; e.g.:
$env:Database__Provider = "SqlServer"
$env:ConnectionStrings__DefaultConnection = "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;"
dotnet ef database update --project backend\Ticketing.Backend\Ticketing.Backend.csproj --startup-project backend\Ticketing.Backend\Ticketing.Backend.csproj

# 3) Verify columns exist (see Section 3)
.\tools\_handoff_tests\verify-schema-and-migrations.ps1 -ConnectionString "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;"
```

If the app runs under IIS and uses **AutoMigrateOnStartup**, recycling the app pool after deploying the new migration will also apply it (no manual `dotnet ef database update` needed).

---

## 2. Option B: Idempotent SQL script + sqlcmd

Use this when you prefer to run a single SQL script (e.g. from a DBA or release pipeline) without running the .NET app.

### 2.1 Generate idempotent script from migrations (optional)

To produce a single SQL file that includes the two migrations (Key→FieldKey and audit columns):

```powershell
cd c:\Users\user\Desktop\42\TikQ

# From migration before the two fixes to latest (generates SQL for 20260223100000 + 20260224100000)
dotnet ef migrations script 20260222054458_AddTechnicianSubcategoryPermissions 20260224100000_AddSubcategoryFieldDefinitionAuditColumns `
  --project backend\Ticketing.Backend\Ticketing.Backend.csproj `
  --startup-project backend\Ticketing.Backend\Ticketing.Backend.csproj `
  --idempotent `
  --output tools\sql\migrations-subcategory-field-definitions-idempotent.sql
```

Note: The generated script may include other migrations between those two; you can use it as-is or copy only the `SubcategoryFieldDefinitions`-related statements. For a **standalone idempotent script** that only fixes this table, use the provided file below.

### 2.2 Apply the standalone idempotent script to TikQ

A pre-written idempotent script is in the repo:

- **Script:** `tools\sql\fix-subcategory-field-definitions-schema.sql`

**PowerShell (Windows auth):**

```powershell
cd c:\Users\user\Desktop\42\TikQ
sqlcmd -S . -d TikQ -E -i tools\sql\fix-subcategory-field-definitions-schema.sql
```

**PowerShell (SQL auth):**

```powershell
sqlcmd -S YourServer -d TikQ -U YourUser -P YourPassword -i tools\sql\fix-subcategory-field-definitions-schema.sql
```

**Important:** After running the script manually, you must **record the migrations in `__EFMigrationsHistory`** so the app does not try to re-apply them. Insert the two migration IDs if they are not already present:

```sql
-- Run in TikQ database (e.g. via sqlcmd or SSMS)
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260223100000_RenameSubcategoryFieldDefinitionKeyToFieldKey', N'8.0.4');
INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260224100000_AddSubcategoryFieldDefinitionAuditColumns', N'8.0.4');
```

(Use `IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'...')` if you want to make the inserts idempotent.)

### 2.3 Verify columns exist

See Section 3 below.

---

## 3. Verify columns and migration history

Run the verification script (recommended):

```powershell
cd c:\Users\user\Desktop\42\TikQ
.\tools\_handoff_tests\verify-schema-and-migrations.ps1 -ConnectionString "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;"
```

The script prints:

1. Current DB name and whether required columns exist on `SubcategoryFieldDefinitions`.
2. Migration history tail (last rows of `__EFMigrationsHistory`).
3. Success/failure messages.

**Manual verification (SQL):**

```sql
-- Columns on SubcategoryFieldDefinitions
SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'SubcategoryFieldDefinitions'
ORDER BY ORDINAL_POSITION;

-- You should see: FieldKey, SortOrder, IsActive, CreatedAt, UpdatedAt (and others).

-- Migration history tail
SELECT TOP 5 MigrationId, ProductVersion
FROM [dbo].[__EFMigrationsHistory]
ORDER BY MigrationId DESC;
```

---

## 4. Endpoint verification checklist

After applying the schema fix, confirm these endpoints work (they use `GetByIdWithIncludesAsync` and therefore hit `SubcategoryFieldDefinitions`):

| # | Method | Endpoint | What to check |
|---|--------|----------|----------------|
| 1 | **GET** | `/api/tickets/{id}` | Replace `{id}` with a real ticket GUID. Expect 200 and full ticket payload (category, subcategory, field values, messages, etc.). No 500 with "Invalid column name". |
| 2 | **POST** | `/api/tickets/{id}/seen` | Replace `{id}` with a real ticket GUID. Expect 200 (or 204). No 500 with "Invalid column name". |
| 3 | **GET** | `/api/tickets/{id}/messages` | Replace `{id}` with a real ticket GUID. Expect 200 and list of messages. No 500 with "Invalid column name". |

**Quick test (PowerShell):**

```powershell
$baseUrl = "http://localhost:8080"   # or your backend URL
$ticketId = "<valid-ticket-guid>"    # get one from GET /api/tickets or DB

# 1) GET ticket
Invoke-RestMethod -Uri "$baseUrl/api/tickets/$ticketId" -Method Get

# 2) POST seen (may require auth cookie or Bearer token)
Invoke-RestMethod -Uri "$baseUrl/api/tickets/$ticketId/seen" -Method Post

# 3) GET messages
Invoke-RestMethod -Uri "$baseUrl/api/tickets/$ticketId/messages" -Method Get
```

If any of these return 500 with a message containing `Invalid column name 'FieldKey'` (or `CreatedAt`/`IsActive`/`SortOrder`/`UpdatedAt`), the schema fix was not applied or not applied to the database the app is using.

---

## 5. Related docs

- [MIGRATIONS.md](MIGRATIONS.md) – General EF Core migration usage and AutoMigrateOnStartup.
- [SQLSERVER_IIS_AUDIT_REPORT.md](SQLSERVER_IIS_AUDIT_REPORT.md) – Failure modes and troubleshooting.

---

*Runbook: Schema fix SubcategoryFieldDefinitions. Fix by migrations or idempotent SQL; verify with verify-schema-and-migrations.ps1.*

# Database migrations

TikQ uses **Entity Framework Core** migrations. The database provider is selected via **Database:Provider** (or environment variable **Database__Provider**): `Sqlite` or `SqlServer` (case-insensitive). Unknown values cause startup failure.

## Production / IIS environment variables

For **Production** with **SQL Server**, set these (e.g. in IIS Application Pool → Configuration → Environment variables or web.config):

| Variable | Description |
|----------|-------------|
| **Database__Provider** | `SqlServer` or `Sqlite`. appsettings.Production.json defaults to `SqlServer`. |
| **ConnectionStrings__DefaultConnection** | SQL Server connection string. **Required** when Provider is SqlServer in Production; otherwise the app throws at startup (fail-fast). |

If **Database__Provider** is invalid (not Sqlite/SqlServer), the app throws at startup with a clear message.

## Apply migrations

From the backend project directory (where the DbContext and migrations live):

```bash
dotnet ef database update --project "Ticketing.Backend" --startup-project "Ticketing.Backend"
```

If your solution layout differs, point `--project` to the project that contains `Infrastructure/Data/Migrations` and `AppDbContext`, and `--startup-project` to the executable (e.g. the API host).

- **SQL Server**: Migrations run against the database specified in `ConnectionStrings:DefaultConnection`. Ensure the connection string is correct and the database exists (it can be created manually or by the app if configured to do so).
- **SQLite**: The database file path is in the connection string (e.g. `Data Source=tikq.db`). The file is created if it does not exist.

After a successful run, `__EFMigrationsHistory` will list the applied migrations and the schema will match the current model.

**AutoMigrateOnStartup:** In Development, migrations run on app startup by default (`Database:AutoMigrateOnStartup` true). In Production, the default is false; set `Database:AutoMigrateOnStartup` to true in config (or **Database__AutoMigrateOnStartup** via env) if you want the app to apply pending migrations on startup. Otherwise apply migrations manually (see above).

### First-run migration procedure (SQL Server)

When **AutoMigrateOnStartup** is true and the SQL Server connection is valid, the app applies all pending migrations on startup. Use this for first deployment or when you want the app to self-update the schema.

| Step | Action |
|------|--------|
| 1 | Set **Database__Provider** = `SqlServer` and **ConnectionStrings__DefaultConnection** = your SQL Server connection string (e.g. via IIS Application Pool env or `deploy-iis.ps1`). |
| 2 | Set **Database__AutoMigrateOnStartup** = `true` (config or env **Database__AutoMigrateOnStartup**). |
| 3 | Ensure the database exists and the app identity has permission to create/alter tables and insert into `__EFMigrationsHistory` (e.g. `db_owner` or `db_ddladmin` + `db_datawriter`). |
| 4 | Start or recycle the app. Logs will show `[MIGRATION] Pending migrations: ...`, then `[MIGRATION] Applying: ...`, then `[MIGRATION] Result: applied N migration(s). Migrations completed successfully.` |
| 5 | Verify: **GET /api/health** → `database.pendingMigrationsCount` should be **0** and `database.lastMigrationId` should be the latest migration ID. |
| 6 | *(Optional)* After first run, set **Database__AutoMigrateOnStartup** = `false` and apply future migrations manually (`dotnet ef database update`) for tighter control. |

**Fail-fast:** In **Production** with **SqlServer**, if migration fails (e.g. permissions, timeout), the app **stops startup** and does not serve traffic until the database or connection is fixed.

### Enabling AutoMigrateOnStartup for first deployment

For the **first deployment** (new server or new database), you can let the app apply migrations on startup:

1. **Set the flag** in config or environment:
   - **Config:** In `appsettings.Production.json` set `"Database": { "AutoMigrateOnStartup": true }` or set the same in your deployment config.
   - **Environment (IIS):** Add `Database__AutoMigrateOnStartup` = `true` in the Application Pool environment variables.
2. Ensure **ConnectionStrings:DefaultConnection** (or **ConnectionStrings__DefaultConnection**) points to the correct SQL Server database. The app pool identity must have permissions to create/alter tables and run migrations (e.g. `db_ddladmin` or equivalent).
3. Start or recycle the app. On startup the app will log `[MIGRATION] Target provider: SqlServer`, list pending migrations, apply them, and log success or fail.
4. **Optional after first run:** Set `AutoMigrateOnStartup` back to `false` and apply future migrations manually (e.g. `dotnet ef database update`) so you control when schema changes are applied.

Migrations run **after** the host is built, under the same process identity (e.g. IIS app pool). They do not run during publish or in a separate process, so there are no extra permission or path issues beyond normal app startup.

## First run checklist

Before or right after the first deployment, verify:

| Step | What to check |
|------|----------------|
| **Connection OK** | Backend can reach the database. Use **GET /api/health**: response should include `database.provider` and `database.connectionInfoRedacted`. If the app fails to start, check `ConnectionStrings__DefaultConnection` and that the database exists. |
| **Permissions OK** | The account running the app (e.g. IIS app pool identity) can connect and, if using AutoMigrateOnStartup, can create/alter tables and insert into `__EFMigrationsHistory`. For SQL Server, ensure the login has appropriate roles (e.g. `db_owner` or `db_ddladmin` + `db_datawriter`). |
| **Migrate OK** | If AutoMigrateOnStartup is true: check logs for `[MIGRATION] Migrations completed successfully` and that no migration error is reported. If AutoMigrateOnStartup is false: run `dotnet ef database update` from the backend project and confirm no errors, then start the app. |

If migration fails in **Production** with **SqlServer**, the app **fails fast** (throws and does not start) so you fix the database or connection before serving traffic. In Development or with SQLite, the app logs the error and continues so you can inspect and fix manually.

### Health check: migration status

**GET /api/health** returns safe migration diagnostics (no secrets):

- **database.pendingMigrationsCount** – number of migrations not yet applied. After a successful startup with **AutoMigrateOnStartup** = true and a valid SQL Server connection, this should be **0**.
- **database.lastMigrationId** – the ID of the last applied migration (e.g. `20260222054458_AddTechnicianSubcategoryPermissions`). Omitted if the app cannot connect to the database.

Use these to verify that first-run migrations completed: `pendingMigrationsCount === 0` and `lastMigrationId` set.

**Verification gate:** If **AutoMigrateOnStartup** = true and the SQL Server connection is valid, **pendingMigrationsCount** should become **0** after startup. If it does not:

| Cause | What to check | Fix |
|-------|----------------|-----|
| Connection permissions | App identity cannot create/alter tables or write to `__EFMigrationsHistory`. | Grant `db_ddladmin` (or equivalent) and ensure database exists. |
| Wrong provider | Health shows `provider: Sqlite` but you expect SqlServer. | Set **Database__Provider** = `SqlServer` and **ConnectionStrings__DefaultConnection** (e.g. via deploy-iis.ps1 or IIS env). |
| Assembly scan | Migrations not found at runtime. | Ensure migrations live in the same assembly as `AppDbContext` and have correct `[Migration("...")]` / `[DbContext(...)]` attributes. Run `dotnet test --filter MigrationDiscoveryTests` to verify no duplicate IDs and discoverability. |
| Command timeout | Migration runs too long and times out. | Increase SQL Server command timeout in connection string or DbContext options if needed. |

## Key → FieldKey migration (SubcategoryFieldDefinitions)

Migration **20260223100000_RenameSubcategoryFieldDefinitionKeyToFieldKey** renames the column `Key` to `FieldKey` in table `SubcategoryFieldDefinitions` so the app works reliably on **SQL Server** (where `Key` is a reserved keyword).

- **SQL Server**: The migration uses conditional `IF EXISTS` / `IF NOT EXISTS` so it can be run safely even if the index or column was already changed.
- **SQLite**: The migration adds column `FieldKey`, copies data from `Key`, then drops `Key`.

If this migration is **not** applied, the app still tries to use the `FieldKey` column. The repository includes a **backward-compatible fallback**: if the query fails with “Invalid column name 'FieldKey'” (SQL Server) or “no such column” (SQLite), it retries using the legacy `Key` column (aliased as `FieldKey`) so the API continues to return the same JSON (`"key"`). To avoid 500 errors and get the correct schema, run:

```bash
dotnet ef database update
```

Then confirm in SQL Server that the column is `FieldKey`:

```sql
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = N'dbo' AND TABLE_NAME = N'SubcategoryFieldDefinitions' AND COLUMN_NAME IN (N'FieldKey', N'Key');
```

You should see `FieldKey` only.

## SubcategoryFieldDefinitions: missing SortOrder, IsActive, CreatedAt, UpdatedAt (SQL Server)

If you see **Invalid column name 'CreatedAt'**, **'SortOrder'**, **'IsActive'**, or **'UpdatedAt'** when loading a ticket (e.g. GET /api/tickets/{id}), the SQL Server table `SubcategoryFieldDefinitions` is missing columns that the EF model expects. Apply the migrations **20260223100000_RenameSubcategoryFieldDefinitionKeyToFieldKey** and **20260224100000_AddSubcategoryFieldDefinitionAuditColumns** (via `dotnet ef database update` or the idempotent SQL script), then verify with the runbook and script:

- **Runbook:** [SCHEMA_FIX_SUBCATEGORY_FIELD_DEFINITIONS.md](SCHEMA_FIX_SUBCATEGORY_FIELD_DEFINITIONS.md)
- **Verification script:** `.\tools\_handoff_tests\verify-schema-and-migrations.ps1 -ConnectionString "Server=.;Database=TikQ;Trusted_Connection=True;TrustServerCertificate=True;"`

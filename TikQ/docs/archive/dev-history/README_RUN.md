# Backend - How to Run

## Entry Point

The backend entry point is: **`Ticketing.Backend.csproj`**

**Note:** The `src/` directory contains an old project structure that is excluded from compilation. Do not use `src/Ticketing.Api/Ticketing.Api.csproj` as the entry point.

## Quick Start

### Prerequisites
- .NET 8 SDK installed
- SQLite (bundled with .NET)

### Run Backend

```powershell
cd backend/Ticketing.Backend
dotnet run
```

Or explicitly:

```powershell
cd backend/Ticketing.Backend
dotnet run --project Ticketing.Backend.csproj
```

### Build Only

```powershell
cd backend/Ticketing.Backend
dotnet clean
dotnet build
```

### Apply Migrations Manually (if needed)

```powershell
cd backend/Ticketing.Backend
dotnet ef database update
```

## What Happens on Startup

1. **Database Path Resolution:**
   - Resolves SQLite DB path to `App_Data/ticketing.db` (absolute path)
   - Logs the resolved path

2. **Migrations:**
   - Automatically applies pending migrations via `Database.MigrateAsync()`
   - Logs applied and pending migrations
   - Post-migration check: Verifies `DefaultValue` column exists in `SubcategoryFieldDefinitions`
   - If column is missing, attempts to add it automatically

3. **Seeding:**
   - Seeds initial data (users, categories, etc.)

4. **Server:**
   - Starts on `http://localhost:5000` (HTTP)
   - Swagger UI available at `http://localhost:5000/swagger`

## Default Test Users

- **Admin:** `admin@test.com` / `Admin123!`
- **Technician:** `tech1@test.com` / `Tech123!`
- **Client:** `client1@test.com` / `Client123!`

## Troubleshooting

### Migration Errors

If you see "duplicate column" errors:
- This is expected if the column already exists
- The app will continue running (error is caught and logged)
- Check logs for: `[MIGRATION] DefaultValue column already exists - no action needed`

If you see "no such column: DefaultValue":
- The migration may not have applied
- Restart the backend (migrations apply on every startup)
- Or run manually: `dotnet ef database update`

### Database Location

The database file is located at:
- `backend/Ticketing.Backend/App_Data/ticketing.db`

The path is resolved relative to `ContentRoot`, so it's always in the same location regardless of working directory.

## Project Structure

```
Ticketing.Backend/
├── Domain/          # Entities, Enums (no dependencies)
├── Application/     # DTOs, Services (depends on Domain)
├── Infrastructure/  # Data (DbContext, Migrations), Auth (depends on Domain)
├── Api/             # Controllers (depends on Application, Infrastructure)
├── Program.cs        # Entry point, DI configuration
└── Ticketing.Backend.csproj  # Main project file
```

---

**Last Updated:** 2025-12-30







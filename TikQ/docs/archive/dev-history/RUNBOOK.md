# TikQ Runbook

This document provides step-by-step instructions for running, troubleshooting, and maintaining the TikQ ticketing system.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Quick Start](#quick-start)
3. [Running the Application](#running-the-application)
4. [Database Management](#database-management)
5. [Verification Steps](#verification-steps)
6. [Troubleshooting](#troubleshooting)
7. [Development Workflow](#development-workflow)

---

## Prerequisites

### Required Software

- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** and **npm** - [Download](https://nodejs.org/)
- **Git** (for version control)

### Verify Installation

```powershell
# Check .NET version
dotnet --version
# Should show: 8.0.x or higher

# Check Node.js version
node --version
# Should show: v18.x.x or higher

# Check npm version
npm --version
```

---

## Quick Start

### 1. Clone and Setup

```powershell
# Navigate to project root
cd D:\Projects\TikQ

# Install frontend dependencies
cd frontend
npm install

# Restore backend dependencies
cd ..\backend\Ticketing.Backend
dotnet restore
```

### 2. Run Sanity Check

```powershell
# From project root
.\tools\sanity.ps1
```

This will:
- Build backend and frontend
- Check for common issues
- Verify project structure

---

## Running the Application

### Option 1: Using Helper Scripts (Recommended for Windows)

#### Starting the Backend (Windows - Recommended)

**Always use the helper script to prevent file-lock errors:**

```powershell
# From project root
.\tools\run-backend.ps1
```

This script:
- ✅ Automatically stops any stale backend processes
- ✅ Prevents MSB3027/MSB3021 file-lock errors
- ✅ Handles port conflicts (falls back to 5001 if 5000 is in use)
- ✅ Starts backend on `http://127.0.0.1:5000` (or 5001 if needed)
- ✅ Shows Swagger URL in output

**The backend will:**
- Start on `http://127.0.0.1:5000` (HTTP)
- Automatically apply database migrations on startup
- Seed default users (see [Default Users](#default-users))
- Enable Swagger UI at `/swagger`

#### Stopping the Backend

**To stop the backend cleanly:**

```powershell
# From project root
.\tools\stop-backend.ps1
```

This script:
- ✅ Finds and stops processes on ports 5000 and 5001
- ✅ Stops processes named `Ticketing.Backend` or `Ticketing.Api`
- ✅ Stops dotnet processes running Ticketing DLLs
- ✅ Only stops processes confirmed to be this backend (safety check)
- ✅ Shuts down build server to release file locks
- ✅ Prints what was stopped (PID + name)

**Manual stop:** Press `Ctrl+C` in the terminal where backend is running.

#### Important: Preventing File-Lock Errors on Windows

**❌ DO NOT run `dotnet run` while another instance is running:**
- This causes MSB3027/MSB3021 errors (file locked by another process)
- The executable (`Ticketing.Backend.exe`) cannot be overwritten while running

**✅ ALWAYS use the helper scripts:**
- `.\tools\run-backend.ps1` - Automatically stops stale processes first
- `.\tools\stop-backend.ps1` - Manually stop if needed

**Manual start (not recommended on Windows):**
```powershell
cd backend\Ticketing.Backend
# First, stop any running instances:
..\..\tools\stop-backend.ps1
# Then start:
dotnet run
```

#### Frontend

```powershell
# From project root
cd frontend
npm run dev
```

The frontend will start on `http://localhost:3000`

### Option 2: Manual Start

**Terminal 1 - Backend:**
```powershell
cd backend\Ticketing.Backend
dotnet run
```

**Terminal 2 - Frontend:**
```powershell
cd frontend
npm run dev
```

---

## Database Management

### Database Provider (Sqlite / SqlServer)

The backend supports two database providers, selected **only by configuration** (not by environment name):

- **`Database:Provider`**: `"Sqlite"` (default) or `"SqlServer"`.
- **`ConnectionStrings:DefaultConnection`**: For Sqlite use `Data Source=App_Data/ticketing.db` (or path). For SqlServer use a full SQL Server connection string.

**Development (default):** `appsettings.Development.json` sets `Provider: Sqlite` and uses the SQLite file under `App_Data/ticketing.db`. No changes needed for local dev.

**Production (SQL Server):** `appsettings.Production.json` sets `Provider: SqlServer`. Do **not** put secrets in the file. On IIS or your host, set the connection string via environment variable:

- **`ConnectionStrings__DefaultConnection`** = `Server=...;Database=TikQ;User Id=...;Password=...;TrustServerCertificate=true;` (or use Integrated Security).

Example (PowerShell, before starting the app):

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=YOUR_SERVER;Database=TikQ;User Id=...;Password=...;TrustServerCertificate=true;"
$env:Database__Provider = "SqlServer"
dotnet run
```

**Rollback if SQL Server fails:** Set `Database:Provider` back to `Sqlite` (e.g. in appsettings or via `Database__Provider=Sqlite`), ensure `ConnectionStrings:DefaultConnection` points to your SQLite file, and restart. The SQLite file is never deleted or modified by the provider switch.

**Smoke test (both providers):** From repo root:

```powershell
# Sqlite (backend must be running, or use -StartBackend)
.\tools\_handoff_tests\test-db-provider.ps1 -StartBackend

# SqlServer (pass your connection string)
.\tools\_handoff_tests\test-db-provider.ps1 -StartBackend -Provider SqlServer -ConnectionString "Server=.;Database=TikQ;Integrated Security=true;TrustServerCertificate=true;"
```

### Database Location

The SQLite database is located at:
```
backend\Ticketing.Backend\App_Data\ticketing.db
```

### Automatic Migrations

Migrations are **automatically applied** when the backend starts. The startup process:
1. Checks for pending migrations
2. Applies them automatically
3. Seeds default data (users, categories, etc.)

### Reset Development Database

**⚠️ WARNING: This will delete all data in the development database!**

```powershell
# From backend directory
cd backend\Ticketing.Backend
.\tools\reset-dev-db.ps1
```

The script will:
1. Ask for confirmation (type `YES` to proceed)
2. Create a timestamped backup in `App_Data\backup\`
3. Delete the current database
4. Provide instructions to restart the backend

**After reset:**
1. Start the backend: `dotnet run`
2. Migrations will be applied automatically
3. Default users will be seeded

### Reset SQL Server DB (test only)

Use this when the TikQ SQL Server database is partially created (e.g. after a failed migration) and you want a clean database before re-running migrations. **Destroys all data in the TikQ database — test environments only.**

**Option 1 — SSMS:** Open and run the script on your SQL Server instance:

```
tools/sql/reset-tikq-db.sql
```

**Option 2 — PowerShell (requires sqlcmd):** From repo root, dry-run by default; add `-Force` to execute:

```powershell
.\tools\sql\reset-tikq-db.ps1 -Server "." -Force
```

Without `-Force`, the script only prints what it would do and exits.

### Manual Migration Commands

```powershell
cd backend\Ticketing.Backend

# List pending migrations
dotnet ef migrations list

# Create a new migration (if needed)
dotnet ef migrations add MigrationName

# Apply migrations manually (usually not needed - auto-applied on startup)
dotnet ef database update
```

---

## Verification Steps

### 1. Backend Health Check

```powershell
# Check if backend is running
curl http://localhost:5000/api/ping
# Expected: {"message":"pong"}

# Or open in browser
# http://localhost:5000/swagger
```

### 2. Frontend Build

```powershell
cd frontend
npm run build
# Should complete without errors
```

### 3. Field Definitions Endpoints

**Prerequisites:** Backend running, admin user logged in

```powershell
# Get admin token (login via frontend or Swagger)
$token = "YOUR_JWT_TOKEN"

# Test GET endpoint
curl -H "Authorization: Bearer $token" `
  http://localhost:5000/api/admin/subcategories/1/fields

# Expected: JSON array of field definitions (may be empty)
```

### 4. End-to-End Field Creation

1. **Start both services** (backend + frontend)
2. **Login as admin**: `admin@test.com` / `Admin123!`
3. **Navigate to**: Admin Dashboard → Category Management
4. **Open**: "مدیریت فیلدهای زیر دسته" dialog for any subcategory
5. **Add a field**:
   - Key: `testField`
   - Label: `Test Field`
   - Type: `Text`
   - Click "افزودن فیلد"
6. **Verify**: Field appears in the list immediately

---

## Troubleshooting

### Backend Issues

#### "No such column: DefaultValue"

**Symptom:** Backend returns 500 error when accessing field definitions endpoints.

**Cause:** Database schema is out of sync with migrations.

**Solution:**
1. **Restart the backend** - migrations are applied automatically on startup
2. If that doesn't work:
   ```powershell
   cd backend\Ticketing.Backend
   .\tools\reset-dev-db.ps1
   # Then restart backend
   ```

#### Backend Won't Start

**Check:**
- Port 5000/7000 is not in use
- Database file is not locked (close any DB viewers)
- .NET SDK is installed correctly

**Solution:**
```powershell
# Use the helper script (recommended)
.\tools\run-backend.ps1

# Or manually:
cd backend\Ticketing.Backend
.\..\..\tools\stop-backend.ps1  # Stop any stale processes first
dotnet clean
dotnet build
dotnet run
```

#### MSB3027/MSB3021: File Locked by Another Process

**Symptom:**
```
MSB3027: Unable to copy apphost.exe -> Ticketing.Backend.exe because it is being used by another process
MSB3021: Unable to copy file because it is locked
```

**Cause:** A previous backend instance is still running, or `dotnet run` was executed while another instance was active.

**Solution:**
```powershell
# Stop all backend processes
.\tools\stop-backend.ps1

# Then start fresh
.\tools\run-backend.ps1
```

**Prevention:** Always use `.\tools\run-backend.ps1` instead of running `dotnet run` directly. The script automatically stops stale processes first.

#### Project Structure Note

**Important:** The project `backend/Ticketing.Backend/src/Ticketing.Api/Ticketing.Api.csproj` is a **library** (controllers only), not a runnable project. It does not have a `Program.cs` entry point.

**The actual runnable project is:**
- `backend/Ticketing.Backend/Ticketing.Backend.csproj` (root project)

When you run `.\tools\run-backend.ps1`, it runs the root `Ticketing.Backend.csproj` project, which includes all controllers from the `Ticketing.Api` library.

#### Port 5000 Already In Use

**Symptom:** 
```
System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
```

**Cause:** A previous backend instance is still running, or another application is using port 5000.

**Solution (Recommended):**
```powershell
# Use the safe backend runner script
cd backend\Ticketing.Backend
.\tools\run-backend.ps1
```

This script will:
- Detect processes using port 5000
- Verify they are this backend project (safety check)
- Stop only the correct backend processes
- Start a fresh backend instance

**Manual Solution:**
If the script doesn't work or you need to investigate:

1. **Find what's using port 5000:**
   ```powershell
   netstat -ano | findstr :5000
   ```
   Look for the PID in the last column.

2. **Check if it's our backend:**
   ```powershell
   # Replace <PID> with the actual process ID
   tasklist /FI "PID eq <PID>"
   ```

3. **Stop the process:**
   ```powershell
   # Only if it's confirmed to be our backend!
   taskkill /PID <PID> /F
   ```

4. **Then start backend:**
   ```powershell
   cd backend\Ticketing.Backend
   dotnet run
   ```

**Development Mode Diagnostics:**
When running in Development mode, the backend will automatically detect port conflicts and print helpful diagnostics including:
- PID of the process using port 5000
- Process name and path
- Exact commands to fix the issue

**Prevention:**
Always use `.\tools\run-backend.ps1` to start the backend. It handles stale processes automatically.

#### Migration Errors

**Symptom:** "Migration already applied" or "Column already exists" errors.

**Solution:** These are handled automatically by Program.cs. If you see them, the app should still continue. Check logs for actual errors.

### Frontend Issues

#### Build Errors

**Symptom:** `npm run build` fails with TypeScript or syntax errors.

**Solution:**
```powershell
cd frontend
npm run typecheck  # Check for type errors
npm run lint       # Check for lint errors
# Fix reported errors
npm run build
```

#### "Cannot connect to backend"

**Check:**
- Backend is running on `http://localhost:5000`
- CORS is configured correctly (check `appsettings.json`)
- No firewall blocking the connection

**Solution:**
```powershell
# Test backend connectivity
curl http://localhost:5000/api/ping
```

#### Dialog Not Opening / Fields Not Loading

**Check:**
- Browser console for errors (F12)
- Network tab for failed API calls
- Backend logs for errors

**Common causes:**
- Backend not running
- Invalid JWT token (logout and login again)
- Database schema mismatch (see "No such column: DefaultValue" above)

### Database Issues

#### Database Locked

**Symptom:** "database is locked" errors.

**Solution:**
1. Stop the backend
2. Close any SQLite database viewers
3. Restart the backend

#### Database Corruption

**Symptom:** Unexpected errors, data inconsistencies.

**Solution:**
```powershell
cd backend\Ticketing.Backend
.\tools\reset-dev-db.ps1
# Restart backend to recreate database
```

---

## Development Workflow

### Making Changes

1. **Create a feature branch:**
   ```powershell
   git checkout -b feature/your-feature-name
   ```

2. **Make changes:**
   - Backend: Edit files in `backend\Ticketing.Backend\`
   - Frontend: Edit files in `frontend\`

3. **Test locally:**
   ```powershell
   # Run sanity check
   .\tools\sanity.ps1
   
   # Test manually
   # Start backend + frontend
   # Test the feature
   ```

4. **Commit changes:**
   ```powershell
   git add .
   git commit -m "feat: description of changes"
   ```

### Adding Database Migrations

1. **Modify entity models** in `Domain\Entities\`

2. **Create migration:**
   ```powershell
   cd backend\Ticketing.Backend
   dotnet ef migrations add YourMigrationName
   ```

3. **Test migration:**
   ```powershell
   # Reset dev DB and restart backend
   .\tools\reset-dev-db.ps1
   dotnet run
   ```

4. **Verify:** Check that migrations are applied correctly in startup logs

### Code Structure

**Backend:**
- `Domain/` - Entities, Enums (no dependencies)
- `Application/` - Services, DTOs (depends on Domain)
- `Infrastructure/` - Data access, Auth (depends on Domain)
- `Api/` - Controllers (depends on Application + Infrastructure)

**Frontend:**
- `app/` - Next.js pages
- `components/` - React components
- `lib/` - API clients, utilities
- `hooks/` - Custom React hooks

---

## Default Users

After database seeding, these users are available:

| Role | Email | Password |
|------|-------|----------|
| Admin | `admin@test.com` | `Admin123!` |
| Technician | `tech1@test.com` | `Tech123!` |
| Client | `client1@test.com` | `Client123!` |

**⚠️ These are for development only. Change passwords in production!**

---

## Environment Variables

### Frontend

Create `frontend/.env.local` (optional):
```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
```

### Backend

Configuration is in `backend/Ticketing.Backend/appsettings.json`.

For production, set `JWT_SECRET` environment variable:
```powershell
$env:JWT_SECRET="your-secure-secret-key-here"
```

---

## Additional Resources

- **Architecture:** See `backend/Ticketing.Backend/ARCHITECTURE.md`
- **API Documentation:** `http://localhost:5000/swagger` (when backend is running)
- **Sanity Script:** `tools/sanity.ps1` - Full project verification

---

## Support

If you encounter issues not covered here:

1. Check backend logs (console output)
2. Check browser console (F12)
3. Run `.\tools\sanity.ps1` for diagnostics
4. Review error messages carefully - they often contain hints

---

**Last Updated:** 2025-12-30
**Branch:** `fix/full-sync-sanity-20251230`






# Subcategory Fields Migration Fix

## Problem
The backend was failing with `SQLite Error 1: 'no such column: s.DefaultValue'` when querying `SubcategoryFieldDefinitions`, even though the migration logic attempted to add the column.

## Root Cause
The post-migration check in `Program.cs` was using a `SELECT` query to verify if the column exists, which could fail in unexpected ways. The check needed to use SQLite's `PRAGMA table_info` to properly inspect the actual database schema.

## Solution
Updated `backend/Ticketing.Backend/Program.cs` to:

1. **Use PRAGMA table_info**: Instead of trying to query the column (which fails if it doesn't exist), we now use `PRAGMA table_info(SubcategoryFieldDefinitions)` to inspect the actual table schema and check if `DefaultValue` column exists.

2. **Proper connection management**: The code now properly handles connection state (checking if connection is already open before opening/closing).

3. **Robust error handling**: If the PRAGMA check fails (e.g., table doesn't exist), it gracefully falls back to attempting to add the column.

## Changes Made

### `backend/Ticketing.Backend/Program.cs`
- Replaced the `SELECT DefaultValue` verification query with `PRAGMA table_info` check
- Improved connection state management for the PRAGMA query
- Enhanced error handling to properly detect when column is missing vs. already exists

## Verification Steps

1. **Stop any running backend instances**

2. **Build the backend**:
   ```powershell
   cd backend\Ticketing.Backend
   dotnet clean
   dotnet build
   ```

3. **Run the backend**:
   ```powershell
   dotnet run --project src/Ticketing.Api/Ticketing.Api.csproj
   ```

4. **Check the startup logs** for:
   - `[MIGRATION] Verifying DefaultValue column exists in SubcategoryFieldDefinitions...`
   - Either:
     - `[MIGRATION] DefaultValue column exists - verified via PRAGMA` (if column exists)
     - `[MIGRATION] Successfully added DefaultValue column` (if column was missing and added)

5. **Test the API endpoint**:
   ```powershell
   # Get an admin token first (login via frontend or use existing token)
   $token = "YOUR_ADMIN_JWT_TOKEN"
   Invoke-WebRequest -Uri "http://localhost:5000/api/admin/subcategories/18/fields" -Headers @{Authorization="Bearer $token"} | Select-Object StatusCode, Content
   ```
   
   Expected: `StatusCode: 200` with JSON array (possibly empty `[]`)

6. **Test in Frontend**:
   - Start frontend: `cd frontend; npm run dev`
   - Login as Admin
   - Navigate to Admin → Category Management
   - Click the "fields/مدیریت فیلدهای زیر دسته" button for any subcategory
   - Should load without errors (empty list if no fields, or list of existing fields)

## Expected Behavior

- **If column exists**: Log shows "DefaultValue column exists - verified via PRAGMA", no ALTER TABLE attempted
- **If column missing**: Log shows "DefaultValue column missing - attempting to add...", then "Successfully added DefaultValue column"
- **If ALTER TABLE fails with "duplicate column"**: Log shows "DefaultValue column already exists - no action needed" (handles race conditions)

## Notes

- The migration `20251230120000_AddMissingColumnsToSubcategoryFieldDefinitions.cs` should add the column during normal migration flow
- The PRAGMA check in `Program.cs` is a **safety net** for cases where migrations didn't apply correctly
- This fix is **idempotent** - safe to run multiple times
- No data loss - `DefaultValue` is nullable, so existing rows remain valid













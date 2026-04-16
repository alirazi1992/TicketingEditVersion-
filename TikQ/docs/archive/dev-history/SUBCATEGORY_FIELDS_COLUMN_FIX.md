# Fix for Missing DefaultValue Column

## Problem
The backend was throwing an error:
```
SQLite Error 1: 'no such column: s.DefaultValue'
```

This occurred when trying to query `SubcategoryFieldDefinitions` table, which was missing the `DefaultValue` column even though a migration existed to add it.

## Root Cause
The migration `20251230120000_AddMissingColumnsToSubcategoryFieldDefinitions` exists but may not have been applied to the database, or the migration system didn't detect it properly.

## Solution

### 1. Post-Migration Safety Check (Program.cs)
Added a fallback check in `Program.cs` that runs after migrations:
- Attempts to add the `DefaultValue` column if it's missing
- If the column already exists, logs and continues (no error)
- This ensures the column exists even if the migration didn't apply correctly

### 2. Manual Script (Optional)
Created `backend/Ticketing.Backend/tools/add-defaultvalue-column.ps1` for manual column addition if needed.

## How to Apply the Fix

### Option 1: Restart Backend (Recommended)
Simply restart the backend server. The post-migration check will automatically add the column if it's missing:

```powershell
cd backend/Ticketing.Backend
dotnet run --project src/Ticketing.Api/Ticketing.Api.csproj
```

Check the logs for:
```
[MIGRATION] Successfully added DefaultValue column (was missing)
```
or
```
[MIGRATION] DefaultValue column already exists - no action needed
```

### Option 2: Manual SQL (If restart doesn't work)
If you have SQLite command-line tools installed:

```powershell
cd backend/Ticketing.Backend
.\tools\add-defaultvalue-column.ps1
```

Or manually with sqlite3:
```bash
sqlite3 App_Data/ticketing.db "ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN DefaultValue TEXT;"
```

### Option 3: Reset Database (Last Resort)
If the database is in a bad state, use the reset script (creates backup first):

```powershell
cd backend/Ticketing.Backend
.\tools\reset-dev-db.ps1
```

## Verification

After applying the fix, verify the column exists:

1. **Check backend logs** - Should see migration success message
2. **Test the API** - Try loading fields for a subcategory:
   ```bash
   curl -H "Authorization: Bearer <admin-token>" \
        http://localhost:5000/api/admin/subcategories/18/fields
   ```
3. **Check database** (if you have sqlite3):
   ```bash
   sqlite3 App_Data/ticketing.db "PRAGMA table_info(SubcategoryFieldDefinitions);"
   ```
   Should see `DefaultValue` in the column list.

## Files Changed

- `backend/Ticketing.Backend/Program.cs` - Added post-migration column check
- `backend/Ticketing.Backend/Infrastructure/Data/Migrations/20251230120000_AddMissingColumnsToSubcategoryFieldDefinitions.cs` - Simplified migration
- `backend/Ticketing.Backend/tools/add-defaultvalue-column.ps1` - Manual column addition script (NEW)

## Notes

- The fix is **additive and safe** - it won't break existing functionality
- If the column already exists, the check will just log and continue
- The migration will still run normally for fresh databases
- This is a safety net for databases that may have been created before the migration existed







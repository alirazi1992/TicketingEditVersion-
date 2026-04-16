# Troubleshooting: Subcategory Fields Error

## Current Error
```
[apiRequest] ERROR GET http://localhost:5000/api/admin/subcategories/18/fields: {}
Error: خطای پایگاه داده: لطفاً سرور بک‌اند را راه‌اندازی مجدد کنید تا مایگریشن‌ها اعمال شوند.
```

## Root Cause
The `DefaultValue` column is missing from the `SubcategoryFieldDefinitions` table in the SQLite database. The migration exists but hasn't been applied yet.

## Solution: Restart Backend

**The backend MUST be restarted** to apply the migration that adds the missing column.

### Steps:

1. **Stop the current backend** (if running):
   - Press `Ctrl+C` in the terminal running the backend
   - Or close the terminal/process

2. **Restart the backend**:
   ```powershell
   cd backend/Ticketing.Backend
   dotnet run --project src/Ticketing.Api/Ticketing.Api.csproj
   ```

3. **Check the startup logs** for:
   ```
   [MIGRATION] Successfully added DefaultValue column (was missing)
   ```
   OR
   ```
   [MIGRATION] DefaultValue column already exists - no action needed
   ```

4. **Verify the fix**:
   - Open the browser console
   - Try to open the field designer modal again
   - The error should be gone

## What Happens on Restart

The `Program.cs` file includes a post-migration safety check that:
1. Runs after all migrations are applied
2. Attempts to add the `DefaultValue` column if it's missing
3. Logs the result (success or "already exists")
4. Continues startup even if the column already exists (no error)

## If Restart Doesn't Work

### Option 1: Manual SQL (if you have sqlite3)
```powershell
cd backend/Ticketing.Backend
sqlite3 App_Data/ticketing.db "ALTER TABLE SubcategoryFieldDefinitions ADD COLUMN DefaultValue TEXT;"
```

### Option 2: Use the PowerShell script
```powershell
cd backend/Ticketing.Backend
.\tools\add-defaultvalue-column.ps1
```

### Option 3: Reset Database (last resort - creates backup first)
```powershell
cd backend/Ticketing.Backend
.\tools\reset-dev-db.ps1
```

## Verification

After restarting, check:

1. **Backend logs** show migration success
2. **Browser console** - no more 500 errors
3. **UI** - field designer modal opens without errors
4. **Database** (optional):
   ```sql
   PRAGMA table_info(SubcategoryFieldDefinitions);
   ```
   Should show `DefaultValue` in the column list

## Why the Error Body Shows `{}`

The error body showing as `{}` means:
- The response might be empty or not properly formatted
- The frontend is correctly detecting it's a 500 error
- The error handling is working (showing user-friendly message)

After restart, the backend will return proper error responses with details if something else goes wrong.

## Files Changed

- `backend/Ticketing.Backend/Program.cs` - Post-migration column check
- `backend/Ticketing.Backend/Api/Controllers/AdminFieldDefinitionsController.cs` - ProblemDetails responses
- `frontend/lib/api-client.ts` - Better error parsing
- `frontend/lib/field-definitions-api.ts` - Schema error detection

## Next Steps

1. ✅ Restart backend
2. ✅ Verify column exists
3. ✅ Test field designer
4. ✅ Add a test field to confirm everything works







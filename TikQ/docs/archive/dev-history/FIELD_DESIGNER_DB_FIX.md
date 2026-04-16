# Field Designer Database Error Fix

## Root Cause

The field designer UI was showing the error:
> "خطای پایگاه داده: لطفاً سرور بک‌اند را راه‌اندازی مجدد کنید تا مایگریشن‌ها اعمال شوند."

**Root Cause**: The `SubcategoryFieldDefinitions` table existed but was missing the `Name` column, even though:
- The entity model (`SubcategoryFieldDefinition`) has a `Name` property
- The EF Core configuration maps `Name` as required
- The migration `20251230000000_AddSubcategoryFieldDefinitions` includes `Name` column
- The repository queries select `Name` column

This indicates **schema drift** - the table was created incorrectly or a migration didn't apply fully.

## Solution Applied

### Phase 1: Reproduce & Identify
- Confirmed error: `SQLite Error 1: 'no such column: Name'`
- Verified entity model expects `Name` column
- Confirmed migration should have created it

### Phase 2: Safe Additive Fixes

#### 2A: Created Additive Migration
**File**: `Infrastructure/Data/Migrations/20260102063005_AddNameColumnToSubcategoryFieldDefinitions.cs`

- Adds `Name` column as nullable TEXT
- Backfills existing rows: `UPDATE ... SET Name = Key WHERE Name IS NULL`
- Safe additive change - no table recreation

#### 2B: Updated Schema Guard (Program.cs)
**Changed**: Replaced risky table recreation with safe additive column fixes

**Before**: Schema guard attempted to `DROP TABLE` and recreate, which failed due to foreign key constraints.

**After**: Schema guard now:
1. Detects missing critical columns (`Name`, `Label`, `Key`, `Type`, `IsRequired`, `SubcategoryId`)
2. Creates timestamped database backup before changes
3. Uses `ALTER TABLE ADD COLUMN` to add missing columns additively
4. Backfills data where needed (e.g., `Name = Key` for existing rows)
5. Never drops or recreates tables
6. Logs exactly what was fixed

**Key Safety Features**:
- Always creates backup before schema changes
- Only uses additive SQL (`ALTER TABLE ADD COLUMN`)
- Handles nullable vs non-null columns appropriately
- Provides default values where needed

#### 2C: Improved Error Messages
**File**: `Api/Controllers/AdminFieldDefinitionsController.cs`

**Development Mode**:
- Returns detailed `ProblemDetails` with:
  - Missing column name
  - Exact error message
  - Fix suggestion (restart backend for schema guard to run)

**Production Mode**:
- Returns safe generic message: "Database schema needs upgrade. Please contact the administrator."
- Error code: `SCHEMA_UPGRADE_REQUIRED`

## Verification Steps

### 1. Build Backend
```powershell
cd backend\Ticketing.Backend
dotnet clean
dotnet build
```

Expected: Build succeeds with 0 errors

### 2. Run Backend
```powershell
dotnet run
```

**Expected Logs**:
```
[STARTUP] Resolved SQLite DB Path: <path>
[MIGRATION] Starting database migration...
[MIGRATION] Applied migrations: ...
[SCHEMA_GUARD] Verifying SubcategoryFieldDefinitions table schema...
[SCHEMA_GUARD] Existing columns: Id, SubcategoryId, Key, Label, Type, ...
[SCHEMA_GUARD] Missing critical columns detected: Name
[SCHEMA_GUARD] Database backed up to: <backup-path>
[SCHEMA_GUARD] Added column Name as nullable
[SCHEMA_GUARD] Backfilled Name: X rows updated
[SCHEMA_GUARD] Successfully added all missing critical columns
[SCHEMA_GUARD] After fixes, table has columns: Id, SubcategoryId, Name, Key, Label, ...
```

### 3. Test API Endpoints

**GET Fields**:
```powershell
$token = "<admin-token>"
Invoke-RestMethod -Uri "http://localhost:5000/api/admin/subcategories/1/fields" `
    -Method GET -Headers @{Authorization="Bearer $token"}
```

Expected: Returns `200 OK` with array of fields (may be empty `[]`)

**POST Create Field**:
```powershell
$body = @{
    name = "testField"
    label = "Test Field"
    key = "testField"
    type = "Text"
    isRequired = $false
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5000/api/admin/subcategories/1/fields" `
    -Method POST -Headers @{Authorization="Bearer $token"; "Content-Type"="application/json"} `
    -Body $body
```

Expected: Returns `201 Created` with created field object

### 4. Test UI
1. Open Admin Dashboard → مدیریت دسته‌بندی‌ها
2. Click "طراحی فیلدهای سفارشی" for any subcategory
3. Dialog should open without error
4. Should be able to:
   - View existing fields (if any)
   - Add new field
   - Edit existing field
   - Delete field

## Files Changed

1. **Infrastructure/Data/Migrations/20260102063005_AddNameColumnToSubcategoryFieldDefinitions.cs** (new)
   - Additive migration to add `Name` column

2. **Program.cs**
   - Updated `EnsureSubcategoryFieldDefinitionsSchemaAsync` to use additive fixes only
   - Removed table recreation logic
   - Added critical column detection and backfilling

3. **Api/Controllers/AdminFieldDefinitionsController.cs**
   - Improved error messages for schema errors
   - Development vs Production error handling

4. **tools/verify-field-designer.ps1** (new)
   - Verification script for field designer endpoints

## Commits Made

1. `fix(db): stop schema guard table recreation; add additive column repair`
2. `fix(db): add migration/backfill for SubcategoryFieldDefinitions.Name`
3. `fix(api): improve error responses for field definitions schema issues`
4. `test(tools): add verify-field-designer.ps1`

## Safety Measures

✅ Database backup created before any schema changes  
✅ Only additive SQL changes (`ALTER TABLE ADD COLUMN`)  
✅ No table drops or recreations  
✅ Backfill data for existing rows  
✅ Detailed logging of all schema fixes  
✅ Error messages distinguish Development vs Production  

## Expected Behavior After Fix

- Backend starts successfully
- Schema guard detects and fixes missing columns automatically
- Field designer API endpoints return `200 OK`
- UI can open field designer dialog without errors
- Users can add/edit/delete custom fields

## Troubleshooting

If schema guard still fails:
1. Check backend logs for `[SCHEMA_GUARD]` messages
2. Verify database file path in logs: `[STARTUP] Resolved SQLite DB Path`
3. Check if backup was created: `App_Data/ticketing.db.backup.*`
4. Manually verify table schema:
   ```sql
   PRAGMA table_info(SubcategoryFieldDefinitions);
   ```

If migration doesn't apply:
1. Check `__EFMigrationsHistory` table for applied migrations
2. Manually run: `dotnet ef database update`
3. Verify migration file exists in `Infrastructure/Data/Migrations/`




































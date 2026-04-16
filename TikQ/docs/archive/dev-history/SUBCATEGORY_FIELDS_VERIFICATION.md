# Subcategory Fields Verification Guide

## Problem Fixed

- **Schema Mismatch**: Database table `SubcategoryFieldDefinitions` was missing the `DefaultValue` column
- **Error**: `SQLite Error 1: 'no such column: s.DefaultValue'`
- **Solution**: Added migration `20251230120000_AddMissingColumnsToSubcategoryFieldDefinitions` to add missing column

## How to Run

### Backend

```powershell
cd backend/Ticketing.Backend
dotnet clean
dotnet build
dotnet run
```

**Expected Output:**
- `[STARTUP] Resolved SQLite DB Path: <path>`
- `[MIGRATION] Starting database migration...`
- `[MIGRATION] Applied migrations: ...`
- `[MIGRATION] Pending migrations: AddMissingColumnsToSubcategoryFieldDefinitions`
- `[MIGRATION] Migrations after apply: ...` (should include the new migration)
- `[MIGRATION] Database migration completed successfully`

The migration is applied **automatically on startup**. If you see errors about "column already exists", that's okay - it means the column was already added manually or in a previous run.

### Frontend

```powershell
cd frontend
npm run dev
```

## Verification Steps

### 1. Backend API Tests (using PowerShell)

#### Get Admin Token
```powershell
$loginResponse = Invoke-WebRequest -Uri "http://localhost:5000/api/auth/login" `
  -Method POST `
  -Headers @{"Content-Type"="application/json"} `
  -Body '{"email":"admin@test.com","password":"Admin123!"}'

$token = ($loginResponse.Content | ConvertFrom-Json).token
Write-Host "Token: $token"
```

#### Test GET (should return empty array or existing fields)
```powershell
$subcategoryId = 18  # Replace with actual subcategory ID

$response = Invoke-WebRequest -Uri "http://localhost:5000/api/admin/subcategories/$subcategoryId/fields" `
  -Headers @{Authorization="Bearer $token"}

$fields = $response.Content | ConvertFrom-Json
Write-Host "Fields count: $($fields.Count)"
$fields | ConvertTo-Json -Depth 5
```

**Expected**: `200 OK` with JSON array (empty `[]` if no fields exist)

#### Test POST (create a new field)
```powershell
$body = @{
  name = "deviceBrand"
  label = "برند دستگاه"
  key = "deviceBrand"
  type = "Select"
  isRequired = $true
  options = @(
    @{value="Dell"; label="دل"},
    @{value="HP"; label="اچ پی"},
    @{value="Lenovo"; label="لنوو"}
  )
} | ConvertTo-Json

$createResponse = Invoke-WebRequest -Uri "http://localhost:5000/api/admin/subcategories/$subcategoryId/fields" `
  -Method POST `
  -Headers @{Authorization="Bearer $token"; "Content-Type"="application/json"} `
  -Body $body

$created = $createResponse.Content | ConvertFrom-Json
Write-Host "Created field ID: $($created.id)"
$created | ConvertTo-Json -Depth 5
```

**Expected**: `201 Created` with created field object

#### Test GET again (verify persistence)
```powershell
$response = Invoke-WebRequest -Uri "http://localhost:5000/api/admin/subcategories/$subcategoryId/fields" `
  -Headers @{Authorization="Bearer $token"}

$fields = $response.Content | ConvertFrom-Json
Write-Host "Fields count after create: $($fields.Count)"
# Should show the newly created field
```

#### Test DELETE
```powershell
$fieldId = $created.id  # From previous step

Invoke-WebRequest -Uri "http://localhost:5000/api/admin/subcategories/$subcategoryId/fields/$fieldId" `
  -Method DELETE `
  -Headers @{Authorization="Bearer $token"}

Write-Host "Field deleted (should return 204 No Content)"
```

**Expected**: `204 No Content`

### 2. Frontend UI Tests

1. **Login as Admin**
   - Email: `admin@test.com`
   - Password: `Admin123!`

2. **Navigate to Category Management**
   - Go to Admin Dashboard
   - Click "مدیریت دسته‌بندی‌ها"

3. **Open Field Designer**
   - Find any subcategory
   - Click the gear icon (⚙️) next to it
   - **Verify**: Modal opens with two-column layout

4. **Test Loading State**
   - **Verify**: Spinner shows while loading
   - **Verify**: Fields appear in left column when loaded

5. **Test Empty State**
   - If no fields exist, **verify**: "هیچ فیلدی تعریف نشده است" message in left column

6. **Test Error State** (if schema error occurs)
   - **Verify**: Red error box appears with message
   - **Verify**: "تلاش مجدد" (Retry) button is visible
   - Click retry button
   - **Verify**: Request is retried

7. **Test Add Field**
   - Fill in right column form:
     - **شناسه**: `deviceBrand`
     - **عنوان**: `برند دستگاه`
     - **نوع**: `لیست انتخابی`
     - **گزینه‌ها**: `Dell:دل, HP:اچ پی, Lenovo:لنوو`
     - Check **فیلد اجباری** if needed
   - Click **افزودن فیلد**
   - **Verify**: 
     - Success toast appears
     - Field appears in left column immediately
     - Form is cleared
     - Field persists after page refresh

8. **Test Validation**
   - Try adding field with empty key → **Verify**: Inline error "شناسه فیلد الزامی است"
   - Try adding field with invalid key (starts with number) → **Verify**: Format error
   - Try adding field with duplicate key → **Verify**: Duplicate error
   - Try adding Select field without options → **Verify**: Options required error

9. **Test Edit Field**
   - Click edit icon (✏️) next to a field
   - **Verify**: Field switches to edit mode
   - Modify label or other properties
   - Click **ذخیره**
   - **Verify**: Changes persist, success toast appears

10. **Test Delete Field**
    - Click delete icon (🗑️) next to a field
    - **Verify**: Confirmation dialog appears
    - Confirm deletion
    - **Verify**: Field removed from list, success toast appears

11. **Test Persistence**
    - Add/edit/delete fields
    - Close modal
    - Refresh browser page
    - Re-open field designer
    - **Verify**: All changes persisted

## Troubleshooting

### Migration Not Applied

If you see `no such column: DefaultValue` error:

1. **Check migration was created**:
   ```powershell
   ls backend/Ticketing.Backend/Infrastructure/Data/Migrations/20251230120000_*.cs
   ```

2. **Check migration history**:
   - Look for `[MIGRATION] Applied migrations:` in backend logs
   - Should include `AddMissingColumnsToSubcategoryFieldDefinitions`

3. **Manual migration** (if needed):
   ```powershell
   cd backend/Ticketing.Backend
   dotnet ef database update
   ```

### Column Already Exists Error

If migration fails with "column already exists":
- This is **acceptable** - it means the column was already added
- The migration system will handle this gracefully
- Backend will continue to run normally

### 404 Not Found

If GET returns 404:
- **Check**: Subcategory ID exists in database
- **Check**: User has Admin role
- **Check**: Authorization token is valid

### 500 Internal Server Error

If you see 500 errors:
- **Check backend logs** for detailed error message
- **Check**: Database file exists and is accessible
- **Check**: Migrations were applied successfully
- **Check**: Connection string is correct

## Expected Behavior

✅ **GET** `/api/admin/subcategories/{id}/fields` → `200 OK` with `[]` or array of fields
✅ **POST** `/api/admin/subcategories/{id}/fields` → `201 Created` with created field
✅ **PUT** `/api/admin/subcategories/{id}/fields/{fieldId}` → `200 OK` with updated field
✅ **DELETE** `/api/admin/subcategories/{id}/fields/{fieldId}` → `204 No Content`

✅ **UI**: Two-column layout (fields list left, add form right)
✅ **UI**: Loading, empty, and error states work correctly
✅ **UI**: Validation shows inline errors
✅ **UI**: Success toasts appear after operations
✅ **UI**: Changes persist after refresh

## Files Changed

- `backend/Ticketing.Backend/Infrastructure/Data/Migrations/20251230120000_AddMissingColumnsToSubcategoryFieldDefinitions.cs` (NEW)
- `backend/Ticketing.Backend/Program.cs` (enhanced migration logging)
- `backend/Ticketing.Backend/Api/Controllers/AdminFieldDefinitionsController.cs` (enhanced error handling)
- `frontend/components/subcategory-field-designer-dialog.tsx` (redesigned UI, added error state)

## Safety Notes

✅ **Additive Migration**: Only adds missing column, doesn't drop anything
✅ **Nullable Column**: `DefaultValue` is nullable, so existing rows remain valid
✅ **Idempotent**: Migration can be re-applied safely
✅ **Auto-Applied**: Migration runs automatically on backend startup
✅ **No Data Loss**: All existing data is preserved










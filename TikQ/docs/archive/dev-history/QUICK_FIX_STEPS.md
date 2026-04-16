# Quick Fix Steps - Supervisor API

## TL;DR

The endpoints exist and are configured correctly. The issue is most likely that the logged-in user is **not a supervisor**.

## Quick Fix (2 minutes)

### Step 1: Start Backend
```powershell
.\tools\run-backend.ps1
```

### Step 2: Test Endpoints
```powershell
.\test-supervisor-endpoints.ps1
```

### Step 3: Get Token
1. Open http://localhost:3000
2. Login
3. Press F12 (DevTools)
4. Console tab
5. Run: `localStorage.getItem('ticketing.auth.token')`
6. Copy the token (without quotes)

### Step 4: Test With Auth
```powershell
.\test-supervisor-endpoints.ps1 -Token "PASTE_TOKEN_HERE"
```

### Step 5: Apply Fix

#### If Status is 401 (Most Likely)

**Problem**: User is not a supervisor.

**Fix**:
```sql
-- Replace 'your-email@example.com' with your actual email
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

**Then**: Refresh browser, page should work.

#### If Status is 404

**Problem**: Controller not loaded.

**Fix**: Restart backend.

#### If Status is 500

**Problem**: Backend exception.

**Fix**: Check backend console logs for error details.

## Verify Fix

1. Open http://localhost:3000
2. Navigate to supervisor page
3. **Expected**: Page shows list or "تکنسینی یافت نشد"
4. **Expected**: Console shows `200 OK`
5. **Expected**: No errors

## Console Output After Fix

### Success
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```

### Before Fix (401)
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  responseText: "{\"message\":\"Only supervisor technicians can perform this action.\"}",
  ...
}
```

## What Was Changed

1. **Frontend logging** - Now shows full error details (not `{}`)
2. **Test script** - Created `test-supervisor-endpoints.ps1`
3. **Documentation** - Created diagnostic guides

## Files Modified

- `frontend/lib/api-client.ts` - Enhanced error logging
- `test-supervisor-endpoints.ps1` - New test script
- Various `.md` files - Documentation

## That's It!

The root cause is most likely that the user needs to be made a supervisor in the database. Run the SQL above and it should work.

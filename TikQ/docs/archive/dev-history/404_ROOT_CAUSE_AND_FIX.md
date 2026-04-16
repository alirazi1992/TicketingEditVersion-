# 404 Root Cause & Fix

## Problem

Both endpoints return **404 Not Found**:
- `GET /api/supervisor/technicians`
- `GET /api/supervisor/technicians/available`

## Root Cause

The **SupervisorController exists** but the **backend needs to be restarted** to load it.

### Evidence

1. ✅ **Controller File Exists**
   - Location: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`
   - Route: `[Route("api/supervisor")]`
   - Endpoints:
     - `[HttpGet("technicians")]` → `/api/supervisor/technicians`
     - `[HttpGet("technicians/available")]` → `/api/supervisor/technicians/available`

2. ✅ **Service Registered**
   - `ISupervisorService` → `SupervisorService` registered in Program.cs
   - Service methods implemented:
     - `GetTechniciansAsync()`
     - `GetAvailableTechniciansAsync()`

3. ✅ **Controllers Configured**
   - `builder.Services.AddControllers()` - Line 1310
   - `app.MapControllers()` - Line 1835
   - Middleware order correct

4. ✅ **Project Compiles**
   - `dotnet build` returns 0 errors
   - Controller namespace matches other controllers

### Why 404?

**Most Likely**: The backend was started BEFORE the SupervisorController was created/saved.

When you run `dotnet run`, it compiles and loads the assembly. If you add a new controller file after that, the running process won't see it until you restart.

## Solution

### Step 1: Stop Backend (if running)

```powershell
.\tools\stop-backend.ps1
```

Or manually:
```powershell
Get-Process -Name "Ticketing.Backend","dotnet" | Where-Object { $_.Path -like "*TikQ*" } | Stop-Process -Force
```

### Step 2: Clean Build (Optional but Recommended)

```powershell
cd backend/Ticketing.Backend
dotnet clean
dotnet build
```

### Step 3: Start Backend

```powershell
.\tools\run-backend.ps1
```

### Step 4: Verify Endpoints

```powershell
# Test health (should work)
curl -i http://localhost:5000/api/health

# Test supervisor endpoints (should return 401 Unauthorized, NOT 404)
curl -i http://localhost:5000/api/supervisor/technicians
curl -i http://localhost:5000/api/supervisor/technicians/available
```

**Expected Result**: `401 Unauthorized` (not 404)

The endpoints now exist, but require authentication. Response:
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### Step 5: Test With Auth

Get token from frontend:
1. Login to http://localhost:3000
2. Console: `localStorage.getItem('ticketing.auth.token')`
3. Copy token

Test:
```powershell
$token = "YOUR_TOKEN_HERE"
curl -i http://localhost:5000/api/supervisor/technicians -H "Authorization: Bearer $token"
```

**Expected Results**:

#### A) 200 OK ✅
```json
[]
```
or
```json
[
  {
    "technicianUserId": "...",
    "technicianName": "...",
    "inboxTotal": 10,
    "inboxLeft": 5,
    "workloadPercent": 50
  }
]
```

**Meaning**: Success! User is a supervisor. Empty array means no linked technicians yet.

#### B) 401 Unauthorized ❌
```json
{
  "message": "Only supervisor technicians can perform this action."
}
```

**Meaning**: User is authenticated but not a supervisor.

**Fix**:
```sql
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

## Verification Checklist

After restarting backend:

- [ ] Backend starts without errors
- [ ] Health endpoint returns 200: `curl http://localhost:5000/api/health`
- [ ] Supervisor endpoints return 401 (not 404): `curl http://localhost:5000/api/supervisor/technicians`
- [ ] With auth token, endpoints return 200 or 401 (not 404)
- [ ] Frontend console shows 401 or 200 (not 404)
- [ ] Frontend page shows loading → error/empty/list (not blank)

## Frontend Verification

After backend restart:

1. Open http://localhost:3000
2. Navigate to supervisor page
3. Open DevTools → Console
4. Check for:

**Before Fix (404)**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 404,
  statusText: "Not Found",
  responseText: "",
  ...
}
```

**After Fix (401 - needs supervisor role)**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  responseText: "{\"message\":\"Only supervisor technicians can perform this action.\"}",
  ...
}
```

**After Making User Supervisor (200 - Success)**:
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```

## Common Issues

### Still Getting 404 After Restart?

**Check 1**: Verify controller file exists
```powershell
Test-Path "backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs"
# Should return: True
```

**Check 2**: Verify it compiles
```powershell
cd backend/Ticketing.Backend
dotnet build
# Should show: 0 Error(s)
```

**Check 3**: Check backend startup logs
Look for:
```
Now listening on: http://localhost:5000
```

**Check 4**: Verify correct port
Frontend expects: `http://localhost:5000`
Backend listening on: Check startup logs

### Getting 401 Instead of 200?

**Solution**: Make user a supervisor (see Step 5B above)

### Getting 500 Internal Server Error?

**Solution**: Check backend console logs for exception details

## Summary

**Problem**: 404 Not Found  
**Root Cause**: Backend needs restart to load new controller  
**Solution**: `.\tools\stop-backend.ps1` then `.\tools\run-backend.ps1`  
**Expected After Fix**: 401 Unauthorized (then make user supervisor for 200 OK)

---

**Status**: Ready to test - restart backend and verify endpoints return 401 instead of 404

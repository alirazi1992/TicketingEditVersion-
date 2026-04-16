# Root Cause Fix Plan - Supervisor API

## Current Status

### ✅ What's Already Done

1. **Backend Controller Exists**
   - File: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`
   - Route: `[Route("api/supervisor")]`
   - Auth: `[Authorize]` - Requires JWT authentication
   - Endpoints implemented:
     - `GET /api/supervisor/technicians`
     - `GET /api/supervisor/technicians/available`

2. **Backend Service Implemented**
   - `GetTechniciansAsync()` - Gets linked technicians
   - `GetAvailableTechniciansAsync()` - Gets available technicians to link

3. **Frontend Auth Configured**
   - Sends `Authorization: Bearer <token>` header
   - Includes `credentials: "include"` for CORS

4. **Frontend Logging Improved**
   - Now logs full error details:
     - status, statusText
     - url, method
     - responseText (raw)
     - parsed JSON body

### ❌ What's NOT Working

**Problem**: Requests to `/api/supervisor/technicians` are failing.

**Unknown**: The actual HTTP status code and response body (need to run backend to see).

## Most Likely Root Cause

Based on the code analysis:

### Hypothesis: 401 Unauthorized (90% probability)

**Why**:
1. Controller has `[Authorize]` attribute
2. Endpoints check if user is a supervisor: `await EnsureSupervisorAsync(supervisorUserId)`
3. If user is not a supervisor, throws `UnauthorizedAccessException`
4. Returns: `Unauthorized(new { message = "Only supervisor technicians can perform this action." })`

**Expected Response**:
```json
{
  "message": "Only supervisor technicians can perform this action."
}
```

**Fix**: Make user a supervisor in database:
```sql
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'user@example.com')
```

### Alternative: 404 Not Found (5% probability)

**Why**: Controller not loaded or route mismatch.

**Fix**: Restart backend, verify controller is compiled.

### Alternative: 500 Internal Server Error (5% probability)

**Why**: Backend exception in service logic.

**Fix**: Check backend logs for exception details.

## Testing Plan

### Step 1: Start Backend
```powershell
.\tools\run-backend.ps1
```

### Step 2: Run Test Script
```powershell
.\test-supervisor-endpoints.ps1
```

This will:
- ✅ Test health endpoint (verify backend is running)
- ✅ Test supervisor endpoints without auth (should get 401)
- ℹ️ Show instructions to get token

### Step 3: Get Token & Test With Auth
```powershell
# 1. Login to frontend
# 2. Get token from console: localStorage.getItem('ticketing.auth.token')
# 3. Run:
.\test-supervisor-endpoints.ps1 -Token "YOUR_TOKEN_HERE"
```

This will:
- ✅ Test both endpoints with authentication
- ✅ Show actual status code and response body
- ✅ Provide specific fix instructions based on result

### Step 4: Apply Fix Based on Result

#### If 200 OK ✅
**Meaning**: Everything works! Frontend issue only.

**Action**: 
- Check frontend Network tab
- Verify Authorization header is sent
- Check if token is valid

#### If 401 Unauthorized ❌
**Meaning**: User is not a supervisor.

**Action**:
```sql
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

#### If 404 Not Found ❌
**Meaning**: Route doesn't exist.

**Action**:
1. Restart backend
2. Check backend logs for controller registration
3. Verify `SupervisorController.cs` is compiled

#### If 500 Internal Server Error ❌
**Meaning**: Backend exception.

**Action**: Check backend console logs for exception details.

## Frontend Changes Made

### File: `frontend/lib/api-client.ts`

**Change**: Enhanced error logging (line ~617)

**Before**:
```typescript
if (!silent && shouldLogError(method, url, res.status)) {
  console.error(`[apiRequest] ERROR ${method} ${url}`, {
    status: res.status,
    statusText: res.statusText,
    contentType,
    body: errorBody || "(no body)",
    rawText: responseText ? responseText.substring(0, 500) : "(empty)",
    message: errorMessage,
  });
}
```

**After**:
```typescript
if (!silent) {
  const errorInfo = {
    status: res.status,
    statusText: res.statusText,
    url: url,
    method: method,
    contentType: contentType,
    responseText: responseText ? responseText.substring(0, 500) : "(empty)",
    parsed: errorBody,
    message: errorMessage,
  };
  console.error(`[apiRequest] ERROR ${method} ${url}`, errorInfo);
  
  // Also log as separate lines for clarity
  console.error(`  Status: ${res.status} ${res.statusText}`);
  console.error(`  URL: ${url}`);
  console.error(`  Response: ${responseText ? responseText.substring(0, 200) : "(empty)"}`);
}
```

**Why**: 
- Temporarily disabled deduplication to see all errors
- Added separate console.error lines for clarity
- Ensures we see the actual error details

## Expected Console Output After Fix

### With Backend Running & User is Supervisor
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```

### With Backend Running & User is NOT Supervisor
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 401 Unauthorized
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  url: "http://localhost:5000/api/supervisor/technicians",
  method: "GET",
  contentType: "application/problem+json",
  responseText: "{\"message\":\"Only supervisor technicians can perform this action.\"}",
  parsed: { message: "Only supervisor technicians can perform this action." },
  message: "Only supervisor technicians can perform this action."
}
  Status: 401 Unauthorized
  URL: http://localhost:5000/api/supervisor/technicians
  Response: {"message":"Only supervisor technicians can perform this action."}
```

## Success Criteria

- [ ] Backend starts successfully
- [ ] Test script shows 401 without auth
- [ ] Test script shows 200 OK with auth (after making user supervisor)
- [ ] Frontend Network tab shows 200 OK
- [ ] Frontend console shows clear error details (not `{}`)
- [ ] Page shows list of technicians or empty state

## Files Modified

1. `frontend/lib/api-client.ts` - Enhanced error logging
2. `test-supervisor-endpoints.ps1` - Created test script
3. `DIAGNOSE_SUPERVISOR_API.md` - Created diagnostic guide
4. `ROOT_CAUSE_FIX_PLAN.md` - This file

## Next Actions

1. **Start backend**: `.\tools\run-backend.ps1`
2. **Run test script**: `.\test-supervisor-endpoints.ps1`
3. **Get token from frontend**: Login → Console → `localStorage.getItem('ticketing.auth.token')`
4. **Test with auth**: `.\test-supervisor-endpoints.ps1 -Token "YOUR_TOKEN"`
5. **Apply fix based on result** (most likely: make user a supervisor)
6. **Verify in browser**: Check Network tab shows 200 OK

---

**Status**: ✅ Ready for testing

All code changes are complete. The actual root cause will be revealed when the backend is running and we see the real HTTP status code and response body.

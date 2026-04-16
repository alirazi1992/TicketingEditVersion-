# Testing Checklist - Supervisor API Fix

## Before Testing

### 1. Rebuild Backend
```powershell
cd backend/Ticketing.Backend
dotnet build
```
✅ Should show: `Build succeeded. 0 Error(s)`

### 2. Start Backend
```powershell
.\tools\run-backend.ps1
```
✅ Should show:
```
Now listening on: http://localhost:5000
CORS Origins: http://localhost:3000, ...
```

### 3. Start Frontend
```powershell
cd frontend
npm run dev
```
✅ Should show:
```
Local: http://localhost:3000
```

## Testing in Browser

### Step 1: Open Browser Console
1. Navigate to http://localhost:3000
2. Press F12 to open DevTools
3. Go to Console tab
4. Clear console (Ctrl+L or click 🚫)

### Step 2: Login
1. Login with any user account
2. Check console for login request:
   ```
   [apiRequest] POST http://localhost:5000/api/auth/login
   [apiRequest] POST http://localhost:5000/api/auth/login → 200 OK
   ```

### Step 3: Navigate to Supervisor Dashboard
1. Click on supervisor menu/link
2. Watch console closely

## What to Check

### ✅ GOOD Signs (Fix Working)

#### Console Output
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```
OR (if not supervisor):
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 401 Unauthorized
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  contentType: "application/problem+json",
  body: { message: "Only supervisor technicians can perform this action." },
  rawText: "{\"message\":\"Only supervisor technicians can perform this action.\"}",
  message: "Only supervisor technicians can perform this action."
}
```

#### Key Points
- ✅ Error object is NOT empty `{}`
- ✅ Shows `status`, `statusText`, `body`, `rawText`
- ✅ Same error is NOT repeated multiple times
- ✅ If repeated, only logged once per 5 seconds

#### Network Tab
1. Open DevTools → Network tab
2. Find request to `/api/supervisor/technicians`
3. Check:
   - ✅ Request Headers include: `Authorization: Bearer eyJ...`
   - ✅ Response shows actual status code (200, 401, 403, etc.)
   - ✅ Response body shows JSON (not empty)

### ❌ BAD Signs (Fix Not Working)

#### Console Output
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {}
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {}
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {}
```

#### Problems
- ❌ Error object is empty `{}`
- ❌ Same error repeated many times per second
- ❌ No status code shown
- ❌ No response body shown

#### If This Happens
1. Hard reload: Ctrl+Shift+R (or Cmd+Shift+R on Mac)
2. Clear browser cache
3. Restart frontend dev server
4. Check you're using the updated code

## Detailed Test Scenarios

### Scenario 1: User IS a Supervisor

**Expected**:
1. Console shows: `200 OK`
2. UI shows list of managed technicians (or empty state)
3. No errors in console
4. No repeated requests

**If Fails**:
- Check backend logs for errors
- Verify database has linked technicians
- Check Network tab for actual response

### Scenario 2: User is NOT a Supervisor

**Expected**:
1. Console shows: `401 Unauthorized` OR `403 Forbidden`
2. Error log includes full details (status, body, message)
3. UI shows error message with status code
4. Error logged only ONCE (not repeated)

**To Fix**:
```sql
-- Make user a supervisor
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

### Scenario 3: Backend Not Running

**Expected**:
1. Console shows: `Failed to fetch` or `BACKEND_UNREACHABLE`
2. Error includes diagnostic info (tried URLs, health check)
3. UI shows "Cannot connect to backend" message

**To Fix**:
- Start backend: `.\tools\run-backend.ps1`

### Scenario 4: Invalid/Expired Token

**Expected**:
1. Console shows: `401 Unauthorized`
2. User is redirected to login page
3. Token is cleared from localStorage

**To Fix**:
- Login again

## Quick Browser Console Tests

### Test 1: Check Token
```javascript
console.log('Token:', localStorage.getItem('ticketing.auth.token')?.substring(0, 20) + '...')
```
✅ Should show: `Token: eyJhbGciOiJIUzI1Ni...`  
❌ If null: Login again

### Test 2: Check User
```javascript
console.log('User:', JSON.parse(localStorage.getItem('ticketing.auth.user')))
```
✅ Should show: `{ id: "...", name: "...", role: "..." }`

### Test 3: Manual Request
```javascript
fetch('http://localhost:5000/api/supervisor/technicians', {
  headers: {
    'Authorization': 'Bearer ' + localStorage.getItem('ticketing.auth.token')
  },
  credentials: 'include'
})
.then(r => r.json().then(data => ({ status: r.status, data })))
.then(console.log)
.catch(console.error)
```
✅ Should show: `{ status: 200, data: [...] }` or `{ status: 401, data: {...} }`

### Test 4: Check Error Deduplication
```javascript
// Call endpoint multiple times quickly
for (let i = 0; i < 5; i++) {
  fetch('http://localhost:5000/api/supervisor/technicians', {
    headers: { 'Authorization': 'Bearer ' + localStorage.getItem('ticketing.auth.token') },
    credentials: 'include'
  })
}
```
✅ Should see: 5 request logs, but only 1 error log (if it fails)  
❌ Should NOT see: 5 error logs

## Success Checklist

After testing, verify:

- [ ] Console shows request URL
- [ ] Console shows response status (200, 401, 403, etc.)
- [ ] If error, console shows full error object (not `{}`)
- [ ] Error includes: `status`, `statusText`, `body`, `rawText`, `message`
- [ ] Same error is NOT logged more than once per 5 seconds
- [ ] Network tab shows Authorization header
- [ ] Network tab shows actual HTTP response
- [ ] UI shows appropriate state (data, loading, or error)
- [ ] If error in UI, it shows status code
- [ ] Retry button works (if error)
- [ ] No infinite retry loops

## If All Tests Pass

✅ **Fix is working!**

The root causes have been resolved:
1. ✅ `credentials: "include"` added to fetch
2. ✅ `GetAvailableTechniciansAsync()` implemented
3. ✅ Error logging shows full details (not `{}`)
4. ✅ Error deduplication prevents spam
5. ✅ Component error handling works correctly

## If Tests Fail

### Check 1: Code Updated?
```powershell
# Check git status
git status

# Should show modified files:
# frontend/lib/api-client.ts
# backend/.../ISupervisorService.cs
# backend/.../SupervisorService.cs
# backend/.../SupervisorController.cs
```

### Check 2: Backend Rebuilt?
```powershell
cd backend/Ticketing.Backend
dotnet build
# Should show: 0 Error(s)
```

### Check 3: Services Restarted?
- Stop backend (Ctrl+C)
- Stop frontend (Ctrl+C)
- Start backend: `.\tools\run-backend.ps1`
- Start frontend: `cd frontend && npm run dev`

### Check 4: Browser Cache Cleared?
- Hard reload: Ctrl+Shift+R
- Or clear cache in DevTools → Network → Disable cache

### Check 5: Database Setup?
```sql
-- Check if user is supervisor
SELECT u.Email, t.IsSupervisor
FROM Users u
JOIN Technicians t ON t.UserId = u.Id
WHERE u.Email = 'your-email@example.com'

-- If IsSupervisor is 0 or NULL, update it:
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

## Report Issues

If still failing after all checks, provide:

1. **Console Output**: Copy full error log
2. **Network Tab**: Screenshot of request/response
3. **Backend Logs**: Copy any errors from backend console
4. **Database Check**: Result of supervisor query
5. **Browser**: Which browser and version
6. **Code Status**: Output of `git status`

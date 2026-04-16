# Testing Supervisor API - Root Cause Investigation

## Current Status

The frontend is calling:
- `GET /api/supervisor/technicians`
- `GET /api/supervisor/technicians/available`

### What We've Fixed So Far

1. ✅ **Created SupervisorController.cs** with all required endpoints
2. ✅ **Enhanced error logging** in api-client.ts to show status/body (not `{}`)
3. ✅ **Added error deduplication** (max once per 5 seconds)
4. ✅ **Added `credentials: "include"`** to fetch calls (backend CORS allows it)
5. ✅ **Component error handling** already has status codes and proper UI

### Backend Configuration Verified

- **Authentication**: JWT Bearer (not cookies)
- **CORS**: Configured with `AllowCredentials()` and allows `http://localhost:3000`
- **Controller**: SupervisorController.cs exists and compiles successfully

## Next Step: Test the Actual Server Response

### Option 1: Manual Browser Test

1. Start backend:
   ```powershell
   .\tools\run-backend.ps1
   ```

2. Start frontend:
   ```powershell
   cd frontend
   npm run dev
   ```

3. Open browser console and check:
   - What HTTP status is returned?
   - What's in the response body?
   - Is the Authorization header being sent?

4. Look for logs like:
   ```
   [apiRequest] GET http://localhost:5000/api/supervisor/technicians
   [apiRequest] GET http://localhost:5000/api/supervisor/technicians → 401 Unauthorized
   [apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
     status: 401,
     statusText: "Unauthorized",
     contentType: "application/problem+json",
     body: { ... },
     rawText: "...",
     message: "..."
   }
   ```

### Option 2: PowerShell Test

```powershell
# Get your token from browser console:
# localStorage.getItem('ticketing.auth.token')

$token = "YOUR_JWT_TOKEN_HERE"
$headers = @{
    "Authorization" = "Bearer $token"
}

# Test endpoint
Invoke-RestMethod -Uri "http://localhost:5000/api/supervisor/technicians" -Headers $headers -Method GET
```

## Expected Issues and Solutions

### Issue 1: 401 Unauthorized (Most Likely)

**Symptoms**:
- Status: 401
- Body: `{ "type": "https://tools.ietf.org/html/rfc7235#section-3.1", "title": "Unauthorized", "status": 401 }`

**Cause**: User is not authenticated or token is invalid/expired

**Solution**:
- Verify token exists: Check `localStorage.getItem('ticketing.auth.token')`
- Verify token is valid: Decode JWT at jwt.io
- Verify Authorization header is sent: Check Network tab in DevTools
- If token expired: Login again

### Issue 2: 403 Forbidden

**Symptoms**:
- Status: 403
- Body: `{ "message": "Only supervisor technicians can perform this action." }`

**Cause**: User is authenticated but not a supervisor

**Solution**:
- Check database: User's Technician record must have `IsSupervisor = true`
- Login with a supervisor account
- Or update database:
  ```sql
  UPDATE Technicians SET IsSupervisor = 1 WHERE UserId = 'user-guid-here'
  ```

### Issue 3: 404 Not Found

**Symptoms**:
- Status: 404
- Body: Empty or `{ "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4", "title": "Not Found", "status": 404 }`

**Cause**: Controller not loaded or route mismatch

**Solution**:
- Restart backend to ensure controller is loaded
- Check backend startup logs for route registration
- Verify controller file exists: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`

### Issue 4: 500 Internal Server Error

**Symptoms**:
- Status: 500
- Body: Contains exception details

**Cause**: Server-side exception

**Solution**:
- Check backend console logs for full exception
- Common causes:
  - Database connection issue
  - Missing service registration
  - Null reference in service logic

## Verification Checklist

After starting both backend and frontend:

- [ ] Backend logs show: `Now listening on: http://localhost:5000`
- [ ] Backend logs show: `CORS Origins: http://localhost:3000, ...`
- [ ] Frontend console shows request: `[apiRequest] GET http://localhost:5000/api/supervisor/technicians`
- [ ] Frontend console shows response status (not just error)
- [ ] Error log includes: `status`, `statusText`, `body`, `rawText` (not `{}`)
- [ ] Same error is NOT logged more than once per 5 seconds
- [ ] Network tab shows Authorization header: `Bearer eyJ...`
- [ ] Network tab shows actual HTTP status code

## What to Report

If still failing, provide:

1. **HTTP Status Code**: From Network tab or console log
2. **Response Body**: Full JSON response
3. **Authorization Header**: Is it present? (show first 20 chars of token)
4. **Backend Logs**: Any errors or warnings
5. **User Role**: What role is the logged-in user?
6. **Database Check**: Does the user have `IsSupervisor = true`?

## Quick Fixes Based on Status

```javascript
// In browser console, check what's actually happening:

// 1. Check if token exists
console.log('Token:', localStorage.getItem('ticketing.auth.token')?.substring(0, 20) + '...')

// 2. Make a test request
fetch('http://localhost:5000/api/supervisor/technicians', {
  headers: {
    'Authorization': 'Bearer ' + localStorage.getItem('ticketing.auth.token')
  }
})
.then(r => r.json().then(data => ({ status: r.status, data })))
.then(console.log)
.catch(console.error)

// 3. Check user info
console.log('User:', JSON.parse(localStorage.getItem('ticketing.auth.user')))
```

# Supervisor API Testing Guide

## Quick Start

### 1. Start the Backend
```powershell
.\tools\run-backend.ps1
```

Wait for the backend to start. You should see:
```
Now listening on: http://localhost:5000
```

### 2. Start the Frontend
```powershell
cd frontend
npm run dev
```

### 3. Test in Browser

1. Open http://localhost:3000
2. Login with a supervisor account
3. Open browser console (F12)
4. Navigate to the supervisor dashboard

### 4. Verify No Console Spam

**Before the fix**, you would see:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians: {}
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians: {}
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians: {}
... (repeated many times)
```

**After the fix**, you should see:
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```

Or if there's an error (only logged once per 5 seconds):
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 404 Not Found
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 404,
  statusText: "Not Found",
  contentType: "application/problem+json",
  body: { title: "Not Found", status: 404 },
  rawText: "{\"title\":\"Not Found\",\"status\":404}",
  message: "API request failed with status 404"
}
```

## Testing with PowerShell Script

### Get Your JWT Token

1. Login to the frontend
2. Open browser console (F12)
3. Run:
   ```javascript
   localStorage.getItem('ticketing.auth.token')
   ```
4. Copy the token (without quotes)

### Run the Test Script

```powershell
.\tools\test-supervisor-endpoints.ps1 -Token "your-jwt-token-here"
```

### Expected Output

```
==================================
Supervisor API Endpoint Tester
==================================

Checking backend health...
✓ Backend is running

Testing: Get list of managed technicians
  GET http://localhost:5000/api/supervisor/technicians
  ✓ SUCCESS
  Response: [{"technicianUserId":"...","technicianName":"..."}]

Testing: Get available technicians to link
  GET http://localhost:5000/api/supervisor/technicians/available
  ✓ SUCCESS
  Response: []

Testing: Get tickets available for assignment
  GET http://localhost:5000/api/supervisor/tickets/available-to-assign
  ✓ SUCCESS
  Response: []

==================================
Test Summary
==================================
Passed: 3 / 3

✓ All tests passed!
```

## Manual Testing Checklist

### Test Error Deduplication

1. Open browser console
2. Navigate to supervisor dashboard
3. Observe console logs
4. **Expected**: Each unique error is logged at most once per 5 seconds
5. **Expected**: No repeated spam of identical errors

### Test Error Messages

1. Stop the backend
2. Refresh the frontend
3. Check console logs
4. **Expected**: Error messages show:
   - HTTP status code (e.g., 404, 500)
   - Status text (e.g., "Not Found")
   - Response body (parsed JSON or raw text)
   - Never shows empty object `{}`

### Test UI Error States

1. With backend stopped, navigate to supervisor dashboard
2. **Expected**: UI shows error message with status code
3. **Expected**: "Retry" button is visible
4. Click "Retry" button
5. **Expected**: Loading indicator appears
6. **Expected**: Error is shown again (no infinite loop)

### Test Successful Flow

1. Ensure backend is running
2. Login as a supervisor user
3. Navigate to supervisor dashboard
4. **Expected**: Technician list loads successfully
5. Click "افزودن تکنسین" (Add Technician)
6. **Expected**: Available technicians dialog opens
7. **Expected**: No console errors

## Common Issues and Solutions

### Issue: 401 Unauthorized

**Symptoms**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  ...
}
```

**Solutions**:
- Token is expired → Login again
- Token is invalid → Clear localStorage and login again
- User is not authenticated → Ensure you're logged in

### Issue: 403 Forbidden

**Symptoms**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 403,
  statusText: "Forbidden",
  ...
}
```

**Solutions**:
- User is not a supervisor → Login with a supervisor account
- Check database: User's Technician record should have `IsSupervisor = true`

### Issue: 404 Not Found

**Symptoms**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 404,
  statusText: "Not Found",
  ...
}
```

**Solutions**:
- Controller not loaded → Restart backend
- Check backend logs for controller registration
- Verify `SupervisorController.cs` exists in Api/Controllers folder

### Issue: 500 Internal Server Error

**Symptoms**:
```
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 500,
  statusText: "Internal Server Error",
  body: { ... },
  ...
}
```

**Solutions**:
- Check backend console logs for exception details
- Check database connection
- Verify all required services are registered in Program.cs

### Issue: Still Seeing Console Spam

**Symptoms**:
- Same error logged multiple times per second
- Error object shows `{}`

**Solutions**:
- Clear browser cache and hard reload (Ctrl+Shift+R)
- Verify you're using the updated `api-client.ts`
- Check that `shouldLogError()` function exists in api-client.ts

## Verifying the Fix

### 1. Check Error Deduplication

Run this in browser console:
```javascript
// Should only log once per 5 seconds, not repeatedly
fetch('http://localhost:5000/api/supervisor/technicians', {
  headers: { 'Authorization': 'Bearer ' + localStorage.getItem('ticketing.auth.token') }
})
```

Repeat the fetch multiple times quickly. You should see:
- Multiple request logs
- Only ONE error log (if it fails)
- Subsequent errors are throttled

### 2. Check Error Object Structure

When an error occurs, the console should show:
```javascript
{
  status: 404,              // ✓ HTTP status code
  statusText: "Not Found",  // ✓ Status text
  contentType: "...",       // ✓ Content type
  body: { ... },            // ✓ Parsed body (or raw text)
  rawText: "...",           // ✓ Raw response text
  message: "..."            // ✓ Error message
}
```

NOT:
```javascript
{}  // ✗ Empty object
```

### 3. Check Component Behavior

1. Open React DevTools
2. Watch the `supervisor-technician-management` component
3. Verify:
   - `loadList()` is called only once on mount
   - `loadAvailableTechs()` is called only when dialog opens
   - No infinite render loops
   - Error states are properly set

## Success Criteria

✅ **No console spam**: Same error logged max once per 5 seconds  
✅ **Useful error messages**: Always shows status, statusText, body (never `{}`)  
✅ **No infinite loops**: Components don't repeatedly call failing endpoints  
✅ **Good UX**: Users see clear error messages with retry options  
✅ **All endpoints work**: Supervisor API returns 200 OK or appropriate error codes  

## Next Steps After Verification

1. Test supervisor functionality end-to-end:
   - Link/unlink technicians
   - Assign/remove ticket assignments
   - View technician summaries
   - Download CSV reports

2. Test with different user roles:
   - Supervisor user (should work)
   - Regular technician (should get 403)
   - Client user (should get 403)
   - Admin user (depends on requirements)

3. Test edge cases:
   - No linked technicians
   - No available tickets
   - Invalid technician IDs
   - Invalid ticket IDs

4. Performance testing:
   - Large number of technicians
   - Large number of tickets
   - Concurrent requests

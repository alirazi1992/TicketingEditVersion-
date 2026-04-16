# Diagnose Supervisor API - Step by Step

## Current Status

**Backend Controller**: ✅ EXISTS
- File: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`
- Route: `[Route("api/supervisor")]`
- Auth: `[Authorize]` - **REQUIRES AUTHENTICATION**
- Endpoints:
  - `[HttpGet("technicians")]` → `/api/supervisor/technicians`
  - `[HttpGet("technicians/available")]` → `/api/supervisor/technicians/available`

**Frontend Logging**: ✅ IMPROVED
- Now logs full error details including status, statusText, responseText, parsed body

## Step 1: Start Backend

```powershell
.\tools\run-backend.ps1
```

Wait for:
```
Now listening on: http://localhost:5000
```

## Step 2: Test Endpoints Without Auth

```powershell
# Test health (should work)
curl -i http://localhost:5000/api/health

# Test supervisor endpoints (should return 401)
curl -i http://localhost:5000/api/supervisor/technicians
curl -i http://localhost:5000/api/supervisor/technicians/available
```

**Expected Result**: `401 Unauthorized` because `[Authorize]` requires authentication.

**Response Body Should Show**:
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401,
  "traceId": "..."
}
```

## Step 3: Get Auth Token

1. Start frontend: `cd frontend && npm run dev`
2. Open http://localhost:3000
3. Login with any user
4. Open browser console (F12)
5. Run: `localStorage.getItem('ticketing.auth.token')`
6. Copy the token (without quotes)

## Step 4: Test Endpoints With Auth

```powershell
$token = "YOUR_TOKEN_HERE"

# Test with auth
curl -i http://localhost:5000/api/supervisor/technicians -H "Authorization: Bearer $token"
curl -i http://localhost:5000/api/supervisor/technicians/available -H "Authorization: Bearer $token"
```

**Possible Results**:

### A) 200 OK ✅
```
HTTP/1.1 200 OK
Content-Type: application/json

[]
```
**Meaning**: Endpoints work! User is a supervisor. Empty array means no linked technicians.

### B) 401 Unauthorized ❌
```
HTTP/1.1 401 Unauthorized

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

### C) 403 Forbidden ❌
**Meaning**: User doesn't have permission.

**Fix**: Same as 401 - make user a supervisor.

### D) 404 Not Found ❌
**Meaning**: Route doesn't exist or controller not loaded.

**Fix**:
1. Restart backend
2. Check backend logs for controller registration
3. Verify `SupervisorController.cs` is compiled

### E) 500 Internal Server Error ❌
**Meaning**: Backend exception.

**Fix**: Check backend console logs for exception details.

## Step 5: Check Frontend Request

Open browser DevTools → Network tab:

1. Navigate to supervisor page
2. Find request to `/api/supervisor/technicians`
3. Check **Request Headers**:
   ```
   Authorization: Bearer eyJ...
   ```
4. Check **Response**:
   - Status code
   - Response body

## Step 6: Verify Auth Flow

### Check Backend Auth Configuration

File: `backend/Ticketing.Backend/Program.cs`

Look for:
```csharp
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => { ... });
```

**Confirmed**: Backend uses JWT Bearer authentication.

### Check Frontend Auth Sending

File: `frontend/lib/api-client.ts`

Line ~411:
```typescript
if (token) {
  headers["Authorization"] = `Bearer ${token}`;
}
```

**Confirmed**: Frontend sends JWT token in Authorization header.

Line ~443:
```typescript
credentials: "include",
```

**Confirmed**: Frontend includes credentials for CORS.

## Step 7: Check CORS Configuration

File: `backend/Ticketing.Backend/Program.cs`

Look for:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy
            .WithOrigins("http://localhost:3000", ...)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

app.UseCors("DevCors");
```

**Verify**:
- ✅ `WithOrigins` includes `http://localhost:3000`
- ✅ `AllowCredentials()` is present
- ✅ `UseCors` is called BEFORE `UseAuthentication`/`UseAuthorization`

## Common Issues & Fixes

### Issue 1: Frontend Shows Empty Error `{}`

**Cause**: Deduplication or logging issue.

**Fix**: Already fixed - logging now shows full details.

### Issue 2: 401 Unauthorized

**Cause**: User is not a supervisor.

**Fix**:
```sql
-- Check current status
SELECT u.Email, t.IsSupervisor
FROM Users u
JOIN Technicians t ON t.UserId = u.Id
WHERE u.Email = 'your-email@example.com'

-- Make user a supervisor
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

### Issue 3: Token Not Sent

**Cause**: Token is null/undefined in frontend.

**Check**:
```javascript
// In browser console
localStorage.getItem('ticketing.auth.token')
```

**Fix**: Login again if token is missing.

### Issue 4: CORS Error

**Symptoms**: Console shows "CORS policy" error.

**Fix**: Ensure backend CORS allows:
- Origin: `http://localhost:3000`
- Headers: `Authorization`, `Content-Type`
- Methods: `GET`, `POST`, `PUT`, `DELETE`
- Credentials: `true`

### Issue 5: Backend Not Running

**Symptoms**: `Failed to fetch` or connection refused.

**Fix**:
```powershell
.\tools\run-backend.ps1
```

## Expected Frontend Console Output

### Success (200 OK)
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```

### Error (401 Unauthorized)
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

## Debugging Checklist

- [ ] Backend is running on http://localhost:5000
- [ ] Health endpoint works: `curl http://localhost:5000/api/health`
- [ ] Supervisor endpoints return 401 without auth
- [ ] Token exists in localStorage
- [ ] Token is sent in Authorization header (check Network tab)
- [ ] User is a supervisor in database
- [ ] CORS allows http://localhost:3000
- [ ] Frontend console shows full error details (not `{}`)

## Next Steps

1. Start backend
2. Test with curl (steps 2 & 4)
3. Check actual status code and response
4. Apply appropriate fix based on status code
5. Verify in browser that requests return 200 OK

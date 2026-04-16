# Frontend-Backend Connectivity Fix

## Overview

This document describes the fixes applied to ensure reliable connectivity between the Next.js frontend and .NET backend during local development.

## Problem Statement

The frontend was throwing "Failed to fetch" errors when attempting to connect to the backend, even when the backend was running. This was primarily caused by:

1. **CORS Configuration**: Backend CORS policy was too restrictive, not allowing all necessary development origins
2. **Port Configuration**: Backend port was not explicitly configured, leading to potential mismatches
3. **Error Handling**: Frontend error messages were not clear enough for troubleshooting

## Solutions Implemented

### 1. Backend Port Configuration

**Location**: `backend/Ticketing.Backend/Program.cs`

The backend now explicitly listens on `http://localhost:5000` for local development:

```csharp
// Configure URL/Port for local dev
if (string.IsNullOrWhiteSpace(builder.Configuration["ASPNETCORE_URLS"]) && 
    builder.Environment.IsDevelopment())
{
    builder.WebHost.UseUrls("http://localhost:5000");
}
```

**Note**: This can be overridden via the `ASPNETCORE_URLS` environment variable if needed.

### 2. Enhanced CORS Configuration

**Location**: `backend/Ticketing.Backend/Program.cs`

CORS policy now includes all common development origins:

- `http://localhost:3000` (Next.js default)
- `https://localhost:3000`
- `http://127.0.0.1:3000`
- `https://127.0.0.1:3000`
- `http://localhost:3001` (alternative port)
- `https://localhost:3001`
- `http://127.0.0.1:3001`
- `https://127.0.0.1:3001`
- `http://localhost:5173` (Vite default)
- `https://localhost:5173`

**Security**: In production, CORS is restricted to only configured origins from `appsettings.json`.

### 3. Health Endpoint

**Location**: `backend/Ticketing.Backend/Program.cs`

Added a new health check endpoint:

```
GET /api/health
```

Returns:
```json
{
  "status": "healthy",
  "timestamp": "2024-01-01T00:00:00Z",
  "environment": "Development"
}
```

The existing `/api/ping` endpoint remains for backward compatibility.

### 4. Startup Logging

**Location**: `backend/Ticketing.Backend/Program.cs`

The backend now logs important information on startup:

```
========================================
Backend Server Starting
========================================
Environment: Development
Listening on: http://localhost:5000
Base URL: http://localhost:5000
Swagger UI: http://localhost:5000/swagger
Health Check: http://localhost:5000/api/health
CORS Origins: http://localhost:3000, http://127.0.0.1:3000, ...
========================================
```

### 5. Frontend API Base URL

**Location**: `frontend/lib/api-client.ts`

The frontend uses the following priority:

1. `NEXT_PUBLIC_API_BASE_URL` environment variable (if set)
2. Default: `http://localhost:5000`

**Debug Logging**: In development mode, the frontend logs the resolved API base URL to the console.

### 6. Verification Script

**Location**: `tools/verify-backend-connection.ps1`

A PowerShell script to quickly verify backend connectivity:

```powershell
.\tools\verify-backend-connection.ps1
```

The script:
- Tests the `/api/health` endpoint
- Tests the `/api/ping` endpoint
- Provides troubleshooting hints if connection fails

## Configuration

### Backend

**Port**: `http://localhost:5000` (default, can be overridden via `ASPNETCORE_URLS`)

**CORS Origins**: Configured in `appsettings.json`:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:3000",
      "https://localhost:3000",
      "http://localhost:3001",
      "https://localhost:3001"
    ]
  }
}
```

In development, additional origins are automatically added (see above).

### Frontend

**API Base URL**: Set via environment variable or default

Create `.env.local` in the frontend directory:
```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5000
```

Or use the default (no configuration needed).

## Verification Steps

### 1. Start Backend

```powershell
cd backend\Ticketing.Backend
dotnet run
```

Expected output:
```
========================================
Backend Server Starting
========================================
Environment: Development
Listening on: http://localhost:5000
...
```

### 2. Verify Backend is Running

```powershell
.\tools\verify-backend-connection.ps1
```

Expected output:
```
=== Backend Connection Verification ===
[1/2] Testing health endpoint...
✓ Health check passed
[2/2] Testing ping endpoint...
✓ Ping endpoint responded
=== Verification Complete ===
```

### 3. Start Frontend

```powershell
cd frontend
npm run dev
```

Check browser console for:
```
[api-client] API Base URL: http://localhost:5000
[api-client] NEXT_PUBLIC_API_BASE_URL: (not set, using default)
```

### 4. Test Frontend-Backend Connection

1. Open `http://localhost:3000` in browser
2. Open browser DevTools (F12)
3. Check Network tab - API requests should succeed
4. Check Console - no "Failed to fetch" errors

## Troubleshooting

### Backend Not Starting

**Error**: Port 5000 already in use

**Solution**:
1. Find the process using port 5000:
   ```powershell
   netstat -ano | findstr :5000
   ```
2. Stop the process:
   ```powershell
   taskkill /PID <pid> /F
   ```
3. Or use a different port:
   ```powershell
   $env:ASPNETCORE_URLS="http://localhost:5001"
   dotnet run
   ```
   Then update frontend `.env.local`:
   ```
   NEXT_PUBLIC_API_BASE_URL=http://localhost:5001
   ```

### CORS Errors

**Error**: "Access to fetch at 'http://localhost:5000/api/tickets' from origin 'http://localhost:3000' has been blocked by CORS policy"

**Solution**:
1. Verify backend CORS configuration includes your frontend origin
2. Check that `app.UseCors("Frontend")` is called before `app.UseAuthentication()`
3. Ensure backend is running in Development mode

### "Failed to Fetch" Errors

**Error**: "Failed to fetch" or "NetworkError"

**Solution**:
1. Verify backend is running:
   ```powershell
   .\tools\verify-backend-connection.ps1
   ```
2. Check backend logs for errors
3. Verify frontend API base URL matches backend URL
4. Check browser console for specific error messages
5. Ensure no firewall is blocking localhost connections

### Frontend Shows Wrong API URL

**Error**: Frontend is connecting to wrong backend URL

**Solution**:
1. Check `.env.local` file in frontend directory
2. Verify `NEXT_PUBLIC_API_BASE_URL` is set correctly
3. Restart frontend dev server after changing `.env.local`
4. Check browser console for resolved API base URL

## Environment Variables

### Backend

- `ASPNETCORE_URLS`: Override default URL/port (e.g., `http://localhost:5001`)
- `ASPNETCORE_ENVIRONMENT`: Set environment (Development, Production, etc.)

### Frontend

- `NEXT_PUBLIC_API_BASE_URL`: Override default API base URL

## Summary

- **Backend Port**: `http://localhost:5000` (default)
- **Frontend Port**: `http://localhost:3000` (default)
- **CORS**: Automatically configured for development
- **Health Check**: `/api/health`
- **Verification**: Use `tools/verify-backend-connection.ps1`

All connectivity issues should now be resolved. If problems persist, check the troubleshooting section above.



































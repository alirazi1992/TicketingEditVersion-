# TikQ Development Runbook

This document provides the exact commands to run the TikQ application from the repository root.

## Prerequisites

- **.NET 8 SDK** installed and in PATH
- **Node.js 18+** and **npm** installed and in PATH
- **PowerShell** (for Windows scripts)

### Verify Prerequisites

```powershell
dotnet --version  # Should show 8.0.x or higher
node --version     # Should show v18.x.x or higher
npm --version      # Should show 9.x.x or higher
```

## Quick Start (From Repo Root)

### 1. Start Backend

```powershell
.\tools\run-backend.ps1
```

**What this does:**
- Stops any running backend processes
- Checks port 5000 availability (falls back to 5001 if needed)
- Builds and runs the backend on `http://localhost:5000`
- Applies database migrations automatically
- Creates SQLite database at `backend\Ticketing.Backend\App_Data\ticketing.db`

**Expected output:**
```
=== TikQ Backend Runner ===
Step 1: Stopping any running backend processes...
Step 2: Checking port availability...
  Port 5000 is available
Step 3: Starting backend server...
Backend directory: C:\...\backend\Ticketing.Backend
Project: Ticketing.Backend.csproj
URL: http://127.0.0.1:5000
Swagger: http://127.0.0.1:5000/swagger
```

### 2. Verify Backend is Running

```powershell
.\tools\verify-backend.ps1
```

**What this does:**
- Builds the backend to check for compile errors
- Tests the `/api/health` endpoint
- Shows database path and connection status

**Expected output:**
```
=== Backend Verification ===
[1/3] Checking .NET SDK...
✓ .NET SDK: 8.0.xxx
[2/3] Building backend...
✓ Build succeeded
[3/3] Testing health endpoint...
✓ Health check passed
  Status: healthy
  DB Path: C:\...\App_Data\ticketing.db
  DB Connection: Connected
```

### 3. Start Frontend

```powershell
.\tools\run-frontend.ps1
```

**What this does:**
- Checks Node.js installation
- Installs dependencies if needed
- Starts Next.js dev server on `http://localhost:3000`
- Sets `NEXT_PUBLIC_API_BASE_URL` to `http://localhost:5000`

**Expected output:**
```
=== TikQ Frontend Runner ===
Using Node.js: v20.x.x
Starting frontend development server...
Frontend directory: C:\...\frontend
URL: http://localhost:3000
API Base URL: http://localhost:5000
```

### 4. Access the Application

- **Frontend**: http://localhost:3000
- **Backend API**: http://localhost:5000
- **Swagger UI**: http://localhost:5000/swagger
- **Health Check**: http://localhost:5000/api/health

## Verification Commands

### Check Backend Health

```powershell
# From repo root
.\tools\verify-backend.ps1
```

### Test Backend Connection (if backend is already running)

```powershell
# From repo root
.\tools\verify-backend-connection.ps1
```

### Manual Health Check

```powershell
# Using PowerShell
Invoke-WebRequest -Uri "http://localhost:5000/api/health" | ConvertFrom-Json
```

**Expected response:**
```json
{
  "ok": true,
  "status": "healthy",
  "dbPath": "C:\\...\\App_Data\\ticketing.db",
  "canConnectToDb": true,
  "timestamp": "2025-01-02T12:00:00Z",
  "environment": "Development"
}
```

## Stopping Services

### Stop Backend

```powershell
.\tools\stop-backend.ps1
```

### Stop Frontend

Press `Ctrl+C` in the terminal where frontend is running.

## Troubleshooting

### Port 5000 Already in Use

**Symptom:** Backend fails to start with "Port 5000 is already in use"

**Solution:**
```powershell
# Option 1: Use the script (recommended - it will try to free the port or use 5001)
.\tools\run-backend.ps1

# Option 2: Manually stop the process
.\tools\stop-backend.ps1
# Then run again
.\tools\run-backend.ps1

# Option 3: Find and kill the process manually
netstat -ano | findstr :5000
taskkill /PID <pid> /F
```

### Backend Build Errors

**Symptom:** `dotnet build` fails with compile errors

**Solution:**
```powershell
cd backend\Ticketing.Backend
dotnet clean
dotnet restore
dotnet build
# Check error messages and fix issues
```

### Frontend Shows "Cannot connect to backend"

**Symptom:** Frontend displays error banner: "Backend Server Unavailable"

**Checklist:**
1. Backend is running: `.\tools\verify-backend.ps1`
2. Backend is on port 5000: Check backend console output
3. CORS is configured: Backend should allow `http://localhost:3000`
4. No firewall blocking: Check Windows Firewall settings

**Solution:**
```powershell
# 1. Verify backend is running
.\tools\verify-backend.ps1

# 2. If backend is not running, start it
.\tools\run-backend.ps1

# 3. Check health endpoint manually
Invoke-WebRequest -Uri "http://localhost:5000/api/health"
```

### Database Not Found

**Symptom:** Backend starts but health check shows `canConnectToDb: false`

**Solution:**
```powershell
# Backend should create the database automatically on first run
# If it doesn't, check:
cd backend\Ticketing.Backend
dotnet run
# Check console output for migration errors
```

### Frontend Dependencies Not Installed

**Symptom:** `npm run dev` fails with module not found errors

**Solution:**
```powershell
cd frontend
npm install
# Then run again
.\tools\run-frontend.ps1
```

## Project Structure

```
TikQ/
├── backend/
│   └── Ticketing.Backend/
│       ├── Ticketing.Backend.csproj  # Main project file
│       ├── Program.cs                 # Entry point
│       ├── App_Data/
│       │   └── ticketing.db           # SQLite database (created on first run)
│       └── ...
├── frontend/
│   ├── package.json
│   ├── app/                            # Next.js app directory
│   └── ...
└── tools/
    ├── run-backend.ps1                 # Start backend
    ├── run-frontend.ps1                # Start frontend
    ├── verify-backend.ps1              # Build + test backend
    ├── verify-backend-connection.ps1   # Test backend connection
    └── stop-backend.ps1                # Stop backend
```

## Environment Variables

### Backend

- `ASPNETCORE_URLS`: Override default URL/port (e.g., `http://localhost:5001`)
- `ASPNETCORE_ENVIRONMENT`: Set environment (Development, Production)

**Example:**
```powershell
$env:ASPNETCORE_URLS = "http://localhost:5001"
.\tools\run-backend.ps1
```

### Frontend

- `NEXT_PUBLIC_API_BASE_URL`: Override default API base URL

**Example:**
```powershell
$env:NEXT_PUBLIC_API_BASE_URL = "http://localhost:5001"
.\tools\run-frontend.ps1
```

Or create `frontend/.env.local`:
```
NEXT_PUBLIC_API_BASE_URL=http://localhost:5001
```

## Development Workflow

### Making Changes

1. **Start backend:**
   ```powershell
   .\tools\run-backend.ps1
   ```

2. **Start frontend (in another terminal):**
   ```powershell
   .\tools\run-frontend.ps1
   ```

3. **Make changes:**
   - Backend: Edit files in `backend\Ticketing.Backend\`
   - Frontend: Edit files in `frontend\`

4. **Test changes:**
   - Backend: Check Swagger UI at http://localhost:5000/swagger
   - Frontend: Check browser at http://localhost:3000
   - Verify: `.\tools\verify-backend.ps1`

### Before Committing

```powershell
# 1. Build backend
cd backend\Ticketing.Backend
dotnet build

# 2. Build frontend
cd ..\..\frontend
npm run build

# 3. Run verification
cd ..\..
.\tools\verify-backend.ps1
```

## Key Endpoints

- **Health Check**: `GET /api/health` - Returns backend status, DB path, connection status
- **Ping**: `GET /api/ping` - Simple connectivity test
- **Swagger UI**: `GET /swagger` - API documentation and testing

## Database Location

The SQLite database is created at:
```
backend\Ticketing.Backend\App_Data\ticketing.db
```

This path is resolved relative to the backend project's ContentRoot, ensuring consistency regardless of working directory.

## Summary

**To run the app end-to-end:**

1. `.\tools\run-backend.ps1` - Start backend
2. `.\tools\run-frontend.ps1` - Start frontend (in another terminal)
3. Open http://localhost:3000 in browser
4. Backend status indicator will show connection status

**To verify everything works:**

1. `.\tools\verify-backend.ps1` - Builds and tests backend
2. Check http://localhost:5000/api/health - Should return `ok: true`
3. Check frontend - Should show "Backend connected" indicator (dev mode)

---

**Last Updated:** 2025-01-02



































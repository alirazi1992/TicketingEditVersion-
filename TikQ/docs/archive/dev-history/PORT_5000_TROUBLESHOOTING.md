# Port 5000 Troubleshooting Guide

## Symptom

Backend startup fails with:
```
System.IO.IOException: Failed to bind to address http://127.0.0.1:5000: address already in use.
```

This can also cause MSBuild copy errors:
```
MSB3027/MSB3026: cannot copy apphost.exe to bin\Debug\net8.0\Ticketing.Backend.exe because it is locked by "Ticketing.Backend (PID)"
```

## Quick Fix

**Use the safe backend runner script:**
```powershell
cd backend\Ticketing.Backend
.\tools\run-backend.ps1
```

This script automatically:
1. Detects processes using port 5000
2. Verifies they are this backend project (safety check)
3. Stops only the correct backend processes
4. Starts a fresh backend instance

## Manual Troubleshooting

### Step 1: Find What's Using Port 5000

```powershell
netstat -ano | findstr :5000
```

Example output:
```
TCP    127.0.0.1:5000         0.0.0.0:0              LISTENING       12345
```

The last number (12345) is the Process ID (PID).

### Step 2: Identify the Process

```powershell
# Replace 12345 with the actual PID from Step 1
tasklist /FI "PID eq 12345"
```

Example output:
```
Image Name                     PID Session Name        Session#    Mem Usage
========================= ======== ================ =========== ============
Ticketing.Backend.exe      12345 Console                    1     45,123 K
```

### Step 3: Verify It's Our Backend

**Only stop the process if:**
- Process name is `Ticketing.Backend` or `Ticketing.Api`
- Process path is in this repository directory
- Command line contains `dotnet` and `Ticketing.Api` or `Ticketing.Backend`

**Do NOT stop if:**
- Process name is something else (e.g., `node.exe`, `python.exe`, `IIS`)
- You're not sure what the process is

### Step 4: Stop the Process (If Confirmed)

```powershell
# Replace 12345 with the actual PID
taskkill /PID 12345 /F
```

### Step 5: Start Backend

```powershell
cd backend\Ticketing.Backend
dotnet run
```

## Development Mode Diagnostics

When running in Development mode, the backend automatically detects port conflicts and prints:

```
========================================
PORT CONFLICT DETECTED
========================================
Port 5000 is already in use. Backend cannot start.

DIAGNOSTICS:
  Run this command to find the process using port 5000:
    netstat -ano | findstr :5000

SOLUTION:
  1. Run the safe backend runner script:
     .\tools\run-backend.ps1

  2. Or manually stop the process:
     - Find PID using: netstat -ano | findstr :5000
     - Stop it: taskkill /PID <pid> /F

  Process using port 5000:
    PID: 12345
    Name: Ticketing.Backend
    Path: D:\Projects\TikQ\backend\Ticketing.Backend\bin\Debug\net8.0\Ticketing.Backend.exe
========================================
```

## Prevention

**Always use the safe runner script:**
```powershell
.\tools\run-backend.ps1
```

This prevents port conflicts by:
- Automatically stopping stale backend instances
- Verifying processes before stopping (safety)
- Handling file locks from build processes

## Why This Happens

1. **Stale Process:** Previous backend instance didn't shut down cleanly
2. **Multiple Instances:** Accidentally started backend twice
3. **Build Lock:** MSBuild process is still holding the executable file
4. **Other Application:** Another application is using port 5000

## Safety Features

The `run-backend.ps1` script includes safety checks:

✅ **Verification Before Stopping:**
- Checks process command line contains "dotnet" and "Ticketing"
- Verifies process path is in this repository
- Confirms process name matches our backend

❌ **Won't Stop:**
- Processes that don't match our backend
- System processes
- Other applications using port 5000

If the script detects a non-backend process on port 5000, it will:
- Print a warning
- Show the process details
- Exit with error code 1
- Provide manual instructions

## Related Issues

- **MSBuild File Lock:** The script also shuts down the build server to release file locks
- **Database Lock:** If database is locked, close any DB viewers (e.g., DB Browser for SQLite)

## Still Having Issues?

1. Check if port 5000 is actually in use:
   ```powershell
   netstat -ano | findstr :5000
   ```

2. Check for multiple backend processes:
   ```powershell
   tasklist | findstr Ticketing
   ```

3. Restart your terminal/IDE to clear any cached processes

4. If all else fails, restart your computer (last resort)


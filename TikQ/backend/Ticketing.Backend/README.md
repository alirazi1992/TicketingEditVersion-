# Ticketing Backend (ASP.NET Core)

This is a full C# / ASP.NET Core Web API backend for the Next.js ticketing frontend. It uses Entity Framework Core with SQLite, JWT authentication, and seeded demo data.

## Prerequisites
- .NET 8 SDK
- SQLite (bundled with .NET provider)

## Getting Started
1. Install dependencies
   ```bash
   dotnet restore
   ```
2. Apply migrations (optional if using automatic migration on startup)
   From the backend project directory (`Ticketing.Backend`):
   ```bash
   dotnet ef database update
   ```
   If you see SQLite errors like "no such column: t.AcceptedAt", the DB was created before a migration added that column. Either run the command above to apply pending migrations, or restart the API (startup schema guards may add missing columns such as `AcceptedAt` on `TicketTechnicianAssignments`).
3. Run the API
   ```bash
   dotnet run
   ```

The API listens on `http://localhost:5000` (HTTPS `https://localhost:7000`). CORS is enabled for `http://localhost:3000`.

## Dev Reset (DLL lock cleanup)
If `dotnet run` fails with MSB3027/MSB3021 due to locked DLLs, stop the old process and reset the build:

```powershell
# Stop a known PID (example)
Stop-Process -Id 18092 -Force

# Stop any running backend by name
Get-Process -Name "Ticketing.Backend" -ErrorAction SilentlyContinue | Stop-Process -Force

# Stop dotnet processes running the backend DLL
Get-CimInstance Win32_Process -Filter "Name = 'dotnet.exe'" |
  Where-Object { $_.CommandLine -match "Ticketing.Backend\.dll" } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
```

Or run the repo script (recommended):

```powershell
.\scripts\dev-reset.ps1
```

You can also pass a PID:

```powershell
.\scripts\dev-reset.ps1 -Pid 18092
```

## Default Users
- Admin: `admin@test.com` / `Admin123!`
- Technician: `tech1@test.com` / `Tech123!`
- Technician: `tech2@test.com` / `Tech123!`
- Client: `client1@test.com` / `Client123!`
- Client: `client2@test.com` / `Client123!`

## Example Requests
- Login
  ```bash
  curl -X POST http://localhost:5000/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"admin@test.com","password":"Admin123!"}'
  ```
- Create Ticket (as client)
  ```bash
  curl -X POST http://localhost:5000/api/tickets \
    -H "Authorization: Bearer <token>" \
    -H "Content-Type: application/json" \
    -d '{"title":"VPN not connecting","description":"Cannot connect to VPN","categoryId":1,"priority":"High"}'
  ```

## Notes
- The database is automatically migrated and seeded on startup. Schema guards also ensure critical columns exist (e.g. `AcceptedAt` on `TicketTechnicianAssignments`); if a column is missing, run `dotnet ef database update` from the `Ticketing.Backend` folder or restart the API.
- Update the `Jwt:Secret` in `appsettings.json` or set `JWT_SECRET` environment variable for production.

# How to Run the Backend

## Quick Start

From the `backend/Ticketing.Backend` directory:

```powershell
dotnet clean
dotnet build
dotnet run
```

The server will start on `http://localhost:5000` (or the port configured in `appsettings.json`).

## Alternative: Run from API Project

If you prefer to run from the API project directly:

```powershell
cd src/Ticketing.Api
dotnet run
```

## What Happens on Startup

1. **Database Migration**: Migrations are applied automatically on startup
2. **Data Seeding**: Initial data (admin user, categories) is seeded if the database is empty
3. **Swagger UI**: Available at `http://localhost:5000/swagger`

## Configuration

- **Database**: SQLite database at `App_Data/ticketing.db` (relative to ContentRoot)
- **JWT Settings**: Configured in `appsettings.json` under `Jwt` section
- **CORS**: Configured to allow `http://localhost:3000` and `http://localhost:3001`

## Troubleshooting

### CS5001 Error: Program does not contain a static 'Main' method
- **Solution**: Ensure `Program.cs` exists in the root directory
- The root `Program.cs` uses `ApplicationPart` to load controllers from `src/Ticketing.Api`

### Port Already in Use
- Change the port in `appsettings.json` or set `ASPNETCORE_URLS` environment variable:
  ```powershell
  $env:ASPNETCORE_URLS="http://localhost:5001"
  dotnet run
  ```

### Database Migration Errors
- Ensure the database file is not locked by another process
- Check that the `App_Data` directory exists and is writable
- Review migration logs in the console output

## Testing the API

### Health Check
```bash
curl http://localhost:5000/api/ping
# Expected: {"message":"pong"}
```

### Swagger UI
Open `http://localhost:5000/swagger` in your browser to explore the API endpoints.

## Project Structure

- **Root `Program.cs`**: Entry point that loads controllers from API project
- **`src/Ticketing.Api/Program.cs`**: Original API project entry point (can also be run directly)
- **Controllers**: Located in `src/Ticketing.Api/Controllers/`
- **Services**: Located in `src/Ticketing.Infrastructure/Services/`
- **Repositories**: Located in `src/Ticketing.Infrastructure/Data/Repositories/`











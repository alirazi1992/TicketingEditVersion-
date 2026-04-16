# GET /api/health – Response schema

**Endpoint:** `GET /api/health` (unauthenticated)

IIS typically hosts **Ticketing.Api.dll** (e.g. `web.config`: `arguments=".\Ticketing.Api.dll"`). The health endpoint is implemented in `src/Ticketing.Api/Program.cs` and returns the stable shape below for handoff and verification (`tools/_handoff_tests/verify-prod.ps1`).

**Stable JSON shape:**

```json
{
  "ok": true,
  "status": "healthy",
  "timestamp": "<utc>",
  "environment": "<ASPNETCORE_ENVIRONMENT>",
  "contentRoot": "<IHostEnvironment.ContentRootPath>",
  "database": {
    "provider": "SqlServer" | "Sqlite" | "Unknown",
    "connectionInfoRedacted": "<server;db or sqlite path, no secrets>",
    "path": "<sqlite file path if Sqlite, else null>",
    "canConnect": true | false,
    "error": "<nullable string>",
    "dataCounts": { "categories": n, "tickets": n, "users": n },
    "pendingMigrationsCount": n,
    "lastMigrationId": "<nullable string>"
  }
}
```

- **provider** – From `DbContext.Database.ProviderName`: `"SqlServer"`, `"Sqlite"`, or `"Unknown"`. Use for verification and to confirm SQL Server vs Sqlite.
- **connectionInfoRedacted** – Safe for logging; no passwords or secrets.
- **path** – Set only for Sqlite (file path); `null` for SqlServer.
- **canConnect** – From `context.Database.CanConnectAsync()`; on failure, **error** is set with a safe message.
- **dataCounts** – Counts for Categories, Tickets, Users; on table/query failure, count is 0 and **error** is set.
- **pendingMigrationsCount** / **lastMigrationId** – From `GetPendingMigrationsAsync()` count and last applied migration; use to confirm migration state (e.g. `pendingMigrationsCount === 0`).

Scripts should read **provider** from `database.provider` first, with fallback to inferring from `database.path` for older builds. If `database.provider` is missing or empty, `verify-prod.ps1` reports **PROVIDER: FAIL**.

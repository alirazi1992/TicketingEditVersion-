# SQL Server + IIS Readiness / Audit Report

**Audience:** Company IT, deployment and operations.  
**Purpose:** Deployment risks, known failure modes, and troubleshooting for TikQ backend on IIS with SQL Server.  
**Scope:** Readiness audit and runbook reference only — no code changes, no refactors.

---

## 1. Current Architecture Summary

| Layer | Detail |
|-------|--------|
| **Provider selection** | **Environment** `Database__Provider` (if set) overrides **config** `Database:Provider`. Config defaults to `Sqlite`. In **Production**, the app **requires** `Database__Provider` to be set (env); if missing, startup throws. |
| **Dev / SQLite** | When provider is not set or is `Sqlite`, the app uses SQLite (default in Development). SQLite path comes from `ConnectionStrings:DefaultConnection` or default `App_Data/ticketing.db`. |
| **Production / SQL Server** | Set `Database__Provider=SqlServer` and `ConnectionStrings__DefaultConnection`. App fails fast if Production + SqlServer and connection string is missing or empty. |
| **IIS hosting** | In-process (`hostingModel="inprocess"`). App Pool identity (e.g. **IIS APPPOOL\TikQ**) runs the worker process. |
| **Config source** | Environment variables (Process → Machine → User); deploy script injects into `web.config` for the app. |
| **Logs** | Stdout/stderr from the app go to **`<PublishDir>\logs\stdout_*.log`** (e.g. `C:\publish\tikq-backend-<timestamp>\logs`). |

**Production fail-fast behavior:** (1) **Missing provider:** Production requires `Database__Provider` env var; if unset, startup throws. (2) **SqlServer without connection string:** If provider is SqlServer and `ConnectionStrings:DefaultConnection` is missing or empty, startup throws. (3) **SQLite in Production:** SQLite as the main app database is rejected unless `AllowSqliteInProduction=true` (validated at startup). (4) **Migrations:** In Production with SqlServer, if migrations fail on startup the exception is rethrown and the app does not start.

---

## 2. Known Failure Modes

| # | Failure mode | Symptom | Where to check (stdout logs) | Root cause | Exact fix steps |
|---|--------------|---------|------------------------------|------------|-----------------|
| **1** | **SQL login missing (18456 / IIS APPPOOL\TikQ)** | IIS returns **500.30**; app does not start. Health check fails or is unreachable. | `<PublishDir>\logs\stdout_*.log` — search for `18456` or `Login failed for user 'IIS APPPOOL` | Connection string uses Integrated Security (Trusted_Connection) but SQL Server has no Windows login for the App Pool identity. | 1. Open **SSMS**, connect as account that can create logins (e.g. sa). 2. Run **`tools/_handoff_tests/sqlserver-permissions.sql`** (or `sqlserver-permissions.ps1 -DatabaseName TikQ -AppPoolName TikQ` for custom names and execute printed SQL). 3. Recycle App Pool: `Restart-WebAppPool -Name TikQ`. 4. Re-run health check or `verify-prod.ps1`. See [IIS_SQLSERVER_PERMISSIONS.md](IIS_SQLSERVER_PERMISSIONS.md). |
| **2** | **Wrong connection string / unreachable SQL** | 500.30 or health returns 200 but `database.canConnect: false` and `database.error` set. | `<PublishDir>\logs\stdout_*.log` — look for `[STARTUP]` or `[MIGRATION]` and SqlException/network errors. | `ConnectionStrings__DefaultConnection` wrong (server name, instance, firewall), or SQL Server not running / not reachable from IIS host. | 1. Verify SQL Server is running and reachable from the IIS server (e.g. `Test-NetConnection -ComputerName <server> -Port 1433`). 2. Confirm env var `ConnectionStrings__DefaultConnection` (Machine/Process/User or in web.config) has correct Server, Database, and Integrated Security or User Id/Password. 3. If using Integrated Security, ensure login exists (see failure mode 1). 4. Recycle App Pool and retry. |
| **3** | **EF migration failure / 500.30** | App crashes at startup with 500.30; stdout shows migration or schema errors. | `<PublishDir>\logs\stdout_*.log` — look for `[MIGRATION]` and exception stack (e.g. duplicate column, invalid object). | Pending migrations fail (e.g. permission, schema conflict, or provider mismatch). In Production + SqlServer the app fails fast on migration failure. | 1. Fix DB permissions (App Pool identity needs sufficient rights; db_owner for initial setup is typical). 2. Resolve schema conflicts (e.g. run migrations manually in SSMS if needed; see [MIGRATIONS.md](MIGRATIONS.md)). 3. Ensure `Database__Provider` and connection string match the actual DB (SqlServer vs Sqlite). 4. Recycle App Pool and redeploy if needed. |
| **4** | **Missing env vars (Jwt__Secret, CORS, etc.)** | 500.30 or startup fails before HTTP; stdout shows "JWT secret is not configured" or "Cors:AllowedOrigins must be configured". | `<PublishDir>\logs\stdout_*.log` — first lines after process start; look for validation messages. | Production requires: **Jwt__Secret** or **TikQ_JWT_SECRET** (deploy-iis.ps1 reads TikQ_JWT_SECRET and injects as Jwt__Secret); **Cors__AllowedOrigins** (or `Cors:AllowedOrigins`); for SQL Server also **Database__Provider** and **ConnectionStrings__DefaultConnection**. | 1. **JWT:** Set `TikQ_JWT_SECRET` (≥32 chars) or `Jwt__Secret` at Machine/Process/User (deploy-iis.ps1 reads `TikQ_JWT_SECRET` and injects as `Jwt__Secret`). 2. **CORS:** Set `Cors__AllowedOrigins` to frontend origin(s) (e.g. `https://tikq.contoso.com`). 3. **DB:** Set `Database__Provider=SqlServer` and `ConnectionStrings__DefaultConnection` when using SQL Server. 4. Restart App Pool so app picks up env. |
| **5** | **CORS / HTTPS misconfig** | Browser: CORS errors (blocked request, wrong origin). Cookie auth: login succeeds but whoami fails or cookie not sent (e.g. X-Auth-Cookie-Present: false). | N/A (browser dev tools / network); backend stdout may show normal 200 for API. | **CORS:** Frontend origin not in `Cors:AllowedOrigins`. **HTTPS/cookie:** App behind IIS HTTPS but X-Forwarded-Proto not sent, or AuthCookies:SecurePolicy not `Always`, so cookie is set without Secure and browser drops it. | **CORS:** Add frontend origin to `Cors__AllowedOrigins` (env or appsettings), recycle App Pool. **Cookie/HTTPS:** Configure IIS to send **X-Forwarded-Proto: https** (ARR or URL Rewrite). Set **AuthCookies__SecurePolicy=Always** and **AuthCookies__SameSite=Lax** (or per runbook). See [IIS_HTTPS.md](IIS_HTTPS.md). |
| **6** | **All routes 404** | Every request (including `/`, `/api/health`, `/swagger`) returns 404. | `<PublishDir>\logs\stdout_*.log` — check for `[STARTUP] Routes mapped`; if missing, app may have failed before mapping. | Route mapping not reached (e.g. app crashed during startup before `MapControllers()`/health). Or wrong physical path so IIS serves wrong folder. | Confirm IIS site physical path points to the correct publish folder (containing the app DLL and web.config). Check stdout log for startup exceptions; fix any config/DB issue that prevents pipeline from reaching endpoint mapping. See [ROUTES_404.md](ROUTES_404.md). |

---

## 3. Where to Check (Stdout Logs)

- **Path:** `<PublishDir>\logs\stdout_*.log` (e.g. `C:\publish\tikq-backend-20260223120000\logs`).
- **When:** After recycle/restart; first 200 lines often enough for startup and migration.
- **Deploy script:** After a failed verification, `deploy-iis.ps1` prints the path to the latest log and, for SQL Server, checks for 18456 and points to the SQL permissions script.

---

## 4. Before Deploy Checklist

- [ ] **JWT secret:** `TikQ_JWT_SECRET` or `Jwt__Secret` set, ≥32 characters (deploy-iis.ps1 enforces this).
- [ ] **Database:** For SQL Server: `Database__Provider=SqlServer` and `ConnectionStrings__DefaultConnection` set (correct server, database, Integrated Security or credentials).
- [ ] **SQL Server login:** If using Integrated Security, Windows login and DB user for **IIS APPPOOL\TikQ** (or your App Pool name) already created — run `tools/_handoff_tests/sqlserver-permissions.sql` once if not done.
- [ ] **CORS:** `Cors__AllowedOrigins` (or `Cors:AllowedOrigins`) set to frontend origin(s) for Production (required at startup).
- [ ] **ASPNETCORE_ENVIRONMENT:** Set to `Production` on the host when Production behavior is desired.
- [ ] **Optional first run:** `Database__AutoMigrateOnStartup=true` if you want migrations to run on startup (default in Production is false).

---

## 5. After Deploy Verification Steps

1. **GET /api/health**
   - **Expected:** HTTP 200, JSON with `database.provider` = **"SqlServer"**, `database.canConnect` = true, `status` = "healthy".
   - **Script:** From repo root:  
     `.\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer`

2. **verify-prod.ps1 expected PASS**
   - **HEALTH: PASS** (health OK, provider check passed).
   - **ENV: PASS** when expecting SqlServer (Database__Provider and ConnectionStrings__DefaultConnection present).
   - **OVERALL: PASS** (exit code 0).

3. **Optional:** Login + whoami:  
   `.\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer -LoginEmail "admin@example.com" -LoginPassword "YourPassword"`

---

## 6. Boundaries — What We Intentionally Did Not Do

| Item | Why |
|------|-----|
| **No refactors** | This report is documentation and audit only; no application or script logic changes. |
| **No SQLite removal** | SQLite remains the dev and fallback provider; Production continues to reject SQLite unless `AllowSqliteInProduction=true`. |
| **No change to App Pool identity** | IIS APPPOOL\TikQ (or configured name) is the documented identity; SQL permissions and runbooks assume it. |
| **No new features or config schema** | Only documenting existing behavior and failure modes. |

---

## 7. Related Runbooks

| Document | Use |
|----------|-----|
| [IIS_SQLSERVER_PERMISSIONS.md](IIS_SQLSERVER_PERMISSIONS.md) | Step-by-step SQL login/user for App Pool identity (fix 18456). |
| [DEPLOYMENT_REQUIRED_CONFIG.md](DEPLOYMENT_REQUIRED_CONFIG.md) | Required env vars, CORS, HTTPS, first run, and failure scenarios. |
| [IIS_HTTPS.md](IIS_HTTPS.md) | Forwarded headers, AuthCookies, cookie verification behind IIS HTTPS. |
| [ROUTES_404.md](ROUTES_404.md) | When every route returns 404. |
| [HEALTH_SCHEMA.md](HEALTH_SCHEMA.md) | GET /api/health response shape for scripts and monitoring. |
| [MIGRATIONS.md](MIGRATIONS.md) | EF Core migrations and manual application. |

---

*Document version: 1.1 — SQL Server + IIS readiness audit. Architecture and config keys aligned with codebase. No code changes.*

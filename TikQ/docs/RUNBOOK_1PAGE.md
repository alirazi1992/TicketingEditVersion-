# TikQ - Production Runbook (1 page)

**راهنمای تحویل کامل (فارسی):** [RUNBOOK_IIS_SQLSERVER_FA.md](RUNBOOK_IIS_SQLSERVER_FA.md) — متغیرهای env، IIS، تأیید و رفع خرابی‌های رایج.

## URLs
- Frontend (Prod): http://localhost:3000
- Frontend (Dev):  http://localhost:3001
- Backend (IIS):   http://localhost:8080
- Health Check:    http://localhost:8080/api/health

## How to verify active DB provider using /api/health
- **GET** `http://localhost:8080/api/health` (or your backend base URL + `/api/health`).
- Response includes **`database.provider`** (`"SqlServer"` or `"Sqlite"`), **`database.connectionInfoRedacted`** (server + database name only; no passwords), **`auth.windowsAuthEnabled`**, **`auth.windowsAuthMode`** (`"Off"` | `"Optional"` | `"Enforce"`), **`process.identity`**, and **`effectiveEnvVarsPresent`** (booleans for key env vars: `Jwt__Secret`, `ConnectionStrings__DefaultConnection`, `Database__Provider`, `CompanyDirectory__Enabled`). Use this to confirm which provider and connection are active in Production without exposing secrets.

## Environment variables (Backend - IIS web.config)

**Required for Production when using SQL Server (fail-fast if missing):**

| Variable | Required when | Description |
|----------|----------------|-------------|
| **Database__Provider** | Production (or set in appsettings.Production.json) | `SqlServer` or `Sqlite` (case-insensitive). If unset, appsettings.Production.json defaults to `SqlServer`. |
| **ConnectionStrings__DefaultConnection** | Production + SqlServer | SQL Server connection string. If Provider is SqlServer and this is missing/empty, the app throws at startup. |
| **Database__AutoMigrateOnStartup** | First run only | Set to `true` for first deployment so EF migrations run on startup; set back to `false` after. |
| **Jwt__Secret** | Production | Required in Production (min 32 chars). |

**Other:**

- Production requires Cors:AllowedOrigins=["<frontend-origin>"] (e.g. https://your-frontend).
- Bootstrap (first run only when Users table is empty):
  - TikQ_BOOTSTRAP_ADMIN_PASSWORD / EMAIL
  - TikQ_BOOTSTRAP_CLIENT_PASSWORD / EMAIL
  - TikQ_BOOTSTRAP_TECH_PASSWORD / EMAIL
  - TikQ_BOOTSTRAP_SUPERVISOR_PASSWORD / EMAIL

## Test accounts (initial bootstrap)
- admin@local (Admin)
- client@local (Client)
- tech@local (Technician)
- supervisor@local (Supervisor = Technician + IsSupervisor=true)
NOTE: Change passwords after first login.

## Database
- **Provider selection:** `Database:Provider` in config (or env **Database__Provider**) — `Sqlite` or `SqlServer`. Unknown values cause startup failure. Health endpoint reports active provider in `database.provider`.
- **SQLite:** Default for dev. File: &lt;publish&gt;\App_Data\ticketing.db (or path in ConnectionStrings:DefaultConnection).
- **SQL Server (Production):** Set **Database__Provider** = `SqlServer` and **ConnectionStrings__DefaultConnection** to your connection string. If either is missing when Provider is SqlServer in Production, the app fails at startup.
- **AutoMigrateOnStartup:** Default true in Development, false in Production. Set `Database:AutoMigrateOnStartup` (or **Database__AutoMigrateOnStartup**) to run EF migrations on startup when desired. For **first deployment**, set it to true so the app applies migrations on first run (see docs/01_Runbook/MIGRATIONS.md for the “Enable AutoMigrateOnStartup for first deployment” and “First run checklist” sections: connection OK, permissions OK, migrate OK).
- Backup: copy ticketing.db while app is stopped (Recycle AppPool / IIS reset)
- **Minimal seed:** On production startup, if the Categories table is empty, the backend inserts one default category (e.g. Hardware / Laptop) so clients can create tickets. Log: `[SEED_MIN] Categories empty; inserting defaults…` or `[SEED_MIN] Skipped (already has categories).`

## Restart / Recovery
- Recycle IIS AppPool "TikQ" OR restart IIS (iisreset)
- Verify: GET /api/health returns 200

## Frontend API base (env precedence)
- **Production (next build / next start):** Uses `.env.production` only for API URL. Set `NEXT_PUBLIC_API_BASE_URL` there (e.g. `http://localhost:8080`). Do **not** set `NEXT_PUBLIC_API_BASE_URL` in `.env.local` when building for production, or it will override.
- **Development (next dev):** Use `.env.development.local` for API URL (copy from `.env.development.local.example`). Optional: `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000`.
- **Expected env files:** `.env.production` (prod API URL); `.env.development.local` (dev only, gitignored); `.env.local` not required for prod and should not contain `NEXT_PUBLIC_*` when doing a production build.

# TikQ - Production Runbook (1 page)

## URLs
- Frontend (Prod): http://localhost:3000
- Frontend (Dev):  http://localhost:3001
- Backend (IIS):   http://localhost:8080
- Health Check:    http://localhost:8080/api/health

## Environment variables (Backend - IIS web.config)
- Jwt__Secret : required
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
- SQLite file: <publish>\App_Data\ticketing.db
- Backup: copy ticketing.db while app is stopped (Recycle AppPool / IIS reset)
- **Minimal seed:** On production startup, if the Categories table is empty, the backend inserts one default category (e.g. Hardware / Laptop) so clients can create tickets. Log: `[SEED_MIN] Categories empty; inserting defaults…` or `[SEED_MIN] Skipped (already has categories).`

## Restart / Recovery
- Recycle IIS AppPool "TikQ" OR restart IIS (iisreset)
- Verify: GET /api/health returns 200

## Frontend API base (env precedence)
- **Production (next build / next start):** Uses `.env.production` only for API URL. Set `NEXT_PUBLIC_API_BASE_URL` there (e.g. `http://localhost:8080`). Do **not** set `NEXT_PUBLIC_API_BASE_URL` in `.env.local` when building for production, or it will override.
- **Development (next dev):** Use `.env.development.local` for API URL (copy from `.env.development.local.example`). Optional: `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000`.
- **Expected env files:** `.env.production` (prod API URL); `.env.development.local` (dev only, gitignored); `.env.local` not required for prod and should not contain `NEXT_PUBLIC_*` when doing a production build.

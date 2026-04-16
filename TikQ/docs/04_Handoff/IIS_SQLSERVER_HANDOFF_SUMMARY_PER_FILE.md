# IIS + SQL Server handoff — summary per file

One-pass implementation with six phases and gates. No secrets in logs, health, or docs. No seeding endpoints.

---

## Backend (Ticketing.Backend)

| File | Role |
|------|------|
| **Program.cs** | Phase 1–2: Binds `DatabaseOptions` and `BootstrapOptions`; fail-fast in Production when `Database__Provider` missing or when SqlServer and connection string missing. Registers `BootstrapSeederService`, forwarded headers (X-Forwarded-Proto/For), `ConfigureHttpJsonOptions` (camelCase for /api/health). **Health:** `/api/health` returns `provider`, `connectionInfoRedacted`, `path`, `canConnect`, `pendingMigrationsCount`, `lastMigrationId`, `dataCounts`, `effectiveEnvVarsPresent` (env presence booleans only). No secrets. Phase 6: `UseForwardedHeaders` before `UseAuthentication`. |
| **Infrastructure/Data/DatabaseOptions.cs** | Section `Database`. Properties: `Provider` (SqlServer/Sqlite), `AutoMigrateOnStartup`. `NormalizedProvider` throws if invalid. |
| **Infrastructure/Data/BootstrapOptions.cs** | Section `Bootstrap`. `Enabled`, `AdminEmail`, `AdminPassword`, optional TestClient/TestTech/TestSupervisor. |
| **Infrastructure/Data/BootstrapSeederService.cs** | Phase 5: Runs only when `Enabled` and Users table empty. Seeds admin (required) and optional client/tech/supervisor. Logs "Seed applied: N user(s)." or "Seed skipped: {reason}". No secrets. |
| **Infrastructure/Data/StartupMigrationRunner.cs** | Phase 3: When `AutoMigrateOnStartup` true, logs pending/applied migrations, calls `MigrateAsync`. In Production + SqlServer, rethrows on failure (fail-fast). Logs "Result: applied N migration(s)." |
| **Infrastructure/Auth/AuthCookiesOptions.cs** | Phase 6: Section `AuthCookies`. `SameSite` (Lax/Strict/None), `SecurePolicy` (SameAsRequest/Always). |
| **Api/Controllers/AuthController.cs** | Phase 6: `SetAccessCookie`/`ClearAccessCookie` use `AuthCookiesOptions`. **Whoami** sets response header `X-Auth-Cookie-Present: true|false`. Login sets cookie (no auth semantic change). |

---

## Deploy and verification scripts

| File | Role |
|------|------|
| **backend/Ticketing.Backend/deploy-iis.ps1** | Phase 4: Validates JWT secret (TikQ_JWT_SECRET) and optional Bootstrap env vars. Injects into web.config: `Database__Provider`, `ConnectionStrings__DefaultConnection`, `Database__AutoMigrateOnStartup`, `Jwt__Secret`, `Bootstrap__Enabled` + Bootstrap__* when set. Redacts secrets in logs. Recycles App Pool, optional GET /api/health verification. |
| **tools/_handoff_tests/verify-prod.ps1** | GATE 1–2, 4: GET /api/health; asserts provider (e.g. SqlServer); optional login+whoami. Supports camelCase/PascalCase and provider inference from path. Outputs PASS/FAIL; exit 0/1. Never logs secrets. |
| **tools/_handoff_tests/verify-login.ps1** | GATE 5–6: POST /api/auth/login, GET /api/auth/whoami with session. Asserts Set-Cookie (session cookie or header) and isAuthenticated=true. Logs X-Auth-Cookie-Present. Creds from params or TikQ_LOGIN_EMAIL / TikQ_LOGIN_PASSWORD. No secrets in output. |

---

## Docs (English)

| File | Role |
|------|------|
| **docs/01_Runbook/MIGRATIONS.md** | Phase 3: AutoMigrateOnStartup, first-run procedure, health `pendingMigrationsCount`/`lastMigrationId`, verification gate table. |
| **docs/01_Runbook/BOOTSTRAP.md** | Phase 5: Bootstrap vars, verify-login.ps1 usage, GATE 5, "If login verification fails" table. |
| **docs/01_Runbook/IIS_HTTPS.md** | Phase 6: Forwarded headers, AuthCookies options, X-Auth-Cookie-Present, verification gate (Set-Cookie + whoami), failure diagnosis table. |
| **docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md** | Deployment config reference (if present). |

---

## Docs (Persian — تحویل IIS + SQL Server)

| File | Role |
|------|------|
| **docs/RUNBOOK_IIS_SQLSERVER_FA.md** | Full handoff runbook in Persian: required env vars, IIS settings, verification (/api/health fields including pendingMigrationsCount/lastMigrationId), verify-prod.ps1 and verify-login.ps1, common failures and fixes, cookie/HTTPS, final checklist. **Updated** with Phases & Gates table (GATE 1–6) and verify-login.ps1 section. |

---

## Config (no secrets)

| File | Role |
|------|------|
| **appsettings.json** | Development defaults: Provider Sqlite, AutoMigrateOnStartup true, AuthCookies SameSite Lax / SecurePolicy SameAsRequest. |
| **appsettings.Production.json** | Production defaults: Provider SqlServer, AutoMigrateOnStartup false, AuthCookies SecurePolicy Always. Connection string placeholder (override via env). |

---

## Tests

| File | Role |
|------|------|
| **Ticketing.Backend.Tests/MigrationDiscoveryTests.cs** | Phase 3: No duplicate [Migration] IDs; all migrations discoverable (test-time gate). |

---

## Gate summary

| Gate | How to verify |
|------|----------------|
| GATE 1 | GET /api/health → response includes `database.provider` and `database.connectionInfoRedacted`. |
| GATE 2 | Run `verify-prod.ps1 -BaseUrl <url> -ExpectProvider SqlServer` → OVERALL: PASS. |
| GATE 3 | With AutoMigrateOnStartup true and valid SQL Server, after startup GET /api/health → `database.pendingMigrationsCount` === 0. |
| GATE 4 | Run deploy-iis.ps1 (env set), then verify-prod.ps1 → PASS. |
| GATE 5 | GET /api/health → `database.dataCounts.users` > 0; run verify-login.ps1 with bootstrap creds → PASS. |
| GATE 6 | verify-login.ps1 reports Set-Cookie present and whoami isAuthenticated=true (and X-Auth-Cookie-Present). |

# Deployment Required Configuration

This document describes the configuration required to deploy TikQ in production (intranet) and, when used, to connect it to the organization’s Company/Directory database. It is intended for system administrators and infrastructure teams.

**SonarQube scanning** is optional and not required for build or deploy. The repository may include or omit SonarQube/SonarScanner artifacts; they do not affect runtime behavior.

---

## Dual User Stores

TikQ supports two categories of users:

1. **Local users** — Stored in the TikQ database (email, password hash, role, profile). Used for offline/testing and as the system-of-record for roles and authorization.
2. **Server users** — Sourced from **CompanyDirectory** (Boss DB) as a **read-only** user directory. The Company DB provides profile fields only: Email, FullName, IsActive/IsDisabled. **No passwords are stored or read from the Company DB.** Authentication for these users is done with passwords stored in the TikQ DB.

**Shadow user creation:** When a user logs in and is not found in the TikQ DB, and CompanyDirectory is enabled, TikQ looks up the user by email in the Company Directory. If found and active, it creates or updates a **shadow user** in the TikQ DB with default role (e.g. Client) unless a role mapping already exists. TikQ **never writes** to the Company DB. Server users must have their password set in TikQ (e.g. by an admin via **pre-provision**). Admins can set a password for any user by email using the Admin-only endpoint `POST /api/admin/roles/set-password` (body: `{ "email": "...", "newPassword": "..." }`).

---

## Break-Glass Emergency Admin

When the main server or directory is unavailable, an **emergency admin** (break-glass) login can be enabled. It mirrors server Admin privileges and uses a **separate login route** and an **emergency key** so use is explicit and auditable.

- **Frontend:** Dedicated route `/login-emergency`. Form requires **Email**, **Password**, and **Emergency key**.
- **Backend:** Endpoint `POST /api/auth/emergency-login` (body: `{ "email", "password", "emergencyKey" }`). Only active when `EmergencyAdmin:Enabled=true`. Validates the emergency key and credentials, ensures an Admin user exists in the TikQ DB with the same profile, and signs the user in. In Production, **no default passwords** are allowed; if Emergency Admin is enabled but secrets are missing, startup fails.

**Required environment variables for Emergency Admin** (when `EmergencyAdmin:Enabled=true` in Production or ProductionHandoffMode):

| Variable | Description |
|----------|-------------|
| `EmergencyAdmin__Enabled` | Set to `true` to enable emergency login. |
| `EmergencyAdmin__Email` | Email that must be used for emergency login. |
| `EmergencyAdmin__FullName` | Display name for the emergency admin user (e.g. "Break-Glass Admin"). |
| `EmergencyAdmin__Password` | Password for emergency login. **Must be at least 8 characters.** Set via environment in Production (e.g. `EmergencyAdmin__Password`). |
| `EmergencyAdmin__Key` | Extra secret key required in the login form. **Set via environment only in Production** (e.g. `EmergencyAdmin__Key`). |

---

## Required Environment Variables

**JWT secret (mandatory in production)**  
- **Key**: `Jwt:Secret` (appsettings or env `Jwt__Secret`), or `JWT_SECRET` (environment variable).  
- **Required**: In Production (or when `ProductionHandoffMode=true`), startup fails if no JWT secret is set.  
- **Recommendation**: Use a strong secret (e.g. 32+ characters). Set via environment variable so it is not committed (e.g. `JWT_SECRET` or `Jwt__Secret` in IIS or host configuration).

**Company Directory connection (when enabled)**  
- **When**: Only when `CompanyDirectory:Enabled=true`.  
- **Required**:  
  - `CompanyDirectory:ConnectionString` must be non-empty (SQL Server connection to the read-only Company/Directory DB).  
  - `CompanyDirectory:Mode` must be one of: `Enforce`, `Optional`, `Friendly`.  
- **Important**: The Company DB is used **read-only** (identity lookup only). No schema changes or writes are performed by TikQ against this database.

**Production database**  
- **Default**: In Production, using **SQLite** as the main app database causes startup to **fail** unless explicitly allowed.  
- **To use SQL Server**: Set `ConnectionStrings:DefaultConnection` to your SQL Server connection string.  
- **To allow SQLite in Production** (e.g. for testing only): Set `AllowSqliteInProduction=true` in config.

**CORS (mandatory in production)**  
- **Production requires** `Cors:AllowedOrigins=["<frontend-origin>"]` (e.g. your frontend URL). If empty or missing in Production or when `ProductionHandoffMode=true`, startup fails with: "Cors:AllowedOrigins must be configured in production."

**Production flags**  
- **ASPNETCORE_ENVIRONMENT**: Set to `Production` on the production host so that production validation and SQLite rejection run.  
- **ProductionHandoffMode** (optional): When set to `true`, the app applies production-style validation and disables debug/maintenance endpoints even if environment is not Production.

**Bootstrap users (first run only)**  
- To seed initial users when the database is empty, set **Bootstrap:Enabled** and admin credentials. In Production, **Bootstrap:Enabled** defaults to **false**; set it explicitly for the first run. See [BOOTSTRAP.md](BOOTSTRAP.md) for exact environment variables (`Bootstrap__Enabled`, `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword`, and optional test accounts). Do not commit passwords; use environment variables.

---

## Database Responsibilities

**TikQ database (primary)**  
- All application data: users, roles, tickets, categories, custom fields, assignments, settings.  
- This is the only database on which TikQ runs migrations and performs writes.  
- In production, use SQL Server (or another supported provider) via `ConnectionStrings:DefaultConnection`. SQLite is blocked unless `AllowSqliteInProduction=true`.

**Company DB (read-only user directory)**  
- Used only when `CompanyDirectory:Enabled=true`.  
- **Read-only**: Identity lookup only (e.g. email, display name, IsActive/IsDisabled). **No passwords** are read from or stored in the Company DB; server users authenticate with passwords stored in TikQ.  
- **No migrations**: TikQ does not run Entity Framework migrations or any schema changes against the Company DB.  
- **No writes**: No INSERT, UPDATE, DELETE, or DDL against the Company DB. Application-level guards enforce this; the organization should also grant the connection user read-only permissions at the database level.

Roles and landing paths are stored and managed **only in the TikQ database**. The Company DB does not supply or override roles. Shadow users created from the directory get a default role (e.g. Client) in TikQ until an admin assigns a different role or pre-provisions a password.

---

## Security Requirements

**HTTPS**  
Use HTTPS in production for the backend and frontend. Configure certificates and bindings on the host (IIS or Kestrel reverse proxy).

**Cookie flags and IIS / reverse proxy**  
When using cookie-based auth behind IIS or another reverse proxy:

- **Forwarded headers**: The app is configured to use `X-Forwarded-Proto` and `X-Forwarded-For` with `KnownNetworks`/`KnownProxies` cleared so the app trusts the proxy. This ensures `Request.IsHttps` is correct when TLS is terminated at IIS.
- **AuthCookies** (optional config):
  - `AuthCookies:SameSite` — `"Lax"` (default), `"Strict"`, or `"None"` (use `"None"` only for cross-site; requires Secure).
  - `AuthCookies:SecurePolicy` — `"SameAsRequest"` (cookie Secure only when request is HTTPS) or `"Always"` (recommended when behind HTTPS in production). For IIS with HTTPS, set `AuthCookies:SecurePolicy` to `"Always"` (e.g. in appsettings.Production.json or env `AuthCookies__SecurePolicy=Always`).
- Cookies are set with `HttpOnly=true`, `Path=/`. The `/api/auth/whoami` response includes a diagnostic header `X-Auth-Cookie-Present: true|false` (safe; indicates only presence of the auth cookie).

**Secret storage**  
Do not commit JWT secrets or connection strings to source control. Use environment variables, a secure vault, or host-specific configuration (e.g. IIS environment variables, Azure Key Vault).

---

## Deployment Modes

**Intranet deployment**  
TikQ is designed to run on the organization’s internal network. Backend and frontend are hosted on internal servers; the frontend’s API base URL (e.g. `NEXT_PUBLIC_API_BASE_URL`) must point to the backend’s intranet URL.

**Hybrid auth (Company Directory + TikQ)**  
When Company Directory is enabled, users not found in TikQ are looked up in the directory by email. If found and active, a shadow user is created or updated in TikQ with a default role (e.g. Client). **Authentication is always with the password stored in TikQ** (local users or admin pre-provisioned passwords for server users). Roles are assigned only in TikQ.

**Email/password fallback**  
TikQ supports email/password login against the TikQ database. This can be used as the sole auth method or as fallback when Company Directory is optional or unavailable.

**Windows Authentication (optional)**  
TikQ supports Windows Integrated Auth with three modes: **Off**, **Optional**, and **Enforce**. How IT should configure IIS (Anonymous vs Windows Authentication) for each mode is described in **[WINDOWS_AUTH_IIS.md](WINDOWS_AUTH_IIS.md)**.

---

## Failure Scenarios

| Scenario | Cause | Action |
|----------|--------|--------|
| **Missing role** | User exists in Company DB but has no user or role in TikQ (e.g. in Enforce mode). | Provision the user in TikQ and assign a role (Admin, Technician, or Client). |
| **Missing config** | JWT secret not set in production; or Company Directory enabled but connection string or Mode missing; or CORS origins empty in production. | Set `JWT_SECRET` (or `Jwt:Secret`); if using Company Directory, set `CompanyDirectory:ConnectionString` and `CompanyDirectory:Mode`, or set `CompanyDirectory:Enabled=false`; set `Cors:AllowedOrigins` to your frontend origin(s). |
| **Company DB unavailable** | Company Directory is enabled but the directory database is down or unreachable. | Restore directory availability, or temporarily disable Company Directory; ensure timeouts and monitoring are in place. |
| **SQLite in production** | Main app database is SQLite and `AllowSqliteInProduction` is not set. | Set `ConnectionStrings:DefaultConnection` to SQL Server (or intended DB), or set `AllowSqliteInProduction=true` only if acceptable for the environment. |
| **Bootstrap admin password** | No users in DB and bootstrap would run, but `BootstrapAdmin:Password` is missing or shorter than 8 characters. | Set `BootstrapAdmin:Email`, `BootstrapAdmin:Password` (min 8 chars), and `BootstrapAdmin:FullName` when using bootstrap for first user. |
| **SQL login 18456 (IIS App Pool)** | Connection string uses Integrated Security (Trusted_Connection) but SQL Server has no login for the IIS App Pool identity (e.g. `IIS APPPOOL\TikQ`). App fails at startup with 500.30. | Create the Windows login and DB user: run the script in `tools/_handoff_tests/sqlserver-permissions.sql` (or `sqlserver-permissions.ps1 -DatabaseName TikQ -AppPoolName TikQ` for custom names). Then recycle the Application Pool. See [IIS_SQLSERVER_PERMISSIONS.md](IIS_SQLSERVER_PERMISSIONS.md). |

**Common startup messages**  
- *"JWT secret is not configured for production"* — Set `Jwt:Secret` or `JWT_SECRET`.  
- *"CompanyDirectory:ConnectionString is empty"* — Set the connection string or set `CompanyDirectory:Enabled=false`.  
- *"CompanyDirectory:Mode must be one of: Enforce, Optional, Friendly"* — Set `CompanyDirectory:Mode` accordingly.  
- *"SQLite is not allowed as the main app database in Production"* — Use SQL Server (or set `AllowSqliteInProduction=true` if acceptable).  
- *"BootstrapAdmin:Password is missing or too short"* — Set bootstrap admin config when the database has no users.  
- *"Cors:AllowedOrigins must be configured in production"* — Set `Cors:AllowedOrigins` in appsettings (e.g. `["https://your-frontend"]`).
- *"Login failed for user 'IIS APPPOOL\TikQ'"* (SqlException 18456) — SQL Server has no login for the IIS App Pool identity. Run `tools/_handoff_tests/sqlserver-permissions.sql` (see [IIS_SQLSERVER_PERMISSIONS.md](IIS_SQLSERVER_PERMISSIONS.md)), then recycle the App Pool.

---

## First Run Behavior

**Bootstrap rules**  
If the Users table is empty, the application can create a first admin user from configuration (`BootstrapAdmin:Email`, `BootstrapAdmin:Password`, `BootstrapAdmin:FullName`). In Production or when `ProductionHandoffMode=true`, the bootstrap password must be set and at least 8 characters; no default password (e.g. `Admin123!`) is used. If missing or too short, startup fails when no users exist.

**Seeding disabled in production**  
Demo seed data (e.g. test users with known passwords like `Test123!`) runs only in **Development** or when `EnableDevSeeding=true`. In production, `EnableDevSeeding` is false by default; do not set it to true unless you understand the security impact.

**Health endpoint**  
- **URL**: `/api/health` (and `/health` for compatibility).  
- **Auth**: Unauthenticated so load balancers and monitors can check app health without credentials.

---

## SQL Server deployment via deploy-iis.ps1

The backend includes a PowerShell script that deploys the app to IIS and supports **SQL Server** as the database provider. The script reads database-related settings from **Machine**, **Process**, and **User** environment variables (same pattern as the JWT secret) and injects them into the application’s `web.config` so the app uses the correct provider and connection at runtime.

**Script location:** `backend/Ticketing.Backend/deploy-iis.ps1` (run from that directory or ensure `$PSScriptRoot` points to the folder containing `Ticketing.Backend.csproj`).

### Atomic deployment (avoids file locks)

The script uses **atomic deployment** so the running app is never overwritten in place (which can cause file locks and failed updates):

1. **Publish to a new versioned folder** — e.g. `C:\publish\tikq-backend-20260223143000`. The running app continues to use the previous folder until the switch.
2. **Configure and set permissions** — web.config and ACLs are applied only to the new folder.
3. **Switch IIS site physical path** — the site’s physical path is updated in one step to the new folder. IIS then serves from the new deployment.
4. **Recycle application pool** — the app pool is recycled so the worker process loads the new binaries from the new path.
5. **Optional cleanup** — after verification passes, the script can keep the last N versioned folders and remove older ones (parameter `-KeepLastNFolders`, default 5; use `0` to skip). Use this to avoid unbounded disk use while retaining rollback copies.

Use this flow for all deployments so you never overwrite files that IIS has locked.

### Environment variables used for database

| Variable | Required | Description |
|----------|----------|-------------|
| `Database__Provider` | No (default: **SqlServer** for this script) | `SqlServer` or `Sqlite`. For IIS deployment the script defaults to `SqlServer` if not set. |
| `ConnectionStrings__DefaultConnection` | **Yes** when `Database__Provider=SqlServer` | SQL Server connection string. If provider is SqlServer and this is missing, the script throws with a clear error. |
| `Database__AutoMigrateOnStartup` | No | Set to `true` to run EF Core migrations on startup (e.g. first deployment). Configurable; only injected if set. |

The script reads each variable from **Process** → **Machine** → **User** (first non-empty value wins). It never logs secret values; connection strings are redacted (e.g. `Password=***`) when logged.

### Validation and security

- If `Database__Provider` is `SqlServer` and `ConnectionStrings__DefaultConnection` is missing or empty, the script **throws** with a clear message and does not deploy.
- Secrets (JWT, connection strings, bootstrap passwords) are **not printed**; only variable names and redacted connection strings appear in output.

### After deployment

- The script **recycles the application pool** after writing `web.config`, so the app picks up the new environment variables.
- A **verification step** runs: it calls `GET /api/health` and, when the requested provider is SqlServer, asserts that the response `database.provider` is `SqlServer`. The script outputs **PASS** or **FAIL** and fails the deploy if verification does not succeed. If the app fails to start with SQL login error 18456, the script prints an explicit message and points to the SQL permissions script and runbook.

### SQL Server permissions (IIS App Pool identity)

When using **Integrated Security** (e.g. `Server=.;Database=TikQ;Trusted_Connection=True;...`) under IIS, the app runs as the Application Pool identity (e.g. `IIS APPPOOL\TikQ`). SQL Server must have a **Windows login** for that identity and a **user** in the TikQ database with sufficient rights to run migrations (e.g. `db_owner` for initial setup).

**If you see:** `Login failed for user 'IIS APPPOOL\TikQ'` (SqlException 18456) in the app’s stdout log or IIS 500.30 after deploy:

1. **Create login, database (if missing), and user** — Run the provided SQL script in SQL Server Management Studio (SSMS), connected as a principal that can create logins and databases (e.g. `sa` or a login with `sysadmin` / `securityadmin` + `dbcreator`):
   - **Script:** `tools/_handoff_tests/sqlserver-permissions.sql` (default: database `TikQ`, App Pool name `TikQ`).
   - **Custom names:** Run `.\tools\_handoff_tests\sqlserver-permissions.ps1 -DatabaseName <DbName> -AppPoolName <AppPoolName>` and execute the printed SQL in SSMS.
2. **Recycle the Application Pool** — e.g. `Restart-WebAppPool -Name TikQ` (or your App Pool name).
3. **Verify** — `GET /api/health` should return 200 and `database.provider: "SqlServer"`. Run `.\tools\_handoff_tests\verify-prod.ps1 -ExpectProvider SqlServer`.

Full step-by-step instructions and copy-paste SQL: **[IIS_SQLSERVER_PERMISSIONS.md](IIS_SQLSERVER_PERMISSIONS.md)**.

### How to run verify-prod.ps1 after deployment

Use the standalone verification script to confirm that the deployed backend (IIS + SQL Server) is healthy and, optionally, that login and session work.

**Script location:** `tools/_handoff_tests/verify-prod.ps1`

**What it does:**

1. Calls **GET /api/health** and prints provider, environment, DB connectivity, and data counts (categories, tickets, users).
2. **Asserts** that `database.provider` is the expected value (e.g. `SqlServer`) when `-ExpectProvider` is set.
3. **Optional:** If `-LoginEmail` and `-LoginPassword` are provided, runs **POST /api/auth/login** and **GET /api/auth/whoami** to verify auth and session.

Output is explicit **PASS** or **FAIL** per step and overall. Passwords and other secrets are never logged; any error text that might contain secrets is redacted.

**Examples:**

```powershell
# From repo root: health only, assert SqlServer
.\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "https://tikq-api.contoso.com" -ExpectProvider SqlServer

# Local IIS (e.g. after deploy-iis.ps1)
.\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer

# With login check (use seed or bootstrap credentials; do not commit passwords)
.\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "https://tikq-api.contoso.com" -ExpectProvider SqlServer -LoginEmail "admin@example.com" -LoginPassword "YourSecurePassword"
```

**Parameters:**

| Parameter | Description |
|-----------|-------------|
| `BaseUrl` | Backend base URL (default: `http://localhost:8080`). |
| `ExpectProvider` | If set (e.g. `SqlServer`), asserts health response `database.provider` matches. Omit or pass empty to skip. |
| `LoginEmail` | Optional. With `LoginPassword`, enables login + whoami check. |
| `LoginPassword` | Optional. Never printed or logged. |
| `TimeoutSeconds` | HTTP timeout (default: 15). |

Exit code: **0** on full success, **1** on any failure.

### Example: set variables then deploy (SQL Server)

```powershell
# Set Machine-level env vars (run as Administrator if using Machine)
[Environment]::SetEnvironmentVariable("Database__Provider", "SqlServer", "Machine")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Server=.;Database=TikQ;Integrated Security=true;TrustServerCertificate=true", "Machine")
[Environment]::SetEnvironmentVariable("Database__AutoMigrateOnStartup", "true", "Machine")   # optional; for first deployment

# JWT secret is still required (e.g. TikQ_JWT_SECRET or set in IIS)
cd backend\Ticketing.Backend
.\deploy-iis.ps1
# Optional: keep only last 3 versioned folders (default 5; 0 = no cleanup)
# .\deploy-iis.ps1 -KeepLastNFolders 3
```

After a successful run, the script prints **Verification: PASS** and the app is using SQL Server with the given connection string.

---

## Checklist Before Go-Live

- [ ] JWT secret set and not a default or development value.  
- [ ] Production database connection string points to SQL Server (or intended DB); SQLite not used unless explicitly allowed.  
- [ ] If Company Directory is enabled: connection string and Mode are set and valid; directory user has read-only access.  
- [ ] Bootstrap admin password (if used) is strong and from config/env, not a default.  
- [ ] If Emergency Admin is enabled: `EmergencyAdmin:Email`, `EmergencyAdmin:Password` (min 8 chars), and `EmergencyAdmin:Key` are set (via env in Production).  
- [ ] Dev seeding and debug endpoints are disabled (default when not in Development and not `EnableDevSeeding`).  
- [ ] Startup log shows `[HANDOFF] Production validation passed` when running in Production or with `ProductionHandoffMode=true`.  
- [ ] `Cors:AllowedOrigins` is set to your frontend origin(s) (required in production).

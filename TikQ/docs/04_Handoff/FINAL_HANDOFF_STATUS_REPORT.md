# TikQ — Final Handoff Status Report

**Date:** February 2026  
**Audience:** Project owner, IT manager, infrastructure team  
**Purpose:** Single reference for what is delivered, how it works, what to verify, and what could still fail.

---

## 1. Executive Summary

### What TikQ Is

TikQ is an **internal help-desk and ticketing application** for organizations. Employees submit support requests; technicians manage and resolve them; administrators configure categories, users, and workflows. The system is designed for **intranet deployment** and can integrate with an existing company directory (read-only) for identity while keeping all application data and roles in its own database.

### What Is Delivered

- **Backend:** ASP.NET Core (.NET 8) REST API with JWT + cookie auth, optional Company Directory (read-only), role-based access (Admin, Technician, Client, Supervisor), and production hardening (fail-fast validation, debug endpoints blocked, no fallback to Client when role is missing).
- **Frontend:** Next.js 15 (TypeScript, React 19) with cookie-based session restore, dashboard gating by role (`/admin`, `/technician`, `/supervisor`, `/client`), and dedicated emergency-admin login route.
- **Documentation:** Deployment config, handoff checklist, diff summary, and “what could still fail” guidance.
- **Two user stores:** Local users (TikQ DB only) and server users (Company Directory for profile; TikQ for password and role). Shadow users are created in TikQ when directory users first log in (after admin sets password).
- **Break-glass:** Emergency admin login (`/login-emergency`, `POST /api/auth/emergency-login`) when enabled and configured.

### What Is NOT Included / Limitations

- No OIDC/OpenID Connect or external identity provider integration.
- No automated provisioning of Company Directory users into TikQ; admins must assign roles and set passwords (e.g. via `POST /api/admin/roles/set-password`).
- Company DB is **read-only**; schema/table names (e.g. `dbo.Users`, columns `Email`, `FullName`, `PasswordHash`, `IsActive`, `IsDisabled`) must match or be adapted in code—no EF migrations against Company DB.
- Swagger remains enabled in production; consider disabling or restricting by IP if the API is exposed on the intranet.
- Frontend assumes intranet: API base URL must be set (`NEXT_PUBLIC_API_BASE_URL`); CORS must allow the frontend origin.

### Readiness Level and Justification

| Readiness | Justification |
|-----------|---------------|
| **~90%** | Core flows (login, roles, tickets, dashboards, Company Directory shadow users, set-password, emergency admin) are implemented and hardened. Production validation fails fast on missing JWT, Company Directory config, SQLite-in-production, and weak bootstrap/emergency secrets. Remaining 10% is operational: correct env/config for your org, Company DB schema alignment, CORS/base URL, and first-run provisioning steps. |

---

## 2. Current Architecture Snapshot

### Backend

- **Stack:** ASP.NET Core (.NET 8), Entity Framework Core, JWT Bearer + cookie (`tikq_access`), optional Windows Integrated Auth.
- **Auth model:** JWT issued after email/password login (or Windows auth when enabled). Cookie is HttpOnly, SameSite Lax, Secure when HTTPS. Role and `landingPath` come only from TikQ DB.
- **Roles:** Admin, Technician, Client, Supervisor (Technician + `IsSupervisor`). Stored in TikQ `Users.Role` and `Technicians.IsSupervisor`.
- **Policies:** Role-based (`[Authorize(Roles = "Admin")]` etc.); `SupervisorOrAdmin` for supervisor-only endpoints.
- **Databases:**
  - **TikQ DB (system of record):** All app data; EF migrations and writes only here. SQL Server recommended for production; SQLite allowed only in Development or when `AllowSqliteInProduction=true`.
  - **Company/Boss DB (read-only):** Used only when `CompanyDirectory:Enabled=true`. ADO.NET SELECT by email; no EF, no writes, no migrations. Identity lookup only (e.g. Email, FullName, IsActive, IsDisabled).

### Frontend

- **Stack:** Next.js 15.2.4, React 19, TypeScript.
- **Routing:** Root `/` redirects to `landingPath` when authenticated, otherwise `/login`. Dashboards: `/admin`, `/technician`, `/supervisor`, `/client` with `RoleGuard` comparing session `landingPath` to `requiredPath`.
- **Auth context:** Session restored via `GET /api/auth/me` with `credentials: 'include'`. User (and `landingPath`) stored in localStorage for display only; auth is cookie + `/me`. On 401 with `error=missing_role`, redirect to `/login?error=missing_role` with message “No valid role or landing path assigned.”
- **Dashboard gating:** `RoleGuard` redirects to `landingPath` or `/login`; no silent fallback to Client when role/landingPath is missing.

### Offline / Intranet Assumptions

- Backend and frontend are hosted on the organization’s internal network.
- No dependency on public internet for core operation.
- Frontend `NEXT_PUBLIC_API_BASE_URL` must point to the backend’s intranet URL; CORS must allow that frontend origin.

---

## 3. Authentication & User Model (Most Important)

### Two User Stores

- **Local users (TikQ DB):** Created by registration or bootstrap. Email, password hash, role, and profile stored in TikQ. Login: email + password against TikQ only.
- **Server users (Company Directory):** Identity comes from the read-only Company/Boss DB (e.g. Email, FullName, IsActive, IsDisabled). **No passwords are used from the Company DB for login.** Authentication for these users is done with **passwords stored in the TikQ DB**, set by an admin (e.g. `POST /api/admin/roles/set-password`).
- **Shadow users:** When a user logs in and is **not** found in the TikQ DB and Company Directory is enabled, TikQ looks up the user by email in the Company Directory. If found and active, it **creates or updates a shadow user in the TikQ DB** with default role Client (unless a role already exists). Fields synced from directory: Email, FullName (updated on subsequent logins). Role and password are only in TikQ; password is a random hash until an admin sets it via set-password.
- **Password provisioning:** Login for a directory user **fails until an admin sets a password in TikQ** using `POST /api/admin/roles/set-password` (body: `{ "email": "...", "newPassword": "..." }`). Admin can also assign role via `POST /api/admin/roles/assign`.

### Emergency Admin

- **Frontend:** Route `/login-emergency`. Form: Email, Password, Emergency key.
- **Backend:** `POST /api/auth/emergency-login` (body: `{ "email", "password", "emergencyKey" }`). Active only when `EmergencyAdmin:Enabled=true`. Validates emergency key and credentials; ensures an Admin user exists in the TikQ DB (creates/updates if needed) and signs the user in. In Production (or ProductionHandoffMode), **no default passwords**: if Emergency Admin is enabled but Email, Password (min 8 chars), or Key are missing, **startup fails** (StartupValidation).

### Summary Table

| Feature | Local Users | Server Users | Shadow Users | Emergency Admin |
|--------|-------------|--------------|--------------|-----------------|
| **Source** | TikQ DB only | Company Directory (read-only) | Created in TikQ when directory user first matched at login | Config: EmergencyAdmin:Email, :Password, :Key |
| **Password stored where** | TikQ DB | TikQ DB (set by admin) | TikQ DB (random until admin set-password) | Config/env (validated at startup when enabled) |
| **How to login** | Email + password → TikQ | Email + password → TikQ (after admin set-password) | Same as server users (shadow is the TikQ record for that email) | Email + password + Emergency key → `/login-emergency` / `emergency-login` |
| **Role source** | TikQ DB | TikQ DB | TikQ DB (default Client until admin assigns) | TikQ DB (Admin) |
| **Failure modes** | Wrong password; no user in TikQ | No TikQ user; password not set; directory inactive/disabled | Login fails until password set in TikQ; ROLE_NOT_ASSIGNED if role invalid | Not enabled; wrong key/email/password; missing config in Production |

---

## 4. Production Safety & Security Hardening

### StartupValidation (Required Env/Config, Fail-Fast)

When `ASPNETCORE_ENVIRONMENT=Production` **or** `ProductionHandoffMode=true` (config or env):

- **JWT:** `Jwt:Secret` (or `JWT_SECRET` / `Jwt__Secret`) must be set; otherwise startup throws.
- **Company Directory:** If `CompanyDirectory:Enabled=true`, then `CompanyDirectory:ConnectionString` and `CompanyDirectory:Mode` (Enforce, Optional, or Friendly) must be set; otherwise startup throws.
- **Emergency Admin:** If `EmergencyAdmin:Enabled=true`, then `EmergencyAdmin:Email`, `EmergencyAdmin:Password` (min 8 chars), and `EmergencyAdmin:Key` must be set; otherwise startup throws.
- **SQLite in Production:** Using SQLite as the main app database causes startup to fail unless `AllowSqliteInProduction=true`.
- **Bootstrap:** When the Users table is empty and bootstrap runs, Production/HandoffMode requires `BootstrapAdmin:Password` set and at least 8 characters (no default `Admin123!`).

Success: log message **`[HANDOFF] Production validation passed`**.

### No Writes to Boss/Company DB

- **SELECT-only:** `SqlServerCompanyUserDirectory` runs only a parameterized SELECT by email. Code guard rejects command text containing INSERT, UPDATE, DELETE, MERGE, CREATE, ALTER, DROP, EXEC.
- **No EF, no migrations** on Company DB. Organization should also grant the Company DB connection user **read-only** permissions at the database level.

### No Fallback to Client on Missing Role (missing_role)

- **Backend:** WhoAmI and Me use `HasValidRoleAndLandingPath(role, landingPath)`. If invalid: WhoAmI returns `isAuthenticated=false`, `authError=missing_role`, `landingPath="/login"`; Me returns **401** with `error=missing_role`.
- **Frontend:** On 401, api-client reads body for `error` or `authError`; if `missing_role`, redirects to `/login?error=missing_role`. Login page shows: “No valid role or landing path assigned. Please contact your administrator.”

### Dev Endpoints Blocked in Production / Handoff

When **not** Development **or** when `ProductionHandoffMode=true`, the following return **404**:

- `/api/debug` (and subpaths)
- `/api/admin/cleanup`
- `/api/auth/diag`
- `/api/auth/debug-users`

### Secrets Handling (What Must Be Set via Env)

- **JWT:** `Jwt:Secret` or `JWT_SECRET` (or `Jwt__Secret`). Use env or vault in production; do not commit.
- **Company Directory:** `CompanyDirectory:ConnectionString` when enabled (and `Mode`).
- **Emergency Admin (when enabled):** `EmergencyAdmin:Email`, `EmergencyAdmin:Password`, `EmergencyAdmin:Key` (e.g. `EmergencyAdmin__Password`, `EmergencyAdmin__Key` in env).
- **Bootstrap (first user):** `BootstrapAdmin:Email`, `BootstrapAdmin:Password`, `BootstrapAdmin:FullName` when DB has no users and bootstrap is used.

### Deployment Hardening Item (Known Security Notice)

- **Next.js version:** Frontend uses Next.js 15.2.4. Before or after deployment, check [Next.js advisories](https://github.com/vercel/next.js/security) and upgrade if needed for security patches.

---

## 5. Delivery Artifacts

| Document | Purpose |
|----------|---------|
| **README.md** | Project overview, what TikQ is, architecture summary, security model, deployment model, responsibility boundary, links to key docs. |
| **docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md** | Required env vars, dual user stores, shadow users, emergency admin, DB responsibilities, failure scenarios, first-run behavior, checklist before go-live. |
| **docs/04_Handoff/HANDOFF_READINESS_CHECKLIST.md** | Pre-handoff and pre-production checklist (production config, bootstrap, auth fail-safe, Company DB read-only, dev surfaces, verification steps). |
| **docs/HANDOFF_DIFF_SUMMARY.md** | Diff-style summary of production-hardening changes (StartupValidation, bootstrap, seeding, debug blocker, missing_role, read-only Company DB, frontend redirect). |
| **docs/04_Handoff/HANDOFF_WHAT_COULD_STILL_FAIL.md** | Risks after handoff: config/env, DB schema, auth/roles, read-only guard, debug surfaces, operational, frontend/CORS. |
| **docs/04_Handoff/FINAL_HANDOFF_STATUS_REPORT.md** | This report: executive summary, architecture, auth model, safety, artifacts, risks, verification, sign-off, open questions. |
| **docs/archive/dev-history/** | Development and debugging notes; not part of the formal delivery set. |

---

## 6. Known Risks / What Could Still Fail

- **CORS / base URL:** Wrong `NEXT_PUBLIC_API_BASE_URL` or CORS not allowing the frontend origin → API calls fail. Verify in production.
- **Company DB schema mismatch:** `SqlServerCompanyUserDirectory` expects table/columns (e.g. `dbo.Users`, `Email`, `FullName`, `PasswordHash`, `IsActive`, `IsDisabled`). If your directory schema differs, identity lookup can fail; adjust SQL/mapping or disable Company Directory.
- **Password provisioning operations:** Server/shadow users cannot log in until an admin sets a password in TikQ. Ensure a clear process (and possibly training) for admins to use `POST /api/admin/roles/set-password` and assign roles.
- **Emergency admin misconfiguration:** If Emergency Admin is enabled but Key/Password/Email are wrong or missing in Production, startup fails (intended). If enabled with weak secrets in config files, risk of misuse; use env-only for Production.
- **Missing env vars:** JWT, Company Directory (if enabled), Emergency Admin (if enabled), bootstrap (if first run). StartupValidation catches most; ensure host is set to `Production` or `ProductionHandoffMode=true` so validation runs.
- **Stale JWTs:** Old tokens without valid role/landingPath get 401/missing_role and redirect to login; users may need to log in again after deployment.
- **Swagger in production:** Still enabled; consider disabling or restricting by IP if the API is reachable on the intranet.

---

## 7. Step-by-Step “How to Verify Quickly” (10–20 min)

1. **Start backend (Development, no production secrets)**  
   - Run from repo root (e.g. `dotnet run` in backend project).  
   - Expect: app starts; no `[HANDOFF] Production validation passed` (validation not run in Development).  
   - Optional: `GET /api/health` → 200.

2. **Start backend as “Production” with missing JWT**  
   - Set `ASPNETCORE_ENVIRONMENT=Production`; do **not** set `Jwt__Secret` or `JWT_SECRET`.  
   - Expect: startup **fails** with message that JWT secret is required.

3. **Start backend as Production with required config**  
   - Set `ASPNETCORE_ENVIRONMENT=Production`, `Jwt__Secret=YourStrongSecretAtLeast32Chars`, and a SQL Server connection string (or `AllowSqliteInProduction=true` for a quick test).  
   - Expect: startup succeeds; log shows **`[HANDOFF] Production validation passed`**.

4. **Blocked dev endpoints**  
   - With Production or `ProductionHandoffMode=true`, call `GET /api/debug/users` or `GET /api/auth/diag`.  
   - Expect: **404**.

5. **Frontend login and missing_role**  
   - Start frontend with `NEXT_PUBLIC_API_BASE_URL` pointing to backend.  
   - Log in with a user that has no valid role/landingPath (or use an invalid token).  
   - Expect: 401 and redirect to `/login?error=missing_role` and message “No valid role or landing path assigned.”

6. **Set-password and login (server/shadow user)**  
   - As Admin, `POST /api/admin/roles/set-password` with `{ "email": "user@example.com", "newPassword": "SecurePass8" }`.  
   - Log in via UI with that email and password.  
   - Expect: success and redirect to appropriate dashboard.

7. **Emergency login (if enabled)**  
   - Set `EmergencyAdmin:Enabled=true`, `EmergencyAdmin:Email`, `EmergencyAdmin:Password` (≥8 chars), `EmergencyAdmin:Key`.  
   - Open `/login-emergency`, submit email, password, and key.  
   - Expect: login as Admin and redirect to `/admin`.

---

## Sign-Off Checklist

- [ ] README.md and docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md reviewed.
- [ ] JWT secret set for production (env/vault), not a default or dev value.
- [ ] Production database: SQL Server (or intended provider); SQLite not used unless explicitly allowed.
- [ ] If Company Directory enabled: connection string and Mode set; directory user has read-only access.
- [ ] If Emergency Admin enabled: Email, Password (min 8), Key set via env in production.
- [ ] Bootstrap admin (if used): strong password from config/env, not default.
- [ ] Startup log shows `[HANDOFF] Production validation passed` when running as Production or ProductionHandoffMode.
- [ ] Dev endpoints (`/api/debug`, `/api/admin/cleanup`, `/api/auth/diag`, `/api/auth/debug-users`) return 404 in production/handoff.
- [ ] Frontend `NEXT_PUBLIC_API_BASE_URL` and CORS verified for production.
- [ ] Process for provisioning server/shadow users (set-password, assign role) agreed and documented.
- [ ] Sign-off: _________________ Date: __________

---

## Open Questions for the Organization

1. **Company DB schema:** Exact table and column names for the directory (e.g. `dbo.Users`, `Email`, `FullName`, `IsActive`, `IsDisabled`). Is `PasswordHash` present? TikQ does not use it for login; auth is always via TikQ password (set-password).
2. **SSL/HTTPS:** Will backend and frontend be served over HTTPS? Certificates and bindings (IIS or reverse proxy) are the organization’s responsibility.
3. **Domain/subpath:** Will the app be served from a subpath or different subdomain? Cookie path/domain may need adjustment.
4. **Swagger:** Disable in production or restrict by IP/feature flag?
5. **Monitoring:** Is `/api/health` (and `/health`) sufficient for load balancers and alerts, or are additional health checks required?
6. **First admin:** Use bootstrap (empty DB + config) or manual creation? Ensure bootstrap password is strong and from env when using Production.

---

*End of Final Handoff Status Report*

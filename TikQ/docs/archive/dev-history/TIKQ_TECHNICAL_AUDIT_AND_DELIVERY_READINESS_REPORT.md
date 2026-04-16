# TikQ — Full Technical Audit & Delivery Readiness Report

**Date:** 2026-02-21  
**Scope:** Current implementation vs. intended final delivery architecture and constraints  
**Type:** Analysis and reporting only (no code changes)

---

## 1. Current Architecture Overview

### 1.1 Backend Auth Flow

- **Login:** `POST /api/auth/login` (AllowAnonymous) → `UserService.LoginAsync`:
  - If **CompanyDirectory.Enabled** and connection string set: identity validated against Company DB (read-only SELECT by email); password verified (PBKDF2/BCrypt/plaintext); then TikQ user resolved by email from TikQ DB. Role and `isSupervisor` come **only** from TikQ (Users + Technicians).
  - If Company Directory disabled or user not in Company DB: credentials checked against TikQ DB only (email + password hash via `IPasswordHasher`).
- **Register:** `POST /api/auth/register` (AllowAnonymous) → explicit role required (no default); Admin creation requires caller to be authenticated Admin (or bootstrap). JWT generated with 30 min expiry for cookie.
- **Session restore:** `GET /api/auth/whoami` (AllowAnonymous) and `GET /api/auth/me` (AllowAnonymous for whoami; 401 when not authenticated for /me). Token from cookie `tikq_access` or `Authorization: Bearer`; Windows Integrated Auth path resolves domain user → email → TikQ user and returns role, `isSupervisor`, `landingPath`.
- **Logout:** `POST /api/auth/logout` clears cookie.

### 1.2 Frontend Auth Flow

- **Session:** Cookie-based; JWT stored in HttpOnly cookie `tikq_access`; frontend does **not** store token in localStorage (auth-context sets `token = null`).
- **Restore:** On load, `fetchCurrentUser()` calls `GET /api/auth/me` with `credentials: 'include'`; response mapped to `User` (including `landingPath`); user persisted to localStorage for display only; routing uses backend `landingPath` or computed fallback.
- **Login/Register:** Response includes `role`, `isSupervisor`, `landingPath`; frontend maps to `User`, persists, and redirects to `landingPath` (or `getLandingPath(mapped)` if missing).

### 1.3 JWT Structure (Claims Included)

- **Claims:** `sub` (userId GUID), `ClaimTypes.NameIdentifier` (same), `email` (JwtRegisteredClaimNames.Email), `ClaimTypes.Role` (`user.Role.ToString()` — e.g. "Admin", "Technician", "Client"), `isSupervisor` ("true"/"false"), `is_supervisor` (duplicate).
- **No** name, permissions, or extra bloat. Role is **always** from TikQ `User.Role`; never from Company Directory.
- **Issuer/Audience:** From config (`Jwt:Issuer`, `Jwt:Audience`). **Expiry:** 30 minutes when used for login cookie; config `Jwt:ExpirationMinutes` (e.g. 240) used when no override passed.

### 1.4 Cookie Usage

- **Name:** `tikq_access`. **Options:** `HttpOnly: true`, `SameSite: Lax`, `Secure: Request.IsHttps`, `Path: "/"`, `Expires: DateTimeOffset.UtcNow.AddMinutes(30)`.
- JWT is read from: (A) `Authorization: Bearer`, (B) cookie `tikq_access`, (C) query `access_token` for SignalR `/hubs/tickets`.

### 1.5 SSO State

- **No OIDC/OpenID Connect or external IdP** in the codebase. Auth is JWT (email/password or Windows Negotiate) + optional Company DB (read-only identity).
- "SSO" in the sense of **Windows Integrated Auth** is optional: `WindowsAuth:Enabled`; when true, `Smart` scheme can forward to Negotiate. No dependency on external SSO provider.

### 1.6 Email/Password State

- **Fully supported.** With CompanyDirectory disabled, login is purely TikQ DB (email + password hash). Registration uses TikQ-only storage. No requirement for Company DB or Windows Auth.

### 1.7 Role Storage Mechanism

- **Roles stored only in TikQ DB:** `Users.Role` (enum: Client=0, Technician=1, Admin=2, Supervisor=3). Technicians table has `IsSupervisor`; supervisor **capability** = Technician role + `Technicians.IsSupervisor`.
- Company DB is **not** used for role; interface `ICompanyUserDirectory` and `CompanyDirectoryUser` have no role fields.

### 1.8 Claims Transformation

- No custom claims transformation middleware found. JWT is generated with role from `user.Role.ToString()` and `isSupervisor` from `Technician.IsSupervisor`. Policy `SupervisorOrAdmin` uses `RequireRole(Admin)` or (Technician + claim `isSupervisor` = "true").

### 1.9 Dashboard Redirect Logic

- **Backend:** `LandingPathResolver.GetLandingPath(role, isSupervisor)` → Admin → `/admin`, Technician+isSupervisor → `/supervisor`, Technician → `/technician`, Supervisor enum → `/supervisor`, default → `/client`. Returned in login/register, WhoAmI, and UserDto (MapToDtoAsync).
- **Frontend:** Root `/` uses `getLandingPathFromSession({ user })` → redirects to `user.landingPath` if valid, else `getLandingPath(user)` (role + isSupervisor). RoleGuard compares `requiredPath` to session landing path and redirects if mismatch.
- **Redirect decision:** Backend is source of truth for `landingPath`; frontend uses it when present and valid, else derives from role (lowercase) + isSupervisor.

### 1.10 Company DB Integration Status

- **Interface:** `ICompanyUserDirectory` with `GetByEmailAsync(email)` returning `CompanyDirectoryUser` (Email, FullName, PasswordHash, IsActive, IsDisabled). No role.
- **Implementation:** When `CompanyDirectory.Enabled` and connection string set → `SqlServerCompanyUserDirectory` (ADO.NET, **SELECT only**). When disabled → `FakeCompanyUserDirectory` (returns null). No EF, no migrations, no write operations on Company DB.

### 1.11 DB Providers (SQLite vs SQL Server)

- **TikQ (main app):** SQLite only. `AppDbContext` uses single connection from `ConnectionStrings:DefaultConnection` (resolved to absolute path under ContentRoot). All EF migrations target this SQLite DB.
- **Company/Boss DB:** SQL Server, **read-only**, used only by `SqlServerCompanyUserDirectory` via raw `SqlConnection` + `ExecuteReaderAsync`. No DbContext or migrations for Company DB.

---

## 2. Production Safety Analysis

| Check | Finding | Risk |
|-------|---------|------|
| Can the system accidentally write to Boss/Company DB? | **No.** Only Company DB code is `SqlServerCompanyUserDirectory`, which runs a single parameterized SELECT. No INSERT/UPDATE/DELETE, no EF. | **None** |
| Are EF migrations isolated from Boss DB? | **Yes.** Migrations apply only to `AppDbContext` (SQLite). Company DB has no EF context. | **None** |
| Is Company DB connection properly separated? | **Yes.** Separate connection string (`CompanyDirectory:ConnectionString`); used only in `SqlServerCompanyUserDirectory`. TikQ data in SQLite only. | **None** |
| Are connection strings safe? | **Partial.** Default appsettings has placeholder Company DB string. Production should use env/secrets; no connection string written to logs in reviewed code. SQLite path logged at startup (absolute path). | **Low** (config hygiene) |
| Any write query targeting Company DB? | **No.** Only `ExecuteReaderAsync` in Company directory. | **None** |
| Read-only enforced by design or only by SQL? | **By design:** no write code path to Company DB. SQL user can still be read-only for defense in depth. | **Good** |
| Dangerous default fallback behavior? | **Yes, one:** In `BootstrapAdminOnceIfNoUsersAsync`, when `BootstrapAdmin:Password` is empty, code defaults to `"Admin123!"`. Used when DB has no users (including first-run production). `appsettings.Production.json` does **not** set BootstrapAdmin; so first deploy with empty DB can create admin with default password if config is not overridden. | **Medium** |

---

## 3. Role & Redirect Logic Analysis

| Item | Verification |
|------|--------------|
| **How roles are resolved** | From TikQ `Users.Role` only. JWT claim `ClaimTypes.Role` = `user.Role.ToString()` (PascalCase). Frontend `roleFromApi()` normalizes to lowercase ("admin", "technician", "client"); "Supervisor" string maps to "technician". |
| **How isSupervisor is injected** | Backend: `ResolveIsSupervisorAsync(userId)` → Technician row by UserId, then `technician.IsSupervisor`. Set in JWT claims and in UserDto/WhoAmI. Frontend: from API response or DTO. |
| **How landingPath is decided** | Backend: `LandingPathResolver.GetLandingPath(role, isSupervisor)`. Returned in AuthResponse, UserDto, WhoAmI. Frontend: prefers `user.landingPath` from API; if invalid/missing, computes from role + isSupervisor (admin→/admin, technician+isSupervisor→/supervisor, technician→/technician, else /client). |
| **Where redirect decision happens** | **Both.** Backend sends `landingPath`; frontend uses it for post-login redirect and root "/" redirect; RoleGuard uses it to allow/redirect per route. |
| **Why all dashboards might redirect to /client** | If backend omits or sends invalid `landingPath` and frontend `user.role` is missing or not "admin"/"technician", `getLandingPath()` falls back to `/client`. Also `roleFromApi(undefined)` = `"client"`. So: stale or partial user (e.g. from localStorage before /me completes), or API not returning role/landingPath, can cause default to /client. |
| **Role casing (Client vs client)** | Backend uses PascalCase in JWT and DTOs ("Admin", "Technician", "Client"). Frontend normalizes to lowercase for routing. No mismatch in logic. |
| **Enum/string mapping** | Backend enum `UserRole`: Client=0, Technician=1, Admin=2, Supervisor=3. Frontend accepts numeric 0,1,2,3 and strings; "supervisor"/"engineer" map to technician. Consistent. |
| **Default fallback to Client** | **Yes.** `getLandingPath(null)` and `getLandingPathFromSession({ user: null })` → `/client`. `roleFromApi(null)` → `"client"`. Backend `LandingPathResolver` default → `/client`. |

---

## 4. Authentication Mode Control

| Question | Answer |
|----------|--------|
| Is SSO fully optional? | **Yes.** No OIDC/SSO; Windows Negotiate is optional via `WindowsAuth:Enabled`. When false, only JWT (email/password or cookie) is used. |
| Can the system run with only email/password? | **Yes.** CompanyDirectory can be disabled; login then uses only TikQ DB. No external IdP required. |
| Can it run intranet-only? | **Yes.** Backend and frontend can be deployed on intranet; `NEXT_PUBLIC_API_BASE_URL` can point to intranet backend; no hardcoded public URLs. |
| What breaks if OIDC config is missing? | **N/A.** There is no OIDC; nothing depends on it. |
| Hard dependency on external identity provider? | **No.** Only optional dependencies: Company DB (read-only) and Windows Auth (optional). |

---

## 5. Security Audit

| Item | Finding |
|------|---------|
| **JWT size** | Minimal claims (sub, email, role, isSupervisor x2). Size is small. |
| **Claims bloat** | Only duplicate is `isSupervisor` / `is_supervisor`; no other bloat. |
| **Cookie flags** | HttpOnly: yes. Secure: when `Request.IsHttps`. SameSite: Lax. Appropriate. |
| **Token lifetime** | Login cookie and token: 30 min. Config `ExpirationMinutes` (240) used when no override (e.g. other token usages). No refresh token; session ends after expiry. |
| **Refresh logic** | **None.** User must re-login after expiry. |
| **Privilege escalation** | Role and isSupervisor from TikQ DB only; no client-controlled role. Admin registration restricted to authenticated Admin. AssignRole is Admin-only. |
| **Supervisor elevation** | Only via Admin assigning role/isSupervisor (TikQ DB). No self-elevation. |
| **Hardcoded passwords in seed** | **Yes.** SeedData uses `Test123!` for seed users and `Admin123!` for admin@test.com. Used only when `IsDevelopment()` (SeedData runs only in Development). BootstrapAdmin defaults to `Admin123!` when config is empty and runs even in Production on empty DB. |
| **Dev backdoors** | Debug endpoints (see below) and dev-only bootstrap/seed. |
| **Dev switch role menus** | **None** found in frontend (no impersonation or role-switch UI). |
| **Debug endpoints** | `GET /api/auth/debug-users` (Admin, Dev only → 404 in prod). `GET /api/auth/diag` (AllowAnonymous, Dev only → NotFound() in prod). `AdminDebugController`: `/api/debug/users`, `technicians`, `tickets/test-query`, `tickets/count` (Admin, Dev only → 404 in prod). `GET /api/debug/data-status` (minimal API, Dev only → 404 in prod). Production middleware returns 404 for `/api/debug*` and `/api/auth/debug-users` before reaching controllers. |
| **Public endpoints without auth** | Intended: `/api/auth/login`, `register`, `logout`, `whoami`, `diag` (diag 404 in prod). `/api/health` (no auth — for health checks). `/api/ping` (no auth). Categories: one AllowAnonymous GET (categories list). All other API endpoints require Authorize or role/policy. |

---

## 6. Clean Architecture Integrity

| Check | Finding |
|------|---------|
| Infrastructure leaking into domain? | Domain entities (e.g. User, Ticket) have no infra references. Some **controllers** use `AppDbContext` directly: AuthController (email existence check, GetDebugUsers), AdminDebugController (all actions), AdminMaintenanceController (user delete). Ideal: use IUserRepository.ExistsByEmailAsync in AuthController; debug/maintenance could stay or be moved behind services. |
| Company directory abstracted? | **Yes.** `ICompanyUserDirectory` in Application; implementations (Fake, SqlServer) in Infrastructure. No Company DB in Domain. |
| Interfaces respected? | Services use repositories and interfaces; Company directory and JWT generator are abstracted. Controllers inject services; the few direct DbContext uses are the exception. |
| Circular dependency? | Not observed; Domain has no refs to Application/Infrastructure. |
| Direct DB context in controllers? | **Yes:** AuthController, AdminDebugController, AdminMaintenanceController. |

---

## 7. Delivery Gap Analysis

| Area | Current State | Required for Delivery | Risk Level | Gap Severity | Fix Required? |
|------|---------------|----------------------|------------|--------------|--------------|
| **Auth** | JWT + cookie; optional Company DB + Windows Auth; no OIDC. | Same; optional SSO; email/password and intranet. | Low | Low | No (optional config) |
| **Roles** | Stored in TikQ only; role + isSupervisor in JWT and DTOs. | Roles only in TikQ; Company DB read-only. | Low | None | No |
| **Redirect** | Backend landingPath + frontend fallback; RoleGuard by path. | Client→/client, Technician→/technician, Technician+supervisor→/supervisor, Admin→/admin. | Low | Low | Verify in prod (e.g. /me always returns landingPath). |
| **Company DB** | Read-only; SELECT only; no EF; isolated. | No write to Boss DB. | Low | None | No |
| **Security** | Cookie flags OK; no refresh; BootstrapAdmin default password in prod when DB empty. | No default admin password in prod; strong secret. | Medium | Medium | **Yes:** Ensure BootstrapAdmin password (and JWT secret) set in production config/env. |
| **Dev cleanup** | Debug endpoints 404 in non-Development; ApiStatusDebug gated by NODE_ENV + env var. | No dev tools in production. | Low | Low | Optional: remove or further gate debug code for clarity. |
| **Deployment config** | JWT secret required in prod (startup fails otherwise). Connection strings from config/env. | Production-safe config. | Medium | Medium | **Yes:** Document and set JWT secret, BootstrapAdmin (or rely on BootstrapUsersIfEmptyAsync env vars only), and Company DB URL if used. |
| **Environment switching** | ASPNETCORE_ENVIRONMENT and appsettings.Production.json; Seed only in Development. | Correct env for prod. | Low | Low | Ensure Production env and appsettings.Production used. |
| **Offline support** | Not required for delivery; preferences cache in localStorage. | Intranet/offline capable if needed. | Low | Low | No (deployment choice). |

---

## 8. Final Readiness Score

### Overall readiness: **~75%**

- **Strong:** Auth model, role/redirect design, Company DB read-only isolation, no write to Boss DB, debug endpoints hidden in production, no AI/OIDC dependency.
- **Gaps:** BootstrapAdmin default password on first-run production; JWT secret and BootstrapAdmin must be explicitly set for production; minor controller–DbContext coupling; no refresh token (by design, but session expiry may surprise users).

### Blocking issues (must fix before delivery)

1. **Production config:** Ensure `Jwt:Secret` (or equivalent env) is set in production (already enforced by startup). Ensure first-run admin is not created with default password: set `BootstrapAdmin:Password` (and preferably Email) in production config or rely on `BootstrapUsersIfEmptyAsync` with env vars `TikQ_BOOTSTRAP_ADMIN_PASSWORD` and `TikQ_BOOTSTRAP_ADMIN_EMAIL` (production path uses these and skips if password missing/short).
2. **Documentation:** Clear production checklist: JWT secret, BootstrapAdmin or env bootstrap, CompanyDirectory if used, CORS origins, `NEXT_PUBLIC_API_BASE_URL` for frontend.

### Non-blocking issues

1. AuthController uses `AppDbContext` for email-exists check; could use `IUserRepository.ExistsByEmailAsync` for cleaner layering.
2. Frontend default to `/client` when role/landingPath missing — ensure /me and login responses always include role and landingPath so this fallback is rare.
3. No refresh token — acceptable if 30 min session is desired; otherwise consider refresh flow later.

### Immediate must-fix list before delivery

1. **Remove or override default BootstrapAdmin password in production** (e.g. in `appsettings.Production.json` or env so that empty DB never gets admin@test.com / Admin123!).
2. **Confirm JWT secret is never empty in production** (already enforced; document where it is set).
3. **Verify** that in production build, `ApiStatusDebug` and any dev-only UI are not shown (NODE_ENV and NEXT_PUBLIC_ENABLE_HEALTH_CHECKS).

### Nice-to-have improvements

- Add refresh token or extended session for better UX.
- Replace direct `AppDbContext` usage in AuthController with repository.
- Explicit integration test: deploy with Production env and empty DB, confirm no default admin password and debug routes return 404.

---

## 9. Production Simulation Checklist

**If deployed tomorrow inside company intranet:**

| Question | Answer |
|----------|--------|
| **What could fail?** | (1) JWT secret not set → startup fails (intended). (2) First-run empty DB with default BootstrapAdmin config → admin created with Admin123!. (3) Frontend points to wrong backend if NEXT_PUBLIC_API_BASE_URL not set for prod. (4) CORS if frontend origin not in allowed list. (5) Windows Auth if enabled but IIS/Kestrel not configured for Negotiate. |
| **What is risky?** | Default BootstrapAdmin password on first deploy; connection strings or paths in config that expose internal structure (low if not logged). |
| **What is stable?** | Auth flow (email/password and cookie); role and redirect logic; read-only Company DB; no writes to Boss DB; debug routes disabled in production; no AI/OIDC dependency. |
| **What is unclear?** | Whether `appsettings.Production.json` is merged and overrides BootstrapAdmin in all deploy paths; exact CORS and base URL used in target intranet. |
| **Dangerous assumptions?** | Assuming "Production" environment is set and appsettings.Production is loaded. Assuming operators set JWT secret and BootstrapAdmin (or env) and do not ship default appsettings.json with Admin123! to production. |

---

**End of report.** No code was modified; this is analysis only.

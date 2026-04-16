# TikQ Pre-Handoff Engineering Audit (Full Project Analysis)

**Date:** 2026-02-22  
**Purpose:** Production handoff to company IT team. Findings are based on actual codebase and configuration only.

---

## 1) AUTH & LOGIN

### Cookie auth flow
- **Cookie name:** `tikq_access` (constant in `AuthController.cs`).
- **Set on:** `POST /api/auth/login`, `register`, `emergency-login`. Cleared on `POST /api/auth/logout`.
- **Options:** `HttpOnly: true`, `SameSite: Lax`, `Secure: Request.IsHttps`, `Path: "/"`, `Expires: DateTimeOffset.UtcNow.AddMinutes(30)`.
- **Domain:** Not set; cookie is host-scoped (request host).
- **JWT:** Stored in cookie only; frontend never reads or sends token in headers (auth-context uses `credentials: 'include'`; no Bearer in body).

### JWT claims content
- **Source:** `JwtTokenGenerator.GenerateToken` (Infrastructure/Auth/JwtTokenGenerator.cs).
- **Claims:** `sub` (user Id), `ClaimTypes.NameIdentifier` (user Id), `JwtRegisteredClaimNames.Email`, `ClaimTypes.Role` (user.Role.ToString()), `isSupervisor`, `is_supervisor` (true/false string). No name, no permissions array.

### WhoAmI endpoint behavior
- **Route:** `GET /api/auth/whoami`, `[AllowAnonymous]`.
- **JWT path:** Reads `email` or `ClaimTypes.Email`, `ClaimTypes.Role` or `role`, `isSupervisor` or `is_supervisor` from claims; computes `landingPath` via `LandingPathResolver.GetLandingPath(parsedRole, isSupervisor)`. If role/landingPath invalid (`HasValidRoleAndLandingPath`), returns `isAuthenticated: false`, `authError: "missing_role"`, `landingPath: "/login"`.
- **Windows path:** If no email in claims, uses `User.Identity.Name` → `_windowsUserMapResolver.ResolveEmail(domainUser)` → `_userService.GetByEmailAsync(resolvedEmail)`; returns user role, isSupervisor, landingPath from TikQ; same fail-safe for invalid role/landingPath.
- **Unauthenticated:** Returns `isAuthenticated: false`, `landingPath: "/login"`.

### Role resolution logic
- **Backend:** Role from TikQ `Users.Role` (enum: Client, Technician, Admin, Supervisor). `isSupervisor` from `Technician.IsSupervisor` (Technician row by UserId). `LandingPathResolver`: Admin → `/admin`, UserRole.Supervisor or (Technician + isSupervisor) → `/supervisor`, Technician → `/technician`, default → `/client`. UserDto and login/register/whoami/me use this; no role from Company DB.

### Redirect logic per role
- **Frontend:** Root `/` (app/page.tsx) and RoleGuard use `getLandingPathFromSession({ user })` from auth-routing. Session comes from `/api/auth/me` (auth-context `fetchCurrentUser`), not whoami. Login page on success calls `router.replace(landingPath)` with backend `landingPath`. RoleGuard: if `landingPath !== requiredPath` redirects to `landingPath`; if no user, redirects to `/login`.

### Security risks
- **JWT dev fallback:** In `Program.cs`, when not in Development, missing JWT secret throws; in Development, empty secret uses `"SuperSecretDevelopmentKey!ChangeMe"`. If deployed as Development with default, key is weak.
- **BootstrapAdmin in appsettings.json:** `BootstrapAdmin:Password` is `"Admin123!"` in committed appsettings.json; production bootstrap uses config when no users — risk if env not overridden (production/Handoff validation requires strong password when no users).
- **Cookie domain:** Not set; fine for same-host; for front/back on different subdomains, may need explicit domain.

---

## 2) RBAC VALIDATION

### Admin-only endpoints
- **Controllers (class-level):** AdminCategoryFieldsController, SmartAssignmentController, TechniciansController, AdminRolesController, UsersController (class Admin), AdminTicketsController, AdminDebugController, AdminFieldDefinitionsController, AdminMaintenanceController (per-action where used), AdminReportsController, AdminSubcategoryFieldsController, AdminAutomationController. CategoriesController: all write/read actions `[Authorize(Roles = nameof(UserRole.Admin))]`.
- **TicketsController:** Many actions explicitly `Authorize(Roles = "Admin")` or `"Technician,Admin"` or `"Client"` per action (e.g. CreateTicket Client; various Admin or Admin+Technician).

### Supervisor vs Technician behavior
- **Policy:** `SupervisorOrAdmin` = `RequireAssertion`: Admin OR (Technician AND claim `isSupervisor` == "true"). Used by SupervisorController (`/api/supervisor/*`).
- **Ticket list scope:** `TicketRepository.QueryListItemsAsync`: Client → `CreatedByUserId == userId`. Technician (includes supervisors): tickets where AssignedToUserId == userId, OR in AssignedTechnicians for userId, OR supervisor scope (SupervisorTechnicianLinks: tickets assigned to linked technicians), OR unassigned with TechnicianSubcategoryPermissions for userId. Admin: no extra filter on baseQuery (sees all).

### Access leaks
- **Register role:** Register endpoint validates role; validRoles in error messages are Client, Technician, Admin (Supervisor not creatable via register; correct). No endpoint found that returns another role’s data without authorization.
- **Technician vs Admin:** Technician-only endpoints (e.g. TechnicianTicketsController) use `[Authorize(Roles = nameof(UserRole.Technician))]`; supervisors pass via Technician + isSupervisor. List filtering is in repository by userId and role (supervisor sees linked technicians’ tickets).

### Missing policies
- **UserRole.Supervisor enum:** Exists but registration and bootstrap create “supervisor” as Technician + IsSupervisor. Policy uses Technician + isSupervisor claim; no separate “Supervisor” role requirement. Consistent.

---

## 3) COMPANY DIRECTORY INTEGRATION

### Read-only SQL usage
- **Implementation:** `SqlServerCompanyUserDirectory` (Infrastructure/CompanyDirectory). Single query: `SELECT Email, FullName, PasswordHash, IsActive, IsDisabled FROM dbo.Users WHERE LOWER(LTRIM(RTRIM(Email))) = @email;`. ADO.NET; no EF. `EnsureReadOnlyCommand` rejects command text containing: INSERT, UPDATE, DELETE, MERGE, CREATE, ALTER, DROP, EXEC, EXECUTE.

### Fields read
- Email, FullName, PasswordHash, IsActive, IsDisabled. Mapped to `CompanyDirectoryUser(Email, FullName, PasswordHash, IsActive, IsDisabled)`.

### Where passwords are checked
- **Login:** `UserService.LoginAsync` verifies password only against TikQ: `_passwordHasher.VerifyHashedPassword(tikqUser, tikqUser.PasswordHash, request.Password)`. Company DB is used only to create/update shadow user when user not in TikQ and directory returns active user; password for login is always TikQ.
- **Dead code:** `VerifyCompanyDirectoryPassword(storedValue, password)` exists in UserService but is never called. Company DB PasswordHash is read but not used for authentication. Documentation ([DEPLOYMENT_REQUIRED_CONFIG.md](docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md)) states server users authenticate with passwords in TikQ only — behavior matches; method is unused.

### If company DB unavailable
- `GetByEmailAsync` (company) returns null. In LoginAsync, shadow user is created only when tikqUser == null and company user exists and is active. If company DB is down, company lookup fails → no shadow creation; existing TikQ users still log in (TikQ lookup first); new users not in TikQ get Unauthorized.

---

## 4) DASHBOARD FLOWS

### Admin
- **Landing route:** `/admin`. RoleGuard `requiredPath="/admin"`. MainDashboard.
- **Ticket operations:** Full access via Admin-only and Admin+Technician endpoints; list = all tickets (no filter in QueryListItemsAsync for Admin).
- **Assignment flows:** Admin can assign via admin ticket APIs and technicians management.

### Supervisor
- **Landing route:** `/supervisor`. RoleGuard `requiredPath="/supervisor"`. MainDashboard.
- **Ticket operations:** Uses same ticket list as Technician (QueryListItemsAsync with role Technician, userId); sees own assigned + tickets of linked technicians (SupervisorTechnicianLinks) + subcategory permissions for unassigned. SupervisorController: technicians list, available technicians, link/unlink, summary — all under `SupervisorOrAdmin`.
- **Data visibility:** Technicians linked to supervisor only; ticket list scoped as above.

### Technician
- **Landing route:** `/technician`. RoleGuard `requiredPath="/technician"`. MainDashboard.
- **Ticket operations:** TechnicianTicketsController and TicketsController actions with Technician (or Technician+Admin). List: assigned to user, or in AssignedTechnicians, or (if supervisor) linked technicians’ tickets, or unassigned with subcategory permission.
- **Assignment flows:** Accept/decline, reply, status changes per backend rules.

### Client
- **Landing route:** `/client`. RoleGuard `requiredPath="/client"`. MainDashboard.
- **Ticket operations:** Create ticket (POST /api/tickets, Client role); list only own (`CreatedByUserId == userId`).
- **Data visibility:** Only own tickets.

---

## 5) TICKETING CORE

### Create ticket flow
- **Endpoint:** `POST /api/tickets`, `[Authorize(Roles = nameof(UserRole.Client))]`, RequestSizeLimit 10MB. Accepts form-data (ticketData + attachments) or JSON body. Parses `TicketCreateRequest`: CategoryId, SubcategoryId, Title, Description, Priority, FieldValues. Validation: Title, CategoryId, SubcategoryId, Priority required/valid. Delegated to `_ticketService` for create; field definitions and custom field values applied there.

### Category/subcategory
- Categories and subcategories managed via CategoriesController (Admin). Ticket create requires CategoryId and SubcategoryId; subcategory drives dynamic field definitions (AdminFieldDefinitionsController, AdminSubcategoryFieldsController, GetFieldDefinitions by category/subcategory).

### Multi-technician assignment
- Ticket has AssignedToUserId (primary) and AssignedTechnicians (TicketTechnicianAssignment). QueryListItemsAsync and ticket detail include assignments; assignment APIs allow multiple technicians; acceptance and activity events tracked.

### Field designer / TicketFieldValue
- SubcategoryFieldDefinition and CategoryFieldDefinition; TicketFieldValue links ticket to field definition with value. Entity and configuration present (TicketFieldValueConfiguration, migrations). Create flow passes field values in request and service persists them (TicketService create uses field definitions and values).

### Timeline & notifications
- TicketActivityEvent for timeline; TicketMessage for replies. SignalR TicketHub for real-time updates; frontend use-signalr connects with credentials. TicketUserState (LastSeenAt) for unread/computed last activity.

---

## 6) FRONTEND PRODUCTION READINESS

### Env usage (.env.production)
- **File present:** `frontend/.env.production` contains `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080` (placeholder). Production build must override with real backend URL (e.g. build-time or server env).

### API base URL
- **Resolved by:** `getApiBaseUrl()` in api-client: first `process.env.NEXT_PUBLIC_API_BASE_URL`; then cached/detected URL; `getDefaultApiBaseUrl()` in url.ts returns `"http://localhost:5000"` only when `NODE_ENV === "development" || "test"`, otherwise `""`. So in production, without env set, default is empty.

### Hardcoded localhost
- **api-client.ts (line 373):** When effectiveBase is invalid (not absolute), code sets `effectiveBase = "http://localhost:5000"` and logs warning — **production risk:** if env is missing or wrong, production can fall back to localhost:5000.
- **client-dashboard.tsx:** `useState<string>("http://localhost:5000")` and `getEffectiveApiBaseUrl() || "http://localhost:5000"` for display/health — wrong in production if backend is elsewhere.
- **login/page.tsx:** Error message string "Ensure the backend is running on http://localhost:5000" (UX only).
- **url.ts:** localhost:5000 only in development/test.

### Cookie usage
- **apiRequest:** `credentials: "include"` for all fetch calls; no token in body or localStorage for JWT. Session restored via `/api/auth/me` with cookie. 401 triggers clear of localStorage user/key and redirect to `/login`. Correct for cookie-based auth.

---

## 7) BACKEND PRODUCTION RISKS

### Seed logic
- **When:** `SeedData.InitializeAsync` runs only when `app.Environment.IsDevelopment() || enableDevSeeding` (Program.cs). Production default: no seed.
- **Risk:** If `EnableDevSeeding=true` in production, seed runs and creates/updates users with known passwords (e.g. Test123!) and demo data — documented but dangerous if misconfigured.

### Bootstrap users
- **BootstrapAdmin:** When no users and not (Development or EnableDevSeeding), `BootstrapAdminOnceIfNoUsersAsync` runs. Production/Handoff: password must be set and ≥ 8 chars or startup throws. appsettings.json has `BootstrapAdmin:Password: "Admin123!"` — must be overridden in production (env or appsettings.Production).
- **BootstrapUsersIfEmptyAsync:** Env-based (TikQ_BOOTSTRAP_ADMIN_PASSWORD etc.); used only when no users and not dev seed path; optional; logs only on failure.

### Hardcoded secrets
- **appsettings.json:** Jwt:Secret is `""`; BootstrapAdmin:Password `"Admin123!"`; CompanyDirectory connection string placeholder; EmergencyAdmin disabled with empty Key/Password. Production validation requires JWT secret and (when used) strong bootstrap/emergency config.
- **Program.cs:** Development JWT fallback `"SuperSecretDevelopmentKey!ChangeMe"` when secret not set.

### Logging
- Startup logging to `logs/startup.log`; unhandled exception handler writes to same. GlobalExceptionHandlerMiddleware logs full exception. No evidence of logging passwords or tokens. Health and migration logs are informational.

### Exception handling
- GlobalExceptionHandlerMiddleware catches unhandled exceptions; maps UnauthorizedAccessException → 403, ArgumentException → 400, KeyNotFoundException/not found InvalidOperationException → 404; else 500. In non-Development, detail is generic message; traceId returned. Development returns detail and stack trace.

---

## 8) DATA SAFETY

### Writes to company DB
- **None.** Company directory uses `SqlServerCompanyUserDirectory` with a single SELECT; keyword guard blocks INSERT/UPDATE/DELETE/DDL/EXEC. No other code path writes to company DB.

### EF migrations risk
- Migrations run on TikQ DbContext only: `await context.Database.MigrateAsync()` in Program.cs. Company DB has no DbContext or migrations. Schema guards (e.g. EnsureSubcategoryFieldDefinitionsSchemaAsync, Ensure*ColumnExists) run against TikQ SQLite after migrate; safe for single primary DB.

### SQLite vs SQL Server
- Default connection string is SQLite (`Data Source=App_Data/ticketing.db`); path resolved to ContentRoot. StartupValidation: in Production (or ProductionHandoffMode), SQLite is disallowed unless `AllowSqliteInProduction=true`; production expected to use SQL Server connection string. SQLite-specific guards (e.g. PRAGMA, column checks) are in code; switching to SQL Server requires connection string and provider change; schema is EF-generated and should be compatible.

---

## 9) DEPLOYMENT RISKS

### IIS compatibility
- Forwarded headers: `UseForwardedHeaders(ForwardedHeaders.XForwardedProto | XForwardedFor)` so that HTTPS and client IP work behind IIS/reverse proxy. DataProtection keys under App_Data/keys; DPAPI on Windows. No explicit IIS module requirements in code; Kestrel behind IIS is standard.

### CORS config
- Policy name `"DevCors"` used for both dev and prod. Production: `allowedCorsOrigins` from `Cors:AllowedOrigins`; if null, `Array.Empty<string>()`. When `allowedCorsOrigins.Length > 0`, policy uses `WithOrigins(allowedCorsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()`. When length is 0, no origins are added — **production with no Cors:AllowedOrigins configured will reject all cross-origin requests** (browser will block API calls from frontend). Must set Cors:AllowedOrigins in production (e.g. frontend origin URL).

### Cookie domain
- Cookie options in AuthController do not set `Domain`. Cookie is tied to request host. If frontend and backend are on different hosts/subdomains, cookies may not be sent unless domain/path are configured (e.g. shared parent domain and Domain set).

### Ports & redirects
- Backend default URLs in dev: `http://localhost:5000`. Frontend .env.production placeholder: `http://localhost:8080`. Production must set backend URL (and frontend base if needed); IIS bindings and any redirect rules are host responsibility.

---

## 10) FINAL HANDOFF SCORE

**Score: 68/100**

### Real blockers (must fix before handoff)
1. **Frontend API base URL in production:** If `NEXT_PUBLIC_API_BASE_URL` is unset or wrong, api-client falls back to `http://localhost:5000` (api-client.ts ~373). Production build must set `NEXT_PUBLIC_API_BASE_URL` to the real backend URL; document and enforce.
2. **CORS in production:** Empty `Cors:AllowedOrigins` results in a policy with no allowed origins; all cross-origin API requests fail. Production config must set `Cors:AllowedOrigins` to the frontend origin(s).
3. **BootstrapAdmin password in repo:** appsettings.json contains `BootstrapAdmin:Password: "Admin123!"`. Production/Handoff validation requires strong password when DB has no users; override via appsettings.Production or environment so no default is used in production.

### Real bugs
1. **Dead code:** `VerifyCompanyDirectoryPassword` in UserService is never called. Company DB PasswordHash is read but not used; behavior matches “auth only against TikQ” — safe but confusing; consider removing or using for optional company-DB auth in future.
2. **client-dashboard.tsx:** Initial state and fallback use `"http://localhost:5000"`; in production with different backend URL, status/health display may point users to wrong URL (cosmetic/diagnostic only).

### Real production risks
1. **JWT secret:** In Development, default key is used if secret not set; ensure production is not run under Development or with empty secret.
2. **EnableDevSeeding:** If set true in production, seed runs and creates users with known passwords (Test123! etc.). Keep false in production.
3. **.env.production:** Contains `localhost:8080`; must be overridden per environment so production does not point to localhost.
4. **Cookie domain:** If frontend and API are on different subdomains, cookie may not be sent; configure Domain (and SameSite/Secure) for that topology.

No generic advice or theoretical items; all items above are derived from the current code and configuration.

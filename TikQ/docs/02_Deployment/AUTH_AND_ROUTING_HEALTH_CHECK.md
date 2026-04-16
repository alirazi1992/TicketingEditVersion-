# Full System Health Check: Authentication and Routing

**Scope:** Backend (.NET), Frontend (Next.js), Login flow (email/password), Role resolution (Client, Technician, Supervisor, Admin).  
**Date:** 2025-02-21.

---

## 1) Backend auth pipeline

### Login endpoint
- **Location:** `backend/Ticketing.Backend/Api/Controllers/AuthController.cs` (lines 279–313).
- **Status:** OK.
- **Details:** `POST /api/auth/login` accepts `{ "email", "password" }`, calls `_userService.LoginAsync`, returns 400 on missing input, 401 on invalid credentials, 403 for disabled user or `RoleNotAssigned`, 200 with `ok`, `user`, `role`, `isSupervisor`, `landingPath`. Cookie is set via `SetAccessCookie(resp.Token)`.

### JWT / cookie generation
- **Location:** `AuthController.cs` (lines 48–71), `Infrastructure/Auth/JwtTokenGenerator.cs` (lines 39–65).
- **Status:** OK.
- **Details:** Cookie name `tikq_access`; HttpOnly, SameSite=Lax, Secure when HTTPS; 30 min expiry. JWT includes `sub`, `NameIdentifier`, `email`, `ClaimTypes.Role` (`user.Role.ToString()`), `isSupervisor`, `is_supervisor`. Token is read from cookie in `Program.cs` (OnMessageReceived, cookie `tikq_access`).

### Claims: role, isSupervisor
- **Location:** `JwtTokenGenerator.cs` (lines 45–53).
- **Status:** OK.
- **Details:** Role from TikQ `User.Role` only; `isSupervisor` from `Technician.IsSupervisor` (resolved in `UserService` before token generation). No role from company directory.

### Authorization policies
- **Location:** `Program.cs` (lines 1841–1853).
- **Status:** OK.
- **Details:** `AdminOnly` → `RequireRole("Admin")`; `SupervisorOnly` → `RequireClaim("isSupervisor", "true")`; `SupervisorOrAdmin` → Admin or (Technician + isSupervisor). Smart scheme forwards JWT (Bearer or cookie) or Negotiate when Windows auth enabled.

---

## 2) Role resolution

### How roles are loaded from TikQ DB
- **Location:** `UserService.cs` (login path and `MapToDtoAsync`), `User` entity, `Technician` entity.
- **Status:** OK.
- **Details:** Login (TikQ-only or company directory) loads user by email from `_userRepository`, then `ResolveIsSupervisorAsync(userId)` from `Technician` table. Role is always `user.Role` (Users table). No role from company DB.

### Supervisor = Technician + isSupervisor
- **Location:** `LandingPathResolver.cs` (lines 21–36), `UserService.ResolveIsSupervisorAsync` (lines 318–322), `MapToDtoAsync` (lines 571–597).
- **Status:** OK.
- **Details:** `UserRole.Supervisor` (3) exists in enum and maps to `/supervisor` in resolver, but registration/assignment only use Client (0), Technician (1), Admin (2). Supervisor UX = Technician + `Technician.IsSupervisor`. Landing path computed as `GetLandingPath(role, isSupervisor)`.

### Admin, Technician, Client mapping
- **Status:** OK.
- **Details:** Backend enum: Client=0, Technician=1, Admin=2, Supervisor=3. Stored in `Users.Role`. Frontend `roleFromApi` maps 0→client, 1 and 3→technician, 2→admin; string "Supervisor"→technician. Consistent.

---

## 3) Frontend auth

### auth-context.tsx
- **Location:** `frontend/lib/auth-context.tsx`.
- **Status:** OK with one minor default (see below).
- **Details:** Session restored via `GET /api/auth/me` with `credentials: 'include'`. No token in localStorage (cookie-only). Login/register use `response.landingPath ?? response.user?.landingPath ?? getLandingPath(mapped)`. User persisted to localStorage for display only; auth is cookie + `/me`.

### roleFromApi logic
- **Location:** `frontend/lib/auth-context.tsx` (lines 53–80).
- **Status:** OK.
- **Details:** Handles number (0,1,2,3) and string (Admin, Technician, Client, Supervisor, engineer). `null`/`undefined` → `"client"`. Supervisor (3 or "supervisor") → `"technician"`; routing to `/supervisor` comes from `isSupervisor` / `landingPath`.

### landingPath calculation
- **Location:** `frontend/lib/auth-routing.ts`, `getLandingPath` / `getLandingPathFromSession`.
- **Status:** OK with one defensive point (see below).
- **Details:** Prefers `user.landingPath` from backend when valid; else admin→/admin, technician+isSupervisor→/supervisor, technician→/technician, default→/client. Root and RoleGuard use `getLandingPathFromSession({ user })` only when `user` is set; unauthenticated redirect to `/login` happens before that.

---

## 4) Routing

- **Client → /client:** `getLandingPath` returns `/client` for role client or unknown; RoleGuard `requiredPath="/client"` on `app/client/page.tsx`. OK.
- **Technician → /technician:** role technician and `!isSupervisor` → `/technician`; `app/technician/page.tsx` uses `RoleGuard requiredPath="/technician"`. OK.
- **Supervisor → /supervisor:** role technician and `isSupervisor` → `/supervisor`; `app/supervisor/page.tsx` uses `RoleGuard requiredPath="/supervisor"`. OK.
- **Admin → /admin:** role admin → `/admin`; `app/admin/page.tsx` uses `RoleGuard requiredPath="/admin"`. OK.
- Root `/` redirects to `getLandingPathFromSession({ user })` when authenticated, else `/login`. OK.

---

## 5) Failure checks

| Scenario | What happens |
|----------|----------------|
| **Role missing / invalid** | Backend: `HasValidRoleAndLandingPath` in WhoAmI/Me; invalid → 401 with `missing_role` or `isAuthenticated: false` and `landingPath: "/login"`. Frontend: 401 on `/me` → api-client clears storage and redirects to `/login` (and optionally `?error=missing_role`). Session cleared, user sent to login. OK. |
| **Token invalid / expired** | JWT validation fails → not authenticated. `/me` returns 401. Frontend: same as above; redirect to login. OK. |
| **User in company DB but not in TikQ DB** | With company directory **Enforce** mode: no TikQ user → `LoginResultKind.RoleNotAssigned` → 403, message "No TikQ role assigned for this account." Login fails; no cookie set. With **Friendly** mode: TikQ user created with Client role and login succeeds. OK. |

---

## 6) Output: problems, risks, exact fixes

### Problems found

1. **Unauthenticated fallback to `/client` in auth-routing (design smell)**  
   - **File:** `frontend/lib/auth-routing.ts`  
   - **Line:** 27, 47–48  
   - **Issue:** `getLandingPath(null)` and `getLandingPathFromSession({ user: null })` return `"/client"`. If any future code used this for redirect without checking `user` first, unauthenticated users could be sent to `/client`.  
   - **Current usage:** Root and RoleGuard always check `!user` and redirect to `/login`, so behavior is correct today.  
   - **Fix (optional, defensive):** Return a dedicated value for “no user” (e.g. `"/login"`) or a type like `LandingPath | "/login"` so callers cannot accidentally send unauthenticated users to a dashboard. Example change in `getLandingPath`: `if (!user) return "/login";` and in `getLandingPathFromSession`: `if (!session.user) return "/login";` (then adjust type/return to allow `"/login"` where used for redirects).

2. **401 redirect does not respect `silent` in api-client**  
   - **File:** `frontend/lib/api-client.ts`  
   - **Line:** 575–591  
   - **Issue:** On 401, redirect to `/login` runs regardless of `silent: true`. For `/me` with `silent: true` this is desired (we want redirect when session is invalid). So no functional bug, but if in future a “silent” 401 should not redirect (e.g. background session check), it would still redirect.  
   - **Fix:** Optional: only redirect on 401 when `!options.silent` (and keep current behavior for `/me` by not using `silent` for that call, or document that `silent` does not suppress 401 redirect).

3. **JWT Secret empty in appsettings**  
   - **File:** `backend/Ticketing.Backend/appsettings.json`  
   - **Issue:** `"Jwt":{"Secret":""}` in repo.  
   - **Status:** **Already mitigated.** `Program.cs` (lines 96–105) resolves secret from env/config and **throws** in non-Development if secret is empty; Development uses a fallback key. Production must set `JWT_SECRET` or `Jwt__Secret` (or other accepted keys).

### Risks before production

1. **JWT Secret:** Must be set in production (see above).  
2. **CORS:** `Cors:AllowedOrigins` includes localhost; production must list actual frontend origins and keep `AllowCredentials()` for cookie auth.  
3. **Cookie Secure:** Set only when `Request.IsHttps`; ensure production runs over HTTPS so the cookie is Secure.  
4. **Company directory Enforce vs Friendly:** Enforce = no TikQ user → 403 (no auto-creation). Friendly = auto-create Client. Confirm intended mode per environment.

### Exact fixes (file + line)

| # | File | Line(s) | Action |
|---|------|---------|--------|
| 1 (optional) | `frontend/lib/auth-routing.ts` | 27, 47–48 | For unauthenticated, return `"/login"` instead of `"/client"` in `getLandingPath` / `getLandingPathFromSession` and adjust types/usages so unauthenticated never get a dashboard path. |
| 2 (optional) | `frontend/lib/api-client.ts` | 575 | Add condition so 401 redirect runs only when `!silent` (if you want silent 401 to not redirect in future). |
| 3 | (N/A) | Program.cs 96–105 | JWT secret: production guard already in place; set JWT_SECRET or Jwt__Secret in production. |

---

## Summary

- **Backend:** Login, JWT/cookie, claims (role, isSupervisor), and authorization policies are consistent and correct. Roles and supervisor flag come only from TikQ DB; supervisor = Technician + IsSupervisor.
- **Frontend:** Auth context, roleFromApi, and landingPath logic align with backend. Routing (Client/Technician/Supervisor/Admin) and failure handling (missing role, invalid token, user in company DB but not TikQ) behave as intended.
- **Remaining:** One optional hardening in auth-routing (unauthenticated → `/login`), optional 401/silent behavior in api-client, and **mandatory** production setup for JWT Secret and CORS.

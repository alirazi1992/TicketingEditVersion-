# Production IIS Login + Whoami and Frontend Redirect Report

**Date:** 2026-02-21  
**Backend:** Production IIS at `http://localhost:8080`  
**Task:** Collect real login/whoami outputs for technician and supervisor; identify frontend redirect path and code locations.

---

## A) Backend (PowerShell – real outputs)

### 1) tech@local / Tech123!

- **POST** `http://localhost:8080/api/auth/login`  
  Body: `{"email":"tech@local","password":"Tech123!"}`  
  Cookies saved to: **`cookies-tech.txt`** (project root)

**Login response (full JSON):**
```json
{
  "ok": true,
  "role": "Technician",
  "isSupervisor": false,
  "landingPath": "/technician",
  "user": {
    "id": "f568383d-5e2a-4436-914c-bac53760c5e9",
    "fullName": "Bootstrap Technician",
    "email": "tech@local",
    "role": "Technician",
    "phoneNumber": null,
    "department": null,
    "avatarUrl": null,
    "isSupervisor": false,
    "landingPath": "/technician"
  }
}
```

**GET** `http://localhost:8080/api/auth/whoami` (with same session cookies):

**Whoami (tech) full JSON:**
```json
{
  "isAuthenticated": true,
  "email": "tech@local",
  "role": "Technician",
  "isSupervisor": false,
  "landingPath": "/technician"
}
```

---

### 2) supervisor@local / Tech123!

- **POST** `http://localhost:8080/api/auth/login`  
  Body: `{"email":"supervisor@local","password":"Tech123!"}`  
  Cookies saved to: **`cookies-super.txt`** (project root)

**Login response (full JSON):**
```json
{
  "ok": true,
  "role": "Technician",
  "isSupervisor": true,
  "landingPath": "/supervisor",
  "user": {
    "id": "5b4ef949-dc31-4dce-acd5-03b71984aecf",
    "fullName": "Bootstrap Supervisor",
    "email": "supervisor@local",
    "role": "Technician",
    "phoneNumber": null,
    "department": null,
    "avatarUrl": null,
    "isSupervisor": true,
    "landingPath": "/supervisor"
  }
}
```

**GET** `http://localhost:8080/api/auth/whoami` (with same session cookies):

**Whoami (supervisor) full JSON:**
```json
{
  "isAuthenticated": true,
  "email": "supervisor@local",
  "role": "Technician",
  "isSupervisor": true,
  "landingPath": "/supervisor"
}
```

---

## B) Frontend

### 1) Where frontend runs and `NEXT_PUBLIC_API_BASE_URL`

- **Dev:** `npm run dev` → **port 3001** (`next dev -p 3001` in `frontend/package.json`).
- **Prod (next start):** Default port **3000** unless `PORT` is set.
- **Runtime API URL:**  
  - Read from `process.env.NEXT_PUBLIC_API_BASE_URL`.  
  - In repo: `frontend/.env.local` has `NEXT_PUBLIC_API_BASE_URL=http://localhost:5000`.  
  - So with default env, frontend URL is **http://localhost:3001** (dev) or **http://localhost:3000** (start); API base is **http://localhost:5000**.  
  - To hit Production IIS (port 8080), set `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080` (e.g. in `.env.local`) and restart the frontend.

### 2) Expected vs actual redirect paths after login

- **tech@local**  
  - Backend returns `landingPath: "/technician"`.  
  - **Expected redirect:** `/technician`.  
  - **Actual (from code):** Frontend uses `response.landingPath` and does `router.replace(landingPath)`, so redirect is **/technician**.

- **supervisor@local**  
  - Backend returns `landingPath: "/supervisor"`.  
  - **Expected redirect:** `/supervisor`.  
  - **Actual (from code):** Same flow → redirect is **/supervisor**.

To *observe* these in the browser: run frontend (e.g. `http://localhost:3001`) with `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080`, open DevTools Network tab, log in as tech then supervisor, and confirm the post-login URL is `/technician` and `/supervisor` respectively.

### 3) Code locations for `landingPath` and redirects

| Responsibility | File | Location (concept) |
|----------------|------|--------------------|
| **Landing path computation** | `frontend/lib/auth-routing.ts` | `getLandingPath(user)`: uses `user.landingPath` from backend when valid; else `role` + `isSupervisor` → Admin → `/admin`, technician → `/technician`, supervisor → `/supervisor`, client → `/client`. `getLandingPathFromSession(session)` uses session user and same rules. |
| **Login returns path** | `frontend/lib/auth-context.tsx` | `login()`: calls `POST /api/auth/login`, then `landingPath = response.landingPath ?? response.user?.landingPath ?? getLandingPath(mapped)` and **returns** that string (lines ~182–190). |
| **Redirect after login** | `frontend/components/login-dialog.tsx` | `handleLogin`: `const landingPath = await login(...)` then `router.replace(landingPath)` (lines ~77, 84). |
| **Redirect after login** | `frontend/app/login/page.tsx` | `onSubmit`: `const landingPath = await login(...)` then `router.replace(landingPath)` (lines ~33–34). |
| **Root "/" redirect** | `frontend/app/page.tsx` | If authenticated: `landingPath = getLandingPathFromSession({ user })`, then `router.replace(landingPath)`. If not: `router.replace("/login")` (lines ~16–23). |
| **Route guard redirect** | `frontend/components/role-guard.tsx` | Uses `getLandingPathFromSession({ user })`. If `landingPath !== requiredPath`, redirects to `landingPath`; if no user, redirects to `/login` (lines ~21–30, 44–46). |
| **API base URL** | `frontend/lib/api-client.ts` | `getBackendUrlCandidates()` / `detectApiBaseUrl()`: use `process.env.NEXT_PUBLIC_API_BASE_URL` or default `http://localhost:5000` (lines ~99–141). |

---

## Summary

| Item | Value |
|------|--------|
| **Whoami (tech)** | `{"isAuthenticated":true,"email":"tech@local","role":"Technician","isSupervisor":false,"landingPath":"/technician"}` |
| **Whoami (supervisor)** | `{"isAuthenticated":true,"email":"supervisor@local","role":"Technician","isSupervisor":true,"landingPath":"/supervisor"}` |
| **Cookies (tech)** | `cookies-tech.txt` |
| **Cookies (supervisor)** | `cookies-super.txt` |
| **Frontend URL (dev)** | http://localhost:3001 |
| **Frontend URL (start)** | http://localhost:3000 (default) |
| **NEXT_PUBLIC_API_BASE_URL (repo default)** | http://localhost:5000 (in `.env.local`); use http://localhost:8080 for IIS. |
| **Redirect tech@local** | `/technician` (from backend `landingPath` + `router.replace` in login and "/" page). |
| **Redirect supervisor@local** | `/supervisor` (same). |
| **Redirect decision** | Backend provides `landingPath` in login and whoami; frontend uses it in `auth-context.tsx` → `login-dialog.tsx` / `login/page.tsx` and in `page.tsx` / `role-guard.tsx` via `getLandingPathFromSession()` from `auth-routing.ts`. |

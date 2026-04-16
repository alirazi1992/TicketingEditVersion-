# Windows Authentication and IIS Configuration

TikQ supports three **WindowsAuth** modes. How IT should configure **IIS Authentication** (Anonymous vs Windows Authentication) depends on the chosen mode.

## App configuration: `WindowsAuth`

| Option | Type | Description |
|--------|------|-------------|
| **WindowsAuth:Enabled** | bool | When `true` and **Mode** is not set, effective mode is **Optional**. When `false` and Mode not set, effective mode is **Off**. |
| **WindowsAuth:Mode** | string | `"Off"` \| `"Optional"` \| `"Enforce"`. Overrides Enabled when set. |

- **Off**: App never attempts Windows auth. `/api/auth/windows` returns **403** with a clear message. Email/password login only.
- **Optional**: If the request has a Windows identity (IIS passed Negotiate), `/api/auth/windows` can issue a JWT cookie; otherwise returns **401** with `WWW-Authenticate: Negotiate`. Email/password login always works.
- **Enforce**: Same as Optional for Windows, plus **all routes except** `/api/health` and `/api/auth/*` require authentication (JWT or Windows). Unauthenticated requests to protected routes get **401** with `WWW-Authenticate: Bearer, Negotiate`.

Email/password login is **never** disabled: `/api/auth/login`, `/api/auth/register`, and the rest of `/api/auth/*` are always available regardless of mode.

---

## How IT should configure IIS for each mode

### Mode: **Off**

| IIS setting | Recommendation |
|------------|----------------|
| **Anonymous Authentication** | **Enabled** (so all traffic can reach the app; auth is handled by the app via JWT only). |
| **Windows Authentication** | **Disabled**. Not used; keeps configuration simple and avoids unnecessary 401 challenges. |

No Windows identity is sent to the app. Users sign in with email/password only.

---

### Mode: **Optional**

| IIS setting | Recommendation |
|------------|----------------|
| **Anonymous Authentication** | **Enabled** (so unauthenticated users can hit login, register, health, and the app can choose to use Windows when present). |
| **Windows Authentication** | **Enabled** (Negotiate/NTLM). Providers: **Negotiate** and optionally **NTLM**. |

- Browsers that send Windows credentials (e.g. intranet, or user prompted by 401) will get a Windows identity; the app can then issue a JWT via `GET/POST /api/auth/windows`.
- Browsers that do not send Windows credentials still get through (Anonymous) and can use email/password login.
- Order in IIS: **Anonymous** first, **Windows** second is typical so that Windows can run when the app returns 401 with `WWW-Authenticate: Negotiate` for `/api/auth/windows`.

---

### Mode: **Enforce**

| IIS setting | Recommendation |
|------------|----------------|
| **Anonymous Authentication** | **Enabled** only for paths that must be reachable without auth: in practice the app allowlists `/api/health` and `/api/auth/*`, so Anonymous is still needed for login, register, whoami, and health. |
| **Windows Authentication** | **Enabled** (Negotiate/NTLM). |

- Same as Optional for IIS: Anonymous + Windows both enabled so that:
  - Health and auth endpoints are reachable without a Windows challenge.
  - Other API routes require authentication; the app returns 401 for unauthenticated requests. Browsers can then respond to `WWW-Authenticate: Negotiate` with Windows credentials, or the client can use a JWT from email/password or from a prior `/api/auth/windows` call.
- If you lock down IIS so that **Anonymous** is disabled for the whole app, only Windows-authenticated users could reach the app at all, including login; that would break email/password login. So keep **Anonymous** enabled and let the app enforce auth on non-allowlisted routes.

---

## Summary table

| Mode     | Anonymous (IIS) | Windows (IIS) | App behavior |
|----------|------------------|---------------|--------------|
| **Off**  | Enabled          | Disabled      | JWT only; Windows endpoint 403. |
| **Optional** | Enabled     | Enabled       | JWT or Windows; `/api/auth/windows` 401 with Negotiate when no Windows identity. |
| **Enforce**  | Enabled     | Enabled       | Same as Optional; non-auth routes require JWT or Windows. |

---

## Config examples

**Off (default):**

```json
"WindowsAuth": {
  "Enabled": false
}
```

Or explicitly:

```json
"WindowsAuth": {
  "Enabled": false,
  "Mode": "Off"
}
```

**Optional (Windows SSO when available):**

```json
"WindowsAuth": {
  "Enabled": true,
  "Mode": "Optional"
}
```

**Enforce (all non-auth routes require login):**

```json
"WindowsAuth": {
  "Enabled": true,
  "Mode": "Enforce"
}
```

Environment variables (e.g. IIS App Pool):

- `WindowsAuth__Enabled` = `true` | `false`
- `WindowsAuth__Mode` = `Off` | `Optional` | `Enforce`

---

## Health check

**GET /api/health** includes:

- **auth.windowsAuthEnabled**: `true` when mode is Optional or Enforce, `false` when Off.
- **auth.windowsAuthMode**: `"Off"` | `"Optional"` | `"Enforce"`.

Use this to confirm the effective mode in production without exposing secrets.

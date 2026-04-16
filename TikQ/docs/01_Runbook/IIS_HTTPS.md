# Cookie auth behind IIS (HTTPS / reverse proxy)

When the TikQ backend runs behind **IIS** (or another reverse proxy) with HTTPS terminated at the proxy, configure the following so cookie auth and `Request.IsHttps` behave correctly.

## Forwarded headers

The app is already configured to:

- Use **X-Forwarded-Proto** and **X-Forwarded-For**
- Clear **KnownNetworks** and **KnownProxies** so the proxy (e.g. IIS) is trusted

No extra middleware is required. Ensure IIS passes the standard forwarded headers to the app (ARR or your reverse proxy config).

## Auth cookie options

Configure **AuthCookies** so cookies use the right Secure and SameSite behavior:

| Option | Values | Notes |
|--------|--------|--------|
| **AuthCookies:SameSite** | `Lax` (default), `Strict`, `None` | Use `None` only when cross-site requests must send the cookie (e.g. cross-origin frontend). |
| **AuthCookies:SecurePolicy** | `SameAsRequest` (default), `Always` | Behind HTTPS: set to **Always** so the cookie is always `Secure`. |

**Example (appsettings.Production.json or environment):**

```json
"AuthCookies": {
  "SameSite": "Lax",
  "SecurePolicy": "Always"
}
```

Environment variables (e.g. IIS Application Pool):

- `AuthCookies__SameSite` = `Lax` | `Strict` | `None`
- `AuthCookies__SecurePolicy` = `SameAsRequest` | `Always`

Cookies are always set with **HttpOnly**, **Path="/"**, and the configured SameSite and Secure behavior.

## Diagnostic

**GET /api/auth/whoami** returns a response header:

- **X-Auth-Cookie-Present**: `true` or `false` (indicates whether the auth cookie was sent; safe, no token value).

Use this to confirm the browser is sending the cookie when debugging proxy or SameSite issues.

## Verification gate (cookie auth behind IIS)

1. **After login:** Response must include **Set-Cookie** (cookie name `tikq_access`).  
   Run **verify-login.ps1**; it checks for Set-Cookie and reports `[COOKIE] Set-Cookie present (tikq_access)` or a warning if missing.
2. **Subsequent whoami:** Same session (cookie sent) must return **isAuthenticated = true** and **X-Auth-Cookie-Present: true**.

If either fails:

| Cause | What to check | Fix |
|-------|----------------|-----|
| **SameSite / Secure mismatch** | Cookie not sent on next request (X-Auth-Cookie-Present: false) or browser rejects cookie. | For same-site HTTPS: **AuthCookies:SameSite** = `Lax`, **AuthCookies:SecurePolicy** = `Always`. For cross-site: SameSite = `None` and SecurePolicy = `Always` (and ensure HTTPS). |
| **HTTPS forcing** | App thinks request is HTTP so cookie is set without Secure; browser drops it on HTTPS. | Ensure **X-Forwarded-Proto: https** is sent by IIS (ARR or URL Rewrite). Use **AuthCookies:SecurePolicy** = `Always` when behind HTTPS. |
| **Forwarded headers missing** | Request.IsHttps is false behind IIS HTTPS. | Configure IIS to send **X-Forwarded-Proto** and **X-Forwarded-For** (Application Request Routing or URL Rewrite outbound rule). Restart app pool after config change. |

**Defaults for IIS HTTPS binding:** In Production, set **AuthCookies:SecurePolicy** = `Always` and **AuthCookies:SameSite** = `Lax` (or `None` only if you need cross-site cookie). appsettings.Production.json already sets SecurePolicy to Always.

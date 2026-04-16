# Bootstrap User Seeding (First Run Only)

Bootstrap creates initial user accounts when the **Users** table is empty. It is intended for **first deployment only** (e.g. SQL Server Production). There are **no API endpoints** for seeding; bootstrap runs only at application startup.

## When It Runs

- **Production**: Bootstrap runs only when **explicitly enabled** and the Users table is empty.
  - If `Bootstrap:Enabled` is not set in Production, it defaults to **false**.
  - If enabled and users already exist, startup logs **"seed skipped"** and does nothing.
- **Development**: Full dev seed (test users, categories, tickets) runs via `SeedData.InitializeAsync` when `EnableDevSeeding` is true or in Development; bootstrap is not used there.

## Enabling Bootstrap for First Run (Production)

Set these **environment variables** (or equivalent config) **only for the first run** on an empty database. After the first startup, you can remove or set `Bootstrap__Enabled=false` so subsequent restarts do not depend on bootstrap.

### Required to enable and create admin

| Variable | Description |
|----------|-------------|
| `Bootstrap__Enabled` | Set to `true` to allow bootstrap when Users table is empty. **Default in Production is false** if not set. |
| `Bootstrap__AdminEmail` | Email for the bootstrap admin account (e.g. `admin@yourcompany.local`). |
| `Bootstrap__AdminPassword` | Password for the admin account. **Must be at least 8 characters.** Set via environment only; do not commit. |

### Optional (recommended only for first-run testing)

| Variable | Description |
|----------|-------------|
| `Bootstrap__AdminFullName` | Display name (default: "Bootstrap Admin"). |
| `Bootstrap__TestClientEmail` | Optional test client email. |
| `Bootstrap__TestClientPassword` | Optional test client password (min 8 chars). |
| `Bootstrap__TestTechEmail` | Optional test technician email. |
| `Bootstrap__TestTechPassword` | Optional test technician password (min 8 chars). |
| `Bootstrap__TestSupervisorEmail` | Optional test supervisor email. |
| `Bootstrap__TestSupervisorPassword` | Optional test supervisor password (min 8 chars). |

### Example (first run only)

```bash
# Minimum to create one admin on first run
Bootstrap__Enabled=true
Bootstrap__AdminEmail=admin@yourcompany.local
Bootstrap__AdminPassword=YourSecurePasswordMin8Chars
```

After first successful startup, either:

- Set `Bootstrap__Enabled=false`, or  
- Omit `Bootstrap__Enabled` (Production defaults it to false).

No secrets (passwords, emails) are exposed by the health endpoint; it only reports `usersCount` and `hasData`.

## Health Endpoint

`GET /api/health` and `GET /health` return:

- **usersCount** (or **dataCounts.users**) – number of users in the database.
- **hasData** – `true` when there is at least one user or category.

No passwords or other secrets are included in the health response.

## Verification (verify-login.ps1)

After a first run with bootstrap enabled, confirm that seeding and login work:

```powershell
cd tools\_handoff_tests
.\verify-login.ps1 -BaseUrl "http://localhost:8080" -LoginEmail "admin@local" -LoginPassword "YourBootstrapPassword"
```

Or use seed credentials from environment (passwords are never logged):

```powershell
$env:TikQ_LOGIN_EMAIL = "admin@local"
$env:TikQ_LOGIN_PASSWORD = "YourBootstrapPassword"
.\verify-login.ps1 -BaseUrl "http://localhost:8080"
```

The script **POST**s to `/api/auth/login` with the credentials, then **GET**s `/api/auth/whoami` with the session cookie and asserts **isAuthenticated** = true. **PASS** = exit 0; **FAIL** = exit 1.

**GATE:** In a fresh DB, enabling bootstrap (Bootstrap__Enabled=true and admin password set via deploy-iis.ps1 or IIS env) must result in **users > 0** (check `/api/health` → `database.dataCounts.users`) and **verify-login.ps1 PASS**.

### If login verification fails

| Cause | What to check | Fix |
|-------|----------------|-----|
| Env vars not reaching IIS | App reads Bootstrap from config; env vars must be in web.config or Application Pool. | Run deploy-iis.ps1 with TikQ_BOOTSTRAP_ADMIN_PASSWORD (and optional TikQ_BOOTSTRAP_*); confirm web.config has Bootstrap__Enabled, Bootstrap__AdminEmail, Bootstrap__AdminPassword. |
| Bootstrap not wired | Seed skipped (users already exist, or Enabled false, or AdminPassword missing/short). | Check startup logs for "[BOOTSTRAP] Seed skipped: ..." or "Seed applied: N user(s)."; ensure Bootstrap:Enabled true and AdminPassword ≥ 8 chars. |
| Password validation | Login returns 401 Invalid email or password. | Ensure password matches what was injected at deploy; Identity password hasher is used (no plain text). |
| DB context mismatch | Health shows users=0 after bootstrap. | Same DB and provider (SqlServer/Sqlite) for bootstrap and API; ensure migrations applied before bootstrap runs. |

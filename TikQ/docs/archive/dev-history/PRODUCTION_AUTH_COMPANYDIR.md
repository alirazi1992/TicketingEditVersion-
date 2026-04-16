# Production Auth & Company Directory — Runbook

This document describes how TikQ uses the **Company Directory** (read-only org DB) for identity and the **TikQ DB** for roles, plus how to assign roles after go-live.

---

## 1. CompanyDirectory configuration

Configure in `appsettings.json` (or environment / `appsettings.Production.json`):

| Setting | Description |
|--------|--------------|
| **Enabled** | `true` = validate login against Company DB; `false` = TikQ DB only. |
| **ConnectionString** | SQL Server connection string to the **Company DB**. Used only for **SELECT**; no writes or migrations. |
| **Mode** | `"Enforce"` (default) or `"Friendly"`. See [Mode handling](#4-mode-handling) below. |

Example:

```json
"CompanyDirectory": {
  "Enabled": true,
  "ConnectionString": "Server=.;Database=CompanyDb;User Id=...;Password=...;",
  "Mode": "Enforce"
}
```

---

## 2. Read-only guarantee (Company DB)

- TikQ uses the Company DB **only for identity**: lookup by email, full name, active/disabled, and **password verification**.
- All queries against the Company DB are **SELECT only**. No INSERT/UPDATE/DELETE, no schema changes, no migrations.
- **Roles and `isSupervisor`** come **only from the TikQ DB** (Users table, Technicians table). The Company Directory must not provide or override roles.

---

## 3. How org login works vs role mapping

1. **Login** (`POST /api/auth/login`):  
   - When CompanyDirectory is enabled, credentials are validated against the **Company DB** (read-only).  
   - If valid, TikQ then looks up the **TikQ user** by email to get **Role** and **isSupervisor**.  
   - TikQ **does not store** org passwords; only identity is read from the Company DB.

2. **Role mapping**:  
   - Roles are stored only in the **TikQ DB** (Users.Role, and Technicians.IsSupervisor for technicians).  
   - Until a user has a role assigned in TikQ, login can fail with `403 ROLE_NOT_ASSIGNED` when Mode is `Enforce`.  
   - After go-live, use the **Admin-only role assignment endpoint** to assign roles (see below).

---

## 4. Mode handling

| Mode | When TikQ user is missing / has no role |
|------|----------------------------------------|
| **Enforce** (default) | Login returns **403** with `error: "ROLE_NOT_ASSIGNED"`. Safe for production. |
| **Friendly** | A minimal TikQ user is created with role **Client** and **no password stored**; login succeeds. |

- **Default is Enforce** for production safety.  
- Empty or missing `Mode` is treated as **Enforce**.

---

## 5. Assigning roles after go-live

Only **Admin** users can assign roles. Use the admin role-assignment endpoint; it writes **only** to the TikQ DB (Users and Technicians).

1. Log in as an Admin (see [Login](#curl-examples) below).
2. Call **POST /api/admin/roles/assign** with the user’s email and desired role (and optionally `isSupervisor` for technicians).
3. If the user does not exist in the TikQ DB, a **minimal TikQ user** is created (no password).  
4. For role **Technician**, a Technician record is created or updated and `isSupervisor` can be set.

---

## 6. Curl examples

Base URL is assumed to be `http://localhost:5000` (or your backend URL). Use `-c cookies.txt` and `-b cookies.txt` to keep the session cookie.

### Login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"admin@company.com\",\"password\":\"YourPassword\"}" \
  -c cookies.txt -v
```

Success: `200` with body containing `ok: true`, `role`, `isSupervisor`, `landingPath`, `user`, and `Set-Cookie: tikq_access=...`.

### WhoAmI (session restore)

```bash
curl -b cookies.txt http://localhost:5000/api/auth/whoami
```

Returns `isAuthenticated`, `email`, `role`, `isSupervisor`, `landingPath` (or `isAuthenticated: false` if not logged in).

### Role assign (Admin only)

Assign role for an org user (creates minimal TikQ user if missing). Replace `YOUR_JWT` with the Admin token, or use the cookie from login.

```bash
# Using cookie (after login with -c cookies.txt)
curl -X POST http://localhost:5000/api/admin/roles/assign \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d "{\"email\":\"user@company.com\",\"role\":0}"
```

Roles: `0` = Client, `1` = Technician, `2` = Admin. For Technician you can set `isSupervisor`:

```bash
curl -X POST http://localhost:5000/api/admin/roles/assign \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d "{\"email\":\"tech@company.com\",\"role\":1,\"isSupervisor\":true}"
```

Success: `200` with `email`, `role`, `isSupervisor`, `landingPath`.

### Role query (Admin only)

Get current TikQ role and isSupervisor by email:

```bash
curl -b cookies.txt "http://localhost:5000/api/admin/roles/by-email?email=user@company.com"
```

Success: `200` with `email`, `role`, `isSupervisor`. If no TikQ user: `404`.

---

## 7. Quick reference

| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/auth/login` | POST | None | Login (Company DB or TikQ DB). |
| `/api/auth/whoami` | GET | Cookie optional | Session restore. |
| `/api/admin/roles/assign` | POST | Admin | Assign role (TikQ DB only). |
| `/api/admin/roles/by-email?email=` | GET | Admin | Query role mapping. |

Errors:

- **403 ROLE_NOT_ASSIGNED**: CompanyDirectory enabled, Mode = Enforce, and no TikQ role for this user. Assign a role via `/api/admin/roles/assign`.
- **403 USER_DISABLED**: User disabled or inactive in Company DB.
- **401 INVALID_CREDENTIALS**: Wrong password or user not in Company DB (when enabled).

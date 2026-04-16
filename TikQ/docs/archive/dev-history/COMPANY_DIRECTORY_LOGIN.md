# Company Directory (Production) Login

When `CompanyDirectory:Enabled` is `true` in `appsettings.json`, `/api/auth/login` validates credentials against the read-only Company DB (SQL Server). Role and `isSupervisor` are resolved from the TikQ DB only; TikQ does not store org passwords.

## Testing with curl (after enabling CompanyDirectory)

1. Set `CompanyDirectory:Enabled` to `true` and set `CompanyDirectory:ConnectionString` to your Company DB (read-only).
2. Ensure the same email exists in the Company DB and in TikQ Users with a role assigned (no auto-creation when CompanyDirectory is enabled).

**Login (success):**
```bash
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"user@company.com\",\"password\":\"YourPassword\"}" -c cookies.txt -v
```
Expect `200` with `{"ok":true,"role":"...","isSupervisor":...,"landingPath":"...","user":{...}}` and `tikq_access` cookie.

**Login (invalid credentials – user missing or wrong password):**
```bash
curl -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d "{\"email\":\"nobody@company.com\",\"password\":\"wrong\"}" -v
```
Expect `401` with `{"message":"Invalid email or password.","error":"INVALID_CREDENTIALS"}`.

**Login (user disabled in Company DB):**
Expect `403` with `{"error":"USER_DISABLED"}`.

**Login (user in Company DB but no TikQ user / role not assigned):**
Expect `403` with `{"error":"ROLE_NOT_ASSIGNED"}`.

**Session restore:**
```bash
curl -b cookies.txt http://localhost:5000/api/auth/whoami
```

Logs use the tag `[COMPANY_DIR]` (e.g. `user_found`, `user_missing`, `disabled`, `password_invalid`, `login_success`). Passwords and hashes are never logged.

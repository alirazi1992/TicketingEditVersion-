# Supervisor Technicians – Cookie Auth Verification

Verification uses **cookie auth only** (`tikq_access` HttpOnly). Use `curl -c` to save cookies on login and `-b` to send them. No Bearer tokens.

## Prerequisites

- Backend running in **Development** at `http://localhost:5000`.
- In Development, `SupervisorTechnicians:Mode` defaults to **AllByDefault** if missing; ensure startup log shows `[SUPERVISOR_MODE] ... ModeResolved=AllByDefault`.

## 1. Confirm config (Development, Admin cookie)

Login as admin, then call the debug endpoint (cookie only):

```bash
set base=http://localhost:5000
curl.exe -s -c c-admin.txt -X POST -H "Content-Type: application/json" --data-binary "{\"email\":\"admin@test.com\",\"password\":\"Admin123!\"}" "%base%/api/auth/login"
curl.exe -s -b c-admin.txt "%base%/api/debug/config/supervisor-mode"
```

**Expected:** HTTP 200 and JSON with `"environmentName": "Development"`, `"modeResolved": "AllByDefault"`. If not, fix backend config or environment.

---

## 2. techsuper@email.com (cookie only)

```bash
set base=http://localhost:5000
curl.exe -i -c c-techsuper.txt "%base%/api/auth/login" -H "Content-Type: application/json" --data-binary "{\"email\":\"techsuper@email.com\",\"password\":\"Test123!\"}"
curl.exe -s -b c-techsuper.txt "%base%/api/supervisor/technicians"
curl.exe -s -b c-techsuper.txt "%base%/api/supervisor/technicians/available"
```

**Expected:**

- Login: HTTP 200 and `Set-Cookie: tikq_access=...`.
- `GET /api/supervisor/technicians`: HTTP 200 and **non-empty JSON array** (length > 0). Example: `[{"technicianUserId":"...","technicianName":"Tech One",...}, ...]`.
- `GET /api/supervisor/technicians/available`: HTTP 200 and **non-empty JSON array** (length > 0).

**Check length (PowerShell):**

```powershell
$base = "http://localhost:5000"
# login techsuper
curl.exe -s -c c-techsuper.txt "$base/api/auth/login" -H "Content-Type: application/json" --data-binary '{\"email\":\"techsuper@email.com\",\"password\":\"Test123!\"}'
$r = curl.exe -s -b c-techsuper.txt "$base/api/supervisor/technicians"
# Must be non-empty (e.g. at least [{"technicianUserId":...}])
if ($r.Length -lt 10) { Write-Host "FAIL: technicians response too short" } else { Write-Host "OK: technicians length" $r.Length }
$r2 = curl.exe -s -b c-techsuper.txt "$base/api/supervisor/technicians/available"
if ($r2.Length -lt 10) { Write-Host "FAIL: available response too short" } else { Write-Host "OK: available length" $r2.Length }
```

---

## 3. supervisor@test.com (cookie only)

```bash
set base=http://localhost:5000
curl.exe -i -c c-supervisor.txt "%base%/api/auth/login" -H "Content-Type: application/json" --data-binary "{\"email\":\"supervisor@test.com\",\"password\":\"Test123!\"}"
curl.exe -s -b c-supervisor.txt "%base%/api/supervisor/technicians"
curl.exe -s -b c-supervisor.txt "%base%/api/supervisor/technicians/available"
```

**Expected:** Same as techsuper: HTTP 200 and **non-empty JSON arrays** for both endpoints (length > 0).

---

## Expected JSON shape (non-empty)

- **GET /api/supervisor/technicians:** Array of objects with at least `technicianUserId`, `technicianName`, `inboxTotal`, `inboxLeft`, `workloadPercent`. Length ≥ 1 (e.g. tech1, tech2).
- **GET /api/supervisor/technicians/available:** Array of objects with at least `id`, `userId`, `fullName`, `email`. Length ≥ 1.

If you see `[]`:

1. Call `/api/debug/config/supervisor-mode` (as admin with cookie): confirm `environmentName` is Development and `modeResolved` is AllByDefault.
2. Confirm backend startup log shows `[SUPERVISOR_MODE] ... ModeResolved=AllByDefault`.
3. Confirm cookie is sent: use same host and `-b` with the cookie file from login.

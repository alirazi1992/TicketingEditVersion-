# TikQ Handoff Readiness Checklist

Use this checklist before organizational source-code handoff and before deploying to production (intranet + Boss DB).

---

## 1. Production config (fail-fast)

- [ ] **JWT secret** set in production (e.g. `Jwt:Secret` or `JWT_SECRET`); no dev default.
- [ ] **Company Directory** (if enabled): `CompanyDirectory:ConnectionString` and `CompanyDirectory:Mode` (Enforce/Optional/Friendly) set and valid.
- [ ] **Main app DB**: In Production, using SQL Server (or intended DB); SQLite rejected unless `AllowSqliteInProduction=true`.
- [ ] Startup log shows: `[HANDOFF] Production validation passed` when running in Production or with `ProductionHandoffMode=true`.

## 2. Bootstrap & seeding

- [ ] **Bootstrap admin**: In Production/HandoffMode, no default password (`Admin123!`); `BootstrapAdmin:Password` set and ≥ 8 characters when DB has no users.
- [ ] **Dev seeding** (`Test123!` users): Not run in Production (default); only in Development or when `EnableDevSeeding=true`.

## 3. Auth fail-safe

- [ ] **Backend**: WhoAmI/Me return `authError=missing_role` or 401 when role/landingPath missing or invalid; no silent fallback to Client.
- [ ] **Frontend**: On 401 with `error=missing_role`, redirect to `/login?error=missing_role`; no default to client role.

## 4. Company/Boss DB read-only

- [ ] **SqlServerCompanyUserDirectory**: Only SELECT used; no INSERT/UPDATE/DELETE/MERGE/CREATE/ALTER/DROP in command text.
- [ ] Log shows `[CompanyDirectory] Read-only enforced` when Company Directory is used.

## 5. Dev surfaces disabled in production

- [ ] **Debug/maintenance** return 404 in Production or when `ProductionHandoffMode=true`: `/api/debug`, `/api/admin/cleanup`, `/api/auth/diag`, `/api/auth/debug-users`.
- [ ] **Health**: `/api/health` and `/health` remain unauthenticated for monitoring.

## 6. Documentation

- [ ] **docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md** present and reviewed (env vars, DB usage, roles, Company DB read-only, ProductionHandoffMode, common failures).

## 7. Documentation and comment cleanup

- [ ] No development or tool-specific trace comments remain in delivered documentation or code comments.

## 8. Pre-handoff verification

- [ ] App starts in **Development** without production secrets.
- [ ] App fails startup in **Production** (or with `ProductionHandoffMode=true`) when JWT secret is missing.
- [ ] App fails startup in **Production** when CompanyDirectory is enabled but ConnectionString is empty.
- [ ] App fails startup in **Production** when using SQLite and `AllowSqliteInProduction` is not true.
- [ ] Optional: Run backend tests if present; no regressions from hardening changes.

---

**Sign-off**: _________________ Date: __________

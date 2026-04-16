# Handoff hardening – diff-style summary of files changed

Minimal, targeted guards for production handoff. No architecture redesign, no unrelated refactors, no new features beyond safety.

---

## Backend (Ticketing.Backend)

### New file

| File | Why |
|------|-----|
| `Infrastructure/StartupValidation.cs` | Centralized production validation: JWT secret required; CompanyDirectory (ConnectionString, Mode) when enabled; reject SQLite in Production unless `AllowSqliteInProduction=true`. Logs `[HANDOFF] Production validation passed`. |

### Modified

| File | Change | Why |
|------|--------|-----|
| `Program.cs` | Run `StartupValidation.ValidateProductionConfig` after JWT resolution when Production or `ProductionHandoffMode`. | Fail fast on missing production config. |
| `Program.cs` | `BootstrapAdminOnceIfNoUsersAsync`: new parameter `requireStrongBootstrapPassword`; when true, require `BootstrapAdmin:Password` set and ≥ 8 chars, no fallback to `Admin123!`; throw if missing when no users. | Prevent default admin password in production. |
| `Program.cs` | Seed block: run `SeedData`/technician sync only when `IsDevelopment() \|\| config.GetValue<bool>("EnableDevSeeding")`. | Dev seeding (Test123!) not run in Production by default. |
| `Program.cs` | Company directory registration: resolve `ILogger<SqlServerCompanyUserDirectory>` and pass to constructor. | Enable read-only log. |
| `Program.cs` | DebugBlocker middleware: also when `ProductionHandoffMode`; block `/api/admin/cleanup`, `/api/auth/diag` in addition to `/api/debug` and `/api/auth/debug-users`. | Disable dev/maintenance surfaces in production/handoff. |
| `Api/Controllers/AuthController.cs` | Add `HasValidRoleAndLandingPath(role, landingPath)`; WhoAmI (JWT and Windows path) return `isAuthenticated=false`, `authError=missing_role` when role/landingPath invalid; Me() return 401 `error=missing_role` when invalid. | Auth fail-safe: no fallback to Client when role/landingPath missing. |
| `Infrastructure/CompanyDirectory/SqlServerCompanyUserDirectory.cs` | Optional `ILogger` ctor; `EnsureReadOnlyCommand(sql)` rejects INSERT/UPDATE/DELETE/MERGE/CREATE/ALTER/DROP/EXEC; log `[CompanyDirectory] Read-only enforced`. | Belt-and-suspenders read-only guarantee for Company DB. |

---

## Frontend

| File | Change | Why |
|------|--------|-----|
| `lib/api-client.ts` | On 401, read response body for `error` or `authError`; if `missing_role`, redirect to `/login?error=missing_role`. | Frontend fail-safe: redirect with error when session has missing role. |
| `app/login/page.tsx` | Use `useSearchParams()`; when `error=missing_role`, set error message "No valid role or landing path assigned. Please contact your administrator." | Show clear message for missing_role redirect. |

---

## Docs and config

| File | Change | Why |
|------|--------|-----|
| `docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md` | **New**. Required env/config (JWT, CompanyDirectory, DB, bootstrap, dev seeding), which DB is prod, roles in TikQ only, Company DB read-only, ProductionHandoffMode, common failure messages. | Delivery doc for deployers. |
| `docs/04_Handoff/HANDOFF_READINESS_CHECKLIST.md` | **New**. Checklist for production config, bootstrap, auth, Company DB read-only, dev surfaces, docs, AI trace, verification. | Handoff sign-off. |
| `docs/HANDOFF_DIFF_SUMMARY.md` | **New**. This file. | Diff-style summary of changes. |
| `.vscode/settings.json` | Comment updated to "Editor indexing". | Documentation cleanup (comment only). |

---

## Unchanged by design

- **Architecture**: No refactor of modules or layers.
- **Business workflows**: No change to ticket/category/technician flows.
- **Health**: `/api/health` and `/health` remain unauthenticated.
- **Other admin routes**: Only `/api/debug`, `/api/admin/cleanup`, `/api/auth/diag`, and `/api/auth/debug-users` are blocked in production; normal admin APIs (e.g. `/api/admin/technicians`, `/api/admin/reports`) unchanged.

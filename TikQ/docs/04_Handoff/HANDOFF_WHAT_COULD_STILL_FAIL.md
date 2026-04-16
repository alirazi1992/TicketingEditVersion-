# What could still fail in production (post-handoff)

This list covers risks that remain after the hardening work. It is for the receiving team to plan for.

---

## 1. Configuration and environment

- **Wrong environment name**: If `ASPNETCORE_ENVIRONMENT` is not set to `Production` on the prod server, production validation and SQLite rejection will not run. Ensure the host is configured with `Production` (or use `ProductionHandoffMode=true`).
- **Secrets in config files**: If JWT or DB secrets are committed in appsettings.Production.json or other tracked files, they can leak. Prefer environment variables or a secure vault.
- **Connection string format**: SQLite detection uses heuristics (e.g. "Data Source=" without "Initial Catalog"/"Database="). Unusual SQL Server connection strings could be misclassified; test with the actual prod connection string.

## 2. Database and migrations

- **Schema drift**: Schema guards and migrations assume a known schema. If the Boss/Company DB schema differs from what `SqlServerCompanyUserDirectory` expects (e.g. table/column names), identity lookup can fail. Validate against the real Company DB schema.
- **EF migrations on wrong DB**: Ensure migrations run only against the TikQ app DB, never against the Company/Boss DB. Current code does not use EF for Company DB; keep it that way.
- **SQLite in production**: If `AllowSqliteInProduction=true` is set to unblock deployment, SQLite remains unsuited for high concurrency or multi-instance deployments. Plan to move to SQL Server.

## 3. Auth and roles

- **Stale or malformed tokens**: Old JWTs without role/landingPath will get 401/missing_role and redirect to login. Users may need to log in again after deployment.
- **Windows / Company Directory**: If Company Directory is enabled but the Company DB is down or unreachable, login can fail. Consider resilience (timeouts, retries) and monitoring.
- **Role assignment**: Roles are only in the TikQ DB. If users exist in the Company DB but have no TikQ user or role (e.g. in Enforce mode), they will get ROLE_NOT_ASSIGNED. Ensure a process to provision TikQ users/roles.

## 4. Company/Boss DB read-only

- **Read-only guard is code-level only**: The guard rejects command text containing write/DDL keywords. It does not replace SQL permissions: the DB user for the Company DB should still have read-only rights at the database level.
- **New code paths**: Any future code that executes commands against the Company DB must use the same guard or equivalent (SELECT-only). New ADO.NET or helpers could bypass the current class.

## 5. Debug and maintenance

- **Other debug endpoints**: Only the listed routes are blocked (e.g. `/api/debug`, `/api/admin/cleanup`, `/api/auth/diag`). Any other debug or internal endpoints added later will be exposed unless explicitly blocked or removed.
- **Swagger in production**: Swagger is still enabled. If the app is reachable on the intranet, consider disabling Swagger in Production or restricting access (e.g. by IP or feature flag).

## 6. Operational

- **Health check dependency**: `/api/health` may depend on DB connectivity. If the DB is down, health can report degraded/unhealthy; load balancers might take the app out of rotation. Ensure DB and connection strings are stable.
- **Bootstrap admin only once**: Bootstrap admin is created only when the Users table is empty. If the first deploy fails after creating the admin, a retry might skip bootstrap; keep credentials safe and document recovery (e.g. manual user insert or env-based bootstrap).
- **Logging and PII**: Startup and app logs do not log passwords. Ensure no new logging of credentials or sensitive user data (e.g. Company DB PasswordHash) in future changes.

## 7. Frontend and network

- **CORS and base URL**: Frontend uses an API base URL (e.g. from env). If the production API URL is wrong or CORS is misconfigured, requests will fail. Verify `NEXT_PUBLIC_API_BASE_URL` (or equivalent) and CORS on the backend.
- **Cookie domain/path**: JWT cookie `tikq_access` uses path `/`. If the app is served from a subpath or different domain, cookie might not be sent; verify cookie options for the production host.

---

**Recommendation**: Run a staged deployment (e.g. staging with ProductionHandoffMode and real Company DB read-only user), then production, and use the [Handoff Readiness Checklist](docs/04_Handoff/HANDOFF_READINESS_CHECKLIST.md) and [DEPLOYMENT_REQUIRED_CONFIG.md](docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md) for each step.

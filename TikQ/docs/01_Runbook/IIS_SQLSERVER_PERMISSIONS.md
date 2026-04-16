# IIS + SQL Server: Permissions for App Pool Identity

This runbook fixes the **500.30** startup failure when the backend uses SQL Server with **Integrated Security** (Trusted_Connection) under IIS and SQL Server returns:

- **"Login failed for user 'IIS APPPOOL\TikQ'"** (SqlException **18456**)

The app is designed to **fail fast** in Production when the database is unreachable (no silent fallback to SQLite). After you create the login and user as below, recycle the Application Pool; then `GET /api/health` should return 200 and `database.provider: "SqlServer"`.

---

## One-time setup (copy-paste)

**Default:** Database name = **TikQ**, Application Pool name = **TikQ** (identity `IIS APPPOOL\TikQ`).

### Option A: Run the SQL file in SSMS

1. Open **SQL Server Management Studio** and connect to your SQL Server instance (e.g. `localhost` or `.`).
2. Connect as a principal that can create logins and databases (e.g. `sa`, or a login with `sysadmin` / `securityadmin` + `dbcreator`).
3. Open the file **`tools/_handoff_tests/sqlserver-permissions.sql`** from the repo and execute it (F5).
4. Recycle the IIS Application Pool:
   ```powershell
   Restart-WebAppPool -Name TikQ
   ```
5. Verify:
   ```powershell
   .\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer
   ```

### Option B: Custom database or App Pool name

If your database or Application Pool has a different name:

```powershell
.\tools\_handoff_tests\sqlserver-permissions.ps1 -DatabaseName YourDb -AppPoolName YourAppPool
```

Copy the printed SQL into SSMS and execute it. Then recycle the App Pool and run `verify-prod.ps1` as above.

---

## What the script does (no secrets)

| Step | Action |
|------|--------|
| 1 | Create Windows login `[IIS APPPOOL\TikQ]` (if it does not exist). |
| 2 | Create database `[TikQ]` if it does not exist. |
| 3 | In database `TikQ`: create user `[IIS APPPOOL\TikQ]` for that login and add it to role **db_owner** (so the app can run EF Core migrations on first start). |

**db_owner** is recommended for the initial migration phase. You can later reduce to a custom role with only the permissions the app needs (see [MIGRATIONS.md](MIGRATIONS.md) and your schema).

---

## If deploy-iis.ps1 fails verification

When you run `deploy-iis.ps1` with `Database__Provider=SqlServer` and the app fails to start, the script checks the **stdout log** in `<PublishDir>\logs`. If it finds error **18456** or "Login failed for user 'IIS APPPOOL", it prints:

- **"SQL login missing for IIS APPPOOL\TikQ. Run the provided SQL script."**
- Steps: run `sqlserver-permissions.sql` (or the .ps1 with your names), recycle App Pool, and see this runbook and [DEPLOYMENT_REQUIRED_CONFIG.md](DEPLOYMENT_REQUIRED_CONFIG.md).

Stdout logs are in the publish folder’s **logs** subfolder (e.g. `C:\publish\tikq-backend-<timestamp>\logs`).

---

## Acceptance

- After running the SQL script and recycling the App Pool:
  - **GET /api/health** returns **200** and `database.provider` is **SqlServer**.
  - **verify-prod.ps1 -ExpectProvider SqlServer** returns **PASS**.

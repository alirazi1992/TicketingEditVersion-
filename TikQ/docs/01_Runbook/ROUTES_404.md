# If You See 404 for All Routes in Production

**Symptom:** Under IIS (or other production hosting), every request returns 404 — including `/`, `/api/health`, `/swagger`.

**Cause:** Controllers and minimal API endpoints are only matched if they are **mapped** in the request pipeline. If route mapping is missing or gated behind an environment check, no endpoint runs and the pipeline returns 404.

**What to check in `Program.cs` (main app: `backend/Ticketing.Backend/Program.cs`):**

1. **Services:** `builder.Services.AddControllers()` (or `AddControllersWithViews`) must be called so controller discovery runs.
2. **Routing:** `app.MapControllers()` must be called **in all environments** — do not wrap it in `if (app.Environment.IsDevelopment())`.
3. **Health:** A health endpoint must be mapped unconditionally, e.g.  
   `app.MapGet("/api/health", ...)`  
   so load balancers and scripts can hit `/api/health` in production.
4. **No early return:** Ensure no middleware or startup logic returns or short-circuits the pipeline for all requests in production (e.g. avoid `if (!WindowsAuth.Enabled) return;` in a way that prevents the request from reaching endpoint routing).

**Verification:** From the repo run:

```powershell
.\tools\_handoff_tests\verify-routes.ps1 -BaseUrl "https://your-backend-url"
```

This script requires `GET /api/health` to return JSON 200. It prints PASS/FAIL and exits with code 0 (pass) or 1 (fail).

**Startup log:** After fixing, you should see in logs:

```text
[STARTUP] Routes mapped: Controllers=ON, Health=/api/health
```

If this line never appears, the app may be failing before endpoint mapping (e.g. during migrations or configuration).

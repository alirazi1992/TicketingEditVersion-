# Fix NU1301 for dotnet restore / dotnet ef

When `dotnet restore` or `dotnet ef migrations list` fails with **NU1301** (e.g. "Unable to load the service index for source https://api.nuget.org/v3/index.json"), try the steps below. They only change NuGet sources and caches; no application code is modified.

## 1. Disable the "Microsoft Visual Studio Offline Packages" source (safe, reversible)

That source can interfere with restore. Disable it at the **user** level:

```powershell
dotnet nuget disable source "Microsoft Visual Studio Offline Packages"
```

If the exact name differs, list sources first and use the name shown:

```powershell
dotnet nuget list source
```

Then disable the one whose URL is `C:\Program Files (x86)\Microsoft SDKs\NuGetPackages\` (or similar offline path).

## 2. Clear NuGet caches

```powershell
dotnet nuget locals all --clear
```

## 3. Restore with no-cache and force

From the **backend** project directory (where `Ticketing.Backend.csproj` and `nuget.config` live):

```powershell
cd "C:\Users\user\Desktop\42\TikQ\backend\Ticketing.Backend"
dotnet restore --no-cache --force
```

Or from repo root, specifying the project:

```powershell
cd "C:\Users\user\Desktop\42\TikQ"
dotnet restore "backend\Ticketing.Backend\Ticketing.Backend.csproj" --no-cache --force
```

## 4. Run EF migrations list

From the backend directory:

```powershell
cd "C:\Users\user\Desktop\42\TikQ\backend\Ticketing.Backend"
dotnet ef migrations list
```

Or from repo root:

```powershell
cd "C:\Users\user\Desktop\42\TikQ"
dotnet ef migrations list --project "backend\Ticketing.Backend\Ticketing.Backend.csproj" --startup-project "backend\Ticketing.Backend\Ticketing.Backend.csproj"
```

---

## Re-enable the offline source (if needed later)

To turn the Microsoft Visual Studio Offline Packages source back on:

```powershell
dotnet nuget enable source "Microsoft Visual Studio Offline Packages"
```

To see where NuGet config was changed:

- User: `%APPDATA%\NuGet\NuGet.Config`
- Or: `dotnet nuget list source` and check which config file is listed for that source.

---

## One-shot script (PowerShell)

Run from repo root. Backend path is relative to repo root.

```powershell
# 1) Disable offline source
dotnet nuget disable source "Microsoft Visual Studio Offline Packages"

# 2) Clear caches
dotnet nuget locals all --clear

# 3) Restore backend (use your actual path to TikQ)
$backendDir = "C:\Users\user\Desktop\42\TikQ\backend\Ticketing.Backend"
Set-Location $backendDir
dotnet restore --no-cache --force

# 4) EF migrations list
dotnet ef migrations list
```

After this, `dotnet restore` and `dotnet ef migrations list` should succeed. If NU1301 persists, check firewall/proxy and that `nuget.org` is the only enabled source (project `nuget.config` already clears and uses only nuget.org when run from the backend folder).

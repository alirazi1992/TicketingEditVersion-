# IIS deploy steps (TikQ site, binding :8080)

Copy/paste these in an elevated PowerShell (Admin). Replace placeholders with your paths.

---

## 1) Determine IIS site physical path

```powershell
# Option 1: WebAdministration (requires module)
Import-Module WebAdministration -ErrorAction SilentlyContinue
(Get-Website -Name "TikQ").PhysicalPath

# Option 2: appcmd (no module)
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" list site "TikQ"
# Look for physicalPath in the output, or:
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" list vdir "TikQ/" /text:physicalPath
```

Note the path (e.g. `C:\inetpub\wwwroot\TikQ` or `D:\publish\TikQ`). This is the folder IIS is currently serving.

---

## 2) Compare with running app contentRoot

```powershell
# ContentRoot returned by the running app (must match IIS physical path if deploy is correct)
(Invoke-RestMethod -Uri "http://localhost:8080/api/health" -Method GET).contentRoot
```

If this path is different from step 1, the running process may be from an old publish or a different folder.

---

## 3) Option A – Overwrite existing physical path (backup first)

Replace `C:\path\to\current\TikQ` with your actual IIS physical path from step 1.  
Replace `C:\path\to\new\publish` with your new publish output folder (e.g. `.\backend\Ticketing.Backend\bin\Release\net8.0\publish`).

```powershell
$currentPath = "C:\path\to\current\TikQ"   # IIS physical path from step 1
$newPublish  = "C:\path\to\new\publish"     # New publish output folder

# Backup
$backupPath = "${currentPath}_backup_$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Copy-Item -Path $currentPath -Destination $backupPath -Recurse -Force
Write-Host "Backup: $backupPath"

# Overwrite with new publish
Get-ChildItem -Path $currentPath -Recurse | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue
Copy-Item -Path "$newPublish\*" -Destination $currentPath -Recurse -Force
Write-Host "Copied new publish to $currentPath"

# Recycle app pool (use your app pool name; often same as site name)
Import-Module WebAdministration -ErrorAction SilentlyContinue
Restart-WebAppPool -Name "TikQ"
Write-Host "App pool recycled."
```

---

## 4) Option B – Point IIS to new publish folder and recycle

Replace `C:\path\to\new\publish` with your new publish output folder.  
Use your actual site name if not "TikQ".

```powershell
$newPath = "C:\path\to\new\publish"   # New publish folder (must exist)
$siteName = "TikQ"

Import-Module WebAdministration -ErrorAction SilentlyContinue
Set-ItemProperty -Path "IIS:\Sites\$siteName" -Name physicalPath -Value $newPath
Restart-WebAppPool -Name $siteName
Write-Host "IIS physical path set to $newPath and app pool recycled."
```

If WebAdministration is not available, use appcmd:

```powershell
$newPath = "C:\path\to\new\publish"
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" set vdir "TikQ/" -physicalPath:$newPath
& "$env:SystemRoot\System32\inetsrv\appcmd.exe" recycle apppool /apppool.name:TikQ
```

---

## 5) Verify after deploy

```powershell
# Quick check: diag/build should not be 404 (401 or 200 is OK before auth)
Invoke-WebRequest -Uri "http://localhost:8080/api/diag/build" -Method GET -UseBasicParsing | Select-Object StatusCode, Content

# Full verify (IIS path, contentRoot, diag-build, create-category)
cd $env:USERPROFILE\Desktop\42\TikQ\tools\_handoff_tests
.\deploy-verify.ps1 -BaseUrl "http://localhost:8080"
```

Expected after a correct deploy:

- **IIS physical path** and **Health contentRoot** match.
- **GET /api/diag/build** returns 401 (requires Admin) or 200, not 404.
- **diag-build.ps1**: Pass.
- **create-category.ps1**: Pass (201 then 409).

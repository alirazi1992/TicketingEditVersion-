# TESTING GUIDE

Quick reference for testing the stabilized project.

## Quick Start

### Start Backend
```powershell
cd backend\Ticketing.Backend
dotnet run --project .\src\Ticketing.Api\Ticketing.Api.csproj
```
✅ Server runs on: http://localhost:5000
✅ Swagger UI: http://localhost:5000/swagger

### Start Frontend
```powershell
cd frontend
npm run dev
```
✅ Server runs on: http://localhost:3000

---

## Automated Smoke Tests

### Quick Start: Run All Tests

**One-command test runner** (recommended):

```powershell
cd C:\Users\a.razi\Desktop\TikQ

# Run all tests (assumes servers are running)
.\tools\run-all-smoke.ps1

# Or auto-start servers if not running
.\tools\run-all-smoke.ps1 -StartBackend
```

This runs both backend and frontend tests and produces a comprehensive report.

### Backend Smoke Tests

**Prerequisites**: Backend must be running on http://localhost:5000

```powershell
cd C:\Users\a.razi\Desktop\TikQ
.\tools\run-smoke-tests.ps1

# With custom parameters
.\tools\run-smoke-tests.ps1 -BaseUrl "http://localhost:5000" -StopOnFail $true
```

**Parameters**:
- `-BaseUrl`: Backend URL (default: http://localhost:5000)
- `-StopOnFail`: Stop on first failure (default: true)
- `-OutFile`: Output file path (default: RUNTIME_SMOKE_REPORT.md)

This script tests:
- ✅ Swagger UI accessibility
- ✅ Public endpoints (categories, debug users) with JSON validation
- ✅ Authentication (login for Admin/Technician/Client) with token validation
- ✅ Protected endpoints (tickets, user info) with role validation
- ✅ Role-based authorization (403 tests)
- ✅ Ticket creation with ID validation
- ✅ Newly added endpoints (PUT /api/tickets/{id}/responsible)

**Results**: Auto-generated in `RUNTIME_SMOKE_REPORT.md` with ✅/❌ status

### Frontend Smoke Tests (Playwright)

**Prerequisites**: 
- Node.js and npm installed
- Frontend dependencies installed (`npm install`)

```powershell
cd frontend

# Install Playwright (first time only)
npx playwright install chromium

# Run smoke tests (will start frontend server automatically)
npx playwright test e2e/smoke.spec.ts

# Run with UI
npx playwright test e2e/smoke.spec.ts --ui

# View HTML report
npx playwright show-report
```

Tests cover:
- ✅ Login page loads without console errors
- ✅ Client login flow
- ✅ Technician login flow  
- ✅ Admin login flow
- ✅ Ticket detail route exists
- ✅ End-to-end: Create ticket via API, verify in UI
- ✅ Route status code validation (no 404/500)
- ✅ Console error detection (fails on critical errors)

**Output**: HTML report, JUnit XML, and summary in `RUNTIME_SMOKE_REPORT.md`

---

## Manual Testing

### Test Credentials

Seed users (created automatically by SeedData):
- **Admin**: `admin@test.com` / `Admin123!`
- **Technician**: `tech1@test.com` / `Tech123!`
- **Client**: `client1@test.com` / `Client123!`

### Critical Test Scenarios

#### 1. Client User Flow
1. Login at http://localhost:3000/login
2. Verify tickets list appears
3. Click a ticket → verify detail page loads
4. Create new ticket → verify it appears in list

#### 2. Technician User Flow
1. Login as Technician
2. Verify assigned tickets appear
3. Open ticket detail
4. Update state/status
5. Add message

#### 3. Admin User Flow
1. Login as Admin
2. View tickets list
3. Assign technicians to ticket
4. **Test responsible technician assignment** (previously broken, now fixed ✅)
5. View assignment queue
6. Manage categories/technicians

### Fixed Features to Verify
- ✅ **Responsible Technician Assignment**: Should work in ticket detail page
- ✅ **User Preferences**: Should load without 404 errors

---

## Known Working Endpoints

### Public
- ✅ `GET /api/auth/debug-users` - Lists all users for testing
- ✅ `GET /api/categories` - Returns categories

### Authenticated (require Bearer token)
- ✅ `GET /api/tickets` - Returns tickets (role-based filtering)
- ✅ `GET /api/tickets/{id}` - Returns ticket detail
- ✅ `PUT /api/tickets/{id}/responsible` - **NOW WORKS** (was missing) ✅
- ✅ `GET /api/technician/tickets` - Returns technician's assigned tickets
- ✅ `GET /api/auth/me` - Returns current user info

---

## Troubleshooting

### Backend won't start
- Check if port 5000 is in use
- Verify database file exists: `backend\Ticketing.Backend\src\Ticketing.Api\App_Data\ticketing.db`
- Run migrations: `dotnet ef database update --project .\src\Ticketing.Infrastructure\Ticketing.Infrastructure.csproj --startup-project .\src\Ticketing.Api\Ticketing.Api.csproj`

### Frontend won't start
- Run `npm install` if needed
- Check if port 3000 is in use
- Clear Next.js cache: `rm -r .next` (or delete `.next` folder)

### API calls fail
- Verify backend is running on port 5000
- Check browser console for CORS errors
- Verify authentication token is present
- Check `NEXT_PUBLIC_API_BASE_URL` environment variable

### Smoke tests fail
- Ensure backend is running before running backend smoke tests
- Check `RUNTIME_SMOKE_REPORT.md` for detailed error messages
- Verify seed users exist (run backend with database seeding)

---

## Reporting Issues

If you find issues during testing:
1. Note the user role (Client/Technician/Admin)
2. Describe the action that failed
3. Check browser console for errors
4. Check backend logs for exceptions
5. Run smoke tests and include results in bug report

---

## Continuous Integration

### GitHub Actions

A CI workflow is available at `.github/workflows/ci.yml` that runs on push/PR:

**Backend Job**:
- Builds .NET project
- Runs migrations
- Starts backend server
- Runs backend smoke tests
- Uploads test report

**Frontend Job**:
- Installs Node.js dependencies
- Builds frontend
- Installs Playwright browsers
- Starts backend server
- Runs Playwright tests
- Uploads test reports and results

### Manual CI Simulation

For local CI/CD testing:

```powershell
# Backend Tests
cd backend\Ticketing.Backend
dotnet build .\src\Ticketing.Api\Ticketing.Api.csproj
dotnet ef database update --project .\src\Ticketing.Infrastructure\Ticketing.Infrastructure.csproj --startup-project .\src\Ticketing.Api\Ticketing.Api.csproj
dotnet run --project .\src\Ticketing.Api\Ticketing.Api.csproj &
Start-Sleep -Seconds 10
cd ..\..
.\tools\run-smoke-tests.ps1
```

```powershell
# Frontend Tests
cd frontend
npm ci
npm run build
npx playwright install chromium
npx playwright test e2e/smoke.spec.ts --reporter=junit
```

---

**Last Updated**: 2025-01-XX

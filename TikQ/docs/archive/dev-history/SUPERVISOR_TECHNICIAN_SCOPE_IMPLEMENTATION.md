# Supervisor–Technician Scope Implementation

## Summary

Supervisor now has a **real, persisted technician scope** via `SupervisorTechnicianLinks`. The supervisor "Add Technician" list is populated from the same Technician table as admin; already-linked technicians are excluded. Seed data links `supervisor@test.com` and `techsuper@email.com` to `tech1@test.com` and `tech2@test.com` so the dev/demo list is non-empty.

---

## A) DB Mapping and API

- **Entity/table:** `SupervisorTechnicianLinks`  
  - Columns: `Id`, `SupervisorUserId`, `TechnicianUserId`, `CreatedAt`  
  - Unique index on `(SupervisorUserId, TechnicianUserId)` (migration `AddSupervisorTechnicianLinks` and `SupervisorTechnicianLinkConfiguration`).

- **Endpoints (existing, behavior clarified):**
  - **GET /api/supervisor/technicians**  
    Returns technicians **linked** to the current user (supervisor or admin). Uses `GetLinksForSupervisorAsync(supervisorUserId)` and builds workload DTOs.
  - **GET /api/supervisor/technicians/available**  
    Returns technicians that can be linked: same source as admin (Technician table), excluding supervisors, current user, and **already-linked** technicians.
  - **POST /api/supervisor/technicians/{technicianUserId}/link**  
    Creates a link. Idempotent: if link exists, returns success without duplicate.
  - **DELETE /api/supervisor/technicians/{technicianUserId}/link**  
    Removes the link.

- **Authorization:** All under `[Authorize(Policy = "SupervisorOrAdmin")]` (admin or technician with `isSupervisor=true`).

---

## B) Supervisor UI

- **Linked list:** Loaded from **GET /api/supervisor/technicians** and shown in "نمایش لیست" and in the main supervisor technician section.
- **Add Technician modal:**  
  - On open: loads **GET /api/supervisor/technicians/available** (only not-yet-linked technicians).  
  - Already-linked technicians are excluded on the backend; frontend also filters by `linkedTechnicianIds`.  
  - Add: **POST .../technicians/{technicianUserId}/link**; then refreshes both linked list and available list and closes modal.  
  - Remove: **DELETE .../technicians/{technicianUserId}/link**; then refreshes linked list.
- Cookie auth and `credentials: 'include'` are unchanged.

---

## C) Seed (Dev/Demo)

- **EnsureSupervisorTechnicianLinksAsync** in `SeedData.cs`:  
  - Ensures links for supervisors: `supervisor@test.com`, `techsuper@email.com`.  
  - Each is linked to: `tech1@test.com`, `tech2@test.com`.  
  - Idempotent: skips pairs that already exist.

After seed, logging in as `supervisor@test.com` (or `techsuper@email.com`) shows at least Tech One and Tech Two in the supervisor’s technician list and allows assigning tickets to them.

---

## Changed Files

### Backend

| File | Change |
|------|--------|
| `Application/Services/SupervisorService.cs` | `GetAvailableTechniciansAsync`: exclude already-linked technicians (use `GetLinksForSupervisorAsync` and filter). `LinkTechnicianAsync`: set `CreatedAt`; already idempotent. `UnlinkTechnicianAsync`: allow admin (same pattern as link). |
| `Infrastructure/Data/SeedData.cs` | Call `EnsureSupervisorTechnicianLinksAsync` after supervisor technician block; add `EnsureSupervisorTechnicianLinksAsync` to seed links for supervisor@test.com, techsuper@email.com → tech1@test.com, tech2@test.com. |

### Frontend

| File | Change |
|------|--------|
| `components/supervisor-technician-management.tsx` | After successful link: call `loadAvailableTechs()` in addition to `loadList()` so the modal’s available list and linked list stay in sync. |

### Unchanged (already correct)

- `SupervisorController.cs` – routes and auth unchanged.
- `SupervisorTechnicianLink` entity and `SupervisorTechnicianLinkConfiguration` – unique index already in place.
- `SupervisorTechnicianLinkRepository` – `GetLinksForSupervisorAsync`, `IsLinkedAsync`, `AddAsync`, `RemoveAsync` unchanged.
- `frontend/lib/supervisor-api.ts` – `getSupervisorTechnicians`, `getSupervisorAvailableTechnicians`, `linkSupervisorTechnician`, `unlinkSupervisorTechnician` unchanged.

---

## Commands to Run

```powershell
# Backend (from repo root)
cd backend\Ticketing.Backend
dotnet build
dotnet run

# Apply migrations if DB is new or migrations not yet applied
dotnet ef database update
```

Seed runs on app startup when the DB is initialized. To re-seed links without dropping DB, ensure seed is invoked (e.g. by your existing startup seed logic); new links are added only for missing (supervisor, technician) pairs.

```powershell
# Frontend (from repo root)
cd frontend
npm run dev
```

---

## Manual Test Checklist

1. **Login as supervisor**  
   - Login as `supervisor@test.com` / `Test123!` (or your seeded supervisor).  
   - Go to `/supervisor`.

2. **Linked list**  
   - Click "نمایش لیست" (or equivalent that shows linked technicians).  
   - **Expected:** At least Tech One and Tech Two (and any other seeded links).  
   - **API:** GET `/api/supervisor/technicians` returns the same list.

3. **Add Technician modal**  
   - Click "افزودن تکنسین" (Add Technician).  
   - **Expected:** Modal opens; list shows technicians that are **not** already linked (e.g. if only tech1 and tech2 are linked, other technicians from admin list appear).  
   - **API:** GET `/api/supervisor/technicians/available` returns only non-supervisor, non–current-user, not-yet-linked technicians.

4. **Add a technician**  
   - Pick one from the available list and add.  
   - **Expected:** Toast "تکنسین اضافه شد"; modal closes; linked list refreshes and includes the new technician.  
   - Re-open "افزودن تکنسین": the added technician no longer appears in the available list.  
   - **API:** POST `/api/supervisor/technicians/{technicianUserId}/link` is idempotent (calling again does not create duplicate).

5. **Remove a technician**  
   - From the linked list, remove one.  
   - **Expected:** Toast "تکنسین حذف شد"; list refreshes; that technician appears again in "Add Technician" when you re-open the modal.  
   - **API:** DELETE `/api/supervisor/technicians/{technicianUserId}/link`.

6. **Assignment**  
   - With at least one linked technician, assign a ticket to that technician (flow that uses linked technicians).  
   - **Expected:** Assignment succeeds; no regression in ticket assignment logic.

7. **Admin endpoints**  
   - **Expected:** GET `/api/admin/technicians` and admin technician directory/dashboard unchanged; admin still sees full technician list.

8. **Re-open modal / no duplicates**  
   - Add a technician, close modal, re-open "افزودن تکنسین".  
   - **Expected:** No duplicate links; available list and linked list match backend state.

---

## Constraints Respected

- No architectural redesign; no new packages.
- Changes limited to supervisor–technician linking (SupervisorService, SeedData, one frontend component).
- Cookie auth and `credentials: 'include'` preserved.
- Unique constraint on `(SupervisorUserId, TechnicianUserId)` prevents duplicates; existing ticket assignment logic unchanged.

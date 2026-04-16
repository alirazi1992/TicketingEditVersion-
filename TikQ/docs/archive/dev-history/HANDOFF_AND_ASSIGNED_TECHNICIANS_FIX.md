# Handoff + Assigned Technicians Display Fix

## Summary

- **Bug 1 (Handoff):** Handoff request could fail (403/500) and UI did nothing or showed generic error. Root causes: owner without active assignment row was rejected; Admin could not perform handoff; after handoff `AssignedToUserId` was not updated.
- **Bug 2 (Assigned section):** "تکنسین‌های واگذار شده (0 فعال)" showed 0 active and owner appeared as inactive because the list came only from `TicketTechnicianAssignments` and did not include the owner from `AssignedToUserId`, and UI used `isActive` instead of a proper “can act” notion.

## Files Changed

### Backend

| File | Change |
|------|--------|
| `Application/DTOs/TicketDtos.cs` | `AssignedTechnicianDto`: added `AccessMode`, `CanAct`. |
| `Application/Services/ITicketService.cs` | `HandoffTicketAsync` added parameter `UserRole? requesterRole = null`. |
| `Application/Services/TicketService.cs` | Handoff: allow owner (AssignedToUserId) without assignment row; allow Admin (pass role, skip supervisor check); set `AssignedToUserId` and `TechnicianId` to new owner after handoff; set new/updated assignment `Role = "Owner"`. Claim: set assignment `Role = "Owner"` (was "Lead"). Added `BuildAssignedTechniciansForResponse`: owner first (synthetic from AssignedToUserId), then all assignments with `AccessMode`/`CanAct`/`IsActive` for UI. |
| `Api/Controllers/TicketsController.cs` | Handoff: pass `context.Value.role` into `HandoffTicketAsync`. |

### Frontend

| File | Change |
|------|--------|
| `lib/api-types.ts` | `ApiTicketTechnicianDto`: added `id`, `isActive`, `role`, `accessMode`, `canAct` (optional). |
| `types/index.ts` | `assignedTechnicians` item type: added `id`, `isActive`, `role`, `accessMode`, `canAct`. |
| `lib/ticket-mappers.ts` | Map `assignedTechnicians` with `id`, `isActive`, `role`, `accessMode`, `canAct`. |
| `app/tickets/[id]/page.tsx` | Active count by `canAct === true \|\| (canAct !== false && isActive)`; handoff dropdown excludes technicians with `canAct === true \|\| isActive`; cards use `canAct`/`accessMode` for styling and badges (مسئول / همکار / کاندید، فعال / غیرفعال); 403 handoff error toast title "دسترسی غیرمجاز". |

## Backend: GET /api/tickets/{id} — DTO shape

- **ownerTechnician:** Not a separate root field; owner is represented as the first entry in `assignedTechnicians` with `accessMode: "Owner"`, `canAct: true`, `isActive: true`.
- **assignedTechnicians[]** (per technician):
  - `id`, `technicianUserId`, `technicianName`, `technicianEmail`, `assignedAt`, `role`
  - **accessMode:** `"Owner"` | `"Collaborator"` | `"Candidate"`
  - **canAct:** `true` for Owner and active Collaborator, `false` for Candidate (when ticket is claimed)
  - **isActive:** For UI: Owner and active Collaborators = true; Candidates when claimed = false

Owner is always included as the first element when `AssignedToUserId` is set (synthetic if no assignment row).

## Handoff: endpoint and authorization

- **Endpoint:** `POST /api/tickets/{id}/handoff`
- **Body:** `{ "toTechnicianUserId": "guid", "deactivateCurrent": true }`
- **Authorization:**
  - **Admin:** Allowed; deactivates current owner’s assignment and assigns to target.
  - **Technician (supervisor):** Allowed if they are the ticket owner (`AssignedToUserId`) or have an active assignment.
  - **Technician (non-supervisor) or Candidate only:** 403 Forbidden (ProblemDetails).
- **Behaviour:**
  - New owner gets an assignment with `Role = "Owner"` (created or updated).
  - `AssignedToUserId` and `TechnicianId` are set to the new owner.
  - 403 is returned as ProblemDetails (e.g. `detail: "You are not assigned to this ticket"` or `"Only supervisors can reassign tickets"`).
- **Frontend:** On success, ticket is re-fetched and UI updates; on 403, toast shows "دسترسی غیرمجاز" and the API `detail` message.

## Verification

1. **Assigned section:** Create ticket → candidates appear. Technician claims → that technician is first in list with "مسئول" and "فعال"; count shows "1 فعال". Grant collaborator → second technician "همکار" and "فعال"; count "2 فعال". Candidates after claim show "غیرفعال / فقط مشاهده".
2. **Handoff:** As supervisor owner (or Admin), open handoff dialog, choose another technician, submit → 200, ticket reloads, new owner is first with "مسئول" and "فعال". As non-supervisor or candidate, handoff → 403 and toast with message.
3. **No schema changes:** No new DB columns or migrations.

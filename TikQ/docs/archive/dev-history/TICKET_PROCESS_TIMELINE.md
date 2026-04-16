# Ticket Process Timeline – Activity Logging

## Summary

Extended the existing **TicketActivityEvents** system (no new DB columns/tables/migrations) so that:

1. **Grant/revoke collaborator** is recorded with Persian message and actor role.
2. **Reply and status change** log the actor’s **role on the ticket** (Owner / Collaborator / Admin / Supervisor), not only "Technician".
3. **Ticket field updates** (description, priority, etc.) are recorded with actor and role.
4. **Frontend timeline** shows who did what, with role badges (مسئول / همکار / سرپرست / ادمین) and Persian labels.

---

## Backend Files Changed

| File | Change |
|------|--------|
| **Domain/Enums/TicketActivityType.cs** | Added `AccessGranted`, `AccessRevoked`. |
| **Application/Services/TicketService.cs** | • **GetActorRoleLabelForTicketAsync(ticket, userId, role)** – returns "Owner", "Collaborator", "Admin", "Supervisor", or "Client" for timeline. • **UpdateCollaboratorAsync** – after grant/revoke, calls **AddEventAsync** with EventType `AccessGranted` or `AccessRevoked`, ActorRole (Admin/Supervisor), MetadataJson `{ messageFa, targetTechnicianUserId, targetTechnicianName }`. • **AddMessageAsync** – uses **GetActorRoleLabelForTicketAsync** (reply logs as Owner/Collaborator/Admin/Supervisor). • **ChangeStatusAsync** – uses **GetActorRoleLabelForTicketAsync** (status change logs with Owner/Collaborator/Admin/Supervisor). • **UpdateTicketAsync** – when `hasNonStatusUpdates`, adds **AddEventAsync** with EventType `TicketUpdated` and actor role from **GetActorRoleLabelForTicketAsync**. • **GetTicketActivitiesAsync** – maps `AccessGranted` → `TicketActivityType.AccessGranted`, `AccessRevoked` → `TicketActivityType.AccessRevoked`. |

No changes to repository, entity, or controller signatures. No new migrations.

---

## Frontend Files Changed

| File | Change |
|------|--------|
| **app/tickets/[id]/page.tsx** | • **getActivityEventLabel** – added `AccessGranted`, `AccessRevoked`, `TicketUpdated` (FA). For AccessGranted/AccessRevoked, if `metadataJson` has `messageFa`, that is used (includes technician name). • **Role badges** – added `Owner` → "مسئول", `Collaborator` → "همکار"; kept Admin → "ادمین", Supervisor → "سرپرست", Technician → "تکنسین", Client → "مشتری". • Timeline event label call now passes `event.metadataJson` so grant/revoke can show the backend message. |

---

## Example Activity Entries

### 1) Grant access

- **EventType:** `AccessGranted`
- **ActorUserId:** Admin or Supervisor user id  
- **ActorRole:** `Admin` or `Supervisor`
- **MetadataJson:**  
  `{ "messageFa": "دسترسی همکاری به تکنسین علی محمدی داده شد", "targetTechnicianUserId": "...", "targetTechnicianName": "علی محمدی" }`
- **Frontend:** Shows "دسترسی همکاری به تکنسین علی محمدی داده شد", badge "ادمین" or "سرپرست".

### 2) Revoke access

- **EventType:** `AccessRevoked`
- **ActorUserId:** Admin or Supervisor user id  
- **ActorRole:** `Admin` or `Supervisor`
- **MetadataJson:**  
  `{ "messageFa": "دسترسی همکاری از تکنسین علی محمدی لغو شد", "targetTechnicianUserId": "...", "targetTechnicianName": "علی محمدی" }`
- **Frontend:** Shows the same message and role badge.

### 3) Collaborator reply

- **EventType:** `ReplyAdded`
- **ActorUserId:** technician who posted
- **ActorRole:** `Owner` or `Collaborator` (from **GetActorRoleLabelForTicketAsync**)
- **MetadataJson:** `{ "messagePreview": "..." }`
- **Frontend:** "پاسخ جدید اضافه شد", badge "مسئول" or "همکار".

### 4) Collaborator status change

- **EventType:** `StatusChanged` or `Revision`
- **ActorUserId:** technician who changed status
- **ActorRole:** `Owner` or `Collaborator`
- **OldStatus / NewStatus:** set as before
- **Frontend:** e.g. "وضعیت از Open به InProgress تغییر کرد", badge "مسئول" or "همکار".

### 5) Ticket field update (description/priority/due date, etc.)

- **EventType:** `TicketUpdated`
- **ActorUserId / ActorRole:** from **GetActorRoleLabelForTicketAsync**
- **Frontend:** "اطلاعات تیکت به‌روزرسانی شد" with correct role badge.

---

## Events Now Recorded (with actor + role)

| Action | EventType | ActorRole source |
|--------|-----------|------------------|
| Admin/Supervisor grants collaborator | `AccessGranted` | Admin / Supervisor |
| Admin/Supervisor revokes collaborator | `AccessRevoked` | Admin / Supervisor |
| Reply/message added | `ReplyAdded` | Owner / Collaborator / Admin / Supervisor |
| Status changed / Reopened | `StatusChanged` / `Revision` | Owner / Collaborator / Admin / Supervisor |
| Ticket fields updated | `TicketUpdated` | Owner / Collaborator / Admin / Supervisor |
| (Existing) Created, Assigned, Handoff, etc. | unchanged | unchanged |

---

## Attachment / other actions

- **Attachment upload:** Current flow does not persist file storage; when an upload endpoint is implemented, add **AddEventAsync** with e.g. `AttachmentAdded` and **GetActorRoleLabelForTicketAsync** in the same way.
- **Close / resolve:** Already go through **ChangeStatusAsync** (or status in **UpdateTicketAsync**), so they are logged with the correct actor and role (Owner / Collaborator / Admin / Supervisor).

---

## Confirmation

- **No DB migrations added.**  
- All new events use the existing **TicketActivityEvents** table and **AddEventAsync** (same columns: TicketId, ActorUserId, ActorRole, EventType, OldStatus, NewStatus, MetadataJson, CreatedAt).

# Ticket Status Implementation Guide

## Overview

This document describes the implementation of new ticket statuses with Persian labels. The backend stores statuses as English enum keys, while the frontend displays Persian labels to users.

## New Status Values

The ticket status enum has been updated to:
1. **Submitted** (ثبت شد) - Default status when ticket is created
2. **Viewed** (مشاهده شد) - Automatically set when technician/admin views a Submitted ticket
3. **Open** (باز) - Ticket is open and ready for work
4. **InProgress** (در حال انجام) - Ticket is being actively worked on
5. **Resolved** (حل شده) - Ticket has been resolved
6. **Closed** (بسته) - Ticket is closed (Admin only)

## Backend Implementation

### Files Changed

1. **`backend/Ticketing.Backend/Domain/Enums/TicketStatus.cs`**
   - Updated enum with new values (Submitted, Viewed, Open, InProgress, Resolved, Closed)

2. **`backend/Ticketing.Backend/Application/Services/TicketService.cs`**
   - `CreateTicketAsync`: Sets default status to `TicketStatus.Submitted`
   - `GetTicketAsync`: Auto-sets status to `Viewed` when technician/admin views a Submitted ticket
   - `UpdateTicketAsync`: Added validation rules:
     - Only Admin can set `Closed`
     - Client cannot set `InProgress`, `Resolved`, or `Closed`
     - Technician can set `Open`, `InProgress`, `Resolved` (but not `Closed`)
   - `AssignTicketAsync`: Sets status to `Open` (not `InProgress`) when assigning

3. **`backend/Ticketing.Backend/Application/Services/SmartAssignmentService.cs`**
   - Updated status filtering to use new enum values
   - Sets status to `Open` (not `InProgress`) when assigning

4. **`backend/Ticketing.Backend/Infrastructure/Data/Migrations/20251228103000_UpdateTicketStatusEnum.cs`**
   - Migration to map old enum values to new ones:
     - Old `New` (0) → New `Submitted` (0) - no change
     - Old `InProgress` (1) → New `Open` (2)
     - Old `WaitingForClient` (2) → New `Open` (2) - no change
     - Old `Resolved` (3) → New `Resolved` (4)
     - Old `Closed` (4) → New `Closed` (5)

5. **Seed Data Updates**
   - `SeedData.cs`: Updated to use `Submitted` instead of `New`
   - `SystemSettings.cs`: Default status set to `Submitted`

### JSON Serialization

Enums are serialized as strings (configured in `Program.cs` with `JsonStringEnumConverter`). API responses will send status values as strings like "Submitted", "Viewed", etc.

## Frontend Implementation

### Files Created/Changed

1. **`frontend/lib/ticket-status.ts`** (NEW)
   - Single source of truth for ticket status types and Persian labels
   - Exports `TicketStatus` type, `TICKET_STATUS_LABELS`, `TICKET_STATUS_OPTIONS`, and `getTicketStatusLabel()` helper

2. **`frontend/lib/api-types.ts`**
   - Updated `ApiTicketStatus` type to match new backend enum

3. **`frontend/lib/ticket-mappers.ts`**
   - Updated to map API statuses directly (no transformation needed since they match)

4. **`frontend/types/index.ts`**
   - Re-exports `TicketStatus` from `@/lib/ticket-status` for backward compatibility

5. **`frontend/components/admin-ticket-list.tsx`**
   - Updated to use new status types and Persian labels
   - Updated filter dropdowns and bulk status update buttons

### Remaining Component Updates

The following components still need to be updated to use the new status values:
- `two-step-ticket-form.tsx`
- `ticket-calendar-overview.tsx`
- `technician-dashboard.tsx`
- `enhanced-auto-assignment.tsx`
- `client-dashboard.tsx`

**Pattern to follow:**
1. Import `TicketStatus` and `TICKET_STATUS_LABELS` from `@/lib/ticket-status`
2. Replace old status string literals ("open", "in-progress", etc.) with new enum values ("Open", "InProgress", etc.)
3. Use `TICKET_STATUS_LABELS[status]` to display Persian labels

## Database Migration

### Applying the Migration

```powershell
cd backend\Ticketing.Backend
dotnet ef database update
```

The migration will:
- Map existing ticket status values from old enum to new enum
- Update both `Tickets` and `TicketMessages` tables
- Preserve data integrity

### Migration Mapping Details

- Old `New` (0) → New `Submitted` (0): No change needed
- Old `InProgress` (1) → New `Open` (2): Tickets that were "in progress" become "open"
- Old `WaitingForClient` (2) → New `Open` (2): Already mapped correctly
- Old `Resolved` (3) → New `Resolved` (4): Status value increases
- Old `Closed` (4) → New `Closed` (5): Status value increases

## Testing Checklist

### Backend Tests

1. **Create Ticket**
   - POST `/api/tickets` as Client
   - Verify response has `status: "Submitted"`

2. **Auto-Set Viewed**
   - Create ticket as Client
   - GET `/api/tickets/{id}` as Technician
   - Verify status changes to `"Viewed"` (not for ticket creator)

3. **Status Updates - Technician**
   - Update ticket status to `"InProgress"` as Technician → Should succeed
   - Update ticket status to `"Resolved"` as Technician → Should succeed
   - Update ticket status to `"Closed"` as Technician → Should fail (403)

4. **Status Updates - Client**
   - Update ticket status to `"InProgress"` as Client → Should fail (403)
   - Update ticket status to `"Resolved"` as Client → Should fail (403)
   - Update ticket status to `"Closed"` as Client → Should fail (403)

5. **Status Updates - Admin**
   - Update ticket status to `"Closed"` as Admin → Should succeed
   - Update ticket status to any other value as Admin → Should succeed

6. **Assignment**
   - Assign ticket to technician → Status should become `"Open"` (not `"InProgress"`)

### Frontend Tests

1. **Display Labels**
   - Open ticket list → Verify Persian labels display correctly
   - Open ticket detail → Verify status shows Persian label

2. **Status Filters**
   - Filter by status → Verify filters work with new status values
   - Verify filter dropdown shows all 6 statuses with Persian labels

3. **Status Updates**
   - Change ticket status → Verify Persian label updates
   - Refresh page → Verify status persists correctly

4. **Create Ticket**
   - Create new ticket → Verify initial status is "ثبت شد" (Submitted)

## API Usage Examples

### Create Ticket (Returns "Submitted")
```json
POST /api/tickets
{
  "title": "Test Ticket",
  "description": "Test Description",
  "categoryId": 1,
  "priority": "Medium"
}

Response:
{
  "id": "...",
  "status": "Submitted",
  ...
}
```

### Update Status
```json
PATCH /api/tickets/{id}
{
  "status": "InProgress"
}

Response:
{
  "id": "...",
  "status": "InProgress",
  ...
}
```

### Get Tickets with Status Filter
```
GET /api/tickets?status=Open
GET /api/tickets?status=InProgress
GET /api/tickets?status=Resolved
```

## Important Notes

1. **Status Storage**: Backend stores status as integer enum, but API sends/receives strings
2. **Persian Labels**: Only displayed in UI, never sent to backend
3. **Permission Rules**: 
   - Clients can only set: Submitted, Viewed, Open
   - Technicians can set: Open, InProgress, Resolved
   - Admins can set: All statuses including Closed
4. **Auto-Viewed**: When a technician/admin views a Submitted ticket, status automatically changes to Viewed
5. **Default Status**: New tickets always start with `Submitted` status

## Troubleshooting

### Migration Issues
If migration fails, check:
- Database connection string
- Existing data compatibility
- Migration file is correct

### Frontend Type Errors
If TypeScript errors occur:
- Ensure all components import `TicketStatus` from `@/lib/ticket-status`
- Update old status string literals to new enum values
- Use `TICKET_STATUS_LABELS` for display

### API Response Issues
If API returns wrong status format:
- Verify `JsonStringEnumConverter` is configured in `Program.cs`
- Check that enum values match between frontend and backend types









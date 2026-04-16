# Technician Expertise-Based Auto-Routing

## Overview

This document describes the implementation of technician expertise-based auto-routing, which automatically assigns tickets to technicians based on their subcategory permissions.

## Feature Description

When a ticket is created with a `CategoryId` and `SubcategoryId`, the system automatically:
1. Finds all active technicians who have permission for that `SubcategoryId`
2. Assigns the ticket to all eligible technicians (multi-technician assignment)
3. Sets the ticket status to `Assigned` (internal) if at least one technician is assigned
4. Creates activity events and notifications for assigned technicians
5. Makes the ticket visible in technician dashboards immediately

## Database Schema

### TechnicianSubcategoryPermissions Table

Created via migration: `20260104000000_AddTechnicianSubcategoryPermissions.cs`

**Columns:**
- `Id` (Guid, PK)
- `TechnicianId` (Guid, FK → Technicians)
- `SubcategoryId` (int, FK → Subcategories)
- `CreatedAt` (DateTime)
- `UpdatedAt` (DateTime?)

**Constraints:**
- Unique index on `(TechnicianId, SubcategoryId)` - prevents duplicate permissions
- Index on `SubcategoryId` - for efficient querying of technicians by subcategory

**Relationships:**
- `Technician` → `SubcategoryPermissions` (one-to-many)
- `Subcategory` → `TechnicianSubcategoryPermissions` (one-to-many)

## Backend Implementation

### Entities

**TechnicianSubcategoryPermission** (`Domain/Entities/TechnicianSubcategoryPermission.cs`)
- Represents a technician's expertise in a specific subcategory
- Links `Technician` to `Subcategory`

**Technician** (updated)
- Added navigation property: `ICollection<TechnicianSubcategoryPermission> SubcategoryPermissions`

### Repositories

**ITechnicianSubcategoryPermissionRepository** (`Application/Repositories/ITechnicianSubcategoryPermissionRepository.cs`)
- `GetByTechnicianIdAsync(Guid technicianId)` - Get all permissions for a technician
- `GetTechnicianUserIdsBySubcategoryIdAsync(int subcategoryId)` - Get user IDs of technicians with permission for a subcategory
- `ReplacePermissionsAsync(Guid technicianId, IEnumerable<int> subcategoryIds)` - Replace all permissions for a technician
- `AddAsync`, `DeleteAsync`, `DeleteByTechnicianIdAsync` - CRUD operations

**ITechnicianRepository** (updated)
- `GetByIdWithIncludesAsync(Guid id)` - Get technician with User and Permissions
- `GetTechnicianUserIdsBySubcategoryAsync(int subcategoryId)` - Alternative method (also in permission repo)

### Services

**TechnicianService** (updated)
- `CreateTechnicianAsync` - Accepts `subcategoryIds` and creates permissions
- `UpdateTechnicianAsync` - Accepts `subcategoryIds` and replaces permissions
- Validates that subcategory IDs exist before creating permissions

**TicketService** (updated)
- `CreateTicketAsync` - After ticket creation:
  1. Calls `GetTechnicianUserIdsBySubcategoryIdAsync` to find eligible technicians
  2. Uses `SetAssignmentsAsync` to assign all eligible technicians
  3. Sets ticket status to `Assigned` if any technicians are assigned
  4. Creates "AssignedTechnicians" activity event
  5. Notifies all assigned technicians

### API Endpoints

**Technician Management** (`/api/admin/technicians`)
- `GET /api/admin/technicians` - Returns technicians with `subcategoryPermissions`
- `POST /api/admin/technicians` - Creates technician with `subcategoryIds` in request body
- `PUT /api/admin/technicians/{id}` - Updates technician and replaces permissions with `subcategoryIds` in request body

**Ticket Endpoints** (existing, enhanced)
- `GET /api/tickets/{id}` - Returns `assignedTechnicians` and `activityEvents`
- `POST /api/tickets` - Auto-assigns technicians based on `SubcategoryId`

### DTOs

**TechnicianSubcategoryPermissionDto**
```csharp
public class TechnicianSubcategoryPermissionDto
{
    public Guid Id { get; set; }
    public int SubcategoryId { get; set; }
    public string? SubcategoryName { get; set; }
    public int? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**TechnicianResponse** (updated)
- Added `List<TechnicianSubcategoryPermissionDto>? SubcategoryPermissions`

**TechnicianCreateRequest / TechnicianUpdateRequest** (updated)
- Added `List<int>? SubcategoryIds`

## Frontend Implementation

### Technician Management UI

**Location:** `components/technician-management.tsx`

**Features:**
1. **Expertise Selection:**
   - Category dropdown (fetched from backend)
   - Subcategory dropdown (filtered by selected category)
   - "Add" button to add selected pair to expertise list
   - Chips/badges showing selected expertise with remove buttons
   - Prevents duplicate selections

2. **Sync with Category Management:**
   - Fetches categories/subcategories from backend on form open
   - Refetches after category CRUD operations (via shared state or explicit revalidate)
   - New categories/subcategories appear immediately without page reload

3. **Technician List:**
   - Displays expertise as compact chips/badges in the table
   - Shows category/subcategory names

### API Client

**Location:** `lib/technicians-api.ts`

**Updated Functions:**
- `createTechnician` - Sends `subcategoryIds` in request body
- `updateTechnician` - Sends `subcategoryIds` in request body
- `getTechnicianById` - Returns technician with permissions

**Location:** `lib/categories-api.ts`
- `getAllCategories` - Used by technician form to populate dropdowns
- `getSubcategories` - Used by technician form to populate subcategory dropdown

### Types

**Location:** `lib/api-types.ts`

**Updated Types:**
- `ApiTechnicianResponse` - Includes `subcategoryPermissions`
- `ApiTechnicianCreateRequest` - Includes `subcategoryIds`
- `ApiTechnicianUpdateRequest` - Includes `subcategoryIds`
- `ApiTechnicianSubcategoryPermissionDto` - New type for permission data

## Workflow

### Admin Creates Technician with Expertise

1. Admin navigates to "مدیریت تکنسین ها"
2. Clicks "Add Technician"
3. Fills in technician details (name, email, etc.)
4. In "Expertise" section:
   - Selects a Category
   - Selects a Subcategory (filtered by category)
   - Clicks "Add" to add to expertise list
   - Repeats for multiple expertise areas
5. Saves technician
6. Backend creates `TechnicianSubcategoryPermission` records

### Client Submits Ticket

1. Client navigates to ticket submission form
2. Selects Category and Subcategory
3. Fills in ticket details and submits
4. Backend:
   - Creates ticket
   - Finds technicians with permission for `SubcategoryId`
   - Assigns ticket to all eligible technicians
   - Sets status to `Assigned` (if technicians found)
   - Creates activity event
   - Notifies technicians

### Technician Views Assigned Ticket

1. Technician logs in
2. Technician dashboard shows tickets assigned to them (via `AssignedTechnicians` array)
3. Unread indicator (blue dot) shows if ticket is unread
4. Opening ticket marks it as read

### Admin Views Ticket

1. Admin opens ticket
2. Sees "Assigned Technicians" list with all assigned technicians
3. Sees activity events including "AssignedTechnicians" event
4. Status shows "تکنسین انتخاب شد" (Assigned) if technicians are assigned

## Status Mapping

The system maintains role-based status visibility:

- **Admin sees:** "تکنسین انتخاب شد" (Assigned) when technicians are assigned
- **Client sees:** "ثبت شد" (Submitted) until technician starts work
- **Technician sees:** "ثبت شد" (Submitted) until they open/start work

This is handled by the existing status mapping logic in `TicketService.MapToResponse`.

## Unread Indicators

- When a ticket is auto-assigned, all assigned technicians see it as unread
- When one technician updates the ticket, other assigned technicians see it as unread
- The actor (technician who made the update) does not see their own action as unread
- Uses existing `TicketReadReceipts` and `UpdatedAt > LastReadAt` logic

## Verification

### Manual Testing Steps

1. **Create Category/Subcategory:**
   - Login as Admin
   - Go to "مدیریت دسته‌بندی‌ها"
   - Create a Category (e.g., "Test Category")
   - Create a Subcategory (e.g., "Test Subcategory")

2. **Create Technician with Expertise:**
   - Go to "مدیریت تکنسین ها"
   - Add Technician
   - Select the Category/Subcategory from step 1
   - Add to expertise list
   - Save

3. **Verify Expertise Sync:**
   - Add a NEW subcategory in category management
   - Open technician edit form
   - Verify new subcategory appears in dropdown (no page reload needed)

4. **Create Ticket:**
   - Login as Client
   - Submit ticket with the Category/Subcategory from step 1
   - Verify ticket is created successfully

5. **Verify Auto-Assignment:**
   - Login as Admin
   - Open the ticket
   - Verify "Assigned Technicians" shows the technician from step 2
   - Verify status shows "تکنسین انتخاب شد"
   - Check Activity Events - should show "AssignedTechnicians"

6. **Verify Technician Dashboard:**
   - Login as the Technician from step 2
   - Go to Technician Dashboard
   - Verify ticket appears in list
   - Verify unread indicator (blue dot)
   - Open ticket - verify unread clears

### Automated Testing

See `tools/verify-technician-expertise-routing.ps1` for a verification script template.

**Note:** Full automation requires:
- Authentication endpoints (login, token management)
- Category/Subcategory CRUD endpoints
- Technician CRUD endpoints
- Ticket creation endpoint
- Ticket query endpoints

## Migration

**Migration Name:** `20260104000000_AddTechnicianSubcategoryPermissions`

**To Apply:**
```bash
cd backend/Ticketing.Backend
dotnet ef database update --project src/Ticketing.Api
```

**To Rollback:**
```bash
dotnet ef database update <previous-migration-name> --project src/Ticketing.Api
```

## Backward Compatibility

- Existing tickets without assignments continue to work
- Existing technician records without permissions continue to work
- Manual assignment (via Admin UI) still works alongside auto-assignment
- All API endpoints remain backward compatible (new fields are optional)

## Future Enhancements

1. **Priority-based Assignment:** Assign to technicians based on workload/priority
2. **Round-robin Assignment:** Distribute tickets evenly among eligible technicians
3. **Skill Level Matching:** Match tickets to technicians based on skill level within subcategory
4. **Auto-assignment Rules:** Admin-configurable rules for auto-assignment behavior
5. **Assignment History:** Track assignment changes over time

## Troubleshooting

### Tickets Not Auto-Assigned

1. **Check Technician Permissions:**
   - Verify technician has permission for the ticket's `SubcategoryId`
   - Check `TechnicianSubcategoryPermissions` table in database

2. **Check Technician Status:**
   - Verify technician is active (`IsActive = true`)
   - Verify technician has a `UserId` (linked to a User account)

3. **Check Ticket Creation:**
   - Verify ticket has a valid `SubcategoryId`
   - Check backend logs for auto-assignment attempts

4. **Check Database:**
   - Verify migration `20260104000000_AddTechnicianSubcategoryPermissions` is applied
   - Check `TicketTechnicianAssignments` table for assignment records

### Expertise Not Syncing in UI

1. **Check API Calls:**
   - Verify `getAllCategories` and `getSubcategories` are called on form open
   - Check browser network tab for API responses

2. **Check State Management:**
   - Verify categories/subcategories are refetched after CRUD operations
   - Check for caching issues (ensure `no-store` is used for GET requests)

### Duplicate Permissions

- The unique constraint on `(TechnicianId, SubcategoryId)` prevents duplicates
- If duplicates exist, check migration was applied correctly
- Manually remove duplicates if needed (before applying unique constraint)

## Related Documentation

- [Multi-Technician Assignment Report](./MULTI_TECH_ASSIGNMENT_REPORT.md)
- [Ticket Submission Fix Report](./TICKET_SUBMISSION_FIX_REPORT.md)
- [Frontend Backend Connectivity Fix](./FRONTEND_BACKEND_CONNECTIVITY_FIX.md)

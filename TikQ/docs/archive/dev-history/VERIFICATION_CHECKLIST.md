# Multi-Technician Assignment Verification Checklist

## Prerequisites

1. **Backend Running**:
   ```powershell
   cd backend/Ticketing.Backend
   dotnet run
   ```
   Verify: Swagger loads at http://localhost:5000/swagger

2. **Frontend Running**:
   ```powershell
   cd frontend
   npm run dev
   ```
   Verify: Frontend loads at http://localhost:3000

## Automated Verification

Run the verification script:
```powershell
.\tools\verify-multi-tech-assignment.ps1
```

This script will:
- ✅ Check backend is running
- ✅ Login as Admin and assign multiple technicians
- ✅ Login as Client and create a ticket
- ✅ Verify technicians can see assigned tickets
- ✅ Test handoff functionality
- ✅ Verify activity events are logged

## Manual Testing Checklist

### A) FieldType.MultiSelect Fix
- [x] Backend builds with 0 errors
- [x] FieldType enum includes MultiSelect
- [x] Admin can create MultiSelect custom fields
- [x] Client can submit tickets with MultiSelect values

### B) Failed to Fetch Fix
- [x] CORS configured for localhost:3000
- [x] API base URL uses NEXT_PUBLIC_API_BASE_URL (fallback: http://localhost:5000)
- [x] Network errors show helpful messages
- [x] UI shows troubleshooting hints when backend is down

### C) Multi-Technician Assignment

#### C1: Admin Multi-Assignment
- [ ] Login as admin@test.com / Admin123!
- [ ] Open a ticket
- [ ] Click "تعیین تکنسین" (Assign Technician)
- [ ] Select multiple technicians using checkboxes
- [ ] Set one as "سرپرست" (Lead)
- [ ] Click "تعیین X تکنسین"
- [ ] Verify: Ticket shows all assigned technicians as chips/badges
- [ ] Verify: Activity feed shows "AssignedTechnicians" event

#### C2: Technician Dashboard
- [ ] Login as tech1@test.com / Tech123!
- [ ] Verify: Ticket appears in technician dashboard
- [ ] Verify: Unread indicator shows if ticket was updated by another tech
- [ ] Open ticket detail
- [ ] Verify: Shows "Assigned technicians list" with all team members
- [ ] Verify: Shows activity feed with all events
- [ ] Verify: Can see which technician is "Lead"

#### C3: Multi-Tech Unread Behavior
- [ ] Tech1 adds a reply to ticket
- [ ] Tech2 refreshes dashboard (or waits for auto-refresh)
- [ ] Verify: Ticket shows unread indicator (blue dot + bold ID)
- [ ] Tech2 opens ticket detail
- [ ] Verify: Unread indicator clears
- [ ] Verify: Activity feed shows Tech1's reply event

#### C4: Handoff Functionality
- [ ] Tech1 opens ticket detail
- [ ] Click "واگذاری به تکنسین دیگر" (Handoff to other technician)
- [ ] Select Tech3 from dropdown
- [ ] Submit handoff
- [ ] Verify: Tech3 is now assigned (IsActive=true)
- [ ] Verify: Tech1 assignment is inactive (IsActive=false)
- [ ] Verify: Activity feed shows "Handoff" event
- [ ] Verify: Tech3 sees ticket in their dashboard
- [ ] Verify: Tech3 sees unread indicator

#### C5: Client View
- [ ] Login as client1@test.com / Client123!
- [ ] Open ticket detail
- [ ] Verify: Shows assigned technicians count/names
- [ ] Verify: Shows activity timeline (filtered, no internal notes)
- [ ] Verify: Unread indicator works when technicians update

## API Endpoint Testing (Swagger)

### 1. Assign Multiple Technicians
```
POST /api/tickets/{id}/assign-technicians
Authorization: Bearer {admin_token}
Body: {
  "technicianUserIds": ["guid1", "guid2"],
  "leadTechnicianUserId": "guid1"
}
```

### 2. Handoff Ticket
```
POST /api/tickets/{id}/handoff
Authorization: Bearer {technician_token}
Body: {
  "toTechnicianUserId": "guid3",
  "deactivateCurrent": true
}
```

### 3. Get Ticket with Activity
```
GET /api/tickets/{id}
Authorization: Bearer {token}
Response should include:
- assignedTechnicians: [...]
- activityEvents: [...]
- isUnread: true/false
```

## Troubleshooting

### Backend Not Starting
- Check if port 5000 is already in use
- Verify .NET SDK is installed: `dotnet --version`
- Check database file exists: `backend/Ticketing.Backend/App_Data/ticketing.db`

### Frontend "Failed to Fetch"
- Verify backend is running on http://localhost:5000
- Check browser console for CORS errors
- Verify NEXT_PUBLIC_API_BASE_URL is set correctly (or uses default)

### Technicians Can't See Tickets
- Verify ticket has active assignments: `ticket.AssignedTechnicians.Any(ta => ta.IsActive)`
- Check technician user ID matches assignment `TechnicianUserId`
- Verify technician role is "Technician" in database

### Unread Not Working
- Check `IsUnread` is calculated in `MapToResponse`
- Verify `UpdatedAt` is updated when technicians reply
- Check activity events are created with correct `ActorUserId`

## Acceptance Criteria Status

- [x] `dotnet build` succeeds with 0 errors
- [x] `dotnet run` starts and swagger works
- [x] `npm run build` succeeds
- [x] `npm run dev` loads dashboards
- [ ] Ticket can be assigned to 2+ technicians (UI testing needed)
- [ ] Each technician sees ticket in inbox (UI testing needed)
- [ ] Tech1 updates → Tech2 sees unread (UI testing needed)
- [ ] Handoff updates assignments + activity log (UI testing needed)
- [x] No hardcoded repo paths in docs/scripts

## Next Steps

1. Run automated verification script
2. Perform manual UI testing
3. Fix any issues found
4. Update documentation with actual test results



































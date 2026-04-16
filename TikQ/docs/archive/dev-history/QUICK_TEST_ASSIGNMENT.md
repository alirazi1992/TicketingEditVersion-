# Quick Test - Ticket Assignment Fix

## Prerequisites

- ✅ Backend running on `http://localhost:5000`
- ✅ Frontend running on `http://localhost:3000`
- ✅ User logged in as supervisor with linked technicians

---

## Test Steps

### 1. Open Browser Console

Press `F12` and go to the **Console** tab.

### 2. Navigate to Supervisor Page

Go to the supervisor management page in the UI.

### 3. Attempt Assignment

1. Click on a technician to view their details
2. Click "افزودن تیکت" (Add Ticket)
3. Select a ticket from the list by clicking "انتخاب"

### 4. Check Console Output

**Expected Success Logs**:

```javascript
[assignSupervisorTicket] Sending request: {
  endpoint: "/api/supervisor/technicians/abc-123.../assignments",
  technicianUserId: "abc-123...",
  ticketId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // ✅ Guid format
  payload: { ticketId: "3fa85f64-5717-4562-b3fc-2c963f66afa6" }
}

[handleAssign] Assigning ticket: {
  ticketId: "3fa85f64-5717-4562-b3fc-2c963f66afa6",  // ✅ Guid format
  technicianUserId: "abc-123..."
}
```

**Expected Toast**:
```
تیکت واگذار شد
```

**NOT Expected (Old Error)**:
```javascript
[handleAssign] Error assigning ticket: {
  status: 400,
  body: { message: "TicketId is required", field: "ticketId" }
}
```

---

## Verify in Network Tab

1. Open DevTools → **Network** tab
2. Filter by "assignments"
3. Find the POST request
4. Click on it and check:

**Request URL**:
```
http://localhost:5000/api/supervisor/technicians/{guid}/assignments
```

**Request Payload**:
```json
{
  "ticketId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

**Response (200 OK)**:
```json
{
  "message": "Ticket assigned successfully"
}
```

---

## Test with curl (Optional)

```powershell
# 1. Get auth token from browser console
# localStorage.getItem('ticketing.auth.token')

# 2. Get a ticket Guid from the available tickets list
# In console, after opening the assignment dialog:
# Copy the 'id' field from any ticket object

# 3. Run curl
$token = "YOUR_TOKEN"
$techId = "TECHNICIAN_GUID"
$ticketId = "TICKET_GUID"

curl -i -X POST "http://localhost:5000/api/supervisor/technicians/$techId/assignments" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d "{`"ticketId`":`"$ticketId`"}"
```

**Expected Output**:
```
HTTP/1.1 200 OK
Content-Type: application/json

{"message":"Ticket assigned successfully"}
```

---

## Success Checklist

- [ ] Console shows `[assignSupervisorTicket] Sending request` with Guid ticketId
- [ ] Console shows `[handleAssign] Assigning ticket` with Guid ticketId
- [ ] Network tab shows POST with `{"ticketId":"<guid>"}` payload
- [ ] Response is 200 OK with success message
- [ ] Toast shows "تیکت واگذار شد"
- [ ] Ticket appears in technician's assigned list
- [ ] No "TicketId is required" error

---

## Troubleshooting

### Issue: Still getting "TicketId is required"

**Check**:
1. Console log shows `ticketId: "TCK-001"` (display ID) instead of Guid
   - **Fix**: Ensure you're using the latest code with `ticket.id` instead of `ticket.ticketId`
2. Payload shows `ticketId: null` or `ticketId: ""`
   - **Fix**: Check that backend is returning `id` field in the ticket list response

### Issue: Console shows empty object `{}`

**Check**:
1. Error object is not being captured correctly
   - **Fix**: Ensure you're using the latest error logging code
2. Clear browser cache and hard reload (Ctrl+Shift+R)

### Issue: ticketId is undefined

**Check**:
1. Backend response doesn't include `id` field
   - **Solution**: Verify backend `MapTicketSummary` includes `Id = ticket.Id`
2. Frontend DTO doesn't have `id` field
   - **Solution**: Ensure `ApiSupervisorTicketSummaryDto` has `id: string`

---

## Key Changes Summary

1. **DTO**: Added `id` field to `ApiSupervisorTicketSummaryDto`
2. **UI**: Changed `handleAssign(ticket.ticketId)` → `handleAssign(ticket.id)`
3. **Validation**: Added guard clause for empty ticketId
4. **Logging**: Enhanced logs to show actual payload

**Result**: Frontend now sends Guid instead of display ID ✅

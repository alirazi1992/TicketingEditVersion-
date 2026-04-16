# Quick Test Guide - Console Error Fixes

## Prerequisites

1. Backend running on `http://localhost:5000`
2. Frontend running on `http://localhost:3000`
3. User logged in with supervisor role

## Test 1: loadTickets Error Logging ✅

**Steps**:
1. Open browser console (F12)
2. Navigate to homepage
3. If messages fail to load, check console output

**Expected**:
```javascript
[loadTickets] Failed to load messages {
  ticketId: "...",
  endpoint: "/api/tickets/.../messages",
  status: 404,  // or other status
  statusText: "Not Found",
  message: "...",
  body: { ... },  // actual response body
  rawText: "...",  // first 200 chars
  traceId: "..."
}
```

**NOT**:
```javascript
[loadTickets] Failed to load messages {}  // ❌ Empty object
```

---

## Test 2: React Key Warnings ✅

### 2a. Ticket Detail Page

**Steps**:
1. Open browser console (F12)
2. Navigate to any ticket detail page: `http://localhost:3000/tickets/{id}`
3. Scroll to "تکنسین‌های واگذار شده" section
4. Check console for warnings

**Expected**: No warnings

**NOT**:
```
Warning: Each child in a list should have a unique "key" prop.
```

### 2b. Supervisor Management Page

**Steps**:
1. Open browser console (F12)
2. Navigate to supervisor management (if available in UI)
3. Open "واگذاری تیکت" dialog
4. Check console for warnings

**Expected**: No warnings

---

## Test 3: Assignment Error Messages ✅

### 3a. Test with Unlinked Technician

**Steps**:
1. Go to supervisor management page
2. Try to assign a ticket to a technician that is NOT linked
3. Check the toast notification

**Expected Toast**:
```
خطا در واگذاری تیکت
Technician is not linked to this supervisor
You must link this technician before assigning tickets to them
```

**Expected Console**:
```javascript
[handleAssign] Error assigning ticket: {
  ticketId: "...",
  technicianUserId: "...",
  status: 400,
  message: "Technician is not linked to this supervisor",
  body: {
    message: "Technician is not linked to this supervisor",
    field: "technicianUserId",
    details: "You must link this technician before assigning tickets to them"
  },
  details: "You must link this technician before assigning tickets to them"
}
```

### 3b. Test with Ticket Not Owned by Supervisor

**Steps**:
1. Try to assign a ticket that is NOT currently assigned to you
2. Check the toast notification

**Expected Toast**:
```
خطا در واگذاری تیکت
Cannot assign ticket. Either the ticket is not assigned to you, or it doesn't exist.
As a supervisor, you can only delegate tickets that are currently assigned to you.
```

### 3c. Test Successful Assignment

**Steps**:
1. Ensure technician is linked to supervisor
2. Ensure ticket is assigned to supervisor
3. Assign the ticket to the technician

**Expected Toast**:
```
تیکت واگذار شد
```

**Expected**: 
- Toast disappears after a few seconds
- Ticket appears in technician's assigned list
- Ticket disappears from available list

---

## Manual Backend Test (Optional)

### Test Assignment Endpoint Directly

```powershell
# Get auth token from frontend
# In browser console: localStorage.getItem('ticketing.auth.token')

$token = "YOUR_TOKEN_HERE"
$techId = "TECHNICIAN_USER_ID"
$ticketId = "TICKET_ID"

# Test 1: Valid assignment
curl -i -X POST "http://localhost:5000/api/supervisor/technicians/$techId/assignments" `
  -H "Authorization: Bearer $token" `
  -H "Content-Type: application/json" `
  -d "{`"ticketId`":`"$ticketId`"}"

# Expected: 200 OK or 400 with detailed message
```

---

## Verification Checklist

After running all tests:

- [ ] No `{}` empty objects in error logs
- [ ] All error logs include `status`, `body`, `rawText`
- [ ] No React key warnings in console
- [ ] Assignment errors show detailed messages
- [ ] Toast notifications display backend error details
- [ ] Console logs include full error context

---

## Common Issues

### Issue: Still seeing `{}`
**Solution**: Clear browser cache and hard reload (Ctrl+Shift+R)

### Issue: Still seeing key warnings
**Solution**: 
1. Check that the data has `id` or `ticketId` fields
2. Verify the fallback keys are being used
3. Check browser console for the exact line number

### Issue: Generic "Failed to assign ticket"
**Solution**:
1. Ensure backend changes are deployed
2. Restart backend: `dotnet run`
3. Check backend logs for exceptions

---

## Success Criteria

✅ All tests pass
✅ No console warnings or errors
✅ Error messages are descriptive and actionable
✅ UI provides clear feedback to users

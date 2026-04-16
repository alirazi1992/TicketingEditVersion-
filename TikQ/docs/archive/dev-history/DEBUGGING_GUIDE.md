# Ticket Submission Debugging Guide

## Quick Start

1. **Open Browser DevTools** (F12)
2. **Go to Console tab**
3. **Submit a ticket** with all required fields
4. **Check logs** - Look for entries starting with:
   - `[TwoStepTicketForm]`
   - `[handleTicketCreate]`
   - `[apiRequest]`

5. **Go to Network tab**
6. **Filter:** `tickets`
7. **Look for:** `POST /api/tickets` request
8. **Check:** Status code, Request payload, Response body

---

## Expected Log Flow (Success)

```
[TwoStepTicketForm] handleFormSubmit called
[TwoStepTicketForm] Category check
[TwoStepTicketForm] VALIDATION PASSED - Calling onSubmit
[handleTicketCreate] Called
[handleTicketCreate] Category check BEFORE loading
[handleTicketCreate] Category mapping AFTER loading
[handleTicketCreate] PAYLOAD READY - About to send API request
[handleTicketCreate] Making API request NOW
[apiRequest] POST http://localhost:5000/api/tickets
[apiRequest] POST http://localhost:5000/api/tickets → 200 OK
[handleTicketCreate] API RESPONSE RECEIVED
[TwoStepTicketForm] onSubmit succeeded, closing modal
```

---

## Failure Scenarios

### Scenario 1: No Handler Runs
**Symptoms:**
- No `[TwoStepTicketForm] handleFormSubmit called` in console
- No network request

**Possible Causes:**
- Form validation failed (check for validation error toast)
- Button not wired correctly
- JavaScript error blocking execution

**Fix:**
- Check console for JavaScript errors
- Verify form validation passes
- Check button `type="submit"`

---

### Scenario 2: Handler Runs But No API Call
**Symptoms:**
- `[TwoStepTicketForm] handleFormSubmit called` appears
- `[handleTicketCreate] Called` appears
- But no `[handleTicketCreate] Making API request NOW`
- No network request

**Possible Causes:**
- Early return in `handleTicketCreate`:
  - Missing token
  - Missing title/description/priority
  - Category not found
  - Category missing backendId

**Check Logs For:**
- `[handleTicketCreate] No token, returning early`
- `[handleTicketCreate] Missing title, returning early`
- `[handleTicketCreate] Category not found in catMap`
- `[handleTicketCreate] Category missing backendId`

**Fix:**
- Check the specific error message in logs
- Ensure all required fields are filled
- Ensure categories are loaded (check `Category check BEFORE loading` log)

---

### Scenario 3: API Call Made But Returns Error
**Symptoms:**
- `[handleTicketCreate] Making API request NOW` appears
- Network request exists
- Status code: 400/401/403/404/500

**Check Network Tab:**
- **Request Payload:** Verify structure matches backend DTO
- **Response Body:** Check error message

**Common Errors:**

**400 Bad Request:**
- Check payload structure
- Verify `categoryId` is integer (not string)
- Verify `priority` is "Low"/"Medium"/"High"/"Critical" (not "low"/"medium"/etc)
- Check for missing required fields

**401 Unauthorized:**
- Token expired or invalid
- Check if user is logged in
- Check token in localStorage

**404 Not Found:**
- Wrong endpoint URL
- Backend route not matching
- Check Network tab for exact URL

**500 Server Error:**
- Backend exception
- Check backend server logs
- Check backend console for stack trace

---

### Scenario 4: API Returns 200 But Ticket Not Visible
**Symptoms:**
- Network request returns 200
- `[handleTicketCreate] API RESPONSE RECEIVED` appears
- Ticket ID in response
- But ticket doesn't appear in dashboard

**Check:**
1. **Response Body:** Does it include `id` field?
2. **Optimistic Update:** Check if `setTickets` is called
3. **Refresh:** Check if `refreshTickets` is called after 200ms
4. **Filters:** Check if ticket status matches dashboard filter
5. **Database:** Verify ticket exists in DB

**Fix:**
- Check `[handleTicketCreate] Invalid response from server` log
- Check `[handleTicketCreate] Refreshing tickets list` log
- Verify ticket status is "Submitted" or "Open" (not filtered out)

---

## Category Loading Issues

### Check Category Loading Logs:

```
[handleTicketCreate] Category check BEFORE loading
  - draftCategory: "hardware"
  - hasBackendIds: true/false
  - targetCategoryExists: true/false
  - targetCategoryHasBackendId: true/false
  - targetCategoryBackendId: 1 (or undefined)
```

**If `targetCategoryHasBackendId: false`:**
- Categories will be loaded from backend
- Check for: `[handleTicketCreate] Missing backendIds - loading from backend...`
- Then check: `[handleTicketCreate] Categories loaded from backend`

**If category still missing backendId after load:**
- Check backend `/api/categories` endpoint
- Verify category exists in backend
- Check slug matching (frontend slug vs backend name)

---

## Network Request Details

### Expected Request:
```
POST http://localhost:5000/api/tickets
Content-Type: application/json
Authorization: Bearer <token>

{
  "title": "string",
  "description": "string",
  "categoryId": 1,
  "subcategoryId": 1,  // optional
  "priority": "Medium",
  "dynamicFields": [...]  // optional
}
```

### Expected Response (200):
```json
{
  "id": "guid",
  "title": "string",
  "description": "string",
  "status": "Submitted",
  "categoryId": 1,
  "priority": "Medium",
  ...
}
```

---

## Backend Logs

Check backend server console for:

```
POST /api/tickets: REQUEST RECEIVED
CreateTicketAsync: ENTERED
CreateTicketAsync: SaveChangesAsync COMPLETED
CreateTicketAsync: SUCCESS
```

**If backend logs show error:**
- Check exception message
- Check stack trace
- Verify database connection
- Check if SaveChangesAsync is called

---

## Database Verification

If ticket is created but not visible, verify in database:

```sql
-- SQLite query
SELECT * FROM Tickets 
WHERE CreatedAt > datetime('now', '-1 hour')
ORDER BY CreatedAt DESC 
LIMIT 5;
```

Check:
- `Id` matches response ID
- `Status` is "Submitted" or "Open"
- `CreatedByUserId` matches current user
- `CategoryId` matches selected category

---

## Common Issues & Solutions

### Issue: "Category not found in catMap"
**Solution:**
- Ensure categories are loaded from backend
- Check category slug matches (frontend uses slugified names)
- Verify `categoriesData` prop is passed to form

### Issue: "Category missing backendId"
**Solution:**
- Categories will auto-load from backend
- If still missing, check backend `/api/categories` endpoint
- Verify category exists in backend database

### Issue: "Invalid response from server"
**Solution:**
- Check Network tab for response body
- Verify response includes `id` field
- Check backend returns proper JSON structure

### Issue: Modal closes but no ticket created
**Solution:**
- Check if error is thrown (should prevent modal close)
- Verify catch block in `handleFormSubmit` doesn't re-throw
- Check if `onClose()` is called before error

---

## Quick Checklist

- [ ] DevTools Console open
- [ ] Network tab open
- [ ] All required fields filled
- [ ] Category selected
- [ ] Priority selected
- [ ] Submit button clicked
- [ ] Console logs checked
- [ ] Network request checked
- [ ] Response status checked
- [ ] Backend logs checked (if available)

---

## Still Not Working?

1. **Share Console Logs:**
   - Copy all logs starting with `[TwoStepTicketForm]` and `[handleTicketCreate]`
   - Include any error messages

2. **Share Network Request:**
   - Screenshot or copy of request payload
   - Screenshot or copy of response body
   - Status code

3. **Share Backend Logs:**
   - Any logs from backend server console
   - Exception messages if any

4. **Check:**
   - Backend server is running
   - Backend is accessible from browser
   - CORS is configured correctly
   - Token is valid and not expired
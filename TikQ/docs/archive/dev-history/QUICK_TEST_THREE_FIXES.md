# Quick Test - Three Fixes

## Prerequisites
- ✅ Backend running: `cd backend/Ticketing.Backend && dotnet run`
- ✅ Frontend running: `cd frontend && npm run dev`
- ✅ Browser console open (F12)

---

## Test 1: React Key Warnings (30 seconds)

### Steps
1. Navigate to supervisor management page
2. Click on any technician to view details
3. Check console for warnings

### Expected ✅
```
(No warnings)
```

### NOT Expected ❌
```
Warning: Each child in a list should have a unique "key" prop.
```

---

## Test 2: loadTickets Auth Errors (1 minute)

### Steps
1. Navigate to home page
2. Watch console during ticket load
3. If you have auth issues, check the log

### Expected ✅ (If auth fails)
```javascript
[loadTickets] Authentication error - stopping message fetches {
  status: 401,
  message: "Unauthorized"
}
```

**Single log**, not repeated 20+ times

### Expected ✅ (If auth succeeds)
```javascript
[loadTickets] Received 15 tickets
```

No message errors

---

## Test 3: SignalR Connection (2 minutes)

### Steps
1. Open browser console
2. Refresh page
3. Watch for SignalR logs

### Expected ✅ (Success)
```javascript
[SignalR] Initializing connection: {
  hubUrl: "http://localhost:5000/hubs/tickets",
  hasToken: true,
  tokenLength: 234
}

[SignalR] Successfully connected to http://localhost:5000/hubs/tickets {
  state: "Connected",
  transport: "WebSockets"
}
```

### Expected ✅ (If 1006 error)
```javascript
[SignalR] Connection closed with error: {
  message: "WebSocket closed with status code: 1006",
  ...
}

[SignalR] WebSocket error details: {
  error: "WebSocket closed with status code: 1006",
  suggestion: "Check: 1) Backend is running, 2) CORS allows credentials, 3) Token is valid, 4) /hubs/tickets route exists"
}
```

**With helpful diagnostic info**

### Verify in DevTools Network Tab
1. Open Network tab
2. Filter by "WS"
3. Should see: `ws://localhost:5000/hubs/tickets`
4. Status: `101 Switching Protocols` ✅

---

## Quick Checklist

After running all tests:

- [ ] No React key warnings
- [ ] loadTickets either succeeds OR shows single auth error (not spam)
- [ ] SignalR logs show connection details (hubUrl, hasToken, transport)
- [ ] If SignalR fails, error includes diagnostic suggestions
- [ ] WebSocket connection visible in Network tab (if successful)

---

## Troubleshooting

### Issue: Still seeing key warnings
**Fix**: Hard reload (Ctrl+Shift+R)

### Issue: loadTickets still spams
**Check**: Are the errors 401/403? If not, they should still log (but deduplicated)

### Issue: SignalR 1006
**Check console for**:
- `hasToken: false` → Login first
- `hubUrl: undefined` → Check NEXT_PUBLIC_API_BASE_URL
- No initialization log → Frontend not loading hook

**Check backend**:
```powershell
curl http://localhost:5000/api/health
# Should return 200 OK
```

---

## Success Criteria

✅ All three issues fixed:
1. No React warnings
2. Auth errors don't spam
3. SignalR has detailed diagnostics

**Time to test**: ~3 minutes total

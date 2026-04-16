# Quick Test Guide - Supervisor Page Fix

## What Was Fixed

1. ✅ **Empty page** - Now shows loading/error/empty/success states
2. ✅ **Console spam** - Errors logged max once per 5 seconds
3. ✅ **Empty error `{}`** - Now shows full status/body/message
4. ✅ **Repeated API calls** - Now loads once when token is ready

## Quick Test (2 minutes)

### Step 1: Start Services
```powershell
# Terminal 1: Backend
.\tools\run-backend.ps1

# Terminal 2: Frontend
cd frontend
npm run dev
```

### Step 2: Open Browser
1. Navigate to http://localhost:3000
2. Open DevTools (F12) → Console tab
3. Clear console (Ctrl+L)

### Step 3: Login & Navigate
1. Login with any account
2. Navigate to supervisor page (مدیریت تکنسین‌ها)

### Step 4: Check Results

#### ✅ PASS: Page Shows Content
You should see ONE of these (not blank):
- "در حال بارگذاری..." (Loading)
- Error message with status code + retry button
- "تکنسینی یافت نشد" (Empty state)
- List of technicians (Success)

#### ✅ PASS: Console is Clean
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 200 OK
```
OR (if error):
```
[apiRequest] GET http://localhost:5000/api/supervisor/technicians → 401 Unauthorized
[apiRequest] ERROR GET http://localhost:5000/api/supervisor/technicians {
  status: 401,
  statusText: "Unauthorized",
  body: { message: "..." },
  ...
}
```

#### ❌ FAIL: Old Behavior
```
[apiRequest] ERROR GET ... {}  ← Empty object
[apiRequest] ERROR GET ... {}  ← Repeated spam
[apiRequest] ERROR GET ... {}
```

## If Test Fails

### Page Still Blank?
```powershell
# Hard reload
Ctrl+Shift+R

# Or restart frontend
cd frontend
npm run dev
```

### Still Getting 401?
```sql
-- Make user a supervisor
UPDATE Technicians 
SET IsSupervisor = 1 
WHERE UserId = (SELECT Id FROM Users WHERE Email = 'your-email@example.com')
```

### Still Seeing `{}`?
1. Check you're using updated code
2. Hard reload browser (Ctrl+Shift+R)
3. Clear browser cache

## Expected UI States

### Loading
```
┌─────────────────────────┐
│ مدیریت تکنسین‌ها        │
├─────────────────────────┤
│  در حال بارگذاری...    │
└─────────────────────────┘
```

### Error
```
┌─────────────────────────┐
│ مدیریت تکنسین‌ها        │
├─────────────────────────┤
│  خطا (401): ...        │
│  [تلاش مجدد]           │
└─────────────────────────┘
```

### Empty
```
┌─────────────────────────┐
│ مدیریت تکنسین‌ها        │
├─────────────────────────┤
│  تکنسینی یافت نشد       │
│  [افزودن اولین تکنسین] │
└─────────────────────────┘
```

### Success
```
┌─────────────────────────┐
│ مدیریت تکنسین‌ها        │
├─────────────────────────┤
│ 3 تکنسین تحت مدیریت     │
│ ┌─────────────────────┐ │
│ │ احمد رضایی    75%  │ │
│ └─────────────────────┘ │
│ ┌─────────────────────┐ │
│ │ فاطمه محمدی   50%  │ │
│ └─────────────────────┘ │
└─────────────────────────┘
```

## Success Criteria

- [ ] Page is NOT blank
- [ ] Shows one of: loading/error/empty/success
- [ ] Console shows request/response
- [ ] Error includes status/body (not `{}`)
- [ ] No repeated console spam
- [ ] Retry button works (if error)

## Done!

If all checks pass, the fix is working correctly.

For detailed documentation, see:
- `SUPERVISOR_PAGE_FIX_COMPLETE.md` - Full technical details
- `SUPERVISOR_API_ROOT_CAUSE_FIX.md` - Previous API fixes

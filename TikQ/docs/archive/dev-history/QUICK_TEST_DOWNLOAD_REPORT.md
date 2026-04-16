# Quick Test - Download Report Fix

## Prerequisites
- ✅ Backend running: `cd backend/Ticketing.Backend && dotnet run`
- ✅ Frontend running: `cd frontend && npm run dev`
- ✅ Logged in as supervisor
- ✅ At least one linked technician

---

## Test (1 minute)

### Steps
1. Navigate to Supervisor Management page
2. Click on any technician to view details
3. Click "دانلود گزارش" button
4. Check browser downloads folder

### Expected ✅

**Button Behavior**:
- Shows "در حال دانلود..." while downloading
- Disabled during download
- Returns to "دانلود گزارش" after complete

**Download**:
- CSV file downloads automatically
- Filename format: `technician-report-TechnicianName-2024-01-15T12-30-45.csv`

**Toast Notification**:
```
گزارش دانلود شد
فایل technician-report-علی احمدی-2024-01-15T12-30-45.csv با موفقیت دانلود شد
```

**Console**:
```javascript
[handleDownloadReport] Downloading report for technician: {
  technicianUserId: "...",
  technicianName: "علی احمدی"
}

[handleDownloadReport] Report downloaded successfully: technician-report-علی احمدی-2024-01-15T12-30-45.csv
```

### NOT Expected ❌
```
❌ 404 Not Found
❌ Button stays enabled during download
❌ No file downloads
❌ No feedback to user
```

---

## Verify CSV Content

### Open Downloaded File

**Expected Columns**:
```
Ticket ID | Title | Status | Client | Created At | Updated At | Type
```

**Expected Data**:
- Active tickets with `Type = Active`
- Archive tickets with `Type = Archive`
- Proper CSV formatting (commas escaped, quotes handled)

---

## Backend Test (Optional)

### Test with curl

```powershell
# 1. Get token from browser console
# localStorage.getItem('ticketing.auth.token')

# 2. Get technician ID from supervisor page

# 3. Test download
$token = "YOUR_TOKEN"
$techId = "TECHNICIAN_GUID"

curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=csv" `
  -o test-report.csv
```

**Expected Output**:
```
< HTTP/1.1 200 OK
< Content-Type: text/csv
< Content-Disposition: attachment; filename="technician-report-{techId}.csv"

Ticket ID,Title,Status,Client,Created At,Updated At,Type
...
```

**Check File**:
```powershell
cat test-report.csv
```

---

## Error Tests

### Test 1: Invalid Technician ID

**Steps**:
1. Modify code temporarily to use invalid GUID
2. Click download button

**Expected**:
- Toast: "خطا در دریافت گزارش"
- Description: "Technician not found or not managed by this supervisor"
- Console logs error details

### Test 2: No Auth Token (Backend Test)

```powershell
curl -v "http://localhost:5000/api/supervisor/technicians/$techId/report"
```

**Expected**: `401 Unauthorized`

---

## Success Checklist

- [ ] Button shows loading state during download
- [ ] CSV file downloads with correct filename
- [ ] Success toast appears with filename
- [ ] Console logs download info
- [ ] CSV contains both active and archive tickets
- [ ] CSV has "Type" column
- [ ] No 404 errors
- [ ] Error handling works (shows backend message)

**Time to test**: ~1 minute

---

## Troubleshooting

### Issue: Still getting 404
**Check**:
1. Backend is running on port 5000
2. Console shows correct URL: `http://localhost:5000/api/...`
3. Hard reload frontend (Ctrl+Shift+R)

**Debug**:
```powershell
# Test backend directly
curl http://localhost:5000/api/health
# Should return 200 OK
```

### Issue: File downloads but is empty
**Check**:
1. Technician has tickets (active or archive)
2. Backend logs for errors
3. Open CSV to verify format

### Issue: Button doesn't show loading
**Solution**: Hard reload (Ctrl+Shift+R) to get latest code

---

## What Was Fixed

**Before**: 
- Relative URL `/api/...` → 404 Not Found
- No loading state
- No success feedback

**After**:
- Full URL `http://localhost:5000/api/...` → 200 OK
- Loading state with disabled button
- Success toast with filename
- Enhanced CSV with both active and archive tickets

# Quick Start - Persian Reports Testing (2 minutes)

## ✅ Implementation Complete

All report downloads now use **FULL Persian (fa-IR)**:
- Persian calendar: ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲
- Persian digits: ۰۱۲۳۴۵۶۷۸۹
- Persian headers: شناسه تیکت, عنوان, وضعیت
- Persian labels: در حال انجام, حل شده, فوری

---

## Prerequisites

1. ✅ Backend running: `cd backend/Ticketing.Backend && dotnet run`
2. ✅ Frontend running: `cd frontend && npm run dev`
3. ✅ Logged in as supervisor or admin

---

## Test 1: Supervisor Technician Report (30 seconds)

### Steps

1. Navigate to: `http://localhost:3000`
2. Login as **supervisor**
3. Click **"مدیریت سرپرست"** (Supervisor Management)
4. Click on any technician to view details
5. Click **"دانلود گزارش"** button

### Expected ✅

**Button**:
- Shows "در حال دانلود..." while downloading
- Disabled during download

**Download**:
- CSV file downloads: `technician-report-TechName-20240115-1530.csv`
- Toast: "گزارش دانلود شد"

**Open CSV in Excel**:
```csv
شناسه تیکت,عنوان,وضعیت,نام مشتری,تاریخ ایجاد,آخرین بروزرسانی,نوع
guid-1,"رفع باگ","در حال انجام","علی احمدی","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۱۴۰۳/۱۱/۱۳ ۱۰:۳۰","فعال"
```

**Verify**:
- ✅ Headers in Persian
- ✅ Dates in Persian calendar (۱۴۰۳/۱۱/۱۲)
- ✅ Digits in Persian (۰۱۲۳)
- ✅ Status in Persian (در حال انجام)

---

## Test 2: Admin Basic Report (30 seconds)

### Steps

1. Login as **admin**
2. Navigate to **"گزارش‌گیری"** (Reports) page
3. Select **"گزارش پایه"** card
4. Choose range: "یک ماه اخیر"
5. Click **"دانلود گزارش"**

### Expected ✅

**Download**:
- CSV downloads: `basic_report_20240101_20240131.csv`
- Toast: "گزارش پایه دانلود شد"

**Open CSV in Excel**:
```csv
شناسه تیکت,عنوان,وضعیت,اولویت,دسته‌بندی,زیردسته,نام مشتری,ایمیل مشتری,تکنسین‌ها,تاریخ ایجاد,آخرین بروزرسانی
guid-1,"مشکل","در حال انجام","زیاد","فنی","نرم‌افزار","محمد","email@...","علی | حسن","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۱۴۰۳/۱۱/۱۳ ۱۰:۳۰"
```

**Verify**:
- ✅ All headers Persian
- ✅ Status: "در حال انجام", "حل شده"
- ✅ Priority: "زیاد", "فوری", "متوسط", "کم"
- ✅ Dates in Persian calendar
- ✅ All digits Persian

---

## Test 3: Admin Analytic Report (30 seconds)

### Steps

1. On Reports page, select **"گزارش تحلیلی"** card
2. Choose range
3. Click **"دانلود گزارش"**

### Expected ✅

**Download**:
- ZIP downloads: `analytic_report_20240101_20240131.zip`
- Toast: "گزارش تحلیلی دانلود شد"

**Extract ZIP**:
- ✅ Contains 4 CSV files:
  - `tickets.csv`
  - `by_category_subcategory.csv`
  - `by_client_category_subcategory.csv`
  - `status_transitions.csv`

**Open each CSV**:
- ✅ All have Persian headers
- ✅ All dates in Persian calendar
- ✅ All digits in Persian
- ✅ All status labels in Persian

---

## Test 4: Backend Direct Test (Optional)

### Get Token

```javascript
// In browser console (F12)
localStorage.getItem('ticketing.auth.token')
```

### Test with curl

```powershell
$token = "YOUR_TOKEN_HERE"
$techId = "TECHNICIAN_GUID"

# Download supervisor report
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=csv" `
  -o test-report.csv

# Check content
cat test-report.csv
```

### Expected Response

```
< HTTP/1.1 200 OK
< Content-Type: text/csv; charset=utf-8
< Content-Disposition: attachment; filename="technician-report-TechName-20240115-1530.csv"

شناسه تیکت,عنوان,وضعیت,نام مشتری,تاریخ ایجاد,آخرین بروزرسانی,نوع
...
```

---

## Test 5: Authorization Tests (Optional)

### No Token (should return 401)

```powershell
curl -v "http://localhost:5000/api/supervisor/technicians/$techId/report"
# Expected: HTTP/1.1 401 Unauthorized
```

### Invalid Format (should return 400)

```powershell
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=pdf"
# Expected: HTTP/1.1 400 Bad Request
# Body: {"message":"فرمت پشتیبانی نمی‌شود. از 'csv' استفاده کنید."}
```

---

## Automated Testing

### Run Full Test Suite

```powershell
# Get token from browser console first
$token = "YOUR_TOKEN"
$techId = "TECHNICIAN_GUID"

# Run tests
.\tools\test-persian-reports.ps1 -Token $token -TechnicianUserId $techId
```

### Expected Output

```
✅ Supervisor Report (CSV): PASS (HTTP 200)
✅ Admin Basic Report (CSV): PASS (HTTP 200)
✅ Admin Analytic Report (ZIP): PASS (HTTP 200)
✅ Contains Persian digits (۰-۹)
✅ Contains Persian headers
✅ Found 4 CSV files in ZIP
Total: 3 / 3 tests passed
```

---

## Checklist

After testing, verify:

- [ ] Supervisor report downloads CSV
- [ ] Admin basic report downloads CSV
- [ ] Admin analytic report downloads ZIP with 4 CSVs
- [ ] All headers are Persian (شناسه تیکت, عنوان, etc.)
- [ ] All dates are Persian calendar (۱۴۰۳/۱۱/۱۲)
- [ ] All digits are Persian (۰۱۲۳۴۵۶۷۸۹)
- [ ] All status labels are Persian (در حال انجام, حل شده)
- [ ] All priority labels are Persian (کم, متوسط, زیاد, فوری)
- [ ] CSVs open correctly in Excel with RTL
- [ ] Loading states work (button shows "در حال دانلود...")
- [ ] Success toasts show in Persian
- [ ] Error toasts show Persian messages
- [ ] 401 returned when no token
- [ ] 400 returned for unsupported format

---

## Troubleshooting

### Issue: CSV shows mojibake (garbled text) in Excel

**Solution**: 
1. File is saved with UTF-8 BOM (already implemented)
2. If still garbled, import manually:
   - Excel → Data → From Text
   - Choose UTF-8 encoding
   - Delimiter: Comma

### Issue: Dates still show as Gregorian

**Check**:
```powershell
cat technician-report.csv | Select-String "۱۴۰۳"
```

If no results, backend may not have restarted. Restart:
```powershell
cd backend/Ticketing.Backend
dotnet run
```

### Issue: Button doesn't show loading state

**Solution**: Hard reload frontend (Ctrl+Shift+R)

### Issue: 404 error

**Check**:
1. Backend is running on port 5000
2. Frontend is calling correct base URL
3. Route exists in SupervisorController

**Debug**:
```powershell
curl http://localhost:5000/api/health
# Should return 200 OK
```

---

## Summary

**What changed**:
- ✅ Created `PersianFormat` utility class
- ✅ Updated all CSV generation to use Persian calendar and digits
- ✅ Updated all headers to Persian
- ✅ Updated all status/priority labels to Persian
- ✅ Added UTF-8 BOM encoding
- ✅ Fixed frontend to use correct API base URL
- ✅ Added loading states and Persian error messages

**What to expect**:
- All downloaded reports are FULLY Persian
- Dates: ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲ (not 2024-01-15 15:02)
- Digits: ۰۱۲۳۴۵۶۷۸۹ (not 0123456789)
- Headers: شناسه تیکت, عنوان, وضعیت (not Ticket ID, Title, Status)
- Status: در حال انجام, حل شده (not InProgress, Solved)

**Testing time**: ~2 minutes for all 3 reports

---

## Documentation

For complete details, see:
- `REPORTS_FA_IR.md` - Full implementation guide (600 lines)
- `PERSIAN_REPORTS_IMPLEMENTATION_SUMMARY.md` - Code reference
- `tools/test-persian-reports.ps1` - Automated testing script

**Ready to test! 🚀**

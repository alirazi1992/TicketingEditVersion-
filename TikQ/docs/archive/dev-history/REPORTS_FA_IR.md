# Persian (fa-IR) Reporting Implementation - COMPLETE

## Overview

All report downloads now use **FULL Persian localization**:
- ✅ Persian calendar dates (۱۴۰۳/۱۱/۱۲ instead of 2024-01-15)
- ✅ Persian digits (۰۱۲۳۴۵۶۷۸۹ instead of 0123456789)
- ✅ Persian headers in CSV files
- ✅ Persian status and priority labels
- ✅ UTF-8 with BOM encoding for proper Excel display

---

## Backend Implementation

### 1. Persian Formatting Utility

**File**: `backend/Ticketing.Backend/Application/Common/PersianFormat.cs`

A centralized utility class that provides:

```csharp
// Convert DateTime to Persian calendar with Persian digits
PersianFormat.ToPersianDateTime(DateTime dt) 
// → "۱۴۰۳/۱۱/۱۲ ۱۵:۰۲"

PersianFormat.ToPersianDate(DateTime dt)
// → "۱۴۰۳/۱۱/۱۲"

// Convert English digits to Persian
PersianFormat.ToPersianDigits(string input)
// "123" → "۱۲۳"

PersianFormat.ToPersianDigits(int number)
// 123 → "۱۲۳"

// Status translations
PersianFormat.GetPersianStatus(TicketStatus status)
// Pending → "در انتظار"
// InProgress → "در حال انجام"
// Solved → "حل شده"
// etc.

PersianFormat.GetPersianPriority(TicketPriority priority)
// Low → "کم"
// Medium → "متوسط"
// High → "زیاد"
// Urgent → "فوری"

// CSV helpers
PersianFormat.EscapeCsv(string value)
// Properly escapes commas, quotes, Persian text

PersianFormat.GetCsvEncoding()
// Returns UTF-8 with BOM for Excel compatibility

PersianFormat.SafeFileName(string name)
// Creates filesystem-safe filenames
```

**Key Features**:
- Uses `PersianCalendar` class for accurate Persian dates
- Converts all digits 0-9 to ۰-۹
- Handles nullable DateTime gracefully
- Ensures CSV values are properly escaped for Persian text
- Always includes BOM in UTF-8 encoding for Excel

---

### 2. Supervisor Technician Report

**Endpoint**: `GET /api/supervisor/technicians/{technicianUserId}/report?format=csv`

**Authorization**: Requires supervisor role

**File**: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`

#### CSV Format (Persian)

```csv
شناسه تیکت,عنوان,وضعیت,نام مشتری,تاریخ ایجاد,آخرین بروزرسانی,نوع
guid-1,"رفع باگ","در حال انجام","علی احمدی","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۱۴۰۳/۱۱/۱۳ ۱۰:۳۰","فعال"
guid-2,"بروزرسانی مستندات","حل شده","فاطمه کریمی","۱۴۰۳/۱۱/۰۵ ۰۹:۱۵","۱۴۰۳/۱۱/۰۸ ۱۶:۴۵","آرشیو"
```

**Headers (Persian)**:
- شناسه تیکت (Ticket ID)
- عنوان (Title)
- وضعیت (Status)
- نام مشتری (Client Name)
- تاریخ ایجاد (Created At)
- آخرین بروزرسانی (Updated At)
- نوع (Type: فعال/آرشیو)

**Filename**: `technician-report-{TechName}-{yyyyMMdd-HHmm}.csv`

**Implementation**:
```csharp
[HttpGet("technicians/{technicianUserId}/report")]
public async Task<ActionResult> GetTechnicianReport(Guid technicianUserId, [FromQuery] string format = "csv")
{
    // ... authorization and data fetching ...
    
    var csv = GeneratePersianCsvReport(summary);
    var bytes = PersianFormat.GetCsvEncoding().GetBytes(csv);
    
    var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
    var techName = PersianFormat.SafeFileName(summary.TechnicianName);
    var fileName = $"technician-report-{techName}-{timestamp}.csv";
    
    return File(bytes, "text/csv; charset=utf-8", fileName);
}
```

---

### 3. Admin Basic Report

**Endpoint**: `GET /api/admin/reports/basic?range=1m&format=csv`

**Authorization**: Requires admin role

**File**: `backend/Ticketing.Backend/Application/Services/ReportService.cs`

#### CSV Format (Persian)

```csv
شناسه تیکت,عنوان,وضعیت,اولویت,دسته‌بندی,زیردسته,نام مشتری,ایمیل مشتری,تکنسین‌ها,تاریخ ایجاد,آخرین بروزرسانی
guid-1,"مشکل ورود","در حال انجام","زیاد","فنی","نرم‌افزار","محمد رضایی","m.rezaei@example.com","علی احمدی | حسن محمدی","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۱۴۰۳/۱۱/۱۳ ۱۰:۳۰"
```

**Headers (Persian)**:
- شناسه تیکت (Ticket ID)
- عنوان (Title)
- وضعیت (Status)
- اولویت (Priority)
- دسته‌بندی (Category)
- زیردسته (Subcategory)
- نام مشتری (Client Name)
- ایمیل مشتری (Client Email)
- تکنسین‌ها (Technicians)
- تاریخ ایجاد (Created At)
- آخرین بروزرسانی (Latest Update)

**Filename**: `basic_report_{startDate}_{endDate}.csv`

---

### 4. Admin Analytic Report

**Endpoint**: `GET /api/admin/reports/analytic?range=1m&format=zip`

**Authorization**: Requires admin role

**File**: `backend/Ticketing.Backend/Application/Services/ReportService.cs`

Returns a **ZIP file** containing 4 CSV files, all with Persian formatting:

#### 4.1 tickets.csv

```csv
شناسه تیکت,عنوان,نام مشتری,شناسه مشتری,دسته‌بندی,زیردسته,تاریخ ایجاد,تاریخ حل,آخرین وضعیت,تکنسین‌ها,زمان حل (دقیقه)
guid-1,"رفع باگ","علی احمدی",guid-user,"فنی","نرم‌افزار","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۱۴۰۳/۱۱/۱۳ ۱۰:۳۰","حل شده","محمد رضایی","۱۲۳۰"
```

#### 4.2 by_category_subcategory.csv

```csv
دسته‌بندی,زیردسته,تعداد تیکت
"فنی","نرم‌افزار","۱۵"
"فنی","سخت‌افزار","۸"
"مالی","صورتحساب","۱۲"
```

#### 4.3 by_client_category_subcategory.csv

```csv
نام مشتری,شناسه مشتری,ایمیل مشتری,دسته‌بندی,زیردسته,تعداد تیکت
"علی احمدی",guid-1,"ali@example.com","فنی","نرم‌افزار","۵"
```

#### 4.4 status_transitions.csv

```csv
شناسه تیکت,وضعیت قبلی,وضعیت جدید,زمان تغییر,مدت زمان از قبلی (دقیقه),نقش,نام کاربر
guid-1,"ایجاد شده","در انتظار","۱۴۰۳/۱۱/۱۲ ۱۵:۰۲","۰","Client","علی احمدی"
guid-1,"در انتظار","در حال انجام","۱۴۰۳/۱۱/۱۲ ۱۵:۳۰","۲۸","Technician","محمد رضایی"
```

**Filename**: `analytic_report_{startDate}_{endDate}.zip`

---

## Frontend Implementation

### 1. Supervisor Technician Report Download

**File**: `frontend/lib/supervisor-api.ts`

```typescript
export async function getSupervisorTechnicianReport(
  token: string,
  technicianUserId: string
): Promise<Blob> {
  const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5000";
  const url = `${baseUrl}/api/supervisor/technicians/${technicianUserId}/report?format=csv`;
  
  const response = await fetch(url, {
    method: "GET",
    headers: {
      Authorization: `Bearer ${token}`,
      "Content-Type": "application/json",
    },
  });

  if (!response.ok) {
    let errorMessage = "Failed to download report";
    try {
      const errorData = await response.json();
      errorMessage = errorData.message || errorMessage;
    } catch {
      errorMessage = `${errorMessage} (${response.status} ${response.statusText})`;
    }
    throw new Error(errorMessage);
  }

  return await response.blob();
}
```

**Component**: `frontend/components/supervisor-technician-management.tsx`

```typescript
const handleDownloadReport = async () => {
  if (!token || !selectedTech) return;
  
  try {
    setReportLoading(true);
    
    const blob = await getSupervisorTechnicianReport(token, selectedTech.technicianUserId);
    
    // Generate filename with timestamp
    const timestamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
    const filename = `technician-report-${selectedTech.technicianName}-${timestamp}.csv`;
    
    // Download
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
    
    toast({
      title: "گزارش دانلود شد",
      description: `فایل ${filename} با موفقیت دانلود شد`,
    });
  } catch (err: any) {
    toast({
      title: "خطا در دریافت گزارش",
      description: err?.message || "لطفاً دوباره تلاش کنید",
      variant: "destructive",
    });
  } finally {
    setReportLoading(false);
  }
};
```

### 2. Admin Reports

**Files**: 
- `frontend/lib/reports-api.ts`
- `frontend/components/admin-reports.tsx`

Already implemented with:
- ✅ Correct API base URL usage
- ✅ Bearer token authentication
- ✅ Proper blob download with filename extraction
- ✅ Persian UI labels and error messages
- ✅ Loading states

---

## Testing Guide

### Test 1: Supervisor Technician Report (Quick)

**Prerequisites**:
- Backend running on port 5000
- Frontend running on port 3000
- Logged in as supervisor
- Has at least one linked technician

**Steps**:
1. Navigate to Supervisor Management
2. Click on a technician to view details
3. Click "دانلود گزارش" button

**Expected Result**:
- ✅ Button shows "در حال دانلود..." while downloading
- ✅ CSV file downloads: `technician-report-TechName-20240115-1530.csv`
- ✅ Toast: "گزارش دانلود شد"
- ✅ Open CSV in Excel:
  - Headers are in Persian: "شناسه تیکت", "عنوان", "وضعیت", etc.
  - Dates are Persian calendar: "۱۴۰۳/۱۱/۱۲ ۱۵:۰۲"
  - Status labels are Persian: "در حال انجام", "حل شده", etc.
  - Proper RTL display

### Test 2: Admin Basic Report

**Prerequisites**:
- Logged in as admin
- Navigate to Reports page

**Steps**:
1. Select "گزارش پایه" card
2. Choose range (e.g., "یک ماه اخیر")
3. Click "دانلود گزارش"

**Expected Result**:
- ✅ CSV downloads: `basic_report_20240101_20240131.csv`
- ✅ Toast: "گزارش پایه دانلود شد"
- ✅ Open CSV: All headers, dates, and status labels in Persian
- ✅ Persian digits throughout

### Test 3: Admin Analytic Report

**Steps**:
1. Select "گزارش تحلیلی" card
2. Choose range
3. Click "دانلود گزارش"

**Expected Result**:
- ✅ ZIP downloads: `analytic_report_20240101_20240131.zip`
- ✅ Toast: "گزارش تحلیلی دانلود شد"
- ✅ Extract ZIP: Contains 4 CSV files
- ✅ All CSVs have Persian headers, dates, and digits

### Test 4: Backend Direct (curl)

#### Technician Report

```powershell
# Get token from browser console
# localStorage.getItem('ticketing.auth.token')

$token = "YOUR_TOKEN"
$techId = "TECHNICIAN_USER_ID"

curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=csv" `
  -o technician-report.csv

# Check file
cat technician-report.csv
# Should show: شناسه تیکت,عنوان,وضعیت...
# Dates should be: ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲
```

**Expected Response**:
```
< HTTP/1.1 200 OK
< Content-Type: text/csv; charset=utf-8
< Content-Disposition: attachment; filename="technician-report-TechName-20240115-1530.csv"
```

#### Basic Report

```powershell
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/admin/reports/basic?range=1m&format=csv" `
  -o basic-report.csv
```

#### Analytic Report

```powershell
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/admin/reports/analytic?range=1m&format=zip" `
  -o analytic-report.zip

# Extract and check
Expand-Archive -Path analytic-report.zip -DestinationPath ./reports
cat ./reports/tickets.csv
```

### Test 5: Authorization Tests

#### No Token (401)

```powershell
curl -v "http://localhost:5000/api/supervisor/technicians/$techId/report"
# Expected: 401 Unauthorized
```

#### Wrong Role (403)

```powershell
# Use client token instead of supervisor token
curl -v -H "Authorization: Bearer $clientToken" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report"
# Expected: 403 Forbidden
```

#### Invalid Technician ID (404)

```powershell
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/00000000-0000-0000-0000-000000000000/report"
# Expected: 404 with Persian message: "تکنسین یافت نشد یا تحت مدیریت این سرپرست نیست"
```

#### Unsupported Format (400)

```powershell
curl -v -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=pdf"
# Expected: 400 with Persian message: "فرمت پشتیبانی نمی‌شود. از 'csv' استفاده کنید."
```

---

## Excel Compatibility

### UTF-8 BOM

All CSV files include UTF-8 BOM (Byte Order Mark) to ensure proper display in Excel:

```csharp
PersianFormat.GetCsvEncoding() // Returns new UTF8Encoding(true)
```

### Opening in Excel

**Method 1**: Double-click CSV file
- Excel should auto-detect UTF-8 with BOM
- Persian text displays correctly RTL

**Method 2**: Import Data (if auto-detect fails)
1. Excel → Data → Get External Data → From Text
2. Select CSV file
3. Choose "UTF-8" encoding
4. Delimiter: Comma
5. Finish

---

## Date Examples

### Persian Calendar Conversion

| Gregorian | Persian |
|-----------|---------|
| 2024-01-15 15:02 | ۱۴۰۳/۱۱/۱۲ ۱۵:۰۲ |
| 2024-02-01 09:30 | ۱۴۰۳/۱۱/۲۵ ۰۹:۳۰ |
| 2023-12-25 | ۱۴۰۲/۱۰/۰۴ |

### Digit Conversion

| English | Persian |
|---------|---------|
| 0123456789 | ۰۱۲۳۴۵۶۷۸۹ |
| 2024 | ۲۰۲۴ |
| 15:30 | ۱۵:۳۰ |

---

## Status Label Mappings

### Ticket Status

| Enum | English | Persian |
|------|---------|---------|
| Pending | Pending | در انتظار |
| InProgress | InProgress | در حال انجام |
| AwaitingInfo | AwaitingInfo | در انتظار اطلاعات |
| OnHold | OnHold | معلق |
| Solved | Solved | حل شده |
| Closed | Closed | بسته شده |
| Cancelled | Cancelled | لغو شده |
| Reopened | Reopened | بازگشایی شده |

### Priority

| Enum | English | Persian |
|------|---------|---------|
| Low | Low | کم |
| Medium | Medium | متوسط |
| High | High | زیاد |
| Urgent | Urgent | فوری |

---

## Files Changed

### Backend

1. **`backend/Ticketing.Backend/Application/Common/PersianFormat.cs`** (NEW)
   - Complete Persian formatting utility
   - Persian calendar conversion
   - Persian digit conversion
   - Status/priority translations
   - CSV escaping and BOM encoding

2. **`backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`**
   - Updated `GetTechnicianReport` to use Persian formatting
   - Persian CSV generation with `GeneratePersianCsvReport`
   - Persian error messages
   - Safe filename generation

3. **`backend/Ticketing.Backend/Application/Services/ReportService.cs`**
   - Added `using Application.Common` for PersianFormat
   - Updated `GenerateBasicReportCsvAsync` with Persian headers and formatting
   - Updated `GenerateTicketDetailsCsv` with Persian
   - Updated `GenerateCategoryCountsCsv` with Persian
   - Updated `GenerateClientFrequencyCsv` with Persian
   - Updated `GenerateStatusTransitionsCsv` with Persian
   - Updated `AddCsvToZip` to use UTF-8 with BOM

### Frontend

4. **`frontend/lib/supervisor-api.ts`**
   - Updated `getSupervisorTechnicianReport` to use full API base URL
   - Enhanced error handling with backend message extraction

5. **`frontend/components/supervisor-technician-management.tsx`**
   - Added `reportLoading` state
   - Enhanced `handleDownloadReport` with loading state, logging, timestamp
   - Updated button to show loading state

### Documentation

6. **`REPORTS_FA_IR.md`** (THIS FILE)
   - Complete implementation guide
   - Testing procedures
   - API documentation

---

## Quick Verification Checklist

After implementation, verify:

- [ ] Backend compiles without errors
- [ ] Frontend compiles without errors
- [ ] Supervisor report downloads CSV with Persian headers
- [ ] Supervisor report shows Persian calendar dates (۱۴۰۳/۱۱/۱۲)
- [ ] Supervisor report shows Persian digits (۰۱۲۳۴)
- [ ] Supervisor report shows Persian status labels (در حال انجام)
- [ ] Admin basic report downloads with Persian formatting
- [ ] Admin analytic report downloads ZIP with 4 Persian CSVs
- [ ] All CSVs open correctly in Excel with RTL
- [ ] 401 returned when no token (not 404)
- [ ] 403 returned when wrong role
- [ ] 404 returned with Persian message for invalid technician ID
- [ ] 400 returned with Persian message for unsupported format
- [ ] Loading states work in UI
- [ ] Success/error toasts show in Persian
- [ ] Filenames are safe and include timestamps

---

## Summary

✅ **Fully implemented Persian (fa-IR) reporting with:**
- Persian calendar dates (Jalali/Shamsi)
- Persian digits (۰-۹)
- Persian headers in all CSV files
- Persian status and priority labels
- UTF-8 with BOM for Excel compatibility
- Proper error messages in Persian
- Authorization checks (401/403/404)
- Frontend download with loading states
- Comprehensive testing guide

**All reports now export in FULL Persian! 🎉**

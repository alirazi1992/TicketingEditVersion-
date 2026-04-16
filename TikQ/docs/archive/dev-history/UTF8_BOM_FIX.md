# UTF-8 BOM Encoding Fix - Persian Text Display Issue

## ✅ Problem Fixed

**Issue**: Persian text in CSV reports was displaying as mojibake (garbled text):
- **Incorrect**: `Ø´Ù†Ø§Ø³Ù‡ ØªÛŒÚ©Øª`
- **Correct**: `شناسه تیکت`

**Root Cause**: CSV files were missing the UTF-8 BOM (Byte Order Mark), causing Excel and other tools to interpret the file as ISO-8859-1 or Windows-1252 instead of UTF-8.

---

## What Was Fixed

### 1. Added `GetCsvBytes()` Method

**File**: `backend/Ticketing.Backend/Application/Common/PersianFormat.cs`

```csharp
/// <summary>
/// Convert CSV string to bytes with UTF-8 BOM prepended
/// This ensures Excel and other tools properly detect Persian text encoding
/// </summary>
public static byte[] GetCsvBytes(string csvContent)
{
    var encoding = new UTF8Encoding(true); // UTF-8 with BOM
    var preamble = encoding.GetPreamble(); // BOM bytes: EF BB BF
    var contentBytes = encoding.GetBytes(csvContent);
    
    // Combine BOM + content
    var result = new byte[preamble.Length + contentBytes.Length];
    Array.Copy(preamble, 0, result, 0, preamble.Length);
    Array.Copy(contentBytes, 0, result, preamble.Length, contentBytes.Length);
    
    return result;
}
```

**Why This Works**:
- Creates UTF-8 encoding with BOM enabled
- Gets the BOM preamble bytes (`EF BB BF` in hex)
- Converts CSV content to UTF-8 bytes
- Concatenates BOM + content into a single byte array
- Excel/Notepad now auto-detects UTF-8 encoding

### 2. Updated Supervisor Report

**File**: `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs`

**Before**:
```csharp
var bytes = Application.Common.PersianFormat.GetCsvEncoding().GetBytes(csv);
```

**After**:
```csharp
var bytes = Application.Common.PersianFormat.GetCsvBytes(csv); // Now includes UTF-8 BOM
```

### 3. Updated Admin Basic Report

**File**: `backend/Ticketing.Backend/Application/Services/ReportService.cs`

**Before**:
```csharp
return PersianFormat.GetCsvEncoding().GetBytes(sb.ToString());
```

**After**:
```csharp
return PersianFormat.GetCsvBytes(sb.ToString()); // Now includes UTF-8 BOM
```

### 4. Updated Admin Analytic Report (ZIP)

**File**: `backend/Ticketing.Backend/Application/Services/ReportService.cs`

**Before**:
```csharp
private static void AddCsvToZip(ZipArchive archive, string fileName, string content)
{
    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
    using var entryStream = entry.Open();
    using var writer = new StreamWriter(entryStream, PersianFormat.GetCsvEncoding());
    writer.Write(content);
}
```

**After**:
```csharp
private static void AddCsvToZip(ZipArchive archive, string fileName, string content)
{
    var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);
    using var entryStream = entry.Open();
    
    // Write BOM + content bytes directly to ensure BOM is included
    var bytes = PersianFormat.GetCsvBytes(content);
    entryStream.Write(bytes, 0, bytes.Length);
}
```

---

## UTF-8 BOM Technical Details

### What is BOM (Byte Order Mark)?

- **BOM**: A special sequence of bytes at the start of a file that indicates encoding
- **UTF-8 BOM**: `EF BB BF` (3 bytes in hex)
- **Purpose**: Tells applications (like Excel) that the file is encoded in UTF-8

### Why Was Text Garbled?

**Without BOM**:
1. File contains UTF-8 encoded Persian text
2. Excel opens file and assumes ISO-8859-1 (default for CSV)
3. Each UTF-8 byte is interpreted as a separate character
4. Result: `Ø´Ù†Ø§Ø³Ù‡ ØªÛŒÚ©Øª` (mojibake)

**With BOM**:
1. File starts with `EF BB BF` bytes
2. Excel reads BOM and knows it's UTF-8
3. Persian text is decoded correctly
4. Result: `شناسه تیکت` ✅

---

## Testing

### Test 1: Rebuild and Run Backend

```powershell
cd backend/Ticketing.Backend
dotnet clean
dotnet build
# Expected: Build succeeded. 0 Error(s)

dotnet run
# Expected: Backend starts on http://localhost:5000
```

### Test 2: Download Supervisor Report

**Steps**:
1. Login as supervisor
2. Navigate to Supervisor Management
3. Click technician → "دانلود گزارش"
4. Download CSV file

**Verify**:
```powershell
# Check if BOM is present (first 3 bytes should be EF BB BF)
$bytes = [System.IO.File]::ReadAllBytes(".\technician-report-*.csv")
$bom = $bytes[0..2]
Write-Host "BOM bytes: $($bom -join ' ')"
# Expected: BOM bytes: 239 187 191 (which is EF BB BF in hex)

# Open in Notepad
notepad ".\technician-report-*.csv"
# Expected: Persian text displays correctly: شناسه تیکت
```

**Open in Excel**:
- Double-click CSV file
- Expected: Headers show: `شناسه تیکت`, `عنوان`, `وضعیت`
- **NOT**: `Ø´Ù†Ø§Ø³Ù‡ ØªÛŒÚ©Øª`

### Test 3: Download Admin Basic Report

**Steps**:
1. Login as admin
2. Navigate to Reports page
3. Select "گزارش پایه"
4. Click "دانلود گزارش"

**Verify**:
```powershell
# Check BOM
$bytes = [System.IO.File]::ReadAllBytes(".\basic_report_*.csv")
Write-Host "BOM present: $(($bytes[0] -eq 239) -and ($bytes[1] -eq 187) -and ($bytes[2] -eq 191))"
# Expected: BOM present: True
```

**Open in Excel**:
- Headers: `شناسه تیکت,عنوان,وضعیت,اولویت,دسته‌بندی` ✅
- Dates: `۱۴۰۳/۱۱/۱۲ ۱۵:۰۲` ✅

### Test 4: Admin Analytic Report (ZIP)

**Steps**:
1. Select "گزارش تحلیلی"
2. Download ZIP
3. Extract all CSV files

**Verify each CSV**:
```powershell
# Check all CSV files in extracted folder
Get-ChildItem ".\analytic-extracted\*.csv" | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $hasBom = ($bytes[0] -eq 239) -and ($bytes[1] -eq 187) -and ($bytes[2] -eq 191)
    Write-Host "$($_.Name): BOM = $hasBom"
}
# Expected: All show "BOM = True"
```

**Open each CSV in Excel**:
- `tickets.csv` → Headers: `شناسه تیکت,عنوان,...` ✅
- `by_category_subcategory.csv` → Headers: `دسته‌بندی,زیردسته,تعداد تیکت` ✅
- `by_client_category_subcategory.csv` → Headers: `نام مشتری,شناسه مشتری,...` ✅
- `status_transitions.csv` → Headers: `شناسه تیکت,وضعیت قبلی,...` ✅

---

## Before & After Comparison

### Before Fix (Mojibake)

**Excel Display**:
```
Ø´Ù†Ø§Ø³Ù‡ ØªÛŒÚ©Øª,Ø¹Ù†ÙˆØ§Ù†,ÙˆØ¶Ø¹ÛŒØª
guid-1,"Ø±ÙØ¹ Ø¨Ø§Ú¯","Ø¯Ø± Ø­Ø§Ù„ Ø§Ù†Ø¬Ø§Ù…"
```

**Hex View** (first bytes):
```
D8 B4 D9 86 D8 A7 D8 B3 D9 87 20 D8 AA DB 8C DA A9 D8 AA
(UTF-8 bytes without BOM)
```

### After Fix (Correct)

**Excel Display**:
```
شناسه تیکت,عنوان,وضعیت
guid-1,"رفع باگ","در حال انجام"
```

**Hex View** (first bytes):
```
EF BB BF D8 B4 D9 86 D8 A7 D8 B3 D9 87 20 D8 AA DB 8C DA A9 D8 AA
(BOM + UTF-8 bytes)
```

---

## If Issue Persists

### Excel Still Shows Mojibake?

**Solution 1**: Hard reload browser cache
```
Ctrl + Shift + R (or Cmd + Shift + R on Mac)
```

**Solution 2**: Clear browser downloads and re-download

**Solution 3**: Import manually in Excel
1. Excel → Data → Get External Data → From Text
2. Choose file
3. Select encoding: **UTF-8**
4. Delimiter: Comma
5. Finish

### Check if BOM is Actually Present

```powershell
# PowerShell script to verify BOM
function Test-UTF8BOM {
    param([string]$FilePath)
    
    if (-not (Test-Path $FilePath)) {
        Write-Host "File not found: $FilePath" -ForegroundColor Red
        return
    }
    
    $bytes = [System.IO.File]::ReadAllBytes($FilePath)
    
    if ($bytes.Length -lt 3) {
        Write-Host "File too small" -ForegroundColor Red
        return
    }
    
    $hasBom = ($bytes[0] -eq 0xEF) -and ($bytes[1] -eq 0xBB) -and ($bytes[2] -eq 0xBF)
    
    if ($hasBom) {
        Write-Host "✅ UTF-8 BOM present: EF BB BF" -ForegroundColor Green
    } else {
        Write-Host "❌ UTF-8 BOM missing! First bytes: $($bytes[0..2] -join ' ')" -ForegroundColor Red
    }
    
    # Show first line of content
    $content = [System.IO.File]::ReadAllText($FilePath, [System.Text.Encoding]::UTF8)
    $firstLine = ($content -split "`n")[0]
    Write-Host "First line: $firstLine"
}

# Test your CSV
Test-UTF8BOM ".\technician-report-*.csv"
```

### Backend Not Using New Code?

Ensure backend was restarted after changes:
```powershell
# Stop backend (Ctrl+C if running)
# Rebuild and run
cd backend/Ticketing.Backend
dotnet clean
dotnet build
dotnet run
```

---

## Summary

✅ **Fixed UTF-8 BOM issue** by creating `GetCsvBytes()` method
✅ **Updated all CSV generation** to include BOM (supervisor, admin basic, admin analytic)
✅ **Persian text now displays correctly** in Excel, Notepad, and other tools
✅ **No more mojibake** (Ø´Ù†Ø§Ø³Ù‡ → شناسه)

**Files Changed**:
1. `backend/Ticketing.Backend/Application/Common/PersianFormat.cs` - Added `GetCsvBytes()`
2. `backend/Ticketing.Backend/Api/Controllers/SupervisorController.cs` - Use `GetCsvBytes()`
3. `backend/Ticketing.Backend/Application/Services/ReportService.cs` - Use `GetCsvBytes()` for basic and analytic reports

**All CSV files now start with UTF-8 BOM (`EF BB BF`)!** 🎉

---

## Quick Test Commands

```powershell
# 1. Rebuild backend
cd backend/Ticketing.Backend
dotnet clean && dotnet build && dotnet run

# 2. In another terminal, download a report
$token = "YOUR_TOKEN"
$techId = "TECHNICIAN_GUID"

curl -H "Authorization: Bearer $token" `
  "http://localhost:5000/api/supervisor/technicians/$techId/report?format=csv" `
  -o test-report.csv

# 3. Verify BOM
$bytes = [System.IO.File]::ReadAllBytes("test-report.csv")
Write-Host "BOM check: $(($bytes[0] -eq 239) -and ($bytes[1] -eq 187) -and ($bytes[2] -eq 191))"
# Expected: BOM check: True

# 4. Open in Excel
start test-report.csv
# Expected: Persian text displays correctly
```

**Persian text should now display perfectly!** ✅

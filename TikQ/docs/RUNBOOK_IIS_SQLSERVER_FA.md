# راهنمای تحویل TikQ — IIS و SQL Server (فارسی)

این سند راهنمای گام‌به‌گام استقرار و تحویل TikQ روی IIS با پایگاه‌داده SQL Server است.

---

## فازها و دروازه‌های تأیید (Phases & Gates)

| فاز | هدف | دروازه (GATE) |
|-----|------|----------------|
| **۱. تشخیص** | بهبود `/api/health`: provider، connection هدف (redacted)، پرچم‌های env | **GATE 1:** `/api/health` به‌وضوح provider و connection target را نشان دهد. |
| **۲. انتخاب Provider و fail-fast** | `Database__Provider` و `ConnectionStrings__DefaultConnection`؛ در Production بدون fallback، در صورت نبودن خطا | **GATE 2:** `verify-prod.ps1` با تنظیم SqlServer باید PASS شود. |
| **۳. Migration runner** | AutoMigrateOnStartup با لاگ؛ در Production با SqlServer در صورت خطا fail-fast | **GATE 3:** بعد از اولین اجرا (وقتی فعال است) `pendingMigrationsCount` برابر ۰ شود. |
| **۴. deploy-iis.ps1** | تزریق همه envهای لازم (provider، connection، migrate، jwt، bootstrap)؛ ریسایکل App Pool؛ اختیاری: تأیید سلامت | **GATE 4:** اجرای deploy-iis.ps1 و سپس verify-prod.ps1 => PASS. |
| **۵. Bootstrap و verify-login** | سید فقط وقتی Enabled و users خالی؛ اسکریپت verify-login.ps1 ورود و whoami را بررسی می‌کند | **GATE 5:** users>0 و لاگین PASS. |
| **۶. کوکی و سخت‌سازی پروکسی IIS** | Forwarded headers + تنظیمات کوکی (SameSite/SecurePolicy) | **GATE 6:** login پاسخ Set-Cookie بدهد و whoami با کوکی isAuthenticated=true برگرداند. |

**اسکریپت‌های تأیید:** `tools\_handoff_tests\verify-prod.ps1` (سلامت + provider + اختیاری لاگین)، `tools\_handoff_tests\verify-login.ps1` (لاگین + whoami + کوکی).

---

## ۱. متغیرهای محیطی الزامی (حالت SQL Server)

برای اجرای بک‌اند در حالت SQL Server این متغیرها را در **IIS** (مثلاً در Application Pool → Configuration → Environment variables یا در `web.config`) تنظیم کنید.

| متغیر | الزام | توضیح |
|--------|--------|--------|
| **Database__Provider** | بله (Production) | مقدار: `SqlServer` یا `Sqlite`. در Production **حتماً** باید ست شود (بدون آن اپ استارت نمی‌شود). |
| **ConnectionStrings__DefaultConnection** | بله (وقتی Provider=SqlServer) | رشته اتصال SQL Server. اگر Provider برابر SqlServer باشد و این خالی باشد، اپلیکیشن در استارت خطا می‌دهد. |
| **Database__AutoMigrateOnStartup** | فقط اولین اجرا | برای **اولین استقرار** برابر `true` قرار دهید تا مایگریشن‌ها در استارت اعمال شوند. بعد از اولین اجرای موفق می‌توانید آن را `false` کنید. |
| **Jwt__Secret** | بله (Production) | حداقل ۳۲ کاراکتر. در Production اگر تنظیم نشود، استارت ناموفق است. |

**سایر (در صورت نیاز):**

- **Cors:AllowedOrigins** در تنظیمات یا env: مبدا(های) فرانت‌اند (مثلاً `https://your-frontend`)
- **Bootstrap (فقط اولین اجرا، وقتی جدول Users خالی است):** اگر از اسکریپت `deploy-iis.ps1` استفاده می‌کنید، متغیرهای `TikQ_BOOTSTRAP_ADMIN_PASSWORD` و `TikQ_BOOTSTRAP_ADMIN_EMAIL` را ست کنید؛ اسکریپت آن‌ها را به‌صورت `Bootstrap__Enabled`, `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` در web.config تزریق می‌کند. اختیاری: `TikQ_BOOTSTRAP_CLIENT_*`, `TikQ_BOOTSTRAP_TECH_*`, `TikQ_BOOTSTRAP_SUPERVISOR_*`

**استفاده از اسکریپت deploy-iis.ps1:** اسکریپت `backend\Ticketing.Backend\deploy-iis.ps1` متغیرهای زیر را در web.config تزریق می‌کند: `Database__Provider`, `ConnectionStrings__DefaultConnection`, `Database__AutoMigrateOnStartup`, `Jwt__Secret` (از TikQ_JWT_SECRET)، و در صورت تنظیم، `Bootstrap__Enabled`, `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` و سایر Bootstrap__*. پس از تزریق، App Pool را ریسایکل و `/api/health` را برای تأیید provider چک می‌کند.

### چک‌لیست متغیرهای محیطی

- [ ] `Database__Provider=SqlServer`
- [ ] `ConnectionStrings__DefaultConnection=...` (رشته اتصال صحیح)
- [ ] `Database__AutoMigrateOnStartup=true` (فقط برای اولین اجرا؛ اسکریپت در حالت SqlServer به‌طور پیش‌فرض true می‌گذارد)
- [ ] `Jwt__Secret=...` یا `TikQ_JWT_SECRET` (حداقل ۳۲ کاراکتر)
- [ ] در صورت استفاده از Bootstrap: `TikQ_BOOTSTRAP_ADMIN_EMAIL` و `TikQ_BOOTSTRAP_ADMIN_PASSWORD` (حداقل ۸ کاراکتر)

---

## ۲. تنظیمات IIS (سایت و Application Pool)

### ۲.۱ احراز هویت: Anonymous در برابر Windows

حالت **WindowsAuth** اپلیکیشن را با تنظیم **Anonymous** و **Windows Authentication** در IIS هماهنگ کنید:

| حالت WindowsAuth | Anonymous (IIS) | Windows Authentication (IIS) | رفتار اپ |
|-------------------|------------------|------------------------------|----------|
| **Off** | فعال | غیرفعال | فقط ورود با ایمیل/رمز؛ endpoint ویندوز ۴۰۳. |
| **Optional** | فعال | فعال (Negotiate/NTLM) | ورود با JWT یا ویندوز؛ درخواست بدون هویت ویندوز به `/api/auth/windows` با ۴۰۱ و `WWW-Authenticate: Negotiate`. |
| **Enforce** | فعال | فعال | مثل Optional؛ مسیرهای غیر auth نیاز به JWT یا ویندوز دارند. |

**نکته:** برای همه حالت‌ها **Anonymous** را فعال نگه دارید تا مسیرهای `/api/health` و `/api/auth/*` (ورود، ثبت‌نام، whoami) بدون چالش ویندوز در دسترس باشند.

### ۲.۲ ریسایکل Application Pool بعد از تزریق env

بعد از تغییر متغیرهای محیطی (مثلاً در `web.config` یا Application Pool) **حتماً** Application Pool را ریسایکل کنید تا اپ با مقادیر جدید بالا بیاید.

**چک‌لیست IIS:**

- [ ] Anonymous و Windows طبق جدول بالا برای حالت انتخاب‌شده تنظیم شده‌اند.
- [ ] بعد از هر تغییر env یا `web.config`، Application Pool ریسایکل شده (یا `iisreset` اجرا شده).
- [ ] اگر از اسکریپت `deploy-iis.ps1` استفاده می‌کنید، خود اسکریپت بعد از نوشتن `web.config` ریسایکل می‌کند؛ در غیر این صورت به‌صورت دستی ریسایکل کنید.

---

## ۳. تأیید (Verification)

### ۳.۱ اندپوینت `/api/health`

**درخواست:** `GET <آدرس-بک‌اند>/api/health` (مثلاً `http://localhost:8080/api/health`).

**فیلدهای مهم پاسخ و نحوه تفسیر:**

| فیلد | معنی |
|-------|--------|
| **ok** | `true` = سرویس و اتصال DB سالم؛ `false` = degraded. |
| **status** | `"healthy"` یا `"degraded"`. |
| **database.provider** | `"SqlServer"` یا `"Sqlite"`. برای Production با SQL Server باید `SqlServer` باشد. |
| **database.connectionInfoRedacted** | سرور و نام دیتابیس (بدون رمز). برای اطمینان از اتصال صحیح. |
| **database.canConnect** | `true` = اتصال به DB برقرار است. |
| **database.dataCounts** | `categories`, `tickets`, `users` — تعداد رکوردها. |
| **database.pendingMigrationsCount** | تعداد مایگریشن‌های اعمال‌نشده؛ بعد از اولین اجرا با AutoMigrateOnStartup باید ۰ باشد (GATE 3). |
| **database.lastMigrationId** | شناسه آخرین مایگریشن اعمال‌شده (بدون رمز). |
| **database.error** | در صورت خطا، پیام (بدون رمز). |
| **auth.windowsAuthEnabled** | آیا Windows Auth فعال است. |
| **auth.windowsAuthMode** | `"Off"` \| `"Optional"` \| `"Enforce"`. |
| **process.identity** | هویت فرایند (کاربر و در صورت وجود WindowsIdentity). |
| **effectiveEnvVarsPresent** | وجود envهای کلیدی (بدون مقدار): `Jwt__Secret`, `ConnectionStrings__DefaultConnection`, `Database__Provider`, `Database__AutoMigrateOnStartup`, `Bootstrap__Enabled`, `CompanyDirectory__Enabled`, `AuthCookies__SecurePolicy`, `AuthCookies__SameSite`. |
| **environment** | نام محیط (مثلاً `Production`). |

اگر `database.provider` برابر `Sqlite` باشد در حالی که انتظار SQL Server دارید، یعنی env تزریق نشده یا Provider اشتباه است.

### ۳.۲ اسکریپت `verify-prod.ps1`

**مسیر:** `tools\_handoff_tests\verify-prod.ps1`

**مراحل استفاده:**

1. **فقط سلامت و تأیید Provider (مثلاً SqlServer):**
   ```powershell
   .\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer
   ```
2. **تست ورود + whoami (با حساب bootstrap یا سید):**
   ```powershell
   .\tools\_handoff_tests\verify-prod.ps1 -BaseUrl "http://localhost:8080" -ExpectProvider SqlServer -LoginEmail "admin@local" -LoginPassword "YourPassword"
   ```

**خروجی:** برای هر بخش HEALTH و در صورت درخواست LOGIN مقدار PASS/FAIL چاپ می‌شود. خروجی نهایی OVERALL: PASS یا FAIL. کد خروج ۰ = موفق، ۱ = ناموفق.

### ۳.۳ اسکریپت `verify-login.ps1`

**مسیر:** `tools\_handoff_tests\verify-login.ps1`

برای تأیید لاگین و کوکی (GATE 5 و 6): پس از bootstrap، با همان ایمیل/رمز سید شده لاگین کنید و whoami با کوکی را چک کنید.

```powershell
.\tools\_handoff_tests\verify-login.ps1 -BaseUrl "http://localhost:8080" -LoginEmail "admin@local" -LoginPassword "YourPassword"
```

یا با env: `TikQ_LOGIN_EMAIL` و `TikQ_LOGIN_PASSWORD`. اسکریپت Set-Cookie را در پاسخ login و سپس isAuthenticated=true در whoami بررسی می‌کند.

**چک‌لیست تأیید:**

- [ ] `GET /api/health` برمی‌گرداند ۲۰۰ و `database.provider` برابر `SqlServer` است.
- [ ] `effectiveEnvVarsPresent` برای متغیرهای لازم `true` است.
- [ ] `verify-prod.ps1 -ExpectProvider SqlServer` نتیجه PASS می‌دهد.
- [ ] در صورت نیاز، تست ورود با `-LoginEmail` و `-LoginPassword` هم PASS است.

---

## ۴. خرابی‌های رایج و رفع آن‌ها

### ۴.۱ هنوز از SQLite استفاده می‌شود (Provider اشتباه / env تزریق نشده)

**علائم:** در `/api/health` مقدار `database.provider` برابر `Sqlite` است یا اتصال به فایل محلی است.

**اقدامات:**

1. مطمئن شوید متغیرهای محیطی برای **Process** یا **Application Pool** تنظیم شده‌اند (نه فقط در یک اسکریپت موقت).
2. اگر از `web.config` استفاده می‌کنید، بلوک `environmentVariables` داخل `<configuration><system.webServer><aspNetCore>` را بررسی کنید و پس از تغییر، Application Pool را ریسایکل کنید.
3. اگر از `deploy-iis.ps1` استفاده می‌کنید، متغیرها را قبل از اجرای اسکریپت در Machine/Process/User ست کنید تا در `web.config` نوشته شوند.
4. یک بار IIS را ریست کنید (`iisreset`) و دوباره `/api/health` را چک کنید.

**چک‌لیست:**

- [ ] `Database__Provider` و `ConnectionStrings__DefaultConnection` در env اپ (App Pool یا web.config) ست شده‌اند.
- [ ] بعد از تغییر، App Pool ریسایکل شده است.

---

### ۴.۲ مایگریشن ناموفق (خطای دسترسی / Permissions)

**علائم:** لاگ استارت حاوی خطای مایگریشن EF است یا اپ اصلاً بالا نمی‌آید.

**اقدامات:**

1. اطمینان از دسترسی اتصال: کاربر/App Pool identity به SQL Server و دیتابیس موردنظر دسترسی داشته باشد و بتواند جدول بسازد/تغییر دهد (مثلاً نقش `db_owner` یا `db_ddladmin` + `db_datawriter`).
2. اگر `Database__AutoMigrateOnStartup=true` است، همان identity اجرای اپ باید این دسترسیها را داشته باشد.
3. در صورت نیاز مایگریشن را دستی اجرا کنید و بعد اپ را استارت کنید:
   ```bash
   dotnet ef database update --project "Ticketing.Backend" --startup-project "Ticketing.Backend"
   ```
   (با اتصال صحیح و env مربوط به SQL Server.)

**چک‌لیست:**

- [ ] رشته اتصال درست و دیتابیس در دسترس است.
- [ ] حساب اتصال مجوز ایجاد/تغییر جدول و نوشتن در `__EFMigrationsHistory` را دارد.
- [ ] بعد از رفع خطا، لاگ شامل `[MIGRATION] Migrations completed successfully` یا مشابه است.

---

### ۴.۳ ورود ناموفق — INVALID_CREDENTIALS (کاربران سید نشده‌اند)

**علائم:** پاسخ `POST /api/auth/login` با وضعیت ۴۰۱ و بدنه حاوی `error: "INVALID_CREDENTIALS"` یا پیام «Invalid email or password».

**دلایل محتمل:**

- جدول Users خالی است و Bootstrap اجرا نشده یا غیرفعال بوده.
- رمز/ایمیل اشتباه است.

**اقدامات:**

1. تعداد کاربران را از `/api/health` → `database.dataCounts.users` ببینید. اگر ۰ است، باید Bootstrap را برای اولین اجرا فعال کنید.
2. برای اولین اجرا متغیرهای Bootstrap را ست کنید: اگر از `deploy-iis.ps1` استفاده می‌کنید، `TikQ_BOOTSTRAP_ADMIN_EMAIL` و `TikQ_BOOTSTRAP_ADMIN_PASSWORD` (حداقل ۸ کاراکتر) را ست کنید تا اسکریپت آن‌ها را به‌صورت `Bootstrap__*` تزریق کند؛ سپس اپ را ریستارت کنید تا کاربر ادمین (و در صورت تنظیم، کاربران تست) ایجاد شوند. در غیر این صورت مستقیماً `Bootstrap__Enabled=true`, `Bootstrap__AdminEmail`, `Bootstrap__AdminPassword` را در env یا config ست کنید.
3. با همان ایمیل/رمز تنظیم‌شده در Bootstrap وارد شوید؛ یا از ادمین بخواهید با `POST /api/admin/roles/set-password` برای کاربران دیگر رمز ست کند.

**چک‌لیست:**

- [ ] اگر `users == 0` است، Bootstrap برای اولین اجرا فعال و envهای آن ست شده‌اند.
- [ ] بعد از اولین استارت موفق، با ایمیل/رمز Bootstrap تست ورود انجام شده است.

---

### ۴.۴ کوکی ست/ارسال نمی‌شود (SameSite / Secure / HTTPS)

**علائم:** بعد از ورود موفق، درخواست‌های بعدی (مثلاً `/api/auth/whoami`) بدون احراز هویت هستند یا مرورگر کوکی را نمی‌فرستد.

**دلایل محتمل:**

- در محیط HTTPS، کوکی باید `Secure` باشد؛ در غیر این صورت مرورگر آن را ذخیره یا ارسال نمی‌کند.
- اگر فرانت و بک‌اند دامنه/پورت متفاوت دارند، `SameSite=None` لازم است و در آن حالت حتماً `Secure=true` لازم است (فقط روی HTTPS).

**اقدامات:**

1. **Secure:** در Production پشت HTTPS، در تنظیمات اپ مقدار `AuthCookies:SecurePolicy` را `Always` قرار دهید (مثلاً در `appsettings.Production.json` یا env: `AuthCookies__SecurePolicy=Always`).
2. **SameSite:** پیش‌فرض `Lax` است. برای سایت cross-site از `AuthCookies:SameSite` برابر `None` استفاده کنید و حتماً با HTTPS و `SecurePolicy=Always`.
3. **Forwarded headers:** اگر پشت IIS با HTTPS هستید، مطمئن شوید هدرهای `X-Forwarded-Proto` و `X-Forwarded-For` تنظیم هستند تا اپ درخواست را به‌عنوان HTTPS ببیند و کوکی را با Secure ست کند.
4. پاسخ `/api/auth/whoami` هدر تشخیصی `X-Auth-Cookie-Present: true|false` دارد؛ با آن می‌توانید ببینید کوکی از سمت کلاینت ارسال شده یا نه.

**چک‌لیست:**

- [ ] در Production با HTTPS، `AuthCookies__SecurePolicy=Always` (یا معادل در config) ست شده است.
- [ ] در صورت cross-site، `SameSite=None` و HTTPS و Secure استفاده شده است.
- [ ] در پشت IIS با خاموش‌سازی TLS، forwarded headers برای Proto/For تنظیم است.

---

## ۵. چک‌لیست نهایی تحویل

- [ ] متغیرهای env الزامی SQL Server و JWT ست شده‌اند.
- [ ] برای اولین اجرا `Database__AutoMigrateOnStartup=true` و در صورت نیاز Bootstrap فعال است.
- [ ] تنظیمات Anonymous/Windows در IIS با حالت WindowsAuth هماهنگ است.
- [ ] بعد از هر تغییر env یا config، App Pool ریسایکل شده است.
- [ ] **GATE 1–2:** `GET /api/health` برمی‌گرداند `database.provider: "SqlServer"` و `connectionInfoRedacted` و وضعیت سالم؛ `verify-prod.ps1 -ExpectProvider SqlServer` => PASS.
- [ ] **GATE 3:** با AutoMigrateOnStartup و اتصال معتبر، `database.pendingMigrationsCount` بعد از استارت برابر ۰ است.
- [ ] **GATE 4:** بعد از اجرای `deploy-iis.ps1`، `verify-prod.ps1` => PASS.
- [ ] **GATE 5:** `database.dataCounts.users > 0` و `verify-login.ps1` با همان credهای bootstrap => PASS.
- [ ] **GATE 6:** پاسخ login شامل Set-Cookie است و whoami با کوکی `isAuthenticated=true` برمی‌گرداند (verify-login.ps1).
- [ ] در صورت استفاده از HTTPS، کوکی با Secure و در صورت لزوم SameSite تنظیم شده و forwarded headers (X-Forwarded-Proto/For) ست است.

---

**مرجع انگلیسی:** برای جزئیات بیشتر به `docs/01_Runbook/` (مثل WINDOWS_AUTH_IIS.md، DEPLOYMENT_REQUIRED_CONFIG.md، MIGRATIONS.md، BOOTSTRAP.md) و `docs/RUNBOOK_1PAGE.md` مراجعه کنید.

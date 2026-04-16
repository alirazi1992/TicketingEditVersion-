# ممیزی احراز هویت و طرح اصلاح حداقلی برای Windows Auth (تاریخ تحویل)

**تاریخ:** ۲۱ فوریه ۲۰۲۵  
**وضعیت:** فقط خواندن (بدون اعمال تغییر در کد)

---

## بخش ۱ — ممیزی (فقط خواندن)

### الف) خط لوله احراز هویت بک‌اند

**فایل:** `backend/Ticketing.Backend/Program.cs`

- **AddAuthentication / AddAuthorization:** بله.
  - `AddAuthentication` حدود خط **۱۶۴۰** با طرح پیش‌فرض `JwtBearerDefaults.AuthenticationScheme`.
  - `AddAuthorization` حدود خط **۱۷۳۱** با پالیسی‌های `AdminOnly`, `SupervisorOnly`, `SupervisorOrAdmin`.
  - `app.UseAuthentication()` و `app.UseAuthorization()` حدود خطوط **۲۲۳۳–۲۲۳۴**.

- **Negotiate / Windows Auth در IIS:** خیر.  
  فقط JWT Bearer پیکربندی شده است (خطوط ۱۶۴۰–۱۷۲۷). هیچ `AddNegotiate()` یا `AddWindows()` و هیچ استفاده از `IISOptions.AuthenticationDisplayName` یا مشابه آن وجود ندارد.

- **نحوه قبول توکن:** JWT از سه جا خوانده می‌شود (خطوط ۱۶۶۰–۱۶۸۲):
  - هدر `Authorization: Bearer`
  - کوکی HttpOnly با نام `tikq_access`
  - کوئری `access_token` برای SignalR روی `/hubs/tickets`

- **اندام‌های دارای `[Authorize]` / `[AllowAnonymous]`:**
  - **AuthController:**  
    `[AllowAnonymous]`: register، login، logout، **whoami** (خط ۳۱۰).  
    `[Authorize]`: debug-users (Admin)، **me** (GET/PUT)، change-password.
  - سایر کنترلرها (مثلاً Tickets، Users، TechnicianTickets، Categories، Settings، …) عمدتاً `[Authorize]` دارند؛ در برخی متدها `[AllowAnonymous]` (مثلاً یک متد در Categories).

- **تبدیل claim / middleware برای whoami:** هیچ middleware یا claims transformation سفارشی که روی `User` (whoami) اثر بگذارد در کد دیده نشد. هویت فقط از JWT پر می‌شود.

---

### ب) اندام whoami

**کنترلر/اکشن:** `backend/Ticketing.Backend/Api/Controllers/AuthController.cs`

- **مسیر:** `GET /api/auth/whoami` (خطوط ۳۰۷–۳۴۶).
- **ویژگی‌ها:** `[AllowAnonymous]` — احراز هویت اجباری نیست.
- **منبع شناسایی کاربر:** فقط **Claims مربوط به JWT**:
  - `User.FindFirstValue("email")` یا `ClaimTypes.Email`
  - `User.FindFirstValue(ClaimTypes.Role)` یا `"role"`
  - `User.FindFirstValue("isSupervisor")` یا `"is_supervisor"`
- **خروجی در صورت احراز (IsAuthenticated == true):**  
  `{ isAuthenticated: true, email, role, isSupervisor, landingPath }`؛ مقدار `landingPath` از همان role و isSupervisor در کد محاسبه می‌شود (Admin→`/admin`, Technician+isSupervisor→`/supervisor`, Technician→`/technician`, در غیر این صورت→`/client`).
- **در صورت عدم احراز:**  
  `{ isAuthenticated: false, email: null, role: null, isSupervisor: false, landingPath: "/login" }`.
- **سازگاری با Windows Auth:** در وضعیت فعلی خیر. چون فقط JWT پیکربندی شده، با Windows Auth خالص (بدون JWT) `User.Identity?.IsAuthenticated` از Windows پر نمی‌شود و whoami همیشه کاربر را «غیر احراز شده» برمی‌گرداند.

---

### ج) منطق ریدایرکت و منبع حقیقت در فرانت‌اند

**فایل‌های اصلی:**

- **`frontend/lib/auth-context.tsx`**  
  - منبع جاری برای «کاربر فعلی»: **فقط `GET /api/auth/me`** (خط ۱۳۷)، نه whoami.  
  - با `apiRequest(..., { silent: true })` و با **credentials: "include"** (از طریق `api-client.ts`) فراخوانی می‌شود.  
  - در صورت ۲۰۰: خروجی به `mapUser(me)` می‌رود و `user.role`, `user.isSupervisor`, `user.landingPath` از DTO پر می‌شوند و در state و localStorage ذخیره می‌شوند.  
  - در صورت ۴۰۱: session پاک و `user = null` می‌شود.

- **`frontend/lib/auth-routing.ts`**  
  - تابع `getLandingPath(user)`: اگر `user.landingPath` معتبر باشد از آن استفاده می‌کند؛ وگرنه از `role` و `isSupervisor` مسیر را محاسبه می‌کند (admin→`/admin`, technician+isSupervisor→`/supervisor`, technician→`/technician`, در غیر این صورت→`/client`).  
  - تابع `getLandingPathFromSession({ user })` همان را برای session فعلی برمی‌گرداند.

- **`frontend/app/page.tsx`**  
  - ریدایرکت روت `/`: اگر `!user` → `/login`؛ اگر `user` وجود داشت → `getLandingPathFromSession({ user })` و سپس `router.replace(landingPath)`.

**چرا ممکن است همه به `/client` ریدایرکت شوند:**

1. **فرانت فقط از `/api/auth/me` استفاده می‌کند، نه whoami.** با Windows Auth و بدون JWT، درخواست به `/me` معمولاً ۴۰۱ برمی‌گرداند و کاربر به `/login` می‌رود؛ پس در این حالت «همه به client» مطرح نیست، مگر بعد از لاگین.
2. **بعد از لاگین:** اگر بک‌اند در پاسخ login یا در خروجی `/me` مقدار اشتباه یا ناقص برای `role` / `isSupervisor` / `landingPath` بفرستد، یا فرانت در `roleFromApi` نقش را به‌اشتباه به `"client"` نگاشت کند، مسیر نهایی به `/client` می‌افتد.
3. **نقش در فرانت (`auth-context.tsx` خطوط ۵۳–۷۸):**  
   مقدار پیش‌فرض در `roleFromApi` برای هر چیزی غیر از admin/technician/supervisor/engineer برابر است با `"client"`. اگر بک‌اند نقش را با نام/فرمت دیگری بفرستد (یا اصلاً نفرستد)، فرانت آن را به client نگاشت می‌کند و در نتیجه `getLandingPath` برابر `/client` می‌شود.

---

### د) محیط و آدرس پایه API

- **خواندن و استفاده از `NEXT_PUBLIC_API_BASE_URL`:**
  - `frontend/lib/url.ts`: تابع `getEffectiveApiBaseUrl()` (خط ۷۱) و `normalizeBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL)`.
  - `frontend/lib/api-client.ts`: خطوط ۱۰۰، ۱۰۸، ۱۶۴، ۱۹۲، ۱۹۸ — برای تشخیص/کش base URL و ساخت درخواست‌ها.
- **مقدار فعلی نمونه:**  
  در `frontend/.env.production` مقدار `NEXT_PUBLIC_API_BASE_URL=http://localhost:8080` است (فقط نمونه). در پروداکشن واقعی باید مطابق دامنه سرور تنظیم شود.
- **سناریوهای پروداکشن:**
  - **همان دامنه (مثلاً فرانت و API هر دو زیر یک دامنه):** می‌توان `NEXT_PUBLIC_API_BASE_URL` را خالی گذاشت یا برابر با همان origin (مثلاً `https://tikq.company.com`) تا درخواست‌ها به همان دامنه بروند (مثلاً `/api/...`).
  - **دامنه جدا (مثلاً فرانت روی یک زیردامنه و API روی زیردامنه دیگر):** باید `NEXT_PUBLIC_API_BASE_URL` را روی آدرس کامل API قرار داد و CORS و کوکی (SameSite/Secure) را برای آن origin پیکربندی کرد.

---

## بخش ۲ — پاسخ سوالات با استناد به کد

**۱) فرانت و بک در پروداکشن روی یک دامنه هستند یا دو origin جدا؟**  
در کد به‌صورت ثابت تعیین نشده است. با `NEXT_PUBLIC_API_BASE_URL` خالی یا برابر با همان origin، درخواهد رفت به همان دامنه (مثلاً `/api`). با مقدار جدا (مثلاً دامنه دیگر)، دو origin خواهید داشت.  
**مدارک:** `frontend/.env.production` (نمونه localhost:8080)، `frontend/lib/url.ts` (تابع `getDefaultApiBaseUrl()` در production برابر `""`)، `frontend/lib/api-client.ts` (استفاده از env برای base).

**۲) آیا فرانت whoami را با credentials (مثلاً `fetch(..., { credentials: 'include' })`) صدا می‌زند؟**  
فرانت اصلاً whoami را صدا نمی‌زند؛ فقط `/api/auth/me` را با همان `apiRequest` که در نهایت `credentials: "include"` دارد فراخوانی می‌کند.  
**مدارک:** `frontend/lib/auth-context.tsx` خط ۱۳۷ (`/api/auth/me`)، `frontend/lib/api-client.ts` خط ۴۲۵ (`credentials: "include"`).

**۳) آیا CORS در بک با AllowCredentials و originهای مشخص (و نه `*`) تنظیم شده است؟**  
بله. در `Program.cs` حدود ۱۸۰۷–۱۸۳۳ از `WithOrigins(allowedCorsOrigins)` و `AllowCredentials()` استفاده شده است. اگر در production هیچ origin در تنظیمات نباشد، آرایه خالی است و در عمل همه درخواست‌های cross-origin رد می‌شوند.  
**مدارک:** `backend/Ticketing.Backend/Program.cs` خطوط ۱۷۶۴–۱۸۳۴ (خواندن `Cors:AllowedOrigins` و استفاده از آن در policy).

**۴) آیا WindowsUserMap / RoleMapping یا هر گونه مپینگ مشابه از قبل وجود دارد؟ کجا؟**  
- **WindowsUserMap:** در کد جستجو شد؛ چنین چیزی وجود ندارد.  
- **RoleMapping:** بله، به‌صورت **RoleMappingResponse** و سرویس **GetRoleByEmailAsync** وجود دارد.  
  - تعریف DTO: `backend/Ticketing.Backend/Application/DTOs/AuthDtos.cs` حدود خطوط ۱۵۸–۱۶۳ (`RoleMappingResponse`: Email, Role, IsSupervisor).  
  - سرویس: `backend/Ticketing.Backend/Application/Services/UserService.cs` حدود ۵۱۴–۵۳۲: با ایمیل، کاربر را از TikQ می‌گیرد و نقش و IsSupervisor را از دیتابیس TikQ برمی‌گرداند (بدون هیچ نقش از دایرکتوری خارجی).  
این برای «نقش TikQ بر اساس ایمیل» است و می‌توان از آن برای مپینگ Windows identity به ایمیل و سپس نقش استفاده کرد (با یک لایه اضافه برای تبدیل Windows identity به email).

**۵) نقش‌های TikQ کجا ذخیره می‌شوند و supervisor چطور نمایش داده می‌شود؟**  
- **جدول/ستون:**  
  - نقش کاربر: جدول **Users**، ستون **Role** (enum: `UserRole`: Client=0, Technician=1, Admin=2, Supervisor=3).  
  - سرپرست: جدول **Technicians**، ستون **IsSupervisor** (boolean). کاربر با Role=Technician که رکورد Technicians با IsSupervisor=true داشته باشد به‌عنوان supervisor در نظر گرفته می‌شود.  
**مدارک:**  
- `backend/Ticketing.Backend/Domain/Enums/UserRole.cs` (تعریف enum)،  
- `backend/Ticketing.Backend/Domain/Entities/User.cs` (Role)،  
- `backend/Ticketing.Backend/Domain/Entities/Technician.cs` (IsSupervisor)،  
- `UserService.ResolveIsSupervisorAsync` و `MapToDtoAsync` (خواندن از Technicians برای IsSupervisor و محاسبه LandingPath).

---

## بخش ۳ — طرح اصلاح حداقلی (امروز؛ بدون SSO/OIDC و بدون ابزار UI)

هدف:  
- در IIS با Windows Integrated Authentication (Negotiate/NTLM) احراز هویت انجام شود.  
- whoami (یا یک endpoint معادل) نقش و isSupervisor صحیح از دیتابیس TikQ برگرداند.  
- فرانت بر اساس همان منبع به `/admin`, `/technician`, `/supervisor`, `/client` ریدایرکت کند.  
- بدون ذخیره رمز در TikQ؛ نقش فقط از دیتابیس TikQ.

### اصول

- اضافه کردن طرح **Negotiate** در کنار JWT (dual scheme) تا در IIS با Windows Auth، `User.Identity` از Windows پر شود.
- نگاشت **Windows identity → ایمیل (یا شناسه کاربر TikQ)** از طریق یک جدول/تنظیم ساده (مثلاً جدول WindowsUserMap یا یک config: DOMAIN\user → email).
- whoami: اگر هویت Windows داشتیم و JWT نداشتیم، با همان Windows identity کاربر را از TikQ پیدا کنیم و role + isSupervisor + landingPath را از دیتابیس برگردانیم؛ در صورت تمایل برای یکپارچگی با `/me` می‌توان یک بار JWT هم صادر کرد و کوکی ست کرد تا فرانت بتواند همان مسیر فعلی را با `/me` ادامه دهد.
- فرانت: یا از whoami به‌عنوان منبع اول برای session (وقتی `/me` ۴۰۱ داد) استفاده کند و بر اساس `landingPath` ریدایرکت کند، یا پس از «لاگین Windows» (که فقط JWT را بر اساس Windows identity صادر می‌کند) همان جریان فعلی `/me` + `getLandingPathFromSession` حفظ شود.

### گام‌های پیشنهادی (چک‌لیست + فایل‌ها)

**بک‌اند**

1. **افزودن پکیج و پیکربندی Negotiate (Windows Auth)**  
   - فایل: `backend/Ticketing.Backend/Program.cs`  
   - نصب پکیج: `Microsoft.AspNetCore.Authentication.Negotiate` (در صورت نبود).  
   - بعد از سرویس‌های JWT، اضافه کردن:
     - `services.AddAuthentication(...).AddNegotiate()` و تنظیم default scheme به یک سیاست ترکیبی (مثلاً اول Negotiate برای whoami، سپس JWT برای بقیه)، یا استفاده از چند scheme و در whoami چک کردن اول Windows سپس JWT.  
   - در IIS مطمئن شوید Windows Authentication فعال و Anonymous در صورت نیاز فقط برای مسیرهای خاص است.

2. **نگاشت Windows identity به کاربر TikQ**  
   - گزینه الف: جدول ساده مثلاً `WindowsUserMap` (ستون‌ها: WindowsIdentity (مثلاً DOMAIN\user یا UPN), Email, شاید CreatedAt).  
   - گزینه ب: قانون ساده در کد (مثلاً از `User.Identity.Name` فرمت `DOMAIN\user` را به `user@domain.com` تبدیل و با Users.Email جستجو).  
   - فایل‌های قابل تغییر: یک سرویس جدید (مثلاً `IWindowsAuthUserResolver`) و در صورت استفاده از جدول: Entity + Migration در `Infrastructure/Data`.

3. **تغییر whoami برای پشتیبانی از Windows**  
   - فایل: `backend/Ticketing.Backend/Api/Controllers/AuthController.cs` (متود WhoAmI).  
   - اگر `User.Identity?.IsAuthenticated == true` از Windows بود (مثلاً با چک کردن `AuthenticationType == "Negotiate"` یا مشابه)، به‌جای خواندن از claims JWT:
     - از سرویس بالا با `User.Identity.Name` (یا UPN) کاربر TikQ را با ایمیل/نقش پیدا کنید.
     - از `UserService.GetRoleByEmailAsync` یا معادل آن (یا مستقیم از همان resolver) role و isSupervisor را از TikQ بگیرید.
     - landingPath را با `LandingPathResolver.GetLandingPath(role, isSupervisor)` محاسبه و در پاسخ whoami برگردانید.
   - در صورت تمایل: اگر کاربر از Windows شناخته شد و در TikQ وجود داشت، یک بار JWT بسازید و با همان `SetAccessCookie` کوکی ست کنید تا درخواست‌های بعدی `/me` هم با JWT کار کنند (در این حالت فرانت نیازی به تغییر زیاد ندارد).

4. **اختیاری: endpoint «لاگین Windows»**  
   - مثلاً `POST /api/auth/windows-login` با `[Authorize(AuthenticationSchemes = "Negotiate")]`: فقط بررسی کند کاربر Windows احراز شده است، با همان resolver کاربر TikQ را پیدا کند، JWT صادر و کوکی ست کند و همان پاسخ login (role, isSupervisor, landingPath, user) را برگرداند. در این حالت فرانت بعد از لود اولیه می‌تواند whoami یا همین endpoint را صدا بزند و بعد جریان فعلی با `/me` را حفظ کند.

5. **CORS و میزبانی IIS**  
   - همان‌طور که هست با `WithOrigins` و `AllowCredentials()` نگه دارید؛ در production حتماً originهای فرانت را در `Cors:AllowedOrigins` قرار دهید.  
   - در IIS برای اپ TikQ: Windows Authentication فعال؛ در صورت نیاز Anonymous را فقط برای مسیرهای مشخص (مثلاً فقط برای login/register اگر لازم است) محدود کنید.

**فرانت‌اند**

6. **استفاده از whoami وقتی `/me` ۴۰۱ داد (برای Windows Auth بدون کوکی اولیه)**  
   - فایل: `frontend/lib/auth-context.tsx`.  
   - در `fetchCurrentUser`: اگر درخواست `/me` با ۴۰۱ مواجه شد، یک بار `GET /api/auth/whoami` با همان `apiRequest(..., { silent: true })` انجام دهید. اگر پاسخ whoami دارای `isAuthenticated === true` و `landingPath` بود، یک شیء حداقلی `User` از روی آن بسازید (مثلاً از email/role/isSupervisor/landingPath) و با `setUser` و `persistUser` ست کنید تا ریدایرکت در `page.tsx` با `getLandingPathFromSession` به `/admin` یا `/technician` یا `/supervisor` یا `/client` درست انجام شود.  
   - اگر ترجیح می‌دهید همیشه بعد از Windows Auth یک JWT صادر شود (از بک با whoami یا windows-login)، می‌توانید بعد از دریافت whoami موفق، یک درخواست به endpointی که کوکی JWT ست می‌کند بزنید و سپس دوباره `/me` را بزنید تا همان جریان فعلی بدون تغییر زیاد در auth-context حفظ شود.

7. **یکسان‌سازی نقش و landingPath**  
   - مطمئن شوید در whoami پاسخ `role` با مقادیری که فرانت در `roleFromApi` می‌شناسد یکی است (مثلاً "Admin", "Technician", "Client") و `landingPath` همیشه یکی از `/admin`, `/technician`, `/supervisor`, `/client` است تا فرانت به‌اشتباه به `/client` نرود.

**چک‌لیست نهایی**

- [ ] در `Program.cs`: AddNegotiate و ترتیب/ترکیب schemeها طوری که whoami بتواند Windows identity را ببیند.
- [ ] سرویس/جدول یا قانون مپینگ Windows identity → User TikQ (ایمیل یا Id).
- [ ] در AuthController.WhoAmI: در صورت احراز Windows، resolve از TikQ و برگرداندن role, isSupervisor, landingPath (و در صورت نیاز صدور JWT و ست کردن کوکی).
- [ ] (اختیاری) POST /api/auth/windows-login برای ست کردن کوکی JWT بعد از Windows Auth.
- [ ] در auth-context: در صورت ۴۰۱ از `/me`، فراخوانی whoami و ساخت session از پاسخ whoami برای ریدایرکت درست.
- [ ] CORS: فقط originهای مجاز و AllowCredentials؛ بدون `*`.
- [ ] تست روی IIS با Windows Authentication فعال و بدون رمز در TikQ؛ نقش فقط از TikQ.

این طرح حداقلی و قابل برگشت است و نیازی به SSO/OIDC یا ابزارهای توسعه در UI ندارد.

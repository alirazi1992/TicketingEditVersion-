# Full Dependency Audit: Ticket Default Settings & Notification Settings

**Purpose:** Production handoff safety verification for potential removal of (1) Ticket Default Settings and (2) Notification Settings from UI and possibly backend.

**Scope:** Static analysis only. No code was refactored or changed.

---

## PART 1 – BACKEND DEPENDENCY ANALYSIS

### Entities and persistence

| Location | Item | Notes |
|----------|------|--------|
| `backend/Ticketing.Backend/Domain/Entities/SystemSettings.cs` | Entity `SystemSettings` | Single-row config: App/General, Ticketing Defaults (DefaultPriority, DefaultStatus, ResponseSlaHours, AutoAssignEnabled, AllowClientAttachments, MaxAttachmentSizeMB), Notifications (EmailNotificationsEnabled, SmsNotificationsEnabled, NotifyOnTicket*), Security. |
| `backend/Ticketing.Backend/Infrastructure/Data/AppDbContext.cs` | `DbSet<SystemSettings> SystemSettings` | Table `SystemSettings`. |
| `backend/Ticketing.Backend/Infrastructure/Data/SeedData.cs` | Seed | `FirstOrDefaultAsync(s => s.Id == 1)`; if null, creates default row (DefaultPriority=Medium, DefaultStatus=Submitted, ResponseSlaHours=24, AutoAssignEnabled=false, AllowClientAttachments=true, Notify* flags, etc.). **Critical:** Ensures row exists on seed. |

### Repositories and services

| File | Method / usage | Criticality | If settings removed or null |
|------|----------------|-------------|-----------------------------|
| `Infrastructure/Data/Repositories/SystemSettingsRepository.cs` | `GetByIdAsync(int id)` | Returns `SystemSettings?`. Used by `SystemSettingsService.UpdateSystemSettingsAsync`. | Caller handles null by creating new entity. |
| `Infrastructure/Data/Repositories/SystemSettingsRepository.cs` | `GetOrCreateDefaultAsync(int id)` | Loads by id; if null, creates new entity (Id, CreatedAt, UpdatedAt only – other properties use C# defaults). **Never returns null.** | N/A – contract is non-null. |
| `Application/Services/SystemSettingsService.cs` | `GetSystemSettingsAsync()` | Calls `_repository.GetOrCreateDefaultAsync(1)` then `MapToResponse(settings)`. | Never returns null. |
| `Application/Services/SystemSettingsService.cs` | `UpdateSystemSettingsAsync(request)` | Calls `GetByIdAsync(1)`; if null, creates new `SystemSettings`, then updates from request. | Safe; no NRE. |

### References in ticket and assignment flows

| File | Method | How settings are used | Criticality | If removed/null |
|------|--------|------------------------|-------------|------------------|
| `Application/Services/TicketService.cs` | Constructor | `ISystemSettingsService _systemSettingsService` injected. | **Not used** in ticket logic. | No impact. |
| `Application/Services/TicketService.cs` | `CreateTicketAsync` | **Does not call** `_systemSettingsService`. Ticket created with `Status = TicketStatus.Submitted`, `Priority = request.Priority`. No read of DefaultPriority, DefaultStatus, ResponseSlaHours, AllowClientAttachments. | **Non-critical** for create. | No change in behavior. |
| `Application/Services/TicketService.cs` | Attachments | Attachments accepted in `CreateTicketAsync`; **no check** of `AllowClientAttachments` or `MaxAttachmentSizeMB`. | Setting is stored but not enforced. | No impact. |
| `Api/Controllers/SmartAssignmentController.cs` | `GetSmartAssignmentStatus()` | `var settings = await _systemSettingsService.GetSystemSettingsAsync(); return Ok(..., Enabled = settings.AutoAssignEnabled);` | **Critical.** Assumes non-null. | If `GetSystemSettingsAsync()` returned null → **NullReferenceException**. |
| `Api/Controllers/SmartAssignmentController.cs` | `UpdateSmartAssignmentStatus()` | `var currentSettings = await _systemSettingsService.GetSystemSettingsAsync();` then builds `SystemSettingsUpdateRequest` with all fields from `currentSettings` (DefaultPriority, DefaultStatus, ResponseSlaHours, notification flags, etc.) and only overrides `AutoAssignEnabled`. | **Critical.** Assumes non-null. | If null → **NullReferenceException**. |
| `Api/Controllers/SmartAssignmentController.cs` | `RunSmartAssignment()` | `var settings = await _systemSettingsService.GetSystemSettingsAsync(); if (!settings.AutoAssignEnabled) return BadRequest(...);` | **Critical.** Assumes non-null. | If null → **NullReferenceException**. |

### SLA, notification senders, background jobs, middleware

| Area | Finding |
|------|--------|
| **SLA logic** | `ResponseSlaHours` is stored and exposed in API/UI. **No backend code** (TicketService, jobs, or other services) was found that computes SLA or uses `ResponseSlaHours`. |
| **NotificationService / email / SMS** | **No service** found that sends email or SMS or that reads `EmailNotificationsEnabled`, `SmsNotificationsEnabled`, or `NotifyOnTicketCreated/Assigned/Replied/Closed`. These flags are only persisted and echoed in `SmartAssignmentController` when building the update request. |
| **Background jobs** | No `BackgroundService` or `IHostedService` uses SystemSettings. |
| **Event handlers** | No event handler found that reads system or notification settings. |
| **Middleware** | No middleware uses SystemSettings or UserPreferences. |

### Controllers

| File | Usage | If removed/null |
|------|--------|------------------|
| `Api/Controllers/SettingsController.cs` | `GetSystemSettings()`, `GetSystemSettingsPlural()` → `GetSystemSettingsAsync()`; `UpdateSystemSettings([FromBody])` → `UpdateSystemSettingsAsync(request)`. | Get never returns null. Update creates row if missing. Removing endpoint would break admin UI and any client calling it. |
| `Api/Controllers/SmartAssignmentController.cs` | As above: three methods use `GetSystemSettingsAsync()` and assume non-null. | NRE if service returned null. |

### User notification preferences (per-user, not system)

| File | Usage | Notes |
|------|--------|--------|
| `Api/Controllers/UsersController.cs` | `GetMyPreferences`, `UpdateMyPreferences` (theme, language, font, **notifications**); `GetMyNotificationPreferences`, `UpdateMyNotificationPreferences`. | Per-user preferences (UserPreferences table). No other backend code sends notifications based on these. |
| `Application/Services/UserPreferencesService.cs` | Get/Update notification preferences (EmailEnabled, PushEnabled, SmsEnabled, DesktopEnabled). | Only used by UsersController and frontend settings UI. |

---

## PART 2 – NULL SAFETY ANALYSIS

| Check | Result |
|-------|--------|
| **Settings assumed non-null?** | **Yes** in `SmartAssignmentController`: all three methods use `settings.*` or `currentSettings.*` with no null check. `SystemSettingsService.GetSystemSettingsAsync()` never returns null (uses `GetOrCreateDefaultAsync`). |
| **First() vs FirstOrDefault()?** | **No `First()` on SystemSettings.** SeedData uses `FirstOrDefaultAsync(s => s.Id == 1)`. Repository uses `FindAsync(id)`. |
| **Null checks** | `SystemSettingsService.UpdateSystemSettingsAsync`: checks `if (settings == null)` and creates. `SystemSettingsRepository.GetOrCreateDefaultAsync`: checks `if (settings == null)` and creates. **SmartAssignmentController**: no null check. |
| **Removal causing NullReferenceException?** | **Yes**, if `GetSystemSettingsAsync()` were changed to return null (or if the service/table were removed and the controller left unchanged). Current implementation does not return null. |

---

## PART 3 – DEFAULT BEHAVIOR IF SETTINGS REMOVED

| Question | Answer |
|----------|--------|
| **Default ticket status?** | Today: **hardcoded** in `CreateTicketAsync` as `TicketStatus.Submitted`. SystemSettings.DefaultStatus is **not** read on create. If settings removed: no change. |
| **Default priority?** | Today: from **request** (`request.Priority`). SystemSettings.DefaultPriority is **not** used in CreateTicketAsync. If settings removed: no change. |
| **SLA behavior?** | `ResponseSlaHours` is **not** used in any SLA calculation or deadline logic in the codebase. If settings removed: no functional change. |
| **Notifications?** | No sender uses system or user notification flags. Notifications would **silently** continue to do nothing (as today). |
| **Feature breakage?** | **Smart assignment** would break if SystemSettings or `GetSystemSettingsAsync()` were removed without changing `SmartAssignmentController` (NRE). **Admin UI** for “Ticket Default Settings” and “Notification Settings” would lose backend persistence if APIs/entities were removed; the **Ticket Default Settings UI** tab and **Notification Settings** tab are already removed from the settings dialog; only the “تنظیمات پیش‌فرض تیکتینگ” admin form and its save still call `/api/settings/system`. |

---

## PART 4 – FRONTEND DEPENDENCY

### API calls and usage

| Location | What | Used by |
|----------|------|--------|
| `frontend/lib/settings-api.ts` | `getSystemSettings(token)` → GET `/api/settings/system`, `updateSystemSettings(token, settings)` → PUT `/api/settings/system` | **Only** `frontend/components/settings-dialog.tsx` (admin tab “تنظیمات پیش‌فرض تیکتینگ”). |
| `frontend/lib/notification-preferences-api.ts` | `getMyNotificationPreferences`, `updateMyNotificationPreferences` → `/api/users/me/notifications` | **Only** `settings-dialog.tsx` – the Notification Settings tab has already been removed from the UI, so these are only referenced in leftover state/handlers (e.g. `notificationPreferences`, `handleNotificationSave`). |
| `frontend/lib/preferences-api.ts` | `getMyPreferences`, `updateMyPreferences` → `/api/Users/me/preferences` | Preferences context and appearance/language; **not** ticket-default or system notification settings. |

### Assumptions and conditional rendering

| Item | Assumption / behavior |
|------|------------------------|
| `settings-dialog.tsx` | When open and user is admin, fetches system settings and fills form; submit calls `updateSystemSettings`. Assumes API returns full `ApiSystemSettingsResponse`. No fallback for “no settings”; loading state only. |
| Dashboard / tickets | **No** frontend code outside the settings dialog reads system settings or ticket-default config. No conditional rendering of dashboard or ticket list based on settings. |

### Would removing UI break dashboard logic?

**No.** System settings and notification preferences are only used in the settings modal. Removing the Ticket Default Settings and Notification Settings UI (tabs/sections) does not break dashboard or ticket list logic. The “تنظیمات پیش‌فرض تیکتینگ” tab still loads and saves via `/api/settings/system`; if that tab were removed too, only the ability to edit those values in the UI would be lost; no other frontend logic depends on them.

---

## PART 5 – RISK SUMMARY

### SAFE TO REMOVE

- **Notification Settings UI (تنظیمات اعلان‌ها) tab**  
  Already removed. Backend endpoints and UserPreferences notification fields can remain; no other feature depends on them for behavior.

- **Ticket Default Settings UI only (if you keep backend)**  
  Removing only the “Ticket Default Settings” section/tab from the settings dialog is safe. Dashboard and ticket creation do not depend on this UI. **Note:** Ticket creation does not use system default status/priority today; it uses hardcoded status and request priority.

### SAFE WITH MODIFICATION

- **Removing SystemSettings entity/table and related backend**  
  Safe only if you:
  1. **SmartAssignmentController:** Stop calling `GetSystemSettingsAsync()` and `UpdateSystemSettingsAsync()`. Either remove the smart-assignment toggle/run endpoints or drive them from another source (e.g. config, env, or a minimal settings table with only `AutoAssignEnabled`).
  2. **SettingsController:** Remove or replace with a stub that returns a fixed DTO (e.g. defaults) so existing clients that call GET `/api/settings/system` do not break.
  3. **TicketService:** No change needed (it does not use settings).

- **Removing per-user Notification Preferences backend**  
  Safe if you remove or stub `GetMyNotificationPreferences` and `UpdateMyNotificationPreferences` and remove or update any frontend that still calls them (currently only leftover code in settings-dialog). No other backend or dashboard logic uses these.

### DANGEROUS – DO NOT REMOVE WITHOUT CHANGES

- **Making `GetSystemSettingsAsync()` return null** (or removing `ISystemSettingsService` / SystemSettings without updating callers).  
  **SmartAssignmentController** would throw **NullReferenceException** in:
  - `GetSmartAssignmentStatus()`
  - `UpdateSmartAssignmentStatus()`
  - `RunSmartAssignment()`

- **Removing the SystemSettings table or repository** while leaving `SmartAssignmentController` and `SystemSettingsService` as-is.  
  Same NRE risk as above once the repository returns null or throws.

---

## Summary table

| Component | Ticket Default Settings | Notification Settings (system) | Notification Settings (per-user) |
|-----------|--------------------------|----------------------------------|-----------------------------------|
| **CreateTicket** | Not used | Not used | Not used |
| **UpdateTicket** | Not used | Not used | Not used |
| **AssignTechnician** | Not used | Not used | Not used |
| **SLA logic** | ResponseSlaHours stored, not used | N/A | N/A |
| **SmartAssignmentController** | Reads/updates full settings (including defaults) | Copies notification flags when updating | N/A |
| **Notification sending** | N/A | No sender uses flags | No sender uses prefs |
| **Frontend dashboard** | No dependency | No dependency | No dependency |
| **Safe to remove UI only** | Yes | Yes (done) | Yes (done) |
| **Safe to remove backend** | Only with SmartAssignment + SettingsController changes | Yes (no consumer) | Yes with endpoint removal/cleanup |

---

*Audit completed by static analysis. No code was modified.*

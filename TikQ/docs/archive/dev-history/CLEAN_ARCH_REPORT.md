# Clean Architecture Refactor Report

**Generated:** 2025-01-02  
**Backend Root:** `C:\Users\a.razi\Desktop\TikQ\backend\Ticketing.Backend`

---

## PHASE 0 — SAFETY CHECKPOINT

**Timestamp:** 2025-01-02

### Git Status
- ✅ Repository checked/initialized
- ✅ Branch created: `refactor/clean-architecture`
- ✅ Tag created: `pre-clean-arch`

### Current Folder Structure

```
Ticketing.Backend/
├── Api/
│   ├── Controllers/
│   ├── Hubs/
│   └── Swagger/
├── Application/
│   ├── DTOs/
│   └── Services/
├── Domain/
│   ├── Entities/
│   └── Enums/
├── Infrastructure/
│   ├── Auth/
│   └── Data/
│       ├── Configurations/
│       └── Migrations/
├── App_Data/
├── Program.cs
└── Ticketing.Backend.csproj
```

---

## PHASE 1 — AUDIT (READ ONLY)

### Current Structure Analysis

**Current Project Structure:**
- Single project: `Ticketing.Backend.csproj`
- Folders organized as layers but not separate projects:
  - `Domain/` - Entities and Enums
  - `Application/` - DTOs and Services
  - `Infrastructure/` - Data (DbContext, Migrations, Configurations) and Auth
  - `Api/` - Controllers and Hubs

**Dependency Flow Analysis:**

1. **Domain Layer (`Domain/`):**
   - ✅ Has no external dependencies
   - ✅ Only uses `System` and `Domain.Enums`
   - ✅ No EntityFrameworkCore, no ASP.NET, no Infrastructure references

2. **Application Layer (`Application/`):**
   - ❌ **Directly depends on `Infrastructure.Data`** (AppDbContext)
   - ❌ **Directly depends on `Api.Hubs`** (NotificationHub, IHubContext)
   - ❌ Uses `Microsoft.EntityFrameworkCore` directly
   - ✅ Contains Service interfaces and implementations
   - ❌ **No repository pattern** - services directly inject and use `AppDbContext`
   - ✅ Contains DTOs
   - Files affected: All 9 service files use AppDbContext directly

3. **Infrastructure Layer (`Infrastructure/`):**
   - ✅ Contains `AppDbContext` and EF Core configurations
   - ✅ Contains Migrations
   - ✅ Contains Auth (JWT)
   - ✅ Depends only on Domain (for entities)
   - ⚠️ Should implement Application interfaces, but Application depends on it (circular violation)

4. **API Layer (`Api/`):**
   - ✅ Main controllers (TicketsController, CategoriesController, etc.) use Application services (good)
   - ❌ Some controllers directly inject `AppDbContext`:
     - `DebugController.cs`
     - `AuthController.cs` (has debug method)
     - `AdminDebugController.cs`
     - `AdminMaintenanceController.cs`
     - `SmartAssignmentController.cs`
   - ✅ Controllers are generally thin (delegate to services)
   - ✅ Depends on Application (for services) and Infrastructure (for DI)

**Project Separation:**
- ❌ **Single project** - everything in `Ticketing.Backend.csproj`
- ❌ Should be 4 separate projects: Domain, Application, Infrastructure, Api

### Clean Architecture Checklist

- [x] Domain has no dependencies ✅
- [ ] Application depends only on Domain ❌ (depends on Infrastructure.Data and Api.Hubs)
- [ ] Infrastructure depends on Application/Domain ❌ (should depend on Application interfaces, but Application depends on Infrastructure)
- [x] API depends on Application (and Infrastructure only for DI wiring) ✅ (mostly, except direct DbContext usage in some controllers)
- [x] Controllers are thin (no business rules) ✅ (mostly, but some use DbContext directly)
- [ ] EF Core is only in Infrastructure ⚠️ (Infrastructure has it, but Application also uses it)
- [ ] Repository interfaces live in Application ❌ (no repository pattern exists)
- [x] DTOs/UseCases live in Application ✅
- [ ] No circular references ❌ (Application → Infrastructure, but should be Infrastructure → Application)

### Critical Violations

1. **Application → Infrastructure Dependency** (WRONG DIRECTION)
   - All Application services inject `AppDbContext` from `Infrastructure.Data`
   - This violates Clean Architecture principle: Application should not depend on Infrastructure

2. **Application → Api Dependency** (WRONG)
   - `TicketService` and `NotificationService` use `IHubContext<NotificationHub>` from `Api.Hubs`
   - Application should not know about API/presentation layer

3. **No Repository Pattern**
   - Application services directly use EF Core DbContext
   - Should have repository interfaces in Application, implementations in Infrastructure

4. **Single Project**
   - All layers in one .csproj file
   - Should be separated: Domain, Application, Infrastructure, Api projects

5. **Some Controllers Use DbContext Directly**
   - Debug/admin controllers bypass Application layer

**Verdict: ❌ This is NOT Clean Architecture**

**Architecture Type:** Layered Architecture with folder organization, but dependencies point in wrong direction and layers are not properly separated.

---

## PHASE 2 — PLAN

### Target Project Structure

Create 4 separate .csproj projects:

```
backend/Ticketing.Backend/
├── src/
│   ├── Ticketing.Domain/
│   │   ├── Entities/
│   │   └── Enums/
│   │   └── Ticketing.Domain.csproj
│   ├── Ticketing.Application/
│   │   ├── DTOs/
│   │   ├── Services/ (interfaces only)
│   │   ├── Repositories/ (interfaces)
│   │   ├── Abstractions/ (SignalR abstraction, etc.)
│   │   └── Ticketing.Application.csproj
│   ├── Ticketing.Infrastructure/
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Repositories/ (implementations)
│   │   │   ├── Configurations/
│   │   │   └── Migrations/
│   │   ├── Auth/
│   │   ├── Notifications/ (SignalR implementation)
│   │   └── Ticketing.Infrastructure.csproj
│   └── Ticketing.Api/
│       ├── Controllers/
│       ├── Hubs/
│       ├── Program.cs
│       ├── appsettings.json
│       └── Ticketing.Api.csproj
```

### Project Dependencies

```
Ticketing.Domain
  └── (no dependencies)

Ticketing.Application
  └── → Ticketing.Domain

Ticketing.Infrastructure
  └── → Ticketing.Application
  └── → Ticketing.Domain

Ticketing.Api
  └── → Ticketing.Application
  └── → Ticketing.Infrastructure (for DI registrations)
```

### File Mapping

| Current Location | Target Project | Target Folder |
|------------------|----------------|---------------|
| `Domain/` | Ticketing.Domain | Root |
| `Application/DTOs/` | Ticketing.Application | `DTOs/` |
| `Application/Services/*.cs` (interfaces) | Ticketing.Application | `Services/` |
| `Application/Services/*.cs` (implementations) | Ticketing.Infrastructure | `Services/` (after refactor) |
| `Infrastructure/Data/AppDbContext.cs` | Ticketing.Infrastructure | `Data/` |
| `Infrastructure/Data/Configurations/` | Ticketing.Infrastructure | `Data/Configurations/` |
| `Infrastructure/Data/Migrations/` | Ticketing.Infrastructure | `Data/Migrations/` |
| `Infrastructure/Auth/` | Ticketing.Infrastructure | `Auth/` |
| `Api/` | Ticketing.Api | Root |
| `Program.cs` | Ticketing.Api | Root |
| `appsettings.json` | Ticketing.Api | Root |

### Refactor Strategy

#### Step 1: Create Repository Pattern
**Problem:** Application services directly use `AppDbContext` from Infrastructure.

**Solution:**
1. Create repository interfaces in `Ticketing.Application/Repositories/`:
   - `ITicketRepository`
   - `IUserRepository`
   - `ICategoryRepository`
   - `INotificationRepository`
   - etc. (one per aggregate root)

2. Implement repositories in `Ticketing.Infrastructure/Data/Repositories/`:
   - `TicketRepository : ITicketRepository`
   - `UserRepository : IUserRepository`
   - etc.

3. Update Application services to depend on repository interfaces instead of `AppDbContext`.

#### Step 2: Extract SignalR Dependency
**Problem:** Application services depend on `Api.Hubs.NotificationHub`.

**Solution:**
1. Create abstraction in `Ticketing.Application/Abstractions/`:
   - `INotificationHub` interface (methods: SendToUser, SendToGroup, etc.)

2. Implement in `Ticketing.Infrastructure/Notifications/`:
   - `SignalRNotificationHub : INotificationHub` (wraps IHubContext)

3. Register in Infrastructure DI (requires Api reference for IHubContext, but this is acceptable for DI wiring).

**Alternative:** Keep SignalR in Api layer, but inject `IHubContext` into Infrastructure service that implements the abstraction.

#### Step 3: Separate Projects
**Steps:**
1. Create new .csproj files
2. Add project references
3. Move files (maintain folder structure)
4. Update namespaces (may need minimal changes)
5. Update Program.cs DI registrations

#### Step 4: Fix Controller DbContext Usage
**Problem:** Some controllers inject `AppDbContext` directly.

**Solution:**
- Option A: Create Application service methods for these operations
- Option B: For debug/admin controllers, accept direct DbContext but document as intentional
- **Prefer Option A** for production code

### Implementation Order (Checkpoints)

**CHECKPOINT A — Project Scaffolding**
1. Create 4 new .csproj files
2. Add correct project references
3. Ensure solution builds (may fail, but structure is ready)

**CHECKPOINT B — Move Domain**
1. Move `Domain/` → `src/Ticketing.Domain/`
2. Update namespace (if needed)
3. Verify Domain project builds with no dependencies

**CHECKPOINT C — Create Repository Interfaces**
1. Create `Ticketing.Application/Repositories/` folder
2. Create repository interfaces (ITicketRepository, IUserRepository, etc.)
3. Move service interfaces to `Ticketing.Application/Services/`
4. Move DTOs to `Ticketing.Application/DTOs/`
5. Update Application project references
6. Build Application (will fail until repositories are implemented)

**CHECKPOINT D — Implement Repositories**
1. Create `Ticketing.Infrastructure/Data/Repositories/` folder
2. Implement repository classes
3. Update Infrastructure project references
4. Build Infrastructure

**CHECKPOINT E — Refactor Services**
1. Move service implementations to Infrastructure (OR keep in Application but inject repositories)
2. Update services to use repository interfaces instead of DbContext
3. Update SignalR dependency (use abstraction)
4. Build both projects

**CHECKPOINT F — Move Infrastructure**
1. Move Infrastructure folder → `src/Ticketing.Infrastructure/`
2. Move Migrations (keep history)
3. Update namespace references
4. Build Infrastructure

**CHECKPOINT G — Move API**
1. Move Api folder → `src/Ticketing.Api/`
2. Move Program.cs, appsettings.json
3. Update DI registrations
4. Build Api project

**CHECKPOINT H — Update DI & Fix Controllers**
1. Update Program.cs to register repositories
2. Fix controllers that use DbContext directly
3. Update all namespace usings
4. Full solution build

### Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Breaking existing functionality | Work checkpoint-by-checkpoint, build after each |
| Migration history lost | Keep entire Migrations folder, update namespace only |
| Circular dependencies | Careful project reference order (Domain → Application → Infrastructure → Api) |
| SignalR wiring complexity | Create abstraction, implement in Infrastructure with Api's IHubContext injected |
| Large service files | Keep existing files, just change dependencies |
| Test failures | Run tests after each checkpoint (if tests exist) |

### Rollback Plan

If refactor fails:
1. `git checkout pre-clean-arch` (revert to tag)
2. OR: `git checkout main` and delete branch `refactor/clean-architecture`
3. All original code is preserved in the tag

### Success Criteria

After refactor:
- ✅ 4 separate .csproj projects
- ✅ Domain has zero dependencies
- ✅ Application depends only on Domain
- ✅ Infrastructure depends on Application + Domain
- ✅ Api depends on Application + Infrastructure
- ✅ All services use repository interfaces (not DbContext directly)
- ✅ SignalR dependency extracted to abstraction
- ✅ `dotnet build` succeeds
- ✅ `dotnet ef migrations list` works
- ✅ Swagger loads and endpoints work
- ✅ No functional changes (same API behavior)

---

## PHASE 3 — IMPLEMENTATION

### CHECKPOINT A — Project Scaffolding ✅ COMPLETE

**Date:** 2025-01-02

**Actions Taken:**
1. Created 4 new .csproj projects in `src/`:
   - `src/Ticketing.Domain/Ticketing.Domain.csproj` (no dependencies)
   - `src/Ticketing.Application/Ticketing.Application.csproj` (references Domain)
   - `src/Ticketing.Infrastructure/Ticketing.Infrastructure.csproj` (references Application + Domain)
   - `src/Ticketing.Api/Ticketing.Api.csproj` (references Application + Infrastructure)

2. Created solution file: `Ticketing.Backend.sln` with all 4 projects

3. Verified project references:
   - ✅ Domain has no dependencies
   - ✅ Application → Domain
   - ✅ Infrastructure → Application + Domain
   - ✅ Api → Application + Infrastructure

**Build Status:**
- ✅ Ticketing.Domain: Builds successfully
- ✅ Ticketing.Application: Builds successfully  
- ✅ Ticketing.Infrastructure: Builds successfully
- ⚠️ Ticketing.Api: Fails (expected - no entry point yet, empty project)

**Files Created:**
- `src/Ticketing.Domain/Ticketing.Domain.csproj`
- `src/Ticketing.Application/Ticketing.Application.csproj`
- `src/Ticketing.Infrastructure/Ticketing.Infrastructure.csproj`
- `src/Ticketing.Api/Ticketing.Api.csproj`
- `Ticketing.Backend.sln`

**Commit:** ✅ Committed (78aba7f)

### CHECKPOINT B — Move Domain Code ✅ COMPLETE

**Date:** 2025-01-02

**Actions Taken:**
1. Created `src/Ticketing.Domain/Entities/` and `src/Ticketing.Domain/Enums/` directories
2. Copied all files from `Domain/Entities/` → `src/Ticketing.Domain/Entities/`
3. Copied all files from `Domain/Enums/` → `src/Ticketing.Domain/Enums/`
4. Updated all namespaces from `Ticketing.Backend.Domain.*` to `Ticketing.Domain.*`
5. Updated all using statements from `Ticketing.Backend.Domain.Enums` to `Ticketing.Domain.Enums`

**Files Moved:**
- 17 Entity files: Attachment.cs, Category.cs, Notification.cs, NotificationPreference.cs, SmartAssignmentRule.cs, Subcategory.cs, SubcategoryFieldDefinition.cs, SystemSettings.cs, Technician.cs, TechnicianSubcategoryPermission.cs, Ticket.cs, TicketActivity.cs, TicketFieldValue.cs, TicketMessage.cs, TicketTechnician.cs, TicketWorkSession.cs, User.cs, UserPreferences.cs
- 8 Enum files: FieldType.cs, NotificationPriority.cs, NotificationType.cs, TicketActivityType.cs, TicketPriority.cs, TicketStatus.cs, TicketTechnicianState.cs, UserRole.cs

**Build Status:**
- ✅ Ticketing.Domain: Builds successfully with 0 errors

**Commit:** ✅ Committed (83fb947)

### CHECKPOINT C — Create Repository Interfaces & Move Application Code ⏳ IN PROGRESS

**Date:** 2025-01-02

**Actions Taken So Far:**
1. Created directories in `src/Ticketing.Application/`: `DTOs/`, `Services/`, `Repositories/`
2. Moved all DTO files from `Application/DTOs/` → `src/Ticketing.Application/DTOs/`
3. Updated DTO namespaces from `Ticketing.Backend.Application.DTOs` to `Ticketing.Application.DTOs`
4. Updated DTO using statements from `Ticketing.Backend.Domain` to `Ticketing.Domain`
5. Extracted and moved service interfaces to `src/Ticketing.Application/Services/`:
   - ITicketService.cs
   - ICategoryService.cs
   - ITechnicianService.cs
   - IUserService.cs
   - INotificationService.cs
   - ISmartAssignmentService.cs
   - IFieldDefinitionService.cs
   - IUserPreferencesService.cs
   - ISystemSettingsService.cs
6. Added missing enums (`LinkUserResult`, `DeleteTechnicianResult`) to `TechnicianDtos.cs`
7. Updated all interface namespaces to `Ticketing.Application.Services`

**Build Status:**
- ✅ Ticketing.Application: Builds successfully with 0 errors
- ✅ All service interfaces extracted and moved
- ✅ All DTOs moved and namespaces updated

**Remaining Work for Checkpoint C:**
- ✅ Create repository interfaces (ITicketRepository, IUserRepository, etc.) - COMPLETE
- ✅ Create SignalR abstraction interface (INotificationHub) - COMPLETE
- ✅ Create Unit of Work interface (IUnitOfWork) - COMPLETE

**Files Created/Moved:**
- `src/Ticketing.Application/DTOs/*.cs` (10 files)
- `src/Ticketing.Application/Services/I*.cs` (9 interface files)
- `src/Ticketing.Application/Repositories/I*.cs` (10 repository interfaces)
- `src/Ticketing.Application/Abstractions/INotificationHub.cs` (SignalR abstraction)

**Repository Interfaces Created:**
- ITicketRepository
- IUserRepository
- ICategoryRepository
- INotificationRepository
- ITechnicianRepository
- ITicketActivityRepository
- ITicketMessageRepository
- ITicketTechnicianRepository
- ITicketWorkSessionRepository
- IUnitOfWork (coordinates all repositories)

**Build Status:**
- ✅ Ticketing.Application: Builds successfully with 0 errors

**Commit:** ✅ Committed (65e36e0)

### CHECKPOINT D — Implement Repositories ✅ COMPLETE

**Date:** 2025-01-02

**Actions Taken:**
1. Created `src/Ticketing.Infrastructure/Data/AppDbContext.cs` with updated namespace
2. Created all repository implementations in `src/Ticketing.Infrastructure/Data/Repositories/`:
   - TicketRepository
   - UserRepository
   - CategoryRepository
   - NotificationRepository
   - TechnicianRepository
   - TicketActivityRepository
   - TicketMessageRepository
   - TicketTechnicianRepository
   - TicketWorkSessionRepository
   - UnitOfWork (coordinates all repositories)

**Build Status:**
- ✅ Ticketing.Infrastructure: Builds successfully with 0 errors

**Note:** SignalR abstraction implementation (SignalRNotificationHub) will be created in Api project since Infrastructure cannot reference Api. This is acceptable - Api layer can implement Application abstractions.

**Files Created:**
- `src/Ticketing.Infrastructure/Data/AppDbContext.cs`
- `src/Ticketing.Infrastructure/Data/Repositories/*.cs` (10 repository files)

**Commit:** ✅ Committed (0464f22)

### CHECKPOINT E — Refactor Services ✅ COMPLETE

**Date:** 2025-01-02

**Objective:** Move service implementations to Infrastructure and refactor to use repositories instead of DbContext.

**Status:** ✅ COMPLETE (All 9 services refactored)

**Actions Taken:**
1. Created `src/Ticketing.Infrastructure/Services/` directory
2. Moved Auth infrastructure (`IJwtTokenGenerator`, `JwtTokenGenerator`, `JwtSettings`) to `src/Ticketing.Infrastructure/Auth/`
3. Refactored all 9 services to use `IUnitOfWork` instead of `AppDbContext`
4. Replaced `IHubContext<NotificationHub>` with `INotificationHub` abstraction for SignalR
5. Updated all service namespaces from `Ticketing.Backend.*` to `Ticketing.Infrastructure.Services`

**Services Refactored (9/9 complete):**
- ✅ CategoryService - COMPLETE
- ✅ SystemSettingsService - COMPLETE  
- ✅ UserPreferencesService - COMPLETE
- ✅ FieldDefinitionService - COMPLETE
- ✅ UserService - COMPLETE (+ Auth infrastructure moved)
- ✅ NotificationService - COMPLETE (+ SignalR abstraction)
- ✅ TechnicianService - COMPLETE
- ✅ SmartAssignmentService - COMPLETE
- ✅ TicketService - COMPLETE (~1663 lines, most complex service)

**Key Changes:**
- All `_context.*` calls replaced with `_unitOfWork.*Repository.*` calls
- All `SaveChangesAsync()` replaced with `_unitOfWork.SaveChangesAsync()`
- All SignalR calls replaced with `_notificationHub.SendToGroupAsync()` or `SendToUserAsync()`
- Added new repository methods where needed (`GetByTicketAndTechnicianIdAsync`, `GetBasicByIdAsync`, etc.)
- All services build successfully with no compilation errors
- ✅ FieldDefinitionService - COMPLETE

**Remaining Services to Refactor (5 services):**
- ⏳ TicketService (~1660 lines) - Most complex, uses SignalR, multiple repositories
- ⏳ UserService (~300 lines) - Uses JWT token generator, password hasher
- ⏳ NotificationService (~500 lines) - Uses SignalR hub context
- ⏳ TechnicianService (~570 lines) - Complex business logic
- ⏳ SmartAssignmentService (~240 lines) - Uses multiple services

**Pattern for Refactoring:**
1. Replace `AppDbContext _context` → `IUnitOfWork _unitOfWork`
2. Replace `IHubContext<NotificationHub>` → `INotificationHub` (where used)
3. Replace `_context.Tickets` → `_unitOfWork.Tickets`
4. Replace `_context.SaveChangesAsync()` → `_unitOfWork.SaveChangesAsync()`
5. Update namespaces from `Ticketing.Backend.*` to `Ticketing.*`
6. Remove `using Microsoft.EntityFrameworkCore` if no longer needed
7. Update `using Ticketing.Backend.Infrastructure.Data` → remove (not needed)

**Build Status:**
- ✅ Ticketing.Infrastructure: Builds successfully with CategoryService
- ⏳ Remaining 8 services need refactoring

**Files Created:**
- `src/Ticketing.Infrastructure/Services/CategoryService.cs`
- `src/Ticketing.Infrastructure/Services/SystemSettingsService.cs`
- `src/Ticketing.Infrastructure/Services/UserPreferencesService.cs`
- `src/Ticketing.Application/Repositories/ISystemSettingsRepository.cs`
- `src/Ticketing.Application/Repositories/IUserPreferencesRepository.cs`
- `src/Ticketing.Infrastructure/Data/Repositories/SystemSettingsRepository.cs`
- `src/Ticketing.Infrastructure/Data/Repositories/UserPreferencesRepository.cs`

**Additional Repositories Added:**
- ISystemSettingsRepository / SystemSettingsRepository
- IUserPreferencesRepository / UserPreferencesRepository

**Commit:** ✅ Committed (ea29273 + latest commit)

---

## PHASE 4 — VERIFICATION

_Results to be documented after implementation..._

---


# Backend Architecture Documentation

**Last Updated:** 2025-12-30

## Overview

The backend follows a **folder-based layered architecture** within a single project (`Ticketing.Backend.csproj`). While not fully compliant with Clean Architecture principles, the structure is organized and maintainable.

## Project Structure

```
Ticketing.Backend/
├── Domain/              # Core business entities and enums
│   ├── Entities/       # Domain models (User, Ticket, Category, etc.)
│   └── Enums/          # Domain enumerations (TicketStatus, UserRole, etc.)
├── Application/         # Application services and DTOs
│   ├── DTOs/           # Data Transfer Objects
│   └── Services/       # Service interfaces and implementations
├── Infrastructure/      # Data access and external concerns
│   ├── Data/           # DbContext, Migrations, Configurations
│   └── Auth/           # JWT authentication
├── Api/                # HTTP API layer
│   └── Controllers/    # REST API controllers
├── Program.cs          # Application entry point, DI configuration
└── Ticketing.Backend.csproj
```

## Layer Dependencies

### Current State

```
Domain
  └── (no dependencies) ✅

Application
  └── → Domain ✅
  └── → Infrastructure.Data (AppDbContext) ⚠️ VIOLATION
  └── → Api.Hubs (SignalR) ⚠️ VIOLATION

Infrastructure
  └── → Domain ✅

Api
  └── → Application ✅
  └── → Infrastructure ✅
  └── → Domain (for entities in some cases) ⚠️ MINOR
```

### Ideal Clean Architecture

```
Domain
  └── (no dependencies)

Application
  └── → Domain only

Infrastructure
  └── → Application (interfaces)
  └── → Domain

Api
  └── → Application
  └── → Infrastructure (for DI only)
```

## Layer Responsibilities

### Domain Layer
- **Purpose:** Core business logic and entities
- **Dependencies:** None (pure domain)
- **Contains:**
  - Entity classes (User, Ticket, Category, etc.)
  - Enums (TicketStatus, UserRole, FieldType, etc.)
- **Rules:**
  - No external dependencies
  - No EF Core attributes (use Fluent API in Infrastructure)
  - No API concerns

### Application Layer
- **Purpose:** Application services and use cases
- **Dependencies:** Domain (✅), Infrastructure.Data (⚠️), Api.Hubs (⚠️)
- **Contains:**
  - DTOs (request/response models)
  - Service interfaces (IFieldDefinitionService, etc.)
  - Service implementations (FieldDefinitionService, etc.)
- **Current Issues:**
  - Services directly inject `AppDbContext` (should use repository interfaces)
  - Some services use SignalR directly (should use abstraction)
- **Rules:**
  - Should depend only on Domain
  - Business logic lives here
  - No direct database access (should use repositories)

### Infrastructure Layer
- **Purpose:** Data persistence and external services
- **Dependencies:** Domain
- **Contains:**
  - `AppDbContext` (EF Core DbContext)
  - Entity configurations (Fluent API)
  - Migrations
  - JWT authentication
- **Rules:**
  - Implements data access
  - Should implement Application repository interfaces (future refactor)
  - No business logic

### Api Layer
- **Purpose:** HTTP API endpoints
- **Dependencies:** Application, Infrastructure, Domain
- **Contains:**
  - Controllers (REST endpoints)
  - SignalR hubs (real-time notifications)
- **Rules:**
  - Thin controllers (delegate to Application services)
  - No business logic
  - Handle HTTP concerns (status codes, content negotiation)

## Architecture Violations

### 1. Application → Infrastructure Dependency ⚠️
**Issue:** Application services directly inject `AppDbContext` from Infrastructure.

**Impact:** Medium - Breaks Clean Architecture principle but doesn't block functionality.

**Fix (Future):** Extract repository interfaces to Application, implement in Infrastructure.

**Example:**
```csharp
// Current (Application/Service/FieldDefinitionService.cs)
public class FieldDefinitionService : IFieldDefinitionService
{
    private readonly AppDbContext _context; // ⚠️ Direct dependency on Infrastructure
    // ...
}

// Ideal (Future)
public class FieldDefinitionService : IFieldDefinitionService
{
    private readonly IFieldDefinitionRepository _repository; // ✅ Depends on interface
    // ...
}
```

### 2. Application → Api Dependency ⚠️
**Issue:** Some services use `IHubContext<NotificationHub>` from Api layer.

**Impact:** Medium - Application shouldn't know about API concerns.

**Fix (Future):** Create abstraction in Application (e.g., `INotificationService`), implement in Infrastructure.

### 3. Some Controllers Use DbContext Directly ⚠️
**Issue:** Debug/admin controllers bypass Application layer.

**Impact:** Low - Only affects debug/admin endpoints.

**Files:**
- `AdminDebugController.cs`
- `AdminMaintenanceController.cs`

**Acceptable:** For admin/debug purposes, but should be documented.

## Migration Strategy (Future)

To achieve full Clean Architecture compliance:

1. **Extract Repository Interfaces**
   - Create `Application/Repositories/` folder
   - Define interfaces (ITicketRepository, IUserRepository, etc.)

2. **Implement Repositories**
   - Create `Infrastructure/Data/Repositories/` folder
   - Implement repository classes using AppDbContext

3. **Refactor Services**
   - Update services to inject repository interfaces
   - Remove direct AppDbContext dependencies

4. **Extract SignalR Abstraction**
   - Create `Application/Abstractions/INotificationHub.cs`
   - Implement in Infrastructure

5. **Separate Projects (Optional)**
   - Split into 4 projects: Domain, Application, Infrastructure, Api
   - Update project references

**See:** `CLEAN_ARCH_REPORT.md` for detailed refactor plan.

## Current Status

✅ **Working:** The current architecture is functional and maintainable for the current scale.

⚠️ **Improvements Needed:** Repository pattern and dependency inversion would improve testability and maintainability.

📋 **Priority:** Low - Not blocking, but recommended for long-term maintainability.

## Entry Point

**Project:** `Ticketing.Backend.csproj`

**Run Command:**
```powershell
cd backend/Ticketing.Backend
dotnet run
```

**See:** `README_RUN.md` for detailed run instructions.

---

**Note:** This architecture documentation reflects the current state. For a detailed refactor plan, see `CLEAN_ARCH_REPORT.md`.







# Handoff 403 Fix – Forbid(scheme) Bug and ProblemDetails

## Root cause

- **Bug:** `POST /api/tickets/{ticketId}/handoff` returned **500** with:  
  `"No authentication handler is registered for the scheme 'You are not assigned to this ticket'"`.
- **Cause:** In ASP.NET Core, `Forbid(string)` and `ForbidAsync(string)` treat the string as an **authentication scheme name**, not an error message. Passing a message (e.g. `ex.Message`) caused the framework to look up that scheme, fail, and throw → 500.

## Exact locations fixed

| File | Line (approx) | Before | After |
|------|----------------|--------|--------|
| `Api/Controllers/TicketsController.cs` | 637 | `return Forbid(ex.Message);` | `return this.ForbiddenProblem(ex.Message);` |
| `Api/Controllers/TechnicianTicketsController.cs` | 105 | `return Forbid("Only supervisors...");` | `return this.ForbiddenProblem("Only supervisors...");` |

## Changes made

### 1) Backend – handoff and all Forbid(message) usage

- **TicketsController.cs**
  - Handoff: catch `UnauthorizedAccessException` → `return this.ForbiddenProblem(ex.Message);` (no longer `Forbid(ex.Message)`).
  - Added `using Ticketing.Backend.Api.Extensions;`.
- **TechnicianTicketsController.cs**
  - Assignable technicians: replaced `Forbid("Only supervisors can view assignable technicians")` with `this.ForbiddenProblem("Only supervisors can view assignable technicians")`.
  - Added `using Ticketing.Backend.Api.Extensions;`.

### 2) Backend – shared helper for 403 ProblemDetails

- **New file:** `Api/Extensions/ControllerBaseExtensions.cs`
  - `ForbiddenProblem(this ControllerBase controller, string detail)`
  - Returns `Problem(statusCode: 403, title: "Forbidden", detail: detail)` (RFC 7807-style).
- All ticket-related 403 responses in `TicketsController` now use `this.ForbiddenProblem(ex.Message)` (or equivalent), including:
  - Handoff, Claim, UpdateTicket, AddMessage, GrantAccess, RevokeAccess, UpdateCollaborator, StatusChangeForbidden, InvalidOperationException (claim).

### 3) Backend – EnsureTechnicianCanAct and middleware

- **EnsureTechnicianCanActAsync** (in `TicketService`) was already correct: it throws `UnauthorizedAccessException("Ticket is read-only for you.")`. No change.
- Controllers catch `UnauthorizedAccessException` and return `ForbiddenProblem(ex.Message)` (403 + ProblemDetails).
- **GlobalExceptionHandlerMiddleware.cs**
  - `UnauthorizedAccessException` is now mapped to **403 Forbidden** (not 401), with `detail = exception.Message`, so any unhandled permission exception still returns 403 + ProblemDetails.

### 4) Frontend – api-client.ts

- **ProblemDetails:** Already preferred `body.detail` then `body.title`; kept and clarified comments.
- **403 vs 500:**
  - 403: logged as **warning** (permission denied), not as server crash; message from `detail` when available.
  - 500: logged as **error** with response snippet (~200 chars).
- **ApiError:** Added optional `isForbidden` and `isServerError` so UI can:
  - 403 → show user-friendly toast / disable actions (do not treat as crash).
  - 500 → show generic server error and log detail.

## Response shape (403)

- **Status:** 403  
- **Body (ProblemDetails):**  
  `{ "status": 403, "title": "Forbidden", "detail": "You are not assigned to this ticket" }`  
  (and optionally `type`, `traceId` from ASP.NET Core ProblemDetails.)

## How to test

1. **Backend**
   - Start backend (e.g. `.\tools\run-backend.ps1`).
   - As a **Candidate** technician on a ticket that is already **claimed** (AssignedToUserId set), call:
     - `POST /api/tickets/{ticketId}/handoff` with body `{ "toTechnicianUserId": "...", "deactivateCurrent": true }`.
   - **Expected:** 403 (not 500).  
     Response body: `{ "status": 403, "title": "Forbidden", "detail": "You are not assigned to this ticket" }` (or similar permission message).
   - Also try: add message, status change. Expect 403 with ProblemDetails when permission is denied, never 500 for these cases.

2. **Response shape**
   - Confirm status 403 and body has `status`, `title`, `detail` (ProblemDetails).
   - Confirm `detail` is the human-readable message (e.g. "You are not assigned to this ticket").

3. **Frontend**
   - From the UI, as a candidate technician on a claimed ticket, trigger handoff (or other restricted action).
   - **Expected:** Readable error in toast (or message), not “server crashed”.
   - **Console:** 403 logged as warning with correct status and message; not logged as a generic server error.

## Constraints respected

- Authorization logic unchanged (no weakening).
- No new DB columns or migrations.
- Admin / TechSupervisor / Technician behavior kept consistent; only the way 403 is returned was fixed and standardized.

# TikQ — Internal Ticketing System

TikQ is an internal help-desk and ticketing application for organizations. It lets employees submit support requests, allows technicians to manage and resolve them, and gives administrators visibility and control over categories, users, and workflows. The system is designed for **intranet deployment** and can integrate with an existing company directory (read-only) for identity while keeping all application data and roles in its own database.

---

## Project Overview

**What TikQ is**  
TikQ is a web-based ticketing system that centralizes support requests, assignment, and resolution. Users open tickets by category; technicians work from assigned queues; admins configure categories, custom fields, and technician assignment rules.

**What problem it solves**  
It replaces ad-hoc email or spreadsheets with a single place to create tickets, track status, attach files, and see history. Roles and permissions are clear: clients submit, technicians resolve, admins configure. The application can run entirely on the intranet with no dependency on public internet services.

---

## System Capabilities

**Ticket lifecycle**  
Tickets move from creation through assignment, work, and closure. Supported states include open, in progress, resolved, and closed. Optional priorities and due dates help with triage. Tickets can have file attachments and an activity timeline.

**Roles**  
- **Client**: Submit tickets, view own tickets, add comments and attachments.  
- **Technician**: View assigned tickets, update status, add notes, collaborate with other technicians.  
- **Admin**: Full access to categories, users, technicians, reports, and system settings.  

**Dashboards**  
Each role has a dedicated dashboard: clients see their requests; technicians see their queue and assignment; admins see overview, reports, and management screens.

**Workflows**  
Admins define categories and subcategories. Optional custom fields (including multi-select) can be attached to subcategories. Ticket assignment can be manual or use automatic rules (e.g. by category or expertise). The system supports optional integration with a company directory for login (Windows or SQL-based); when used, the directory is read-only for identity lookup only.

---

## Architecture Summary

**Backend (.NET)**  
The API is built with ASP.NET Core. It provides REST endpoints for authentication, tickets, categories, users, technicians, and admin operations. The primary data store is configurable (SQL Server recommended for production; SQLite is available for development). Optional Company Directory integration uses a separate, read-only connection to the organization’s directory database.

**Frontend (Next.js)**  
The web UI is a Next.js application (TypeScript, React). It talks to the backend API via a configurable base URL and uses cookie-based or bearer token authentication.

**TikQ database**  
All application data—users, roles, tickets, categories, custom fields, assignments—lives in the TikQ database. This is the only database on which the application runs migrations and writes.

**Company database (read-only)**  
When Company Directory is enabled, the application connects to the organization’s existing directory (e.g. “Boss” or “Company” DB) **read-only**. It is used only for identity (e.g. email, display name, active flag) and optional password verification. No schema changes or writes are performed against this database.

---

## Security Model

**Authentication**  
- **JWT**: Primary mechanism; tokens are issued after successful login and can be sent in cookies or headers.  
- **Cookie**: The frontend can use HTTP-only cookies for the access token when configured.  
- **Windows / Company Directory**: Optional integration for intranet single sign-on or directory-backed login.  

**Role resolution**  
Roles (Admin, Technician, Client) and landing paths are stored and resolved **only in the TikQ database**. The Company DB is not used to assign or override roles. If a user has no valid role in TikQ, they receive an error and must be provisioned by an administrator.

**Read-only Company DB**  
When Company Directory is enabled, the application enforces read-only use of the directory connection (no INSERT/UPDATE/DELETE/DDL). Database-level read-only permissions for the directory are still the organization’s responsibility.

---

## Deployment Model

**Intranet-first**  
TikQ is intended for deployment on the organization’s internal network. The backend and frontend can be hosted on internal servers; no outbound dependency on public internet services is required.

**Environment configuration**  
Production deployment requires explicit configuration: JWT secret, production database connection, and—if Company Directory is used—connection string and mode. Debug and maintenance endpoints are disabled in production. See **docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md** for required environment variables, database responsibilities, and failure scenarios.

**No internet dependency**  
Core operation does not depend on external APIs or third-party SaaS. Optional features (e.g. email) can be configured if the organization chooses.

---

## Responsibility Boundary

**Organization owns**  
- Deployment and hosting of the TikQ backend and frontend.  
- Infrastructure: servers, network, HTTPS, and DNS.  
- Configuration of environment variables and secrets (JWT, connection strings).  
- User and role provisioning in TikQ (e.g. creating users and assigning roles in the TikQ database).  
- If Company Directory is used: ensuring the directory database is available and that the connection string and permissions (read-only) are correct.  

**TikQ does not manage**  
- The Company/Directory database schema or data. TikQ only reads from it when the feature is enabled.  
- Creation or modification of users in the Company DB; identity is looked up, not provisioned there.  
- External identity providers beyond the optional Windows/Directory integration and email/password fallback.  

**Roles live in TikQ**  
All role assignments (Admin, Technician, Client) and application permissions are stored and managed in the TikQ database only.

---

## Documentation

| Document | Purpose |
|----------|---------|
| **docs/01_Runbook/DEPLOYMENT_REQUIRED_CONFIG.md** | Required environment variables, database roles, security and deployment options, common failure scenarios, first-run behavior. |
| **docs/04_Handoff/HANDOFF_READINESS_CHECKLIST.md** | Pre-handoff and pre-production checklist. |
| **docs/HANDOFF_DIFF_SUMMARY.md** | Summary of production-hardening changes. |
| **docs/04_Handoff/HANDOFF_WHAT_COULD_STILL_FAIL.md** | Risks and considerations after handoff. |

Development and debugging notes have been archived under **docs/archive/dev-history/** and are not part of the delivery set.

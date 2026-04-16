# Production: Client Cannot Create Ticket (Categories Empty) — Fix

## Why it failed

- **Symptom:** In production, when the database had **zero categories**, the client UI could not create a ticket (e.g. category/subcategory dropdowns empty or API returning no options).
- **Cause:** Ticket creation requires at least one category and one subcategory. Fresh or migrated production DBs sometimes had an empty `Categories` table (no dev-style seed runs in production), so `GET /api/categories` returned `[]` and the client had nothing to select.

## How it’s fixed

1. **Backend — minimal seed when categories are empty (production only)**  
   After migrations (and user bootstrap), in **production** startup we now:
   - If `Categories` has **0** rows: insert a minimal default set (e.g. one category “Hardware” with one subcategory “Laptop”).
   - If there is already at least one category: do nothing (idempotent).
   - Log clearly: `[SEED_MIN] Categories empty; inserting defaults…` or `[SEED_MIN] Skipped (already has categories).`

2. **Frontend — production always uses `NEXT_PUBLIC_API_BASE_URL`**  
   - Production build uses only `.env.production` for the API base URL (no accidental override from `.env.local`).
   - Dev uses `.env.development.local` (see `.env.development.local.example`); `.env.local` is not required for prod and should not set `NEXT_PUBLIC_*` when building for production.

So:
- **Root cause:** Empty categories in production DB.
- **Fix:** Idempotent minimal category seed on production startup + clear env precedence so the frontend calls the intended backend.

## How to test

- **Categories non-empty:**  
  `curl http://localhost:8080/api/categories` (or your backend URL) returns a non-empty JSON array (e.g. at least one category with subcategories).
- **Client can create a ticket:**  
  Log in as `client@local`, open the create-ticket flow, choose category/subcategory, submit; ticket is created successfully.

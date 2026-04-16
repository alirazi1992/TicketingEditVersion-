# POST /api/categories – manual smoke tests

Use these to verify create-category behavior after the 500 fix (payload + validation + 400/409).

**Base URL:** `http://localhost:8080` (or your backend, e.g. `http://localhost:5000`).  
**Auth:** Admin cookie or `Authorization: Bearer <token>`.

## 1. Valid create → 201/200

```bash
# With cookie auth (after login), or add: -H "Authorization: Bearer YOUR_TOKEN"
curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "http://localhost:8080/api/categories" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Hardware\"}"
```

Expected: HTTP 201 (or 200) and JSON category with `id`, `name`, etc.

## 2. Empty name → 400 validation errors

```bash
curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "http://localhost:8080/api/categories" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"\"}"
```

Expected: HTTP 400 with body like `{"message":"Validation failed","errors":{"name":["Name is required"]}}`.

## 3. Duplicate name → 409

Create a category once (e.g. name `"Hardware"`), then send the same name again:

```bash
curl -s -w "\nHTTP_CODE:%{http_code}" -X POST "http://localhost:8080/api/categories" \
  -H "Content-Type: application/json" \
  -d "{\"name\":\"Hardware\"}"
```

Expected on duplicate: HTTP 409 with `{"message":"Category name already exists","code":"DUPLICATE_NAME"}`.

## PowerShell (cookie-based)

See `_handoff_tests/create-category.ps1` for a full script that logs in as admin and runs create then duplicate (201 then 409).

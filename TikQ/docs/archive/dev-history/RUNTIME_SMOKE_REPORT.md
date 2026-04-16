# BACKEND SMOKE TEST RESULTS

**Generated**: 2025-12-28 09:26:53  
**Backend URL**: http://localhost:5000  
**Duration**: 1.13s

## Summary

- âœ… **Passed**: 15
- âŒ **Failed**: 0
 - **Total**: 15

## Test Results

| Status | Test | Expected | Actual | Description | Error |
|--------|------|----------|--------|-------------|-------|
| âœ… PASS | Swagger UI | 200 | 200 | Swagger UI accessibility |  |
| âœ… PASS | Debug Users Endpoint | 200 | 200 | List all users |  |
| âœ… PASS | Categories (Public) | 200 | 200 | Public categories endpoint |  |
| âœ… PASS | Login (Admin) | 200 | 200 | Login successful with correct role |  |
| âœ… PASS | Login (Technician) | 200 | 200 | Login successful with correct role |  |
| âœ… PASS | Login (Client) | 200 | 200 | Login successful with correct role |  |
| âœ… PASS | Unauthorized Access | 401 | 401 | Protected endpoint returns 401 |  |
| âœ… PASS | GET /api/auth/me (Client) | 200 | 200 | Authenticated user info |  |
| âœ… PASS | GET /api/tickets (Client) | 200 | 200 | Client can view tickets |  |
| âœ… PASS | GET /api/tickets/{id} (Client) | 200 | 200 | Client can view ticket detail |  |
| âœ… PASS | Create Ticket (Client) | 200/201 | 201 | Ticket created successfully |  |
| âœ… PASS | GET /api/technician/tickets | 200 | 200 | Technician tickets endpoint |  |
| âœ… PASS | GET /api/tickets (Admin) | 200 | 200 | Admin can view all tickets |  |
| âœ… PASS | PUT /api/tickets/{id}/responsible (Endpoint exists) | 400/404 | 400 | Responsible endpoint exists (invalid data handled correctly) |  |
| âœ… PASS | Client 403 on Admin Endpoint | 403 | 403 | Client forbidden from admin endpoints |  |


## Details

All tests passed! âœ…


---
*Produced by automated smoke test script*



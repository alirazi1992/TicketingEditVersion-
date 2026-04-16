# Fix for Infinite GET /api/technician/tickets Loop

## Root Cause
The `useEffect` hook in `frontend/app/page.tsx` (line 186-205) has a dependency on the `user` object, which changes reference on each render, causing an infinite loop.

## The Problem
```typescript
useEffect(() => {
  // ... loadTickets code ...
}, [token, user, categoriesReady]);  // ❌ 'user' object reference changes each render
```

## The Fix
Change line 205 from:
```typescript
}, [token, user, categoriesReady]);
```

To:
```typescript
}, [token, user?.id, user?.role, categoriesReady]);
```

## Why It Works
- `user?.id` and `user?.role` are stable primitive values
- They only change when the actual user ID or role changes
- The effect runs only on actual value changes, not reference changes
- This stops the infinite loop: effect -> loadTickets -> setTickets -> re-render -> effect

## Expected Result
- `loadTickets` called once on mount
- No infinite loop
- Backend spam stops
- GET /api/technician/tickets happens once (not continuously)










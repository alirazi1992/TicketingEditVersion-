# Real-Time Synchronization Test Checklist

## Overview
This checklist verifies that ticket status and replies sync in real-time across all dashboards (Client, Technician, Supervisor, Admin) without manual refresh.

## Prerequisites
1. Backend running at http://localhost:5000
2. Frontend running at http://localhost:3000
3. Multiple browser windows/tabs open for different user roles

## Test Setup
Create the following test users (or use existing ones):
- Client: `client@test.com`
- Technician 1: `tech1@test.com`
- Technician 2: `tech2@test.com`
- Admin: `admin@test.com`

---

## Phase 1: Basic Real-Time Status Sync

### Test 1.1: Status Change Propagation (Admin → All)
**Steps:**
1. Open 4 browser windows/tabs:
   - Tab A: Login as Client, view ticket list
   - Tab B: Login as Technician 1, view ticket list
   - Tab C: Login as Technician 2, view ticket list  
   - Tab D: Login as Admin, view ticket list
2. As Admin (Tab D), change a ticket's status from "Open" to "InProgress"
3. **Expected:** Within 2-3 seconds, Tabs A, B, C should show the updated status WITHOUT manual refresh

**Pass:** [ ] | **Fail:** [ ]

### Test 1.2: Status Change Propagation (Technician → All)
**Steps:**
1. Keep the same 4 tabs open
2. As Technician 1 (Tab B), open a ticket and mark it as "Solved"
3. **Expected:** Within 2-3 seconds:
   - Tab A (Client): Shows "Solved"
   - Tab C (Technician 2): Shows "Solved"
   - Tab D (Admin): Shows "Solved"

**Pass:** [ ] | **Fail:** [ ]

### Test 1.3: Redo Status Mapping for Client
**Steps:**
1. Keep the same 4 tabs open
2. As Admin (Tab D), change a ticket's status to "Redo"
3. **Expected:**
   - Tab A (Client): Shows "InProgress" (Redo is hidden from client)
   - Tab B (Technician 1): Shows "Redo"
   - Tab C (Technician 2): Shows "Redo"
   - Tab D (Admin): Shows "Redo"

**Pass:** [ ] | **Fail:** [ ]

---

## Phase 2: Multi-Technician Real-Time Sync

### Test 2.1: Reply Sync Between Assigned Technicians
**Steps:**
1. Create a ticket assigned to BOTH Technician 1 and Technician 2
2. Tab A: Technician 1 views the ticket detail page
3. Tab B: Technician 2 views the same ticket detail page
4. As Technician 2 (Tab B), add a reply: "I'm investigating this issue"
5. **Expected:** Within 2-3 seconds, Technician 1 (Tab A) sees the new reply WITHOUT refresh

**Pass:** [ ] | **Fail:** [ ]

### Test 2.2: Status Change Sync Between Assigned Technicians
**Steps:**
1. Same ticket as Test 2.1
2. As Technician 1 (Tab A), change status to "InProgress"
3. **Expected:** Within 2-3 seconds, Technician 2 (Tab B) sees status change to "InProgress"

**Pass:** [ ] | **Fail:** [ ]

---

## Phase 3: Ticket Detail Page Real-Time Updates

### Test 3.1: Reply Notification on Detail Page
**Steps:**
1. Tab A: Client viewing ticket detail page
2. Tab B: Assigned Technician viewing same ticket
3. Technician (Tab B) adds a reply
4. **Expected:** 
   - Client (Tab A) sees new reply appear
   - Toast notification shown: "پاسخ جدید" (New reply)

**Pass:** [ ] | **Fail:** [ ]

### Test 3.2: Status Change Notification on Detail Page
**Steps:**
1. Same setup as Test 3.1
2. Technician (Tab B) changes status
3. **Expected:**
   - Client (Tab A) sees status badge update
   - Toast notification shown: "تغییر وضعیت" (Status changed)

**Pass:** [ ] | **Fail:** [ ]

---

## Phase 4: Dashboard List Updates

### Test 4.1: Ticket List Auto-Refresh on Update
**Steps:**
1. Tab A: Client dashboard showing ticket list
2. Tab B: Admin dashboard
3. Admin (Tab B) creates a new ticket assignment
4. **Expected:** Client (Tab A) ticket list updates without refresh

**Pass:** [ ] | **Fail:** [ ]

### Test 4.2: Unseen Indicator Updates
**Steps:**
1. Tab A: Technician 1 dashboard showing ticket list
2. Tab B: Client sends a new reply to assigned ticket
3. **Expected:** Technician 1 sees blue dot (unseen indicator) appear on that ticket

**Pass:** [ ] | **Fail:** [ ]

---

## Phase 5: Connection Resilience

### Test 5.1: Reconnection After Network Hiccup
**Steps:**
1. Open a dashboard
2. Disconnect network for 5 seconds
3. Reconnect network
4. **Expected:** SignalR reconnects automatically (check console for "[SignalR] Reconnected!")

**Pass:** [ ] | **Fail:** [ ]

### Test 5.2: Fallback Polling When SignalR Fails
**Steps:**
1. Open a dashboard
2. Block SignalR WebSocket (or stop backend)
3. Wait 30 seconds
4. **Expected:** Dashboard still refreshes via polling fallback

**Pass:** [ ] | **Fail:** [ ]

---

## Phase 6: Backend Integration Tests

Run the backend integration tests:
```bash
cd backend/Ticketing.Backend.Tests
dotnet test
```

### Tests to verify pass:
- [ ] `StatusChange_IsSynchronized_AcrossAllDashboards`
- [ ] `ListEndpoints_ReturnUpdatedStatus_ForAllTechnicians`
- [ ] `RedoStatus_DisplaysAsInProgress_ForClient`
- [ ] `SolvedStatus_VisibleToAllRoles_AsIs`
- [ ] `MapStatusForRole_ReturnsCorrectDisplayStatus`
- [ ] `SeenRead_Transition_OnFirstTechnicianView`
- [ ] `Client_CannotChangeStatus_ToForbiddenValues`
- [ ] `Reply_IsVisibleToAllAssignedTechnicians`
- [ ] `Reply_CreatesActivityEvent`

---

## Summary

| Phase | Test | Status |
|-------|------|--------|
| 1.1 | Status Admin → All | |
| 1.2 | Status Technician → All | |
| 1.3 | Redo mapping for Client | |
| 2.1 | Reply sync between techs | |
| 2.2 | Status sync between techs | |
| 3.1 | Reply notification | |
| 3.2 | Status notification | |
| 4.1 | List auto-refresh | |
| 4.2 | Unseen indicator | |
| 5.1 | Reconnection | |
| 5.2 | Fallback polling | |
| 6 | Backend tests | |

---

## SignalR Events Reference

### Events Emitted by Backend:
- `TicketStatusUpdated` - When ticket status changes
  ```json
  {
    "ticketId": "guid",
    "oldStatus": "Open",
    "newStatus": "InProgress",
    "updatedAt": "2024-01-15T10:00:00Z",
    "actorUserId": "guid",
    "actorRole": "Technician"
  }
  ```

- `TicketUpdated` - When reply added or assignment changed
  ```json
  {
    "ticketId": "guid",
    "updateType": "ReplyAdded" | "AssignmentChanged",
    "updatedAt": "2024-01-15T10:00:00Z",
    "metadata": { ... }
  }
  ```

### Hub URL: `/hubs/tickets`

### Hub Methods:
- `SubscribeToTicket(ticketId)` - Subscribe to specific ticket updates
- `UnsubscribeFromTicket(ticketId)` - Unsubscribe from ticket updates

### Groups:
- `user_{userId}` - Per-user updates
- `ticket_{ticketId}` - Per-ticket updates (for detail page)
- `admins` - All admin updates

---

## Troubleshooting

### SignalR Not Connecting
1. Check browser console for errors
2. Verify hub URL is `/hubs/tickets` (not `/api/notificationHub`)
3. Check auth token is being passed

### Updates Not Appearing
1. Check browser console for SignalR events
2. Verify `[Realtime]` log messages
3. Check backend logs for `BroadcastStatusUpdateAsync` calls

### Fallback Polling Not Working
1. Verify polling interval is 30 seconds
2. Check `realtimeConnected` state in React DevTools

# Responsive QA Checklist — TikQ Frontend

Viewport sizes tested: **360×800** (mobile), **768×1024** (tablet), **1366×768** and **1920×1080** (desktop).

---

## 1. Routes & Pages Tested

| Route / Area | Mobile 360×800 | Tablet 768×1024 | Desktop 1366×768 | Notes / Fixes |
|--------------|----------------|-----------------|------------------|----------------|
| **/login** | ✅ | ✅ | ✅ | Added `overflow-x-hidden`; form and demo credentials wrap. |
| **/ (Admin dashboard)** | ✅ | ✅ | ✅ | Sidebar → Sheet on &lt;md; content `px-3 sm:px-6 lg:px-8`; no horizontal scroll. |
| **/ (Client dashboard)** | ✅ | ✅ | ✅ | Ticket list: cards on mobile, table on md+; table has `overflow-x-auto` + `min-w-[700px]`. |
| **/ (Technician dashboard)** | ✅ | ✅ | ✅ | Same card/table pattern; stats grid stacks on mobile. |
| **/ (Supervisor view)** | ✅ | ✅ | ✅ | Uses same shell; technician tables in modals scroll. |
| **Admin — تخصیص تیکت‌ها (assignment)** | ✅ | ✅ | ✅ | Mobile cards + horizontal scroll table; Analyze modal scrollable. |
| **Admin — مدیریت تیکت‌ها (ticket list)** | ✅ | ✅ | ✅ | Table in `overflow-x-auto` + `min-w-[700px]`. |
| **Admin — تقویم (calendar)** | ✅ | ✅ | ✅ | Smaller gaps/fonts on mobile; day cells `min-w-0 overflow-hidden`; +N more. |
| **Admin — گزارش‌ها (reports)** | ✅ | ✅ | ✅ | Dialogs use default `max-h-[85vh] overflow-y-auto`. |
| **Admin — مدیریت تکنسین‌ها** | ✅ | ✅ | ✅ | Table wrapper `overflow-x-auto`, `min-w-[700px]`. |
| **Admin — مدیریت دسته‌بندی‌ها** | ✅ | ✅ | ✅ | Dialogs responsive. |
| **Admin — تنظیمات خودکار** | ✅ | ✅ | ✅ | Modals scroll. |
| **/tickets/[id]** (ticket detail) | ✅ | ✅ | ✅ | `px-3 sm:px-6`, `overflow-x-hidden`; message bubbles `break-words max-w-full`; ticket id `break-words`. |

---

## 2. Layout & Sidebar

- **Breakpoint:** Sidebar visible at **md (768px)+**; below md, **hamburger** opens **Sheet** (shadcn) from the right.
- **Content:** Main area uses `px-3 sm:px-6 lg:px-8`, `min-w-0` on flex children, `overflow-x-hidden` on page and main.
- **Fix:** Replaced custom overlay + fixed aside with **Sheet** for mobile nav; switched breakpoint from `lg` to `md`.

---

## 3. Tables

- **Wide tables:** Wrapped in a container with `overflow-x-auto` and table `min-w-[700px]` (or `min-w-[700px]` on table) so horizontal scroll is contained.
- **Mobile card layout:** On **admin assignment**, **client tickets**, and **technician tickets**, below **md** a **card list** is shown (`md:hidden`), and the table is **hidden md:block**.
- **Other tables:** Supervisor modals, technician management, admin ticket list: table scroll only (no card layout).

---

## 4. Modals / Dialogs

- **Default (ui/dialog):** `DialogContent` has `w-[95vw] sm:w-[90vw]`, `max-h-[85vh]`, `overflow-y-auto`, `p-4 sm:p-6`.
- **Existing overrides:** Dialogs that already set `max-h-[90vh]` or `max-h-[85vh]` and `overflow-y-auto` or inner scroll (e.g. supervisor link dialog with `flex-1 overflow-y-auto min-h-0`) left as-is.
- **Result:** No modal content forces viewport overflow; vertical scroll works on small screens.

---

## 5. Ticket Detail & Messages

- **Container:** Ticket detail page uses `px-3 sm:px-6 py-4 sm:py-6`, `overflow-x-hidden`, `min-w-0` on main column.
- **Ticket ID:** `break-words` on ticket id element.
- **Message bubbles:** `break-words max-w-full` on message text; card has `min-w-0 overflow-hidden`; author/date wrap with `flex-wrap`.

---

## 6. Calendar

- **Grid:** `gap-1 sm:gap-2`; week day labels `text-[10px] sm:text-xs`, `py-1.5 sm:py-2`, `min-w-0 truncate`.
- **Day cells:** `min-h-[100px] sm:min-h-[120px]`, `p-2 sm:p-3`, `min-w-0 overflow-hidden`, `rounded-xl sm:rounded-2xl`.
- **Tickets in cell:** List has `overflow-y-auto overflow-x-hidden`, `min-h-0`; "+N more" and "نمایش کمتر" unchanged.

---

## 7. Global CSS

- **Long IDs / strings:** `[data-ticket-id], .ticket-id, [class*="ticketId"]` use `word-break: break-word; overflow-wrap: break-word`.
- **Images:** `img:not([width]):not([height])` get `max-width: 100%; height: auto`.
- **Body:** `body { overflow-x: hidden; }` to avoid page-level horizontal scroll.

---

## 8. Verification Summary

| Check | Result |
|-------|--------|
| No horizontal scroll on mobile (except tables) | ✅ |
| Nav/sidebar usable (hamburger + Sheet on &lt;md) | ✅ |
| Forms and inputs usable on all sizes | ✅ |
| Buttons not off-screen | ✅ |
| Calendar usable on mobile (tap day, +N more) | ✅ |
| Modals fit viewport and scroll vertically | ✅ |

---

## 9. Files Changed

- `app/globals.css` — Responsive/break-words/image/body overflow rules.
- `app/layout.tsx` — (unchanged; no overflow changes at root.)
- `app/login/page.tsx` — `overflow-x-hidden` on root.
- `app/tickets/[id]/page.tsx` — Responsive padding, overflow-x-hidden, ticket id and message break-words.
- `components/dashboard-shell.tsx` — md breakpoint, Sheet for mobile nav, responsive padding, min-w-0, overflow-x-hidden.
- `components/ui/dialog.tsx` — DialogContent: w-[95vw] sm:w-[90vw], max-h-[85vh], overflow-y-auto, responsive padding.
- `components/admin-technician-assignment.tsx` — Mobile card list + table overflow + min-w; (dialogs use default).
- `components/admin-ticket-list.tsx` — Table wrapper overflow-x-auto, min-w-[700px].
- `components/client-dashboard.tsx` — Mobile card list + table overflow + min-w.
- `components/technician-dashboard.tsx` — Mobile card list + table overflow + min-w; fixed Card/Dialog nesting.
- `components/technician-management.tsx` — Table min-w-[700px] (wrapper already had overflow-x-auto).
- `components/ticket-calendar-overview.tsx` — Responsive grid/labels/cells and overflow.

---

*Last updated: Responsive pass — layout, tables, modals, ticket detail, calendar, global CSS.*

// Example page to preview the ticket calendar with built-in mock data.
"use client";

import { TicketCalendarOverview } from "@/components/ticket-calendar-overview";

export default function TicketsCalendarDemo() {
  return (
    <div className="p-6" dir="rtl">
      {/* Passing an empty array lets the component load its internal sample tickets. */}
      <TicketCalendarOverview tickets={[]} />
    </div>
  );
}

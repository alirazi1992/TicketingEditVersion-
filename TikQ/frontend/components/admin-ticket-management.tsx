"use client"

import { AdminManagementTickets } from "@/components/admin-management-tickets"
import type { Ticket } from "@/types"

interface AdminTicketManagementProps {
  authToken?: string | null
  tickets?: Ticket[]
  onRefreshTickets?: () => void | Promise<void>
}

export function AdminTicketManagement({
  authToken,
  tickets,
  onRefreshTickets,
}: AdminTicketManagementProps) {
  return (
    <div className="space-y-6">
      <AdminManagementTickets authToken={authToken} tickets={tickets} onRefreshTickets={onRefreshTickets} />
    </div>
  )
}


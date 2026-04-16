/**
 * Ticket Status Definitions and Persian Labels
 * 
 * This file serves as the single source of truth for ticket status types and their Persian labels.
 * Backend stores statuses as English enum keys, but frontend displays Persian labels.
 * 
 * IMPORTANT: Status vs Read-State Separation
 * - STATUS (this file): Workflow state of the ticket (Submitted -> SeenRead -> Open -> InProgress -> Solved)
 * - READ-STATE (isUnseen): Per-user indicator whether user has seen recent activity
 * 
 * CANONICAL STATUSES (stored in DB):
 * - Submitted: New ticket, just created
 * - SeenRead: Ticket has been viewed by technician/admin
 * - Open: Ticket is ready for work
 * - InProgress: Work is actively being done
 * - Solved: Ticket has been solved/answered (terminal)
 * - Redo: Ticket needs rework (internal - clients see InProgress)
 * 
 * VISIBILITY RULES:
 * - Redo: Only visible to Technician/Supervisor/Admin
 *         For Clients, displayStatus = InProgress when canonicalStatus = Redo
 * - All other statuses: Visible to all roles
 * 
 * UI should use ticket.displayStatus for badges/labels (backend handles mapping)
 * Blue dot / unseen indicator should use ticket.isUnseen
 */

export type TicketStatus =
  | "Submitted"
  | "SeenRead"
  | "Open"
  | "InProgress"
  | "Solved"
  | "Redo"

export type UserRole = "client" | "technician" | "admin"

// Base labels for all statuses (Persian)
export const TICKET_STATUS_LABELS: Record<TicketStatus, string> = {
  Submitted: "ثبت شد",
  SeenRead: "مشاهده شد",
  Open: "باز",
  InProgress: "در حال انجام",
  Solved: "حل شده",
  Redo: "بازبینی",
}

// Keep BASE_STATUS_LABELS as alias for backward compatibility
const BASE_STATUS_LABELS = TICKET_STATUS_LABELS

export const TICKET_STATUS_OPTIONS: Array<{ value: TicketStatus; label: string }> = [
  { value: "Submitted", label: BASE_STATUS_LABELS.Submitted },
  { value: "SeenRead", label: BASE_STATUS_LABELS.SeenRead },
  { value: "Open", label: BASE_STATUS_LABELS.Open },
  { value: "InProgress", label: BASE_STATUS_LABELS.InProgress },
  { value: "Solved", label: BASE_STATUS_LABELS.Solved },
  { value: "Redo", label: BASE_STATUS_LABELS.Redo },
]

/**
 * Get status options available for a specific role.
 * Clients should NOT see Redo in dropdowns.
 */
export function getStatusOptionsForRole(role?: UserRole): Array<{ value: TicketStatus; label: string }> {
  if (role === "client") {
    return TICKET_STATUS_OPTIONS.filter(opt => opt.value !== "Redo")
  }
  return TICKET_STATUS_OPTIONS
}

/**
 * Get effective status for display based on user role.
 * NOTE: Backend now handles this mapping via displayStatus field.
 * This function is kept for backward compatibility but should use ticket.displayStatus when available.
 * 
 * Rule: Clients see "InProgress" when canonical status is "Redo"
 */
export function getEffectiveStatus(status: TicketStatus, role?: UserRole): TicketStatus {
  // Clients see "InProgress" when status is "Redo" (internal status)
  if (status === "Redo" && role === "client") {
    return "InProgress"
  }
  return status
}

/**
 * Get Persian label for a ticket status based on user role.
 * NOTE: Prefer using ticket.displayStatus from backend for correct mapping.
 */
export function getTicketStatusLabel(status: TicketStatus, role?: UserRole): string {
  const effectiveStatus = getEffectiveStatus(status, role)
  return BASE_STATUS_LABELS[effectiveStatus] || effectiveStatus
}

/**
 * Get color for a ticket status badge.
 * Uses displayStatus (already mapped by backend) for consistent coloring.
 */
export function getTicketStatusColor(status: TicketStatus, role?: UserRole): string {
  // Use effective status for color mapping
  const displayStatus = getEffectiveStatus(status, role)
  
  switch (displayStatus) {
    case "Submitted":
      return "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200"
    case "SeenRead":
      return "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200"
    case "Open":
      return "bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200"
    case "InProgress":
      return "bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-200"
    case "Solved":
      return "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200"
    case "Redo":
      return "bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200"
    default:
      return "bg-gray-100 text-gray-800 dark:bg-gray-900 dark:text-gray-200"
  }
}

/** Canonical API status values for filtering and comparison. */
const API_STATUS_VALUES: TicketStatus[] = [
  "Submitted",
  "SeenRead",
  "Open",
  "InProgress",
  "Solved",
  "Redo",
]

/**
 * Normalize ticket to a single status value for admin filtering and display.
 * Use this so filter dropdown (API enum values) matches ticket.displayStatus/status.
 */
export function statusForUi(
  t: { displayStatus?: TicketStatus | string; status?: TicketStatus | string } | null | undefined
): TicketStatus {
  const raw = t?.displayStatus ?? t?.status
  if (!raw) return "Submitted"
  const s = String(raw)
  if (API_STATUS_VALUES.includes(s as TicketStatus)) return s as TicketStatus
  return "Submitted"
}

/**
 * Map UI-selected status (TicketStatus) to API enum value for requests.
 * Use for PATCH /api/tickets/:id and POST message body.
 */
export function statusForApi(status: TicketStatus): TicketStatus {
  return API_STATUS_VALUES.includes(status) ? status : "Submitted"
}






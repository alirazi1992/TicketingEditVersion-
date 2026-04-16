/**
 * Canonical ticket status values.
 * These match the backend TicketStatus enum.
 */
export type TicketStatus =
  | "Submitted"
  | "SeenRead"
  | "Open"
  | "InProgress"
  | "Solved"
  | "Redo"

export type TicketPriority = "low" | "medium" | "high" | "urgent"
export type TicketCategory = string

export interface TicketResponse {
  id?: string
  authorName: string
  authorEmail: string
  message: string
  status: TicketStatus
  timestamp: string
}

export interface Ticket {
  id: string
  title: string
  description: string
  /** Canonical status from database - use for internal logic */
  canonicalStatus?: TicketStatus
  /** Display status mapped for user's role - use this for UI display */
  displayStatus?: TicketStatus
  /** @deprecated Use displayStatus for UI display */
  status: TicketStatus
  priority: TicketPriority
  category: TicketCategory
  categoryLabel?: string
  categoryId?: number
  subcategory?: string | null
  subcategoryLabel?: string | null
  subcategoryId?: number | null
  clientName: string
  clientEmail: string
  clientPhone?: string | null
  department?: string | null
  clientId?: string
  createdAt: string
  updatedAt?: string | null
  dueDate?: string | null
  lastActivityAt?: string | null
  lastSeenAt?: string | null
  isUnseen?: boolean | null
  assignedTo?: string | null
  assignedTechnicianName?: string | null
  assignedTechnicianEmail?: string | null
  assignedTechnicianPhone?: string | null
  responses?: TicketResponse[]
  attachments?: Array<{
    id: string
    fileName: string
    fileUrl: string
    fileSize: number
    contentType: string
  }>
  dynamicFields?: Record<string, unknown>
  assignedTechnicians?: Array<{
    id?: string
    technicianId?: string
    technicianUserId: string
    technicianName: string
    technicianEmail?: string | null
    isLead?: boolean
    state?: "Invited" | "Accepted" | "Declined" | "Working" | "Completed"
    assignedAt: string
    isActive?: boolean
    role?: string | null
    accessMode?: string | null
    canAct?: boolean
  }>
  activityEvents?: Array<{
    id: string
    ticketId: string
    actorUserId: string
    actorName: string
    actorRole: string
    eventType: string
    oldStatus?: string | null
    newStatus?: string | null
    metadataJson?: string | null
    createdAt: string
  }>
  latestActivity?: {
    actionType: string
    actorName: string
    actorRole: string
    createdAt: string
    fromStatus?: string | null
    toStatus?: string | null
    summary?: string | null
  } | null
  lastResponseBy?: string | null
  lastResponseAt?: string | null
  lastMessagePreview?: string | null
  lastMessageAt?: string | null
  lastMessageAuthorName?: string | null
  canClaim?: boolean
  claimDisabledReason?: string | null
  isUnread?: boolean | null
  // Access flags (server-side computed)
  canView?: boolean
  canReply?: boolean
  canEdit?: boolean
  isReadOnly?: boolean
  readOnlyReason?: string | null
  canGrantAccess?: boolean
  /** Owner | Collaborator | Candidate | None */
  accessMode?: string | null
  canAct?: boolean
  isFaded?: boolean
  /** True when at least one technician has accepted (AcceptedAt). Do not treat Assigned as Accepted. */
  isAccepted?: boolean
  acceptedAt?: string | null
  acceptedByUserId?: string | null
  [key: string]: unknown
}

export type UserRole = "client" | "technician" | "admin"
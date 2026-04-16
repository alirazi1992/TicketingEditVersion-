import type {
  ApiTicketListItemResponse,
  ApiTicketMessageDto,
  ApiTicketPriority,
  ApiTicketResponse,
  ApiTicketStatus,
} from "@/lib/api-types"
import type { CategoriesData } from "@/services/categories-types"
import type { Ticket, TicketPriority, TicketResponse } from "@/types"
import type { TicketStatus } from "@/lib/ticket-status"

/**
 * Direct mapping: API statuses now match frontend status type exactly.
 * Backend sends: "Submitted" | "SeenRead" | "Open" | "InProgress" | "Solved" | "Redo"
 * Frontend uses the same enum keys internally and displays Persian labels via ticket-status.ts
 * 
 * NOTE: Backend now returns both canonicalStatus and displayStatus.
 * - canonicalStatus: The actual status stored in database
 * - displayStatus: The status to show in UI (mapped based on user role)
 * 
 * For clients, when canonicalStatus=Redo, displayStatus=InProgress.
 * UI components should use displayStatus for rendering.
 */
const statusFromApi: Record<ApiTicketStatus, TicketStatus> = {
  Submitted: "Submitted",
  SeenRead: "SeenRead",
  Open: "Open",
  InProgress: "InProgress",
  Solved: "Solved",
  Redo: "Redo",
}

const statusToApi: Record<TicketStatus, ApiTicketStatus> = {
  Submitted: "Submitted",
  SeenRead: "SeenRead",
  Open: "Open",
  InProgress: "InProgress",
  Solved: "Solved",
  Redo: "Redo",
}

const priorityFromApi: Record<ApiTicketPriority, TicketPriority> = {
  Low: "low",
  Medium: "medium",
  High: "high",
  Critical: "urgent",
}

const priorityToApi: Record<TicketPriority, ApiTicketPriority> = {
  low: "Low",
  medium: "Medium",
  high: "High",
  urgent: "Critical",
}

const slugify = (value: string) =>
  value
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9\u0600-\u06ff\s-]/g, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "") || "category"

export const mapApiStatusToUi = (status: ApiTicketStatus): TicketStatus => statusFromApi[status] ?? "Submitted"

export const mapUiStatusToApi = (status: TicketStatus): ApiTicketStatus => statusToApi[status] ?? "Submitted"

export const mapApiPriorityToUi = (priority: ApiTicketPriority): TicketPriority => priorityFromApi[priority] ?? "medium"

export const mapUiPriorityToApi = (priority: TicketPriority): ApiTicketPriority => priorityToApi[priority] ?? "Medium"

export const mapApiMessageToResponse = (message: ApiTicketMessageDto): TicketResponse => ({
  id: message.id,
  authorName: message.authorName,
  authorEmail: message.authorEmail,
  status: message.status ? mapApiStatusToUi(message.status) : "Submitted",
  message: message.message,
  timestamp: message.createdAt,
})

export const mapApiTicketToUi = (
  ticket: ApiTicketResponse | ApiTicketListItemResponse,
  categories: CategoriesData,
  responses: TicketResponse[] = [],
): Ticket => {
  const categoryEntry = Object.entries(categories).find(([, cat]) => cat.backendId === ticket.categoryId)
  const categorySlug = categoryEntry?.[0] ?? slugify(ticket.categoryName ?? "")
  const subcategoryEntry = categoryEntry?.[1].subIssues
    ? Object.entries(categoryEntry[1].subIssues).find(([, sub]) => sub.backendId === ticket.subcategoryId)
    : undefined
  const subcategorySlug = subcategoryEntry?.[0] ?? (ticket.subcategoryName ? slugify(ticket.subcategoryName) : null)

  // Convert dynamicFields array to object for backward compatibility with existing UI components
  const dynamicFieldsObj: Record<string, unknown> = {}
  if (ticket.dynamicFields && Array.isArray(ticket.dynamicFields)) {
    ticket.dynamicFields.forEach((field) => {
      // Use key as the object key, or fallback to fieldDefinitionId if key is not available
      const fieldKey = field.key || `field_${field.fieldDefinitionId}`
      dynamicFieldsObj[fieldKey] = field.value
    })
  }

  // Use displayStatus for UI rendering (backend handles role-based mapping)
  // Fall back to status for backward compatibility with old API responses
  const displayStatus = ticket.displayStatus ?? ticket.status ?? "Submitted"
  const canonicalStatus = ticket.canonicalStatus ?? ticket.status ?? "Submitted"
  
  return {
    id: ticket.id,
    title: ticket.title,
    description: ticket.description ?? "",
    // canonicalStatus: actual DB status (for internal logic)
    canonicalStatus: mapApiStatusToUi(canonicalStatus),
    // displayStatus: what to show in UI (mapped by backend based on role)
    displayStatus: mapApiStatusToUi(displayStatus),
    // status: legacy field, use displayStatus for UI rendering
    status: mapApiStatusToUi(displayStatus),
    priority: mapApiPriorityToUi(ticket.priority),
    category: categorySlug,
    categoryLabel: categoryEntry?.[1].label ?? ticket.categoryName ?? "",
    categoryId: ticket.categoryId,
    subcategory: subcategorySlug,
    subcategoryLabel: subcategoryEntry?.[1].label ?? ticket.subcategoryName ?? null,
    subcategoryId: ticket.subcategoryId ?? null,
    clientId: ticket.createdByUserId,
    clientName: ticket.createdByName ?? "",
    clientEmail: ticket.createdByEmail ?? "",
    clientPhone: ticket.createdByPhoneNumber ?? null,
    department: ticket.createdByDepartment ?? null,
    createdAt: ticket.createdAt,
    updatedAt: ticket.updatedAt ?? null,
    dueDate: ticket.dueDate ?? null,
    assignedTo: ticket.assignedToUserId ?? null,
    assignedTechnicianName: ticket.assignedTechnicianName ?? ticket.assignedToName ?? null,
    assignedTechnicianEmail: ticket.assignedToEmail ?? null,
    assignedTechnicianPhone: ticket.assignedToPhoneNumber ?? null,
    responses,
    dynamicFields: Object.keys(dynamicFieldsObj).length > 0 ? dynamicFieldsObj : undefined,
    assignedTechnicians: ticket.assignedTechnicians?.map(at => ({
      id: (at as any).id ?? at.technicianId,
      technicianId: at.technicianId ?? (at as any).id,
      technicianUserId: at.technicianUserId,
      technicianName: at.technicianName,
      technicianEmail: at.technicianEmail ?? null,
      isLead: at.isLead ?? ((at as any).role === "Owner" || (at as any).role === "Lead"),
      state: at.state,
      assignedAt: at.assignedAt,
      isActive: (at as any).isActive,
      role: (at as any).role ?? null,
      accessMode: (at as any).accessMode ?? null,
      canAct: (at as any).canAct,
    })),
    activityEvents: ticket.activityEvents?.map(ae => ({
      id: ae.id,
      ticketId: ae.ticketId,
      actorUserId: ae.actorUserId,
      actorName: ae.actorName,
      actorRole: ae.actorRole,
      eventType: ae.eventType,
      oldStatus: ae.oldStatus ?? null,
      newStatus: ae.newStatus ?? null,
      metadataJson: ae.metadataJson ?? null,
      createdAt: ae.createdAt,
    })),
    latestActivity: ticket.latestActivity
      ? {
          actionType: ticket.latestActivity.actionType,
          actorName: ticket.latestActivity.actorName,
          actorRole: ticket.latestActivity.actorRole,
          createdAt: ticket.latestActivity.createdAt,
          fromStatus: ticket.latestActivity.fromStatus ?? null,
          toStatus: ticket.latestActivity.toStatus ?? null,
          summary: ticket.latestActivity.summary ?? null,
        }
      : null,
    attachments: ticket.attachments?.map(att => ({
      id: att.id,
      fileName: att.fileName,
      fileUrl: att.fileUrl,
      fileSize: att.fileSize,
      contentType: att.contentType,
    })),
    lastActivityAt: ticket.lastActivityAt ?? null,
    lastSeenAt: ticket.lastSeenAt ?? null,
    isUnseen: ticket.isUnseen ?? null,
    isUnread: ticket.isUnread ?? null,
    lastMessagePreview: ticket.lastMessagePreview ?? null,
    lastMessageAt: ticket.lastMessageAt ?? null,
    lastMessageAuthorName: ticket.lastMessageAuthorName ?? null,
    canClaim: ticket.canClaim ?? null,
    claimDisabledReason: ticket.claimDisabledReason ?? null,
    canView: (ticket as any).canView ?? null,
    canReply: (ticket as any).canReply ?? null,
    canEdit: (ticket as any).canEdit ?? null,
    isReadOnly: (ticket as any).isReadOnly ?? null,
    readOnlyReason: (ticket as any).readOnlyReason ?? null,
    canGrantAccess: (ticket as any).canGrantAccess ?? null,
    accessMode: (ticket as any).accessMode ?? null,
    canAct: (ticket as any).canAct ?? false,
    isFaded: (ticket as any).isFaded ?? false,
    isAccepted: (ticket as any).isAccepted ?? false,
    acceptedAt: (ticket as any).acceptedAt ?? null,
    acceptedByUserId: (ticket as any).acceptedByUserId ?? null,
  }
}
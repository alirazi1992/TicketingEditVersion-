export type ApiUserRole = "Client" | "Technician" | "Admin"

export interface ApiUserDto {
  id: string
  fullName: string
  email: string
  role: ApiUserRole
  isSupervisor?: boolean
  phoneNumber?: string | null
  department?: string | null
  avatarUrl?: string | null
  /** Landing path from backend: /admin, /supervisor, /technician, or /client. */
  landingPath?: string
}

export interface ApiAuthResponse {
  /** Not returned when using HttpOnly cookie auth; present only for legacy/tools. */
  token?: string
  ok?: boolean
  user?: ApiUserDto
  role?: string
  isSupervisor?: boolean
  /** Landing path for routing (source of truth). */
  landingPath?: string
}

// Category/Subcategory Request types (for create/update)
export interface ApiCategoryRequest {
  name: string
  description?: string | null
  isActive?: boolean
}

export interface ApiSubcategoryRequest {
  name: string
  description?: string | null
  isActive?: boolean
}

// Category/Subcategory Response types
export interface ApiCategoryResponse {
  id: number
  name: string
  description?: string | null
  isActive: boolean
  createdAt?: string
  subcategories: ApiSubcategoryResponse[]
}

export interface ApiSubcategoryResponse {
  id: number
  categoryId: number
  name: string
  description?: string | null
  isActive: boolean
  createdAt?: string
  sortOrder?: number
  subcategoryDisplayCode?: string
}

// Category list response (paginated)
export interface ApiCategoryListResponse {
  items: ApiCategoryResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export type ApiTicketPriority = "Low" | "Medium" | "High" | "Critical"

/**
 * Canonical ticket status values from backend.
 * These match the TicketStatus enum in the backend.
 */
export type ApiTicketStatus =
  | "Submitted"
  | "SeenRead"
  | "Open"
  | "InProgress"
  | "Solved"
  | "Redo"

export interface ApiTicketDynamicFieldResponse {
  fieldDefinitionId: number
  key: string
  label: string
  type: string
  value: string
  isRequired: boolean
}

export interface ApiTicketAttachmentDto {
  id: string
  fileName: string
  fileUrl: string
  fileSize: number
  contentType: string
}

export interface ApiTicketTechnicianDto {
  id?: string
  technicianId?: string
  technicianUserId: string
  technicianName: string
  technicianEmail?: string | null
  isLead?: boolean
  state?: "Invited" | "Accepted" | "Declined" | "Working" | "Completed"
  assignedAt: string
  /** From API: IsActive (DB) — for UI prefer canAct for "active" count */
  isActive?: boolean
  role?: string | null
  /** Owner | Collaborator | Candidate — for UI badge and active count */
  accessMode?: string | null
  /** True when technician can act (Owner or Collaborator). Use for "X فعال" count */
  canAct?: boolean
}

export interface ApiTicketActivityEventDto {
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
}

export interface ApiTicketLatestActivityDto {
  actionType: string
  actorName: string
  actorRole: string
  createdAt: string
  fromStatus?: string | null
  toStatus?: string | null
  summary?: string | null
}

export interface ApiSupervisorTechnicianWorkloadDto {
  technicianUserId: string
  technicianName: string
  technicianEmail: string
  inboxTotal: number
  inboxLeft: number
  workloadPercent: number
}

export interface ApiSupervisorTicketSummaryDto {
  id: string // Guid - use this for API calls
  ticketId: string // Display ID (e.g., "TCK-001") - use this for UI display
  title: string
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI */
  status?: ApiTicketStatus
  createdAt: string
  updatedAt?: string | null
}

export interface ApiSupervisorTechnicianSummaryDto {
  technicianUserId: string
  technicianName: string
  archiveTickets: ApiSupervisorTicketSummaryDto[]
  activeTickets: ApiSupervisorTicketSummaryDto[]
}

export interface ApiTicketActivityDto {
  id: string
  ticketId: string
  actorUserId: string
  actorName: string
  actorEmail: string
  type: "Created" | "Updated" | "StatusChanged" | "Assigned" | "MessageAdded" | "AttachmentAdded" | "Closed" | "Reopened" | "Commented" | "AssignmentChanged" | "TechnicianStateChanged" | "CommentAdded" | "ResponsibleChanged" | "WorkNoteAdded"
  message: string
  createdAt: string
}

export interface ApiTicketResponse {
  id: string
  title: string
  description: string
  categoryId: number
  categoryName: string
  subcategoryId?: number | null
  subcategoryName?: string | null
  priority: ApiTicketPriority
  /** Canonical status stored in database. Use for internal logic only. */
  canonicalStatus: ApiTicketStatus
  /** Display status mapped for the requester's role. Use this for UI display. */
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI or canonicalStatus for logic */
  status?: ApiTicketStatus
  createdByUserId: string
  createdByName: string
  createdByEmail: string
  createdByPhoneNumber?: string | null
  createdByDepartment?: string | null
  assignedToUserId?: string | null
  assignedToName?: string | null
  assignedToEmail?: string | null
  assignedToPhoneNumber?: string | null
  assignedTechnicianName?: string | null
  createdAt: string
  updatedAt?: string | null
  dueDate?: string | null
  lastActivityAt?: string | null
  lastSeenAt?: string | null
  isUnseen?: boolean | null
  dynamicFields?: ApiTicketDynamicFieldResponse[]
  attachments?: ApiTicketAttachmentDto[]
  assignedTechnicians?: ApiTicketTechnicianDto[]
  activityEvents?: ApiTicketActivityEventDto[]
  latestActivity?: ApiTicketLatestActivityDto | null
  isUnread?: boolean | null
  canClaim?: boolean
  claimDisabledReason?: string | null

  // Access flags (server-side enforcement)
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
  /** True when at least one assignment has accepted (AcceptedAt). Do not treat Assigned as Accepted. */
  isAccepted?: boolean
  acceptedAt?: string | null
  acceptedByUserId?: string | null
}

export interface ApiTicketListItemResponse {
  id: string
  title: string
  description?: string | null
  categoryId: number
  categoryName: string
  subcategoryId?: number | null
  subcategoryName?: string | null
  priority: ApiTicketPriority
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI or canonicalStatus for logic */
  status?: ApiTicketStatus
  createdByUserId: string
  createdByName: string
  createdByEmail: string
  createdByPhoneNumber?: string | null
  createdByDepartment?: string | null
  assignedToUserId?: string | null
  assignedToName?: string | null
  assignedToEmail?: string | null
  assignedToPhoneNumber?: string | null
  assignedTechnicianName?: string | null
  createdAt: string
  updatedAt?: string | null
  dueDate?: string | null
  lastActivityAt?: string | null
  lastSeenAt?: string | null
  isUnseen?: boolean | null
  isUnread?: boolean | null
  dynamicFields?: ApiTicketDynamicFieldResponse[]
  attachments?: ApiTicketAttachmentDto[]
  assignedTechnicians?: ApiTicketTechnicianDto[]
  activityEvents?: ApiTicketActivityEventDto[]
  latestActivity?: ApiTicketLatestActivityDto | null
  lastMessagePreview?: string | null
  lastMessageAt?: string | null
  lastMessageAuthorName?: string | null

  // Optional access flags (may be absent on list endpoints)
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
  /** True when at least one assignment has accepted. */
  isAccepted?: boolean
  acceptedAt?: string | null
  acceptedByUserId?: string | null
}

export interface ApiTicketCalendarResponse {
  id: string
  ticketNumber?: string | null
  title: string
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI */
  status?: ApiTicketStatus
  priority: ApiTicketPriority
  categoryName: string
  assignedTechnicianName?: string | null
  createdAt: string
  updatedAt?: string | null
  dueDate?: string | null
}

export interface ApiAdminTicketAssigneeDto {
  technicianUserId: string
  name: string
  role?: string | null
}

export interface ApiAdminTicketAssignmentResult {
  assignees: ApiAdminTicketAssigneeDto[]
  addedTechnicians: ApiAdminTicketAssigneeDto[]
}

export interface ApiAdminTechnicianExpertiseTagDto {
  categoryId: number
  categoryName: string
  subcategoryId: number
  subcategoryName: string
}

export interface ApiAdminTechnicianDirectoryItemDto {
  technicianId: string
  technicianUserId: string
  name: string
  email: string
  department?: string | null
  availability: "Free" | "Busy"
  inboxTotalActive: number
  inboxLeftActiveNonTerminal: number
  expertise: ApiAdminTechnicianExpertiseTagDto[]
}

export interface ApiAdminTicketListItemDto {
  id: string
  title: string
  categoryName: string
  subcategoryName?: string | null
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI */
  status?: ApiTicketStatus
  createdAt: string
  closedAt?: string | null
  lastActivityAt?: string | null
  assignedTechnicians?: ApiAdminTicketAssigneeDto[]
}

export interface ApiAdminTicketListResponse {
  items: ApiAdminTicketListItemDto[]
  totalCount: number
  page: number
  pageSize: number
}

/** Minimal item for GET /api/admin/tickets/by-date?date=YYYY-MM-DD (calendar day list). Based on UpdatedAt (آخرین بروزرسانی). */
export interface ApiAdminTicketByDateItemDto {
  ticketId: string
  title: string
  status: ApiTicketStatus
  priority: string
  updatedAt?: string | null
  assignedToName?: string | null
  code?: string | null
}

export interface ApiAdminTicketDurationDto {
  seconds?: number | null
  display?: string | null
}

export interface ApiAdminTicketResponderDto {
  userId: string
  name: string
  role: string
}

export interface ApiAdminTicketMessageDto {
  id: string
  authorName: string
  authorRole: string
  message: string
  createdAt: string
  status?: ApiTicketStatus | null
}

export interface ApiAdminTicketDetailsDto {
  id: string
  title: string
  description: string
  categoryName: string
  subcategoryName?: string | null
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI */
  status?: ApiTicketStatus
  createdAt: string
  closedAt?: string | null
  lastActivityAt?: string | null
  timeToFirstResponse?: ApiAdminTicketDurationDto | null
  timeToAnswered?: ApiAdminTicketDurationDto | null
  timeToClosed?: ApiAdminTicketDurationDto | null
  clientId: string
  clientName: string
  clientEmail: string
  clientPhone?: string | null
  clientDepartment?: string | null
  assignedTechnicians?: ApiAdminTicketAssigneeDto[]
  responders?: ApiAdminTicketResponderDto[]
  messages?: ApiAdminTicketMessageDto[]
  activityEvents?: ApiTicketActivityEventDto[]
}

export interface ApiTicketMessageDto {
  id: string
  authorUserId: string
  authorName: string
  authorEmail: string
  /** Role of the message author (Client, Technician, Admin, Supervisor). */
  authorRole?: string
  message: string
  createdAt: string
  status?: ApiTicketStatus | null
}

export interface ApiSystemSettingsResponse {
  appName: string
  supportEmail: string
  supportPhone: string
  defaultLanguage: "fa" | "en"
  defaultTheme: "light" | "dark" | "system"
  timezone: string
  defaultPriority: ApiTicketPriority
  defaultStatus: ApiTicketStatus
  responseSlaHours: number
  autoAssignEnabled: boolean
  allowClientAttachments: boolean
  maxAttachmentSizeMB: number
  emailNotificationsEnabled: boolean
  smsNotificationsEnabled: boolean
  notifyOnTicketCreated: boolean
  notifyOnTicketAssigned: boolean
  notifyOnTicketReplied: boolean
  notifyOnTicketClosed: boolean
  passwordMinLength: number
  require2FA: boolean
  sessionTimeoutMinutes: number
  allowedEmailDomains: string[]
}

export type ApiSystemSettingsUpdateRequest = ApiSystemSettingsResponse

export interface ApiUserPreferencesResponse {
  theme: "light" | "dark" | "system"
  fontSize: "sm" | "md" | "lg"
  language: "fa" | "en"
  direction: "rtl" | "ltr"
  timezone: string
  notifications: {
    emailEnabled: boolean
    pushEnabled: boolean
    smsEnabled: boolean
    desktopEnabled: boolean
  }
}

export type ApiUserPreferencesUpdateRequest = ApiUserPreferencesResponse

export interface ApiTechnicianResponse {
  id: string
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
  isActive: boolean
  isSupervisor?: boolean
  role?: "Technician" | "SupervisorTechnician"
  /** Backend may omit or send null when unknown; never 0001-01-01. */
  createdAt?: string | null
  userId?: string | null  // Canonical: same as id for supervisor directory; User.Id for assignment
  subcategoryIds?: number[]
  coverageCount?: number
}

export interface ApiTechnicianCoverageRequest {
  categoryId: number
  subcategoryId: number
}

export interface ApiTechnicianCreateRequest {
  fullName: string
  email: string
  password: string
  confirmPassword: string
  phone?: string | null
  department?: string | null
  isActive?: boolean
  isSupervisor?: boolean
  role?: "Technician" | "SupervisorTechnician"
  subcategoryIds?: number[] | null
  coverage?: ApiTechnicianCoverageRequest[] | null
}

export interface ApiTechnicianUpdateRequest {
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
  isActive?: boolean
  isSupervisor?: boolean
  subcategoryIds?: number[] | null
}

export interface ApiTechnicianStatusUpdateRequest {
  isActive: boolean
}

export interface ApiTechnicianListResponse {
  items: ApiTechnicianResponse[]
  totalCount: number
  page: number
  pageSize: number
}

export interface ApiNotificationPreferencesResponse {
  emailEnabled: boolean
  pushEnabled: boolean
  smsEnabled: boolean
  desktopEnabled: boolean
}

export interface ApiSupervisorTechnicianListItemDto {
  technicianUserId: string
  technicianName: string
  inboxTotal: number
  inboxLeft: number
  workloadPercent: number
}export interface ApiTicketSummaryDto {
  id: string
  title: string
  canonicalStatus: ApiTicketStatus
  displayStatus: ApiTicketStatus
  /** @deprecated Use displayStatus for UI */
  status?: ApiTicketStatus
  clientName: string
  createdAt: string
  updatedAt?: string | null
}export interface ApiSupervisorTechnicianSummaryDto {
  technicianUserId: string
  technicianName: string
  technicianEmail: string
  archiveTickets: ApiTicketSummaryDto[]
  activeTickets: ApiTicketSummaryDto[]
}

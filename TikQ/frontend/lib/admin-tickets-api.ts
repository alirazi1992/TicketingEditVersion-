import { apiRequest, apiGetNoStore } from "./api-client"
import type {
  ApiAdminTicketDetailsDto,
  ApiAdminTicketListResponse,
  ApiAdminTicketListItemDto,
  ApiAdminTicketMessageDto,
  ApiAdminTicketByDateItemDto,
  ApiTicketListItemResponse,
  ApiTicketResponse,
  ApiAdminTicketAssignmentResult,
  ApiAdminTechnicianDirectoryItemDto,
  ApiTechnicianResponse,
  ApiCategoryResponse,
} from "./api-types"
import { parseServerDate } from "./datetime"

const mapTicketToAdminListItem = (
  ticket: ApiTicketResponse | ApiTicketListItemResponse
): ApiAdminTicketListItemDto => {
  const canonicalStatus = ticket.canonicalStatus ?? ticket.status ?? "Submitted"
  const displayStatus = ticket.displayStatus ?? ticket.status ?? canonicalStatus
  return {
    id: ticket.id,
    title: ticket.title,
    categoryName: ticket.categoryName,
    subcategoryName: ticket.subcategoryName ?? null,
    canonicalStatus,
    displayStatus,
    status: displayStatus,
    createdAt: ticket.createdAt,
    closedAt: null,
    lastActivityAt: ticket.lastActivityAt ?? ticket.updatedAt ?? ticket.createdAt,
    assignedTechnicians: ticket.assignedTechnicians?.map((tech) => ({
      technicianUserId: tech.technicianUserId,
      name: tech.technicianName,
      role: null,
    })),
  }
}

const filterByDays = (
  tickets: Array<ApiTicketResponse | ApiTicketListItemResponse>,
  days: number,
  older = false
) => {
  const cutoff = new Date()
  cutoff.setDate(cutoff.getDate() - days)
  return tickets.filter((ticket) => {
    const created = parseServerDate(ticket.createdAt)
    if (!created) return false
    return older ? created < cutoff : created >= cutoff
  })
}

/** Tickets updated on a single day. Prefer dayJalali (e.g. 1404-11-16) so backend uses same Tehran day boundaries as calendar badges. */
export async function getAdminTicketsByDate(
  token: string | null | null,
  date: string
): Promise<ApiAdminTicketByDateItemDto[]> {
  const params = new URLSearchParams({ date })
  return apiGetNoStore<ApiAdminTicketByDateItemDto[]>(
    `/api/admin/tickets/by-date?${params.toString()}`,
    { token }
  )
}

/** Tickets for one Jalali day in Asia/Tehran (UpdatedAt in [startUtc, endUtc)). Single source of truth with calendar badges. */
export async function getAdminTicketsByDayJalali(
  token: string | null | null,
  dayJalali: string
): Promise<ApiAdminTicketByDateItemDto[]> {
  const normalized = dayJalali.trim().replace(/-/g, "/")
  const params = new URLSearchParams({ dayJalali: normalized })
  return apiGetNoStore<ApiAdminTicketByDateItemDto[]>(
    `/api/admin/tickets/by-date?${params.toString()}`,
    { token }
  )
}

export async function getAdminTickets(
  token: string | null | null,
  days = 30,
  page = 1,
  pageSize = 20
): Promise<ApiAdminTicketListResponse> {
  const params = new URLSearchParams({
    days: String(days),
    page: String(page),
    pageSize: String(pageSize),
  })
  try {
    return await apiGetNoStore<ApiAdminTicketListResponse>(`/api/admin/tickets?${params.toString()}`, {
      token,
      silent: true,
    })
  } catch (error: any) {
    if (error?.status) {
      const tickets = await apiGetNoStore<ApiTicketListItemResponse[]>("/api/tickets", { token })
      const filtered = filterByDays(tickets, days, false)
      const paged = filtered.slice((page - 1) * pageSize, page * pageSize)
      return {
        items: paged.map(mapTicketToAdminListItem),
        totalCount: filtered.length,
        page,
        pageSize,
      }
    }
    throw error
  }
}

export async function getAdminArchiveTickets(
  token: string | null | null,
  olderThanDays = 30,
  page = 1,
  pageSize = 20
): Promise<ApiAdminTicketListResponse> {
  const params = new URLSearchParams({
    olderThanDays: String(olderThanDays),
    page: String(page),
    pageSize: String(pageSize),
  })
  try {
    return await apiGetNoStore<ApiAdminTicketListResponse>(
      `/api/admin/tickets/archive?${params.toString()}`,
      { token, silent: true }
    )
  } catch (error: any) {
    if (error?.status) {
      const tickets = await apiGetNoStore<ApiTicketListItemResponse[]>("/api/tickets", { token })
      const filtered = filterByDays(tickets, olderThanDays, true)
      const paged = filtered.slice((page - 1) * pageSize, page * pageSize)
      return {
        items: paged.map(mapTicketToAdminListItem),
        totalCount: filtered.length,
        page,
        pageSize,
      }
    }
    throw error
  }
}

export async function getAdminTicketDetails(
  token: string | null | null,
  ticketId: string
): Promise<ApiAdminTicketDetailsDto> {
  try {
    return await apiGetNoStore<ApiAdminTicketDetailsDto>(`/api/admin/tickets/${ticketId}/details`, {
      token,
      silent: true,
    })
  } catch (error: any) {
    if (error?.status) {
      const ticket = await apiGetNoStore<ApiTicketResponse>(`/api/tickets/${ticketId}`, { token })
      const messages = await apiGetNoStore<ApiAdminTicketMessageDto[]>(`/api/tickets/${ticketId}/messages`, {
        token,
      })
      const responders = Array.from(
        new Map(
          messages
            .filter((m) => m.authorRole && m.authorRole !== "Client")
            .map((m) => [m.authorName, { userId: "", name: m.authorName, role: m.authorRole }])
        ).values()
      )
      const canonicalStatus = ticket.canonicalStatus ?? ticket.status ?? "Submitted"
      const displayStatus = ticket.displayStatus ?? ticket.status ?? canonicalStatus
      return {
        id: ticket.id,
        title: ticket.title,
        description: ticket.description,
        categoryName: ticket.categoryName,
        subcategoryName: ticket.subcategoryName ?? null,
        canonicalStatus,
        displayStatus,
        status: displayStatus,
        createdAt: ticket.createdAt,
        closedAt: null,
        lastActivityAt: ticket.lastActivityAt ?? ticket.updatedAt ?? ticket.createdAt,
        timeToFirstResponse: null,
        timeToAnswered: null,
        timeToClosed: null,
        clientId: ticket.createdByUserId,
        clientName: ticket.createdByName,
        clientEmail: ticket.createdByEmail,
        clientPhone: ticket.createdByPhoneNumber ?? null,
        clientDepartment: ticket.createdByDepartment ?? null,
        assignedTechnicians: ticket.assignedTechnicians?.map((tech) => ({
          technicianUserId: tech.technicianUserId,
          name: tech.technicianName,
          role: null,
        })),
        responders,
        messages,
        activityEvents: ticket.activityEvents ?? [],
      }
    }
    throw error
  }
}

export async function autoAssignAdminTicket(
  token: string | null | null,
  ticketId: string,
  options: {
    categoryId?: number
    subcategoryId?: number
    existingTechnicianUserIds?: string[]
  } = {}
): Promise<ApiAdminTicketAssignmentResult> {
  try {
    return await apiRequest<ApiAdminTicketAssignmentResult>(`/api/admin/tickets/${ticketId}/assign/auto`, {
      method: "POST",
      token,
      silent: true,
    })
  } catch (error: any) {
    if (!error?.status || (error.status !== 404 && error.status !== 400)) {
      throw error
    }
    const matchedTechnicians = await getAdminTechnicianDirectory(token, {
      categoryId: typeof options.categoryId === "number" ? options.categoryId : undefined,
      subcategoryId: typeof options.subcategoryId === "number" ? options.subcategoryId : undefined,
    })
    const matchedIds = matchedTechnicians.map((tech) => tech.technicianUserId)
    const existing = options.existingTechnicianUserIds ?? []
    const unionIds = Array.from(new Set([...existing, ...matchedIds]))
    if (unionIds.length === 0) {
      return { assignees: [], addedTechnicians: [] }
    }
    const updated = await apiRequest<ApiTicketResponse>(`/api/tickets/${ticketId}/assign-technicians`, {
      method: "POST",
      token,
      body: { technicianUserIds: unionIds },
      silent: true,
    })
    const assignees =
      updated.assignedTechnicians?.map((tech) => ({
        technicianUserId: tech.technicianUserId,
        name: tech.technicianName,
        role: null,
      })) ?? []
    const addedTechnicians = assignees.filter((assignee) => !existing.includes(assignee.technicianUserId))
    return { assignees, addedTechnicians }
  }
}

export async function manualAssignAdminTicket(
  token: string | null | null,
  ticketId: string,
  technicianUserIds: string[],
  existingTechnicianUserIds: string[] = []
): Promise<ApiAdminTicketAssignmentResult> {
  try {
    return await apiRequest<ApiAdminTicketAssignmentResult>(`/api/admin/tickets/${ticketId}/assign/manual`, {
      method: "POST",
      token,
      body: { technicianUserIds },
      silent: true,
    })
  } catch (error: any) {
    if (!error?.status || (error.status !== 404 && error.status !== 400)) {
      throw error
    }
    const unionIds = Array.from(new Set([...existingTechnicianUserIds, ...technicianUserIds]))
    if (unionIds.length === 0) {
      return { assignees: [], addedTechnicians: [] }
    }
    const updated = await apiRequest<ApiTicketResponse>(`/api/tickets/${ticketId}/assign-technicians`, {
      method: "POST",
      token,
      body: { technicianUserIds: unionIds },
      silent: true,
    })
    const assignees =
      updated.assignedTechnicians?.map((tech) => ({
        technicianUserId: tech.technicianUserId,
        name: tech.technicianName,
        role: null,
      })) ?? []
    const addedTechnicians = assignees.filter((assignee) => !existingTechnicianUserIds.includes(assignee.technicianUserId))
    return { assignees, addedTechnicians }
  }
}

export async function getAdminTechnicianDirectory(
  token: string | null | null,
  params: {
    search?: string
    availability?: "all" | "Free" | "Busy"
    categoryId?: number
    subcategoryId?: number
  } = {}
): Promise<ApiAdminTechnicianDirectoryItemDto[]> {
  const query = new URLSearchParams()
  if (params.search) query.set("search", params.search)
  if (params.availability && params.availability !== "all") query.set("availability", params.availability)
  if (typeof params.categoryId === "number") query.set("categoryId", String(params.categoryId))
  if (typeof params.subcategoryId === "number") query.set("subcategoryId", String(params.subcategoryId))

  const suffix = query.toString()
  try {
    return await apiGetNoStore<ApiAdminTechnicianDirectoryItemDto[]>(
      `/api/admin/technicians/directory${suffix ? `?${suffix}` : ""}`,
      { token, silent: true }
    )
  } catch (error: any) {
    if (!error?.status) {
      throw error
    }
    const technicians = await apiGetNoStore<ApiTechnicianResponse[]>("/api/admin/technicians", { token })
    const categories = await apiGetNoStore<ApiCategoryResponse[]>("/api/categories", { token })
    const subcategoryMap = new Map<number, { categoryId: number; categoryName: string; subcategoryName: string }>()
    categories.forEach((category) => {
      category.subcategories.forEach((subcategory) => {
        subcategoryMap.set(subcategory.id, {
          categoryId: category.id,
          categoryName: category.name,
          subcategoryName: subcategory.name,
        })
      })
    })

    const normalizedSearch = params.search?.trim().toLowerCase()
    const availabilityFilter = params.availability && params.availability !== "all" ? params.availability : null

    return technicians
      .filter((tech) => tech.isActive && tech.userId)
      .map((tech) => {
        const expertise = (tech.subcategoryIds ?? [])
          .map((subcategoryId) => {
            const entry = subcategoryMap.get(subcategoryId)
            if (!entry) return null
            return {
              categoryId: entry.categoryId,
              categoryName: entry.categoryName,
              subcategoryId,
              subcategoryName: entry.subcategoryName,
            }
          })
          .filter(Boolean) as ApiAdminTechnicianDirectoryItemDto["expertise"]

        return {
          technicianId: tech.id,
          technicianUserId: tech.userId!,
          name: tech.fullName,
          email: tech.email,
          department: tech.department ?? null,
          availability: "Free",
          inboxTotalActive: 0,
          inboxLeftActiveNonTerminal: 0,
          expertise,
        }
      })
      .filter((tech) => {
        if (normalizedSearch) {
          const haystack = `${tech.name} ${tech.email} ${tech.department ?? ""}`.toLowerCase()
          if (!haystack.includes(normalizedSearch)) return false
        }
        if (availabilityFilter && tech.availability !== availabilityFilter) return false
        if (typeof params.categoryId === "number") {
          if (!tech.expertise?.some((tag) => tag.categoryId === params.categoryId)) return false
        }
        if (typeof params.subcategoryId === "number") {
          if (!tech.expertise?.some((tag) => tag.subcategoryId === params.subcategoryId)) return false
        }
        return true
      })
  }
}


import { apiRequest, apiGetNoStore } from "./api-client"
import type { ApiTicketResponse, ApiTicketActivityDto } from "./api-types"

export interface AssignTechniciansRequest {
  technicianUserIds: string[]
  leadTechnicianUserId?: string | null
}

/**
 * Assign multiple technicians to a ticket (Admin only)
 */
export async function assignTechnicians(
  token: string,
  ticketId: string,
  request: AssignTechniciansRequest
): Promise<ApiTicketResponse> {
  return await apiRequest<ApiTicketResponse>(
    `/api/tickets/${ticketId}/assign-technicians`,
    {
      method: "POST",
      token,
      body: request,
    }
  )
}

/**
 * Claim ticket (Technician)
 */
export async function claimTicket(
  token: string,
  ticketId: string
): Promise<ApiTicketResponse> {
  return await apiRequest<ApiTicketResponse>(
    `/api/tickets/${ticketId}/claim`,
    {
      method: "POST",
      token,
    }
  )
}

/**
 * Get ticket activity timeline
 * Returns empty array if endpoint returns 404 (treats as "no activities yet")
 */
export async function getTicketActivities(
  token: string,
  ticketId: string
): Promise<ApiTicketActivityDto[]> {
  try {
    return await apiGetNoStore<ApiTicketActivityDto[]>(
      `/api/tickets/${ticketId}/activities`,
      { token, silent: true }
    )
  } catch (error: any) {
    // If 404, treat as "no activities yet" (silent mode)
    if (error?.status === 404) {
      if (process.env.NODE_ENV === "development") {
        console.log(`[getTicketActivities] Endpoint returned 404 for ticket ${ticketId}, treating as empty activities`)
      }
      return []
    }
    // Re-throw other errors (500, 401, etc.)
    throw error
  }
}

/**
 * Mark ticket as seen/read (Technician or Admin)
 */
export async function markTicketAsSeenRead(
  token: string,
  ticketId: string
): Promise<ApiTicketResponse> {
  return await apiRequest<ApiTicketResponse>(
    `/api/tickets/${ticketId}/seen-read`,
    {
      method: "POST",
      token,
    }
  )
}

/**
 * Start work on ticket (Technician or Admin)
 */
export async function startWorkOnTicket(
  token: string,
  ticketId: string
): Promise<ApiTicketResponse> {
  return await apiRequest<ApiTicketResponse>(
    `/api/tickets/${ticketId}/start-work`,
    {
      method: "POST",
      token,
    }
  )
}

/**
 * Solve ticket (Technician or Admin)
 * Updates ticket status to "Solved"
 */
export async function solveTicket(
  token: string,
  ticketId: string
): Promise<ApiTicketResponse> {
  return await apiRequest<ApiTicketResponse>(
    `/api/tickets/${ticketId}/solve`,
    {
      method: "POST",
      token,
    }
  )
}

































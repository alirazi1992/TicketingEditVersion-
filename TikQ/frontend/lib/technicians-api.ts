import { apiRequest } from "./api-client"
import type {
  ApiTechnicianResponse,
  ApiTechnicianCreateRequest,
  ApiTechnicianUpdateRequest,
  ApiTechnicianStatusUpdateRequest,
  ApiTechnicianListResponse,
} from "./api-types"

/**
 * Get all technicians (Admin only)
 * Backend route: GET /api/admin/technicians
 */
export async function getAllTechnicians(
  token: string | null,
  options?: { page?: number; pageSize?: number; search?: string }
): Promise<ApiTechnicianListResponse> {
  const params = new URLSearchParams()
  if (options?.page) params.set("page", options.page.toString())
  if (options?.pageSize) params.set("pageSize", options.pageSize.toString())
  if (options?.search) params.set("search", options.search)
  const query = params.toString()
  const response = await apiRequest<ApiTechnicianResponse[] | ApiTechnicianListResponse>(
    `/api/admin/technicians${query ? `?${query}` : ""}`,
    {
    method: "GET",
    token,
    }
  )
  if (Array.isArray(response)) {
    return {
      items: response,
      totalCount: response.length,
      page: 1,
      pageSize: response.length,
    }
  }
  return response
}

/**
 * Get current technician's profile (Technician role only)
 * Backend route: GET /api/technician/me
 */
export async function getMyTechnicianProfile(token: string | null): Promise<ApiTechnicianResponse> {
  return apiRequest<ApiTechnicianResponse>("/api/technician/me", {
    method: "GET",
    token,
  })
}

/**
 * Get assignable technicians for supervisor delegation (Supervisor Technician only)
 * Backend route: GET /api/technician/available
 * Returns only active, non-supervisor technicians
 */
export async function getAssignableTechnicians(token: string | null): Promise<ApiTechnicianResponse[]> {
  return apiRequest<ApiTechnicianResponse[]>("/api/technician/available", {
    method: "GET",
    token,
  });
}

/**
 * Get technician by ID (Admin only)
 * Note: This endpoint may not exist in UsersController - check backend implementation
 */
export async function getTechnicianById(
  token: string | null,
  id: string
): Promise<ApiTechnicianResponse> {
  // Check if TechniciansController exists with /api/admin/technicians route
  // If not, this will 404 - handle gracefully if needed
  return apiRequest<ApiTechnicianResponse>(`/api/admin/technicians/${id}`, {
    method: "GET",
    token,
  })
}

/**
 * Create a new technician (Admin only)
 */
export async function createTechnician(
  token: string | null,
  technician: ApiTechnicianCreateRequest
): Promise<ApiTechnicianResponse> {
  return apiRequest<ApiTechnicianResponse>("/api/admin/technicians", {
    method: "POST",
    token,
    body: technician,
  })
}

/**
 * Update technician (Admin only)
 */
export async function updateTechnician(
  token: string | null,
  id: string,
  technician: ApiTechnicianUpdateRequest
): Promise<ApiTechnicianResponse> {
  return apiRequest<ApiTechnicianResponse>(`/api/admin/technicians/${id}`, {
    method: "PUT",
    token,
    body: technician,
  })
}

/**
 * Update technician status (active/inactive) (Admin only)
 */
export async function updateTechnicianStatus(
  token: string | null,
  id: string,
  isActive: boolean
): Promise<void> {
  const requestBody = { isActive }
  console.log("[technicians-api] Updating technician status:", { id, isActive, requestBody })
  
  try {
    await apiRequest(`/api/admin/technicians/${id}/status`, {
      method: "PATCH",
      token,
      body: requestBody,
    })
    console.log("[technicians-api] Technician status updated successfully")
  } catch (error: any) {
    console.error("[technicians-api] Failed to update technician status:", {
      id,
      isActive,
      status: error?.status,
      message: error?.message,
      body: error?.body,
    })
    throw error
  }
}

/**
 * Assign a technician to a ticket (Admin only)
 * Backend route: PUT /api/tickets/{id}/assign-technician
 */
export async function assignTechnicianToTicket(
  token: string | null,
  ticketId: string,
  technicianId: string
) {
  return apiRequest(`/api/tickets/${ticketId}/assign-technician`, {
    method: "PUT",
    token,
    body: { technicianId },
  })
}

/**
 * Link a Technician to a User account (Admin only)
 * A Technician MUST be linked to a User account (with Role=Technician) to be eligible for ticket assignment.
 */
export async function linkTechnicianToUser(
  token: string | null,
  technicianId: string,
  userId: string
): Promise<ApiTechnicianResponse> {
  return apiRequest<ApiTechnicianResponse>(`/api/admin/technicians/${technicianId}/link-user`, {
    method: "PATCH",
    token,
    body: { userId },
  })
}

/**
 * Update technician expertise (subcategory permissions) (Admin only)
 */
export async function updateTechnicianExpertise(
  token: string | null,
  id: string,
  subcategoryIds: number[] | null
): Promise<ApiTechnicianResponse> {
  return apiRequest<ApiTechnicianResponse>(`/api/admin/technicians/${id}/expertise`, {
    method: "PUT",
    token,
    body: { subcategoryIds },
  })
}

/**
 * Delete (soft delete) a technician (Admin only)
 * This performs a soft delete:
 * - Sets IsDeleted=true, DeletedAt=UtcNow
 * - Sets IsActive=false
 * - Locks out the linked user account (prevents login)
 * 
 * Historical ticket data remains intact for audit purposes.
 */
export interface DeleteTechnicianResponse {
  message: string
  technicianId: string
  isDeleted: boolean
  technician?: ApiTechnicianResponse
}

export async function deleteTechnician(
  token: string | null,
  id: string
): Promise<DeleteTechnicianResponse> {
  console.log("[technicians-api] Deleting technician:", { id })
  
  try {
    const response = await apiRequest<DeleteTechnicianResponse>(`/api/admin/technicians/${id}`, {
      method: "DELETE",
      token,
    })
    console.log("[technicians-api] Technician deleted successfully:", response)
    return response
  } catch (error: any) {
    console.error("[technicians-api] Failed to delete technician:", {
      id,
      status: error?.status,
      message: error?.message,
      body: error?.body,
    })
    throw error
  }
}
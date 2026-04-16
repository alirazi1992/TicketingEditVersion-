import { apiRequest } from "./api-client"

export interface ApiTechnicianResponse {
  id: string
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
  isActive: boolean
  createdAt: string
}

export interface ApiTechnicianCreateRequest {
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
  isActive?: boolean
}

export interface ApiTechnicianUpdateRequest {
  fullName: string
  email: string
  phone?: string | null
  department?: string | null
}

export interface ApiTechnicianStatusUpdateRequest {
  isActive: boolean
}

export interface ApiAssignTechnicianRequest {
  technicianId: string
}

/**
 * Get all technicians (Admin only)
 */
export async function getAllTechnicians(token: string | null): Promise<ApiTechnicianResponse[]> {
  return apiRequest<ApiTechnicianResponse[]>("/api/admin/technicians", {
    method: "GET",
    token,
  })
}

/**
 * Get technician by ID (Admin only)
 */
export async function getTechnicianById(token: string | null, id: string): Promise<ApiTechnicianResponse> {
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
 * Update technician status (Admin only)
 */
export async function updateTechnicianStatus(
  token: string | null,
  id: string,
  isActive: boolean
): Promise<void> {
  return apiRequest<void>(`/api/admin/technicians/${id}/status`, {
    method: "PATCH",
    token,
    body: { isActive },
  })
}

/**
 * Assign technician to ticket (Admin only)
 */
export async function assignTechnicianToTicket(
  token: string | null,
  ticketId: string,
  technicianId: string
): Promise<any> {
  return apiRequest<any>(`/api/tickets/${ticketId}/assign-technician`, {
    method: "PUT",
    token,
    body: { technicianId },
  })
}


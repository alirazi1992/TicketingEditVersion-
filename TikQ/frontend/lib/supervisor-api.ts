import { apiRequest, apiGetNoStore } from "./api-client"
import { apiFetch } from "./api"
import type {
  ApiSupervisorTechnicianWorkloadDto,
  ApiSupervisorTechnicianSummaryDto,
  ApiSupervisorTicketSummaryDto,
  ApiTechnicianResponse,
} from "./api-types"

export async function getSupervisorTechnicians(
  token: string
): Promise<ApiSupervisorTechnicianWorkloadDto[]> {
  return apiRequest<ApiSupervisorTechnicianWorkloadDto[]>("/api/supervisor/technicians", {
    method: "GET",
    token,
  })
}

export async function getSupervisorAvailableTechnicians(
  token: string
): Promise<ApiTechnicianResponse[]> {
  return apiRequest<ApiTechnicianResponse[]>("/api/supervisor/technicians/available", {
    method: "GET",
    token,
  })
}

export async function linkSupervisorTechnician(
  token: string,
  technicianUserId: string
): Promise<void> {
  await apiRequest(`/api/supervisor/technicians/${technicianUserId}/link`, {
    method: "POST",
    token,
  })
}

export async function unlinkSupervisorTechnician(
  token: string,
  technicianUserId: string
): Promise<void> {
  await apiRequest(`/api/supervisor/technicians/${technicianUserId}/link`, {
    method: "DELETE",
    token,
  })
}

export async function getSupervisorTechnicianSummary(
  token: string,
  technicianUserId: string
): Promise<ApiSupervisorTechnicianSummaryDto> {
  return apiRequest<ApiSupervisorTechnicianSummaryDto>(
    `/api/supervisor/technicians/${technicianUserId}/summary`,
    {
      method: "GET",
      token,
    }
  )
}

export async function getSupervisorAvailableTickets(
  token: string
): Promise<ApiSupervisorTicketSummaryDto[]> {
  return apiGetNoStore<ApiSupervisorTicketSummaryDto[]>("/api/supervisor/tickets/available-to-assign", {
    token,
  })
}

export async function assignSupervisorTicket(
  token: string,
  technicianUserId: string,
  ticketId: string
): Promise<void> {
  // Log payload for debugging
  if (process.env.NODE_ENV === "development") {
    console.log("[assignSupervisorTicket] Sending request:", {
      endpoint: `/api/supervisor/technicians/${technicianUserId}/assignments`,
      technicianUserId,
      ticketId,
      payload: { ticketId },
    });
  }
  
  await apiRequest(`/api/supervisor/technicians/${technicianUserId}/assignments`, {
    method: "POST",
    token,
    body: { ticketId },
  })
}

export async function removeSupervisorAssignment(
  token: string,
  technicianUserId: string,
  ticketId: string
): Promise<void> {
  await apiRequest(`/api/supervisor/technicians/${technicianUserId}/assignments/${ticketId}`, {
    method: "DELETE",
    token,
  })
}

export async function getSupervisorTechnicianReport(
  token: string | null,
  technicianUserId: string
): Promise<Blob> {
  const path = `/api/supervisor/technicians/${technicianUserId}/report?format=csv`;
  const headers: Record<string, string> = {};
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  const response = await apiFetch(path, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    let errorMessage = "Failed to download report";
    try {
      const errorData = await response.json();
      errorMessage = errorData.message || errorData.detail || errorMessage;
    } catch {
      errorMessage = `${errorMessage} (${response.status} ${response.statusText})`;
    }
    const err = new Error(errorMessage) as Error & { status?: number; statusText?: string };
    err.status = response.status;
    err.statusText = response.statusText;
    throw err;
  }

  return await response.blob();
}

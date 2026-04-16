import { apiRequest } from "./api-client";

export interface SmartAssignmentStatusResponse {
  enabled: boolean;
}

export interface SmartAssignmentUpdateRequest {
  enabled: boolean;
}

export interface SmartAssignmentRunResponse {
  assignedCount: number;
  message: string;
}

export async function getSmartAssignmentStatus(
  token: string
): Promise<SmartAssignmentStatusResponse> {
  return apiRequest<SmartAssignmentStatusResponse>("/api/admin/assignment/smart", {
    method: "GET",
    token,
  });
}

export async function updateSmartAssignmentStatus(
  token: string,
  enabled: boolean
): Promise<SmartAssignmentStatusResponse> {
  return apiRequest<SmartAssignmentStatusResponse>("/api/admin/assignment/smart", {
    method: "PUT",
    token,
    body: { enabled },
  });
}

export async function runSmartAssignment(
  token: string,
  startDate?: string,
  endDate?: string
): Promise<SmartAssignmentRunResponse> {
  const params = new URLSearchParams();
  if (startDate) params.append("start", startDate);
  if (endDate) params.append("end", endDate);
  params.append("scope", "unassigned");

  const query = params.toString();
  const url = `/api/admin/assignment/smart/run${query ? `?${query}` : ""}`;

  return apiRequest<SmartAssignmentRunResponse>(url, {
    method: "POST",
    token,
  });
}


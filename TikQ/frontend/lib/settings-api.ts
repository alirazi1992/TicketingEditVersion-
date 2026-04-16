import { apiRequest } from "./api-client"
import type { ApiSystemSettingsResponse, ApiSystemSettingsUpdateRequest } from "./api-types"

/**
 * Get current system settings (Admin only)
 */
export async function getSystemSettings(token: string | null): Promise<ApiSystemSettingsResponse> {
  return apiRequest<ApiSystemSettingsResponse>("/api/settings/system", {
    method: "GET",
    token,
  })
}

/**
 * Update system settings (Admin only)
 */
export async function updateSystemSettings(
  token: string | null,
  settings: ApiSystemSettingsUpdateRequest
): Promise<ApiSystemSettingsResponse> {
  return apiRequest<ApiSystemSettingsResponse>("/api/settings/system", {
    method: "PUT",
    token,
    body: settings,
  })
}


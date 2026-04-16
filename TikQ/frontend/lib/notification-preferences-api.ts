import { apiRequest } from "./api-client"
import type { ApiNotificationPreferencesResponse, ApiNotificationPreferencesUpdateRequest } from "./api-types"

/**
 * Get current user's notification preferences
 */
export async function getMyNotificationPreferences(token: string | null): Promise<ApiNotificationPreferencesResponse> {
  return apiRequest<ApiNotificationPreferencesResponse>("/api/users/me/notifications", {
    method: "GET",
    token,
  })
}

/**
 * Update current user's notification preferences
 */
export async function updateMyNotificationPreferences(
  token: string | null,
  preferences: ApiNotificationPreferencesUpdateRequest
): Promise<ApiNotificationPreferencesResponse> {
  return apiRequest<ApiNotificationPreferencesResponse>("/api/users/me/notifications", {
    method: "PUT",
    token,
    body: preferences,
  })
}


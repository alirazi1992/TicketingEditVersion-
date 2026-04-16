import { apiRequest } from "./api-client"
import type { ApiUserPreferencesResponse, ApiUserPreferencesUpdateRequest } from "./api-types"

// Module-level flag to track if server supports preferences endpoint
// Once we get a 404, we never call the server again for preferences
let serverPrefsSupported: boolean | null = null // null = unknown, true = supported, false = not supported

const PREFERENCES_STORAGE_KEY = "ticketing-user-preferences"

/** System is Farsi-only: normalize any preferences to language "fa" and direction "rtl". */
function normalizeToFarsi(prefs: ApiUserPreferencesResponse): ApiUserPreferencesResponse {
  return {
    ...prefs,
    language: "fa",
    direction: "rtl",
  }
}

function getDefaultPreferences(): ApiUserPreferencesResponse {
  return {
    theme: "dark",
    fontSize: "md",
    language: "fa",
    direction: "rtl",
    timezone: "Asia/Tehran",
    notifications: {
      emailEnabled: true,
      pushEnabled: true,
      smsEnabled: false,
      desktopEnabled: true,
    },
  }
}

/**
 * Load preferences from localStorage
 */
function loadPreferencesFromStorage(): ApiUserPreferencesResponse | null {
  try {
    const stored = localStorage.getItem(PREFERENCES_STORAGE_KEY)
    if (stored) {
      return JSON.parse(stored) as ApiUserPreferencesResponse
    }
  } catch (error) {
    console.warn("[preferences-api] Failed to parse preferences from localStorage:", error)
  }
  return null
}

/**
 * Save preferences to localStorage
 */
function savePreferencesToStorage(prefs: ApiUserPreferencesResponse): void {
  try {
    localStorage.setItem(PREFERENCES_STORAGE_KEY, JSON.stringify(prefs))
  } catch (error) {
    console.warn("[preferences-api] Failed to save preferences to localStorage:", error)
  }
}

/**
 * Get current user's preferences
 * Strategy:
 * 1. Load from localStorage first (instant, works offline)
 * 2. If server support is unknown, try server once
 * 3. If server returns 404, mark as unsupported and never call again
 * 4. Always persist to localStorage
 */
export async function getMyPreferences(token: string | null): Promise<ApiUserPreferencesResponse> {
  // Always load from localStorage first for instant response
  const storedPrefs = loadPreferencesFromStorage()
  if (storedPrefs) {
    // If we know server doesn't support it, just return localStorage (normalized to Farsi)
    if (serverPrefsSupported === false) {
      return normalizeToFarsi(storedPrefs)
    }
  }

  // If we know server doesn't support preferences, skip server call
  if (serverPrefsSupported === false) {
    return normalizeToFarsi(storedPrefs || getDefaultPreferences())
  }

  // Try server only if support is unknown
  if (serverPrefsSupported === null) {
    try {
      // Try server endpoint - if it works, use it; if 404, mark as unsupported
      const serverPrefs = await apiRequest<ApiUserPreferencesResponse>("/api/Users/me/preferences", {
        method: "GET",
        token,
        silent: false, // Log errors to help debug if endpoint doesn't exist
      })
      
      // Server supports preferences - use server data (normalized to Farsi) and update localStorage
      serverPrefsSupported = true
      const normalized = normalizeToFarsi(serverPrefs)
      savePreferencesToStorage(normalized)
      return normalized
    } catch (error: any) {
      if (error?.status === 404) {
        // Server doesn't support preferences - mark and never call again
        serverPrefsSupported = false
        // Return stored or defaults
        return storedPrefs || getDefaultPreferences()
      }
      // For other errors (network, 500, etc.), return stored or defaults
      return normalizeToFarsi(storedPrefs || getDefaultPreferences())
    }
  }

  // If server is known to support it, fetch from server
  if (serverPrefsSupported === true) {
    try {
      const serverPrefs = await apiRequest<ApiUserPreferencesResponse>("/api/Users/me/preferences", {
        method: "GET",
        token,
        silent: false,
      })
      const normalized = normalizeToFarsi(serverPrefs)
      savePreferencesToStorage(normalized)
      return normalized
    } catch (error: any) {
      // If server call fails after we know it's supported, fall back to localStorage
      return storedPrefs || getDefaultPreferences()
    }
  }

  // If server is known to support it, we should have returned above
  // This is a fallback
  return storedPrefs || getDefaultPreferences()
}

/**
 * Update current user's preferences
 * Strategy:
 * 1. Always save to localStorage immediately (theme toggle must be instant)
 * 2. If server support is unknown, try server once
 * 3. If server returns 404, mark as unsupported and never call again
 * 4. Always return the input preferences (UI already updated)
 */
export async function updateMyPreferences(
  token: string | null,
  preferences: ApiUserPreferencesUpdateRequest
): Promise<ApiUserPreferencesResponse> {
  const prefsResponse = normalizeToFarsi(preferences as ApiUserPreferencesResponse)
  
  // Always save to localStorage immediately (works offline, instant)
  savePreferencesToStorage(prefsResponse)

  // If we know server doesn't support preferences, skip server call
  if (serverPrefsSupported === false) {
    return prefsResponse
  }

  // Try server only if support is unknown
  if (serverPrefsSupported === null) {
    try {
      // Try server endpoint - if it works, use it; if 404, mark as unsupported
      const serverPrefs = await apiRequest<ApiUserPreferencesResponse>("/api/Users/me/preferences", {
        method: "PUT",
        token,
        body: preferences,
        silent: false, // Log errors to help debug if endpoint doesn't exist
      })
      
      // Server supports preferences - use server response
      serverPrefsSupported = true
      savePreferencesToStorage(normalizeToFarsi(serverPrefs))
      return prefsResponse
    } catch (error: any) {
      if (error?.status === 404) {
        // Server doesn't support preferences - mark and never call again
        serverPrefsSupported = false
        // Return input preferences (already saved to localStorage)
        return prefsResponse
      }
      // For other errors (network, 500, etc.), return input preferences
      return prefsResponse
    }
  }

  // If server is known to support it, update on server
  if (serverPrefsSupported === true) {
    try {
      const serverPrefs = await apiRequest<ApiUserPreferencesResponse>("/api/Users/me/preferences", {
        method: "PUT",
        token,
        body: prefsResponse,
        silent: false,
      })
      const normalized = normalizeToFarsi(serverPrefs)
      savePreferencesToStorage(normalized)
      return prefsResponse
    } catch (error: any) {
      // If server call fails after we know it's supported, keep localStorage version
      return prefsResponse
    }
  }

  // If server is known to not support it, just return localStorage version
  return prefsResponse
}


"use client"

import type React from "react"
import { createContext, useContext, useEffect, useState, useCallback } from "react"
import { useTheme } from "next-themes"
import { getMyPreferences, updateMyPreferences } from "./preferences-api"
import { useAuth } from "./auth-context"
import type { ApiUserPreferencesResponse } from "./api-types"

interface PreferencesContextType {
  preferences: ApiUserPreferencesResponse | null
  isLoading: boolean
  updatePreferences: (prefs: ApiUserPreferencesResponse) => Promise<boolean>
  refreshPreferences: () => Promise<void>
}

const PreferencesContext = createContext<PreferencesContextType | undefined>(undefined)

export function PreferencesProvider({ children }: { children: React.ReactNode }) {
  const { user, token } = useAuth()
  const { theme: currentTheme, setTheme: setNextTheme, resolvedTheme } = useTheme()
  const [preferences, setPreferences] = useState<ApiUserPreferencesResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [mounted, setMounted] = useState(false)

  // Handle hydration - wait for mount to avoid SSR mismatch
  useEffect(() => {
    setMounted(true)
  }, [])

  const getDefaultPreferences = (): ApiUserPreferencesResponse => ({
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
  })

  const applyPreferences = useCallback(
    (prefs: ApiUserPreferencesResponse) => {
      if (!mounted) return // Don't apply until mounted to avoid hydration issues
      
      // Apply theme - ensure it's applied to the html element
      const html = document.documentElement
      
      // Remove existing theme classes
      html.classList.remove("light", "dark")
      
      // Apply the theme from preferences
      const themeToApply = prefs.theme === "system" ? "system" : prefs.theme
      setNextTheme(themeToApply)
      
      // For explicit themes, also manually add the class to ensure it's applied immediately
      if (prefs.theme === "dark") {
        html.classList.add("dark")
      } else if (prefs.theme === "light") {
        html.classList.remove("dark") // Remove dark class for light theme
      } else if (prefs.theme === "system") {
        // For system theme, next-themes will handle it based on OS preference
        // Use resolvedTheme to determine actual theme
        if (resolvedTheme === "dark") {
          html.classList.add("dark")
        } else {
          html.classList.remove("dark")
        }
      }

      // Apply font size to both html and body for better cascade
      const body = document.body
      
      html.classList.remove("text-sm", "text-base", "text-lg")
      body.classList.remove("text-sm", "text-base", "text-lg")
      
      if (prefs.fontSize === "sm") {
        html.classList.add("text-sm")
        body.classList.add("text-sm")
      } else if (prefs.fontSize === "lg") {
        html.classList.add("text-lg")
        body.classList.add("text-lg")
      } else {
        html.classList.add("text-base")
        body.classList.add("text-base")
      }

      // Apply language and direction (system is Farsi-only)
      html.setAttribute("lang", "fa")
      html.setAttribute("dir", "rtl")
    },
    [setNextTheme, mounted, resolvedTheme]
  )

  const refreshPreferences = useCallback(async () => {
    setIsLoading(true)
    
    try {
      // getMyPreferences now loads from localStorage first, then tries server if unknown
      // It handles 404 gracefully and never calls server again after first 404
      const prefs = await getMyPreferences(token)
      setPreferences(prefs)
      applyPreferences(prefs)
    } catch (error: any) {
      // This should rarely happen now since getMyPreferences handles all errors gracefully
      // But keep as safety net
      console.error("[preferences-context] Failed to load preferences:", error)
      
      // Fallback to defaults
      const defaults = getDefaultPreferences()
      setPreferences(defaults)
      applyPreferences(defaults)
    } finally {
      setIsLoading(false)
    }
  }, [token, applyPreferences])

  const updatePreferences = useCallback(
    async (prefs: ApiUserPreferencesResponse): Promise<boolean> => {
      // Always update local state immediately for responsive UI (theme toggle must be instant)
      setPreferences(prefs)
      applyPreferences(prefs)
      
      // updateMyPreferences now saves to localStorage immediately and handles server gracefully
      // It never calls server again after first 404, so no error spam
      try {
        const updated = await updateMyPreferences(token, prefs)
        // Update state with server response (if server supported) or input (if localStorage only)
        setPreferences(updated)
        applyPreferences(updated)
        return true
      } catch (error: any) {
        // This should rarely happen now since updateMyPreferences handles all errors gracefully
        // But keep as safety net
        console.error("[preferences-context] Failed to update preferences:", error)
        // Preferences were already applied locally, so UI is still updated
        return true
      }
    },
    [token, applyPreferences]
  )

  // Load preferences once on mount/login - do not refetch in a loop
  useEffect(() => {
    // Only run once when user/token changes, not on every preference state change
    if (user && token) {
      void refreshPreferences()
    } else {
      // When logged out, load from localStorage or defaults
      setIsLoading(false)
      setPreferences(null)
      
      // Load from localStorage when logged out
      const storedPrefs = localStorage.getItem("ticketing-user-preferences")
      let defaults: ApiUserPreferencesResponse
      
      if (storedPrefs) {
        try {
          defaults = JSON.parse(storedPrefs)
        } catch {
          defaults = getDefaultPreferences()
        }
      } else {
        defaults = getDefaultPreferences()
      }
      
      applyPreferences(defaults)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [user, token]) // Only depend on user/token, not on preferences or refreshPreferences to avoid loops

  const value: PreferencesContextType = {
    preferences,
    isLoading,
    updatePreferences,
    refreshPreferences,
  }

  return <PreferencesContext.Provider value={value}>{children}</PreferencesContext.Provider>
}

export function usePreferences() {
  const context = useContext(PreferencesContext)
  if (context === undefined) {
    throw new Error("usePreferences must be used within a PreferencesProvider")
  }
  return context
}


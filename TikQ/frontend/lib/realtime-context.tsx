"use client"

import React, { createContext, useContext, useEffect, useState, useCallback, useRef } from "react"
import { useSignalR, TicketUpdatePayload, TicketStatusUpdatePayload } from "@/hooks/use-signalr"

/**
 * Event handlers for ticket updates
 */
export interface TicketUpdateHandlers {
  onStatusChanged?: (payload: TicketStatusUpdatePayload) => void
  onReplyAdded?: (payload: TicketUpdatePayload) => void
  onAssignmentChanged?: (payload: TicketUpdatePayload) => void
  onAnyUpdate?: (ticketId: string, updateType: string) => void
}

/**
 * Realtime context value
 */
export interface RealtimeContextValue {
  connected: boolean
  connectionState: "disconnected" | "connecting" | "connected" | "reconnecting"
  subscribeToTicket: (ticketId: string) => Promise<void>
  unsubscribeFromTicket: (ticketId: string) => Promise<void>
  addTicketUpdateListener: (handlers: TicketUpdateHandlers) => () => void
  /** Force refresh hint for components - increments when any ticket update occurs */
  refreshHint: number
  /** The last updated ticket ID (for selective refresh) */
  lastUpdatedTicketId: string | null
  /** The last update type */
  lastUpdateType: string | null
}

const RealtimeContext = createContext<RealtimeContextValue | null>(null)

/**
 * Provider component for real-time updates via SignalR
 * Wrap your app with this to enable real-time sync across all dashboards
 */
export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { 
    connected, 
    connectionState, 
    on, 
    subscribeToTicket, 
    unsubscribeFromTicket 
  } = useSignalR()
  
  const [refreshHint, setRefreshHint] = useState(0)
  const [lastUpdatedTicketId, setLastUpdatedTicketId] = useState<string | null>(null)
  const [lastUpdateType, setLastUpdateType] = useState<string | null>(null)
  
  // Store handlers in a ref to avoid re-subscribing on every render
  const handlersRef = useRef<Set<TicketUpdateHandlers>>(new Set())

  // Add a listener for ticket updates
  const addTicketUpdateListener = useCallback((handlers: TicketUpdateHandlers) => {
    handlersRef.current.add(handlers)
    return () => {
      handlersRef.current.delete(handlers)
    }
  }, [])

  // Set up SignalR event listeners
  useEffect(() => {
    if (!connected) return

    // Listen for TicketStatusUpdated events
    const unsubStatus = on("TicketStatusUpdated", (data: TicketStatusUpdatePayload) => {
      console.log("[Realtime] TicketStatusUpdated:", data)
      
      // Trigger refresh hint for all components
      setRefreshHint(prev => prev + 1)
      setLastUpdatedTicketId(data.ticketId)
      setLastUpdateType("StatusChanged")

      // Notify all registered handlers
      handlersRef.current.forEach(handlers => {
        handlers.onStatusChanged?.(data)
        handlers.onAnyUpdate?.(data.ticketId, "StatusChanged")
      })
    })

    // Listen for TicketUpdated events (replies, assignments, etc.)
    const unsubUpdated = on("TicketUpdated", (data: TicketUpdatePayload) => {
      console.log("[Realtime] TicketUpdated:", data)
      
      // Trigger refresh hint for all components
      setRefreshHint(prev => prev + 1)
      setLastUpdatedTicketId(data.ticketId)
      setLastUpdateType(data.updateType)

      // Notify all registered handlers based on update type
      handlersRef.current.forEach(handlers => {
        switch (data.updateType) {
          case "ReplyAdded":
            handlers.onReplyAdded?.(data)
            break
          case "AssignmentChanged":
            handlers.onAssignmentChanged?.(data)
            break
        }
        handlers.onAnyUpdate?.(data.ticketId, data.updateType)
      })
    })

    return () => {
      unsubStatus()
      unsubUpdated()
    }
  }, [connected, on])

  const value: RealtimeContextValue = {
    connected,
    connectionState,
    subscribeToTicket,
    unsubscribeFromTicket,
    addTicketUpdateListener,
    refreshHint,
    lastUpdatedTicketId,
    lastUpdateType,
  }

  return (
    <RealtimeContext.Provider value={value}>
      {children}
    </RealtimeContext.Provider>
  )
}

/**
 * Hook to access real-time context
 */
export function useRealtime(): RealtimeContextValue {
  const context = useContext(RealtimeContext)
  if (!context) {
    // Return a no-op implementation if not wrapped in provider
    // This allows components to work without the provider during SSR
    return {
      connected: false,
      connectionState: "disconnected",
      subscribeToTicket: async () => {},
      unsubscribeFromTicket: async () => {},
      addTicketUpdateListener: () => () => {},
      refreshHint: 0,
      lastUpdatedTicketId: null,
      lastUpdateType: null,
    }
  }
  return context
}

/**
 * Hook to subscribe to ticket updates and trigger callback when ticket is updated
 * 
 * @param ticketId - The ticket ID to watch (optional - if not provided, watches all tickets)
 * @param onUpdate - Callback when the ticket is updated
 */
export function useTicketUpdates(
  ticketId: string | null | undefined,
  onUpdate?: (updateType: string) => void
) {
  const { 
    refreshHint, 
    lastUpdatedTicketId, 
    lastUpdateType,
    addTicketUpdateListener,
    subscribeToTicket,
    unsubscribeFromTicket,
    connected,
  } = useRealtime()

  // Subscribe to specific ticket if ID is provided
  useEffect(() => {
    if (!ticketId || !connected) return
    
    subscribeToTicket(ticketId)
    
    return () => {
      unsubscribeFromTicket(ticketId)
    }
  }, [ticketId, connected, subscribeToTicket, unsubscribeFromTicket])

  // Trigger callback when the watched ticket is updated
  useEffect(() => {
    if (!ticketId || !lastUpdatedTicketId || !lastUpdateType) return
    
    if (lastUpdatedTicketId === ticketId) {
      onUpdate?.(lastUpdateType)
    }
  }, [ticketId, lastUpdatedTicketId, lastUpdateType, onUpdate, refreshHint])

  return {
    refreshHint,
    lastUpdatedTicketId,
    lastUpdateType,
    connected,
  }
}

/**
 * Hook to get notified of any ticket list update (for dashboard ticket lists)
 * Returns a refreshHint that increments on any ticket update
 */
export function useTicketListUpdates(onUpdate?: () => void) {
  const { refreshHint, addTicketUpdateListener, connected } = useRealtime()
  const prevRefreshHint = useRef(refreshHint)

  // Call onUpdate callback when refreshHint changes
  useEffect(() => {
    if (refreshHint !== prevRefreshHint.current) {
      prevRefreshHint.current = refreshHint
      onUpdate?.()
    }
  }, [refreshHint, onUpdate])

  return { refreshHint, connected }
}

export default RealtimeProvider

"use client"

import { useState, useEffect, useRef, useCallback } from "react"
import * as signalR from "@microsoft/signalr"
import { useAuth } from "@/lib/auth-context"
import { getApiBaseUrl } from "@/lib/api-client"

/**
 * SignalR connection states for UI feedback
 */
export type SignalRState = "disconnected" | "connecting" | "connected" | "reconnecting"

/**
 * Ticket update event payload from backend
 */
export interface TicketUpdatePayload {
  ticketId: string
  updateType: "StatusChanged" | "ReplyAdded" | "AssignmentChanged"
  updatedAt: string
  metadata?: {
    oldStatus?: string
    newStatus?: string
    messageId?: string
    authorName?: string
    authorRole?: string
    messagePreview?: string
    newTechnicianUserIds?: string[]
  }
}

/**
 * Ticket status update payload from backend
 */
export interface TicketStatusUpdatePayload {
  ticketId: string
  oldStatus: string
  newStatus: string
  updatedAt: string
  actorUserId: string
  actorRole: string
}

/**
 * Hook return type
 */
export interface UseSignalRReturn {
  connection: signalR.HubConnection | null
  connected: boolean
  connectionState: SignalRState
  invoke: <T = void>(methodName: string, ...args: unknown[]) => Promise<T>
  on: (eventName: string, callback: (...args: unknown[]) => void) => () => void
  off: (eventName: string, callback: (...args: unknown[]) => void) => void
  subscribeToTicket: (ticketId: string) => Promise<void>
  unsubscribeFromTicket: (ticketId: string) => Promise<void>
}

/**
 * Build SignalR hub URL from API base (same origin as REST API to avoid 1006 / CORS).
 * Use getApiBaseUrl() so hub connects to the same host/port the app uses.
 */
export async function getSignalRHubUrl(): Promise<string> {
  const base = await getApiBaseUrl();
  const normalized = base.replace(/\/$/, "");
  return `${normalized}/hubs/tickets`;
}

/**
 * Custom hook for SignalR connection management
 * Provides real-time updates for tickets across all dashboards.
 * Uses the same API base URL as the rest of the app (getApiBaseUrl) so hub and REST match.
 *
 * @param hubUrl - Optional override hub URL; if not set, uses getApiBaseUrl() + "/hubs/tickets"
 * @returns SignalR connection utilities
 */
export function useSignalR(hubUrl?: string): UseSignalRReturn {
  const { token, user } = useAuth();
  const [connection, setConnection] = useState<signalR.HubConnection | null>(null);
  const [connectionState, setConnectionState] = useState<SignalRState>("disconnected");
  const connectionRef = useRef<signalR.HubConnection | null>(null);
  const reconnectTimeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const startPromiseRef = useRef<Promise<void> | null>(null);
  const retryCountRef = useRef(0);
  const warnedRef = useRef(false);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;

    if (!user) {
      if (connectionRef.current) {
        connectionRef.current.stop().catch(() => {});
        connectionRef.current = null;
        setConnection(null);
        setConnectionState("disconnected");
      }
      return () => {
        mountedRef.current = false;
      };
    }

    let cancelled = false;

    (async () => {
      const resolvedHubUrl = hubUrl ?? (await getSignalRHubUrl());
      if (cancelled || !mountedRef.current) return;

      if (
        connectionRef.current &&
        connectionRef.current.state !== signalR.HubConnectionState.Disconnected
      ) {
        return;
      }

      const builder = new signalR.HubConnectionBuilder()
        .withUrl(resolvedHubUrl, {
          accessTokenFactory: () => token ?? "",
          transport:
            signalR.HttpTransportType.WebSockets |
            signalR.HttpTransportType.ServerSentEvents |
            signalR.HttpTransportType.LongPolling,
          skipNegotiation: false,
          withCredentials: true,
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]);

      if (process.env.NODE_ENV === "development") {
        builder.configureLogging(signalR.LogLevel.Information);
      }

      const newConnection = builder.build();

      newConnection.onreconnecting(() => {
        if (mountedRef.current) setConnectionState("reconnecting");
      });

      newConnection.onreconnected(() => {
        if (mountedRef.current) setConnectionState("connected");
      });

      newConnection.onclose((error) => {
        if (!mountedRef.current) return;
        if (error) {
          const msg = error.message || "";
          const is1006 = msg.includes("1006") || msg.includes("WebSocket");
          if (process.env.NODE_ENV === "development" || is1006) {
            console.warn("[SignalR] Connection closed:", {
              message: msg,
              hubUrl: resolvedHubUrl,
              hint: "Hub URL must match backend (same as API). Check CORS, auth, and backend running.",
            });
          }
        }
        setConnectionState("disconnected");
      });

      connectionRef.current = newConnection;
      setConnection(newConnection);
      startConnection(newConnection, resolvedHubUrl);
    })();

    return () => {
      cancelled = true;
      mountedRef.current = false;
      if (reconnectTimeoutRef.current) {
        clearTimeout(reconnectTimeoutRef.current);
        reconnectTimeoutRef.current = null;
      }
      const conn = connectionRef.current;
      if (conn) {
        connectionRef.current = null;
        conn.stop().catch(() => {});
      }
      setConnection(null);
      setConnectionState("disconnected");
      retryCountRef.current = 0;
      warnedRef.current = false;
    };
  }, [user, hubUrl]);

  // Start connection with retry logic (single flight per connection)
  const startConnection = async (conn: signalR.HubConnection, hubUrl: string) => {
    if (conn.state !== signalR.HubConnectionState.Disconnected) return;
    if (startPromiseRef.current) return;

    setConnectionState("connecting");

    try {
      startPromiseRef.current = conn.start();
      await startPromiseRef.current;
      startPromiseRef.current = null;
      retryCountRef.current = 0;
      warnedRef.current = false;
      if (mountedRef.current) setConnectionState("connected");
      if (process.env.NODE_ENV === "development") {
        const transport = (conn as unknown as { connection?: { transport?: { name?: string } } }).connection?.transport?.name ?? "unknown";
        console.log("[SignalR] Connected:", hubUrl, { transport });
      }
    } catch (error: unknown) {
      startPromiseRef.current = null;
      const msg = error instanceof Error ? error.message : String(error);
      if (process.env.NODE_ENV === "development") {
        console.warn("[SignalR] Start failed:", { hubUrl, error: msg, retry: retryCountRef.current });
      }
      if (mountedRef.current) setConnectionState("disconnected");

      const maxRetries = 5;
      retryCountRef.current += 1;
      if (retryCountRef.current >= maxRetries) {
        if (!warnedRef.current) {
          console.warn("[SignalR] Max retries reached; check backend and CORS.");
          warnedRef.current = true;
        }
        return;
      }

      const delay = Math.min(1000 * Math.pow(2, retryCountRef.current), 10000);
      reconnectTimeoutRef.current = setTimeout(() => {
        reconnectTimeoutRef.current = null;
        if (mountedRef.current && conn.state === signalR.HubConnectionState.Disconnected) {
          startConnection(conn, hubUrl);
        }
      }, delay);
    }
  };

  // Invoke a hub method
  const invoke = useCallback(async <T = void>(
    methodName: string,
    ...args: unknown[]
  ): Promise<T> => {
    if (!connectionRef.current || connectionRef.current.state !== signalR.HubConnectionState.Connected) {
      console.warn(`[SignalR] Cannot invoke ${methodName}: not connected`)
      throw new Error("SignalR not connected")
    }
    return connectionRef.current.invoke<T>(methodName, ...args)
  }, [])

  // Subscribe to an event
  const on = useCallback((
    eventName: string,
    callback: (...args: unknown[]) => void
  ): (() => void) => {
    if (!connectionRef.current) {
      console.warn(`[SignalR] Cannot subscribe to ${eventName}: no connection`)
      return () => {}
    }
    
    connectionRef.current.on(eventName, callback)
    
    // Return unsubscribe function
    return () => {
      connectionRef.current?.off(eventName, callback)
    }
  }, [])

  // Unsubscribe from an event
  const off = useCallback((
    eventName: string,
    callback: (...args: unknown[]) => void
  ) => {
    connectionRef.current?.off(eventName, callback)
  }, [])

  // Subscribe to a specific ticket's updates
  const subscribeToTicket = useCallback(async (ticketId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connectionRef.current.invoke("SubscribeToTicket", ticketId)
        console.log(`[SignalR] Subscribed to ticket ${ticketId}`)
      } catch (error) {
        console.error(`[SignalR] Failed to subscribe to ticket ${ticketId}:`, error)
      }
    }
  }, [])

  // Unsubscribe from a specific ticket's updates
  const unsubscribeFromTicket = useCallback(async (ticketId: string) => {
    if (connectionRef.current?.state === signalR.HubConnectionState.Connected) {
      try {
        await connectionRef.current.invoke("UnsubscribeFromTicket", ticketId)
        console.log(`[SignalR] Unsubscribed from ticket ${ticketId}`)
      } catch (error) {
        console.error(`[SignalR] Failed to unsubscribe from ticket ${ticketId}:`, error)
      }
    }
  }, [])

  return {
    connection,
    connected: connectionState === "connected",
    connectionState,
    invoke,
    on,
    off,
    subscribeToTicket,
    unsubscribeFromTicket,
  }
}

export default useSignalR

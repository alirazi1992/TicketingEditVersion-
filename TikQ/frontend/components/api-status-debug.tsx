"use client"

import { useEffect, useState } from "react"
import { getApiBaseUrl } from "@/lib/api-client"
import { apiFetch } from "@/lib/api"

/**
 * Dev-only component to show API connection status
 * Only visible in development mode
 */
export function ApiStatusDebug() {
  const [apiBase, setApiBase] = useState<string>("...")
  const [healthStatus, setHealthStatus] = useState<{
    status: 'connected' | 'failed' | 'checking';
    url?: string;
    error?: string;
    triedUrls?: string[];
    lastStatusCode?: number;
  }>({ status: 'checking' })
  const [lastError, setLastError] = useState<{
    message?: string;
    status?: number;
    url?: string;
  } | null>(null)
  const [exampleUrl, setExampleUrl] = useState<string>("...")
  const [lastRequestUrl, setLastRequestUrl] = useState<string>("...")
  const healthChecksEnabled = process.env.NEXT_PUBLIC_ENABLE_HEALTH_CHECKS === "true"

  useEffect(() => {
    // Only show in development
    if (process.env.NODE_ENV !== "development" || !healthChecksEnabled) {
      return
    }

    const updateStatus = async () => {
      try {
        // Get resolved API base URL
        const base = await getApiBaseUrl()
        setApiBase(base || "(not set)")
        
        // Set example URL
        if (base) {
          setExampleUrl(`${base}/api/tickets`)
        }

        // Check health by fetching from the resolved base URL (with credentials for cookie auth)
        try {
          const controller = new AbortController()
          const timeoutId = setTimeout(() => controller.abort(), 5000)
          const response = await apiFetch("/api/health", {
            signal: controller.signal,
            cache: "no-store",
          })
          clearTimeout(timeoutId)
          
          if (response.ok) {
            setHealthStatus({
              status: 'connected',
              url: base,
            })
          } else {
            setHealthStatus({
              status: 'failed',
              url: base,
              error: `Health endpoint returned ${response.status}`,
              lastStatusCode: response.status,
            })
          }
        } catch (fetchErr: any) {
          setHealthStatus({
            status: 'failed',
            url: base,
            error: fetchErr.name === 'AbortError' 
              ? 'Request timeout' 
              : (fetchErr.message || 'Failed to connect'),
            triedUrls: [base],
          })
        }
        
        // Check for last API error and request URL from window global
        if (typeof window !== "undefined") {
          if ((window as any).__lastApiError) {
            setLastError((window as any).__lastApiError)
          }
          if ((window as any).__lastApiRequestUrl) {
            setLastRequestUrl((window as any).__lastApiRequestUrl)
          }
        }
      } catch (err: any) {
        setHealthStatus({
          status: 'failed',
          error: err.message || 'Unknown error',
        })
      }
    }

    updateStatus()
  }, [])

  // Only render in development
  if (process.env.NODE_ENV !== "development" || !healthChecksEnabled) {
    return null
  }

  const statusColor = healthStatus.status === 'connected' ? 'text-green-600 dark:text-green-400' : 
                      healthStatus.status === 'failed' ? 'text-red-600 dark:text-red-400' : 
                      'text-yellow-600 dark:text-yellow-400'

  return (
    <div className="fixed bottom-4 left-4 z-50 bg-background/90 backdrop-blur-sm border border-border rounded-lg p-3 text-xs font-mono shadow-lg">
      <div className="space-y-1">
        <div>
          <span className="text-muted-foreground">API Base:</span>{" "}
          <span className="font-semibold">{apiBase}</span>
        </div>
        <div>
          <span className="text-muted-foreground">Health:</span>{" "}
          <span className={`font-semibold ${statusColor}`}>
            {healthStatus.status === 'connected' ? 'OK' : 
             healthStatus.status === 'failed' ? 'FAIL' : 
             'CHECKING...'}
          </span>
        </div>
        {healthStatus.url && (
          <div className="text-muted-foreground">
            Resolved: {healthStatus.url}
          </div>
        )}
        {healthStatus.lastStatusCode && (
          <div className="text-muted-foreground text-[10px]">
            HTTP: {healthStatus.lastStatusCode}
          </div>
        )}
        {healthStatus.error && (
          <div className="text-red-600 dark:text-red-400 max-w-xs truncate text-[10px]">
            {healthStatus.error}
          </div>
        )}
        {healthStatus.triedUrls && healthStatus.triedUrls.length > 0 && healthStatus.status === 'failed' && (
          <div className="text-muted-foreground text-[10px] mt-1">
            <div className="font-semibold">Tried {healthStatus.triedUrls.length} URL(s):</div>
            <div className="mt-1 space-y-0.5 max-h-32 overflow-y-auto">
              {healthStatus.triedUrls.map((url, i) => (
                <div key={i} className="text-[9px] opacity-75 break-all">• {url}</div>
              ))}
            </div>
          </div>
        )}
        <div className="text-muted-foreground text-[10px] mt-2 pt-2 border-t border-border">
          Example: {exampleUrl}
        </div>
        {lastRequestUrl && lastRequestUrl !== "..." && (
          <div className="text-muted-foreground text-[10px] mt-1">
            Last Request: {lastRequestUrl}
          </div>
        )}
        {lastError && (
          <div className="text-red-600 dark:text-red-400 text-[10px] mt-1 max-w-xs truncate">
            Last Error: {lastError.message}
          </div>
        )}
      </div>
    </div>
  )
}
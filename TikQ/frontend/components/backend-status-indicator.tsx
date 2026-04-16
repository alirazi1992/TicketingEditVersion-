"use client"

import { useEffect, useState } from "react"
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { AlertCircle } from "lucide-react"
import { getApiBaseUrl } from "@/lib/api-client"
import { apiFetch } from "@/lib/api"

interface BackendDiagnostics {
  url: string;
  status: 'connected' | 'failed';
  error?: string;
  triedUrls?: string[];
  lastStatusCode?: number;
}

export function BackendStatusIndicator() {
  const [diagnostics, setDiagnostics] = useState<BackendDiagnostics | null>(null)
  const [isChecking, setIsChecking] = useState(true)
  const healthChecksEnabled = process.env.NEXT_PUBLIC_ENABLE_HEALTH_CHECKS === "true"

  useEffect(() => {
    if (!healthChecksEnabled) {
      setIsChecking(false)
      return
    }
    let mounted = true
    const checkHealth = async () => {
      try {
        const baseUrl = await getApiBaseUrl()
        
        // Actually test the health endpoint
        const controller = new AbortController()
        const timeoutId = setTimeout(() => controller.abort(), 5000)
        
        try {
          const response = await apiFetch("/api/health", {
            signal: controller.signal,
            cache: "no-store",
          })
          clearTimeout(timeoutId)
          
          if (mounted) {
            if (response.ok) {
              setDiagnostics({
                url: baseUrl,
                status: 'connected',
              })
            } else {
              setDiagnostics({
                url: baseUrl,
                status: 'failed',
                error: `Health endpoint returned ${response.status}`,
                lastStatusCode: response.status,
              })
            }
            setIsChecking(false)
          }
        } catch (fetchErr: any) {
          clearTimeout(timeoutId)
          if (mounted) {
            setDiagnostics({
              url: baseUrl,
              status: 'failed',
              error: fetchErr.name === 'AbortError' 
                ? 'Request timeout - backend may not be responding' 
                : (fetchErr.message || 'Failed to connect to backend'),
              triedUrls: [baseUrl],
            })
            setIsChecking(false)
          }
        }
      } catch (err: any) {
        if (mounted) {
          setDiagnostics({
            url: 'unknown',
            status: 'failed',
            error: err.message || 'Failed to check backend status',
          })
          setIsChecking(false)
        }
      }
    }

    // Initial check
    checkHealth()

    return () => {
      mounted = false
    }
  }, [healthChecksEnabled])

  // Only show error banner when backend is down
  // Show nothing when backend is OK (even during initial check)
  if (isChecking) {
    return null // Don't show anything while checking
  }

  if (!healthChecksEnabled) {
    return null
  }

  if (diagnostics && diagnostics.status === 'failed') {
    // Check if error mentions 404 - this indicates wrong health endpoint URL
    const is404Error = diagnostics.error?.includes('404') || diagnostics.error?.includes('Health endpoint');
    
    return (
      <Alert variant="destructive" className="m-4 border-orange-500 bg-orange-50 dark:bg-orange-950">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle>Backend Server Unavailable</AlertTitle>
        <AlertDescription className="space-y-2">
          <div>
            <strong>Status:</strong> Cannot connect to backend server
          </div>
          {diagnostics.error && (
            <div>
              <strong>Error:</strong> {diagnostics.error}
              {is404Error && (
                <div className="text-sm mt-1 text-orange-700 dark:text-orange-300">
                  The health check endpoint may be misconfigured. Verify the backend has /api/health endpoint.
                </div>
              )}
            </div>
          )}
          {diagnostics.triedUrls && diagnostics.triedUrls.length > 0 && (
            <div>
              <strong>Tried URLs ({diagnostics.triedUrls.length}):</strong>
              <div className="mt-1 space-y-1 text-sm font-mono">
                {diagnostics.triedUrls.slice(0, 5).map((url, i) => (
                  <div key={i} className="opacity-75 break-all">• {url}</div>
                ))}
                {diagnostics.triedUrls.length > 5 && (
                  <div className="opacity-50">... and {diagnostics.triedUrls.length - 5} more</div>
                )}
              </div>
            </div>
          )}
          {diagnostics.lastStatusCode && (
            <div>
              <strong>Last HTTP Status:</strong> {diagnostics.lastStatusCode}
            </div>
          )}
          <div className="text-sm mt-2">
            <strong>To fix:</strong> Start the backend server:
            <code className="ml-2 px-2 py-1 bg-gray-200 dark:bg-gray-800 rounded">
              cd &lt;REPO_ROOT&gt;\backend\Ticketing.Backend ; dotnet run
            </code>
          </div>
        </AlertDescription>
      </Alert>
    )
  }

  // Show nothing when backend is OK
  return null
}












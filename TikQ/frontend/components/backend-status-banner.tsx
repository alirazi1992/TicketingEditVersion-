"use client";

import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle, RefreshCw, XCircle, Database } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { getApiBaseUrl } from "@/lib/api-client";
import { apiFetch } from "@/lib/api";

interface HealthResponse {
  ok: boolean;
  status: string;
  database?: {
    path?: string;
    fileExists?: boolean;
    canConnect?: boolean;
    connected?: boolean;
    error?: string;
    dataCounts?: {
      categories?: number;
      tickets?: number;
      users?: number;
    };
  };
  hasData?: boolean;
  timestamp?: string;
  environment?: string;
  contentRoot?: string;
}

type BackendStatus = "checking" | "connected" | "degraded" | "disconnected";

export function BackendStatusBanner() {
  const [status, setStatus] = useState<BackendStatus>("checking");
  const [healthData, setHealthData] = useState<HealthResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isRetrying, setIsRetrying] = useState(false);
  const [apiUrl, setApiUrl] = useState<string>("");

  const checkHealth = async () => {
    setIsRetrying(true);
    setErrorMessage(null);

    try {
      const baseUrl = await getApiBaseUrl();
      setApiUrl(baseUrl);

      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 5000);

      const response = await apiFetch("/api/health", {
        method: "GET",
        signal: controller.signal,
        cache: "no-store",
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        setStatus("degraded");
        setErrorMessage(`HTTP ${response.status}: ${response.statusText}`);
        return;
      }

      const data: HealthResponse = await response.json();
      setHealthData(data);

      // Determine status based on health response
      const dbConnected = data.database?.canConnect || data.database?.connected;
      const hasData = data.hasData || (data.database?.dataCounts?.users ?? 0) > 0;

      if (!dbConnected) {
        setStatus("degraded");
        setErrorMessage(data.database?.error || "Database connection failed");
      } else if (!hasData) {
        setStatus("degraded");
        setErrorMessage("Database is empty - no seed data found");
      } else {
        setStatus("connected");
      }
    } catch (error: any) {
      setStatus("disconnected");
      if (error.name === "AbortError") {
        setErrorMessage("Connection timeout - backend may not be running");
      } else if (error.message?.includes("Failed to fetch") || error.message?.includes("NetworkError")) {
        setErrorMessage("Cannot reach backend server. Please ensure it is running.");
      } else {
        setErrorMessage(error.message || "Unknown error");
      }
    } finally {
      setIsRetrying(false);
    }
  };

  useEffect(() => {
    checkHealth();
  }, []);

  // Don't show banner when everything is healthy
  if (status === "connected") {
    return null;
  }

  // Show checking state briefly
  if (status === "checking") {
    return (
      <Alert className="mb-4 bg-blue-50 border-blue-200">
        <RefreshCw className="h-4 w-4 animate-spin text-blue-500" />
        <AlertTitle className="text-blue-700">بررسی اتصال به سرور...</AlertTitle>
        <AlertDescription className="text-blue-600">
          در حال بررسی وضعیت سرور و پایگاه داده
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <Alert
      className={`mb-4 ${
        status === "disconnected"
          ? "bg-red-50 border-red-300"
          : "bg-yellow-50 border-yellow-300"
      }`}
    >
      {status === "disconnected" ? (
        <XCircle className="h-4 w-4 text-red-500" />
      ) : (
        <AlertTriangle className="h-4 w-4 text-yellow-500" />
      )}
      <AlertTitle
        className={status === "disconnected" ? "text-red-700" : "text-yellow-700"}
      >
        {status === "disconnected" ? "سرور در دسترس نیست" : "هشدار اتصال"}
      </AlertTitle>
      <AlertDescription
        className={`space-y-2 ${
          status === "disconnected" ? "text-red-600" : "text-yellow-600"
        }`}
      >
        <p>{errorMessage || "خطا در اتصال به سرور"}</p>
        
        {apiUrl && (
          <p className="text-xs opacity-75 font-mono">
            API URL: {apiUrl}
          </p>
        )}

        {healthData?.database && (
          <div className="text-xs opacity-75 space-y-1 border-t pt-2 mt-2">
            <p className="font-semibold flex items-center gap-1">
              <Database className="h-3 w-3" />
              اطلاعات پایگاه داده:
            </p>
            <p>مسیر: {healthData.database.path || "نامشخص"}</p>
            <p>فایل موجود: {healthData.database.fileExists ? "بله" : "خیر"}</p>
            <p>اتصال: {healthData.database.canConnect || healthData.database.connected ? "برقرار" : "قطع"}</p>
            {healthData.database.dataCounts && (
              <p>
                داده‌ها: {healthData.database.dataCounts.users || 0} کاربر،{" "}
                {healthData.database.dataCounts.categories || 0} دسته‌بندی،{" "}
                {healthData.database.dataCounts.tickets || 0} تیکت
              </p>
            )}
          </div>
        )}

        <div className="flex gap-2 pt-2">
          <Button
            variant="outline"
            size="sm"
            onClick={checkHealth}
            disabled={isRetrying}
            className="gap-1"
          >
            <RefreshCw className={`h-3 w-3 ${isRetrying ? "animate-spin" : ""}`} />
            تلاش مجدد
          </Button>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => window.open(`${apiUrl}/api/debug/data-status`, "_blank")}
            className="gap-1 text-xs"
          >
            مشاهده جزئیات
          </Button>
        </div>
      </AlertDescription>
    </Alert>
  );
}

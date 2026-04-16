// lib/api-client.ts
// Robust API client; set NEXT_PUBLIC_API_BASE_URL to customize (dev default: http://localhost:8080).

import { normalizeBaseUrl, joinApi, getEffectiveApiBaseUrl, getDefaultApiBaseUrl } from "./url";

// Session cache for successful API base URL
const API_BASE_URL_CACHE_KEY = "ticketing.api.baseUrl";
const API_BASE_URL_DETECTION_KEY = "ticketing.api.detectionInProgress";

// Use direct calls by default (Option A)
// Set NEXT_PUBLIC_API_BASE_URL to use a specific backend URL (e.g. http://localhost:8080 for IIS).
// If not set in dev, defaults to http://localhost:5000 (matches tools/run-backend.ps1).
const USE_PROXY = false; // Always use direct calls (Option A)

// Get cached API base URL or null
function getCachedApiBaseUrl(): string | null {
  if (typeof window === "undefined") return null;
  try {
    return localStorage.getItem(API_BASE_URL_CACHE_KEY);
  } catch {
    return null;
  }
}

// Cache successful API base URL
function cacheApiBaseUrl(url: string): void {
  if (typeof window === "undefined") return;
  try {
    localStorage.setItem(API_BASE_URL_CACHE_KEY, url);
  } catch {
    // Ignore localStorage errors
  }
}

// Clear cached API base URL (exported for manual cache clearing)
export function clearApiBaseUrlCache(): void {
  if (typeof window === "undefined") return;
  try {
    localStorage.removeItem(API_BASE_URL_CACHE_KEY);
    sessionStorage.removeItem(API_BASE_URL_DETECTION_KEY);
    // Reset the module-level cache
    API_BASE_URL = null;
    apiBaseUrlPromise = null;
  } catch {
    // Ignore localStorage errors
  }
}

// Check if detection is in progress to prevent infinite loops
function isDetectionInProgress(): boolean {
  if (typeof window === "undefined") return false;
  try {
    return sessionStorage.getItem(API_BASE_URL_DETECTION_KEY) === "true";
  } catch {
    return false;
  }
}

function setDetectionInProgress(value: boolean): void {
  if (typeof window === "undefined") return;
  try {
    if (value) {
      sessionStorage.setItem(API_BASE_URL_DETECTION_KEY, "true");
    } else {
      sessionStorage.removeItem(API_BASE_URL_DETECTION_KEY);
    }
  } catch {
    // Ignore sessionStorage errors
  }
}

// Test if a URL is reachable
async function testApiUrl(baseUrl: string, timeout = 2000): Promise<boolean> {
  try {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), timeout);
    
    // Use joinApi to ensure correct URL construction
    const healthUrl = joinApi(normalizeBaseUrl(baseUrl), "/api/health");
    
    const response = await fetch(healthUrl, {
      method: "GET",
      signal: controller.signal,
      cache: "no-store",
      mode: "cors",
      credentials: "include",
    });
    
    clearTimeout(timeoutId);
    return response.ok;
  } catch (error) {
    // Log the error for debugging
    console.warn(`[api-client] Failed to test API URL ${baseUrl}:`, error);
    return false;
  }
}

// Generate candidate URLs in priority order (dev default only in development)
function getBackendUrlCandidates(): string[] {
  const envUrl = normalizeBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL);
  const defaultUrl = getDefaultApiBaseUrl();
  const candidates = envUrl ? [envUrl] : (defaultUrl ? [defaultUrl] : []);
  return Array.from(new Set(candidates));
}

// Detect the correct API base URL
async function detectApiBaseUrl(): Promise<string> {
  const envUrl = normalizeBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL);
  if (envUrl) {
    return envUrl;
  }

  if (isDetectionInProgress()) {
    return getDefaultApiBaseUrl() || "";
  }

  setDetectionInProgress(true);

  try {
    const cachedUrl = getCachedApiBaseUrl();
    if (cachedUrl) {
      return cachedUrl;
    }

    const candidates = getBackendUrlCandidates();
    const selected = candidates[0] || getDefaultApiBaseUrl() || "";
    cacheApiBaseUrl(selected);
    return selected;
  } finally {
    setDetectionInProgress(false);
  }
}

// Initialize API base URL (detected once per session)
let API_BASE_URL: string | null = null;
let apiBaseUrlPromise: Promise<string> | null = null;
const apiLogDedup = new Map<string, number>();
const API_LOG_DEDUP_WINDOW_MS = 10_000;
const healthCheckDedup = new Map<string, number>();
const HEALTH_CHECK_DEDUP_WINDOW_MS = 10_000;

function shouldLogDedup(key: string): boolean {
  const now = Date.now();
  const last = apiLogDedup.get(key);
  if (last && now - last < API_LOG_DEDUP_WINDOW_MS) {
    return false;
  }
  apiLogDedup.set(key, now);
  return true;
}

function shouldPingHealth(key: string): boolean {
  const now = Date.now();
  const last = healthCheckDedup.get(key);
  if (last && now - last < HEALTH_CHECK_DEDUP_WINDOW_MS) {
    return false;
  }
  healthCheckDedup.set(key, now);
  return true;
}

/** In production, NEXT_PUBLIC_API_BASE_URL must be set and an absolute URL. Throws if not. */
function requireProductionApiBaseUrl(): void {
  if (process.env.NODE_ENV !== "production") return;
  const raw = process.env.NEXT_PUBLIC_API_BASE_URL;
  const trimmed = typeof raw === "string" ? raw.trim() : "";
  if (!trimmed || !/^https?:\/\//i.test(trimmed)) {
    throw new Error("NEXT_PUBLIC_API_BASE_URL is required in production and must be an absolute URL.");
  }
}

export async function getApiBaseUrl(): Promise<string> {
  // Production: fail fast if env missing or not absolute; no localhost fallback.
  requireProductionApiBaseUrl();

  // Production must be env-driven. Auto-detect is dev-only.
  const envBase = normalizeBaseUrl(process.env.NEXT_PUBLIC_API_BASE_URL);
  if (envBase) {
    if (!API_BASE_URL) API_BASE_URL = envBase;
    return envBase;
  }

  // Always use direct calls (Option A) - MUST return absolute URL
  // Return cached value if available
  if (API_BASE_URL) {
    return API_BASE_URL;
  }

  // If detection is in progress, wait for it
  if (apiBaseUrlPromise) {
    return apiBaseUrlPromise;
  }

  // Start detection
  apiBaseUrlPromise = detectApiBaseUrl();
  API_BASE_URL = await apiBaseUrlPromise;
  apiBaseUrlPromise = null;

  const fallback = getDefaultApiBaseUrl();
  if (!API_BASE_URL || (!API_BASE_URL.startsWith("http://") && !API_BASE_URL.startsWith("https://"))) {
    if (process.env.NODE_ENV === "production") {
      throw new Error("NEXT_PUBLIC_API_BASE_URL is required in production and must be an absolute URL.");
    }
    if (fallback) {
      console.warn("[api-client] Invalid URL, using dev default:", fallback);
      API_BASE_URL = fallback;
    } else {
      console.error("[api-client] No valid API base URL. Production must set NEXT_PUBLIC_API_BASE_URL.");
    }
  }

  if (typeof window !== "undefined" && process.env.NODE_ENV === "development") {
    console.log("[api-client] Resolved API Base URL:", API_BASE_URL);
    console.log("[api-client] NEXT_PUBLIC_API_BASE_URL:", process.env.NEXT_PUBLIC_API_BASE_URL || "(not set, using dev default)");
    console.log("[api-client] Using direct calls (Option A)");
  }

  return API_BASE_URL;
}

interface ApiRequestOptions {
  method?: string;
  token?: string | null;
  body?: unknown;
  silent?: boolean; // If true, suppress console.error on non-2xx responses (still throws error)
}

/** Options for GET requests that must never be cached (ticket lists, details, messages). */
export interface ApiGetNoStoreOptions {
  token?: string | null;
  silent?: boolean;
}

/**
 * GET request that never caches: uses cache: "no-store" and credentials: "include".
 * Use for all ticket-related GETs (lists, details, messages) so UI reflects latest data.
 * (If you add server-side fetch for tickets, use next: { revalidate: 0 } there.)
 */
export async function apiGetNoStore<TResponse>(
  path: string,
  options: ApiGetNoStoreOptions = {}
): Promise<TResponse> {
  return apiRequest<TResponse>(path, { ...options, method: "GET" });
}

function isAbsoluteUrl(value: string): boolean {
  return /^https?:\/\//i.test(value);
}

function assertNoApiHttp(url: string): void {
  if (!url.includes("/api/http://") && !url.includes("/api/https://")) {
    return;
  }
  const error = new Error(`[api-client] FORBIDDEN: URL contains "/api/http". URL: ${url}`);
  if (process.env.NODE_ENV === "development") {
    console.error(error);
    throw error;
  }
  console.warn(error.message);
}

function normalizeRequestPath(path: string): string {
  if (!isAbsoluteUrl(path)) {
    return path;
  }
  try {
    const parsed = new URL(path);
    return `${parsed.pathname}${parsed.search}`;
  } catch {
    return path;
  }
}

async function pingApiHealth(baseUrl: string): Promise<{ ok: boolean; status?: number; error?: string; url: string }> {
  try {
    const normalizedBase = normalizeBaseUrl(baseUrl);
    const healthUrl = joinApi(normalizedBase, "/api/health");
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 1500);
    const res = await fetch(healthUrl, {
      method: "GET",
      signal: controller.signal,
      cache: "no-store",
      mode: "cors",
      credentials: "include",
    });
    clearTimeout(timeoutId);
    return { ok: res.ok, status: res.status, url: healthUrl };
  } catch (err: any) {
    const normalizedBase = normalizeBaseUrl(baseUrl);
    const healthUrl = joinApi(normalizedBase, "/api/health");
    return {
      ok: false,
      error: err?.message || err?.name || "health-check-failed",
      url: healthUrl,
    };
  }
}

export async function checkApiHealth(): Promise<{ ok: boolean; status?: number; error?: string; url: string }> {
  const baseUrl = await getApiBaseUrl();
  return pingApiHealth(baseUrl);
}

// Custom error class for API errors
export class ApiError extends Error {
  /** True when status is 403 (permission denied); use for user-friendly toast, not "server crashed". */
  isForbidden?: boolean;
  /** True when status >= 500 (server error). */
  isServerError?: boolean;
  constructor(
    message: string,
    public method: string,
    public url: string,
    public status: number,
    public statusText: string,
    public contentType: string,
    public body: unknown,
    public rawText: string | null,
    public requestPath: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// Error deduplication map: key -> last logged timestamp
const errorLogDedup = new Map<string, number>();
const ERROR_LOG_DEDUP_WINDOW_MS = 5000; // 5 seconds

function shouldLogError(method: string, url: string, status: number): boolean {
  const key = `${method}:${url}:${status}`;
  const now = Date.now();
  const last = errorLogDedup.get(key);
  if (last && now - last < ERROR_LOG_DEDUP_WINDOW_MS) {
    return false;
  }
  errorLogDedup.set(key, now);
  return true;
}

export async function apiRequest<TResponse>(
  path: string,
  options: ApiRequestOptions = {}
): Promise<TResponse> {
  const { method = "GET", token, body, silent = false } = options;

  // Get the resolved API base URL (with automatic detection)
  let baseUrl = await getApiBaseUrl();
  // In dev, never use empty base so we always hit backend (e.g. http://localhost:8080), not same-origin
  if (process.env.NODE_ENV === "development" && !normalizeBaseUrl(baseUrl)) {
    baseUrl = getDefaultApiBaseUrl();
  }

  let url: string;
  let requestPath: string;
  let effectiveBase = "";

  if (isAbsoluteUrl(path) && process.env.NODE_ENV === "development") {
    const logKey = `absolute:${method}:${path}`;
    if (shouldLogDedup(logKey)) {
      console.error(`[api-client] FORBIDDEN: apiRequest only accepts relative paths. Got: ${path}`);
    }
  }

  const normalizedInputPath = normalizeRequestPath(path);
  const isAbsoluteInput = isAbsoluteUrl(normalizedInputPath);

  if (isAbsoluteInput) {
    const error = new Error(`[api-client] FORBIDDEN: apiRequest only accepts relative paths. Got: ${normalizedInputPath}`);
    if (process.env.NODE_ENV === "development") {
      throw error;
    }
    throw error;
  }

  // Ensure path starts with /api/
  let apiPath = normalizedInputPath.startsWith("/") ? normalizedInputPath : `/${normalizedInputPath}`;
  if (!apiPath.startsWith("/api/")) {
    // If path doesn't start with /api/, add it
    const cleanPath = apiPath.startsWith("/") ? apiPath.slice(1) : apiPath;
    apiPath = `/api/${cleanPath}`;
  }

  // Always use direct connection (Option A) - MUST use absolute URL
  const normalizedBase = normalizeBaseUrl(baseUrl);

  // GUARD: Prevent relative URLs and /bapi usage
  if (apiPath.startsWith("/bapi")) {
    const error = new Error(`[api-client] FORBIDDEN: Request path starts with "/bapi". This is not allowed. Path: ${apiPath}. Call stack: ${new Error().stack}`);
    console.error(error);
    if (process.env.NODE_ENV === "development") {
      throw error;
    }
  }

  // GUARD: Ensure base URL is absolute
  effectiveBase = normalizedBase;
  if (!effectiveBase || (!effectiveBase.startsWith("http://") && !effectiveBase.startsWith("https://"))) {
    if (process.env.NODE_ENV === "production") {
      throw new Error("NEXT_PUBLIC_API_BASE_URL is required in production and must be an absolute URL.");
    }
    const error = new Error(`[api-client] FORBIDDEN: API base URL is not absolute. Base: "${effectiveBase}", Path: ${apiPath}. This will cause relative URL requests. Call stack: ${new Error().stack}`);
    console.error(error);
    effectiveBase = getDefaultApiBaseUrl() || "";
    if (effectiveBase) {
      console.warn(`[api-client] Using dev default base URL: ${effectiveBase}`);
    }
    if (!effectiveBase) throw error;
  }

  url = joinApi(effectiveBase, apiPath);
  requestPath = apiPath; // For logging

  assertNoApiHttp(url);
  
  // GUARD: Final check - URL must be absolute
  if (!url.startsWith("http://") && !url.startsWith("https://")) {
    const error = new Error(`[api-client] FORBIDDEN: Final URL is not absolute. URL: "${url}", Base: "${effectiveBase}", Path: ${requestPath}. Call stack: ${new Error().stack}`);
    console.error(error);
    if (process.env.NODE_ENV === "development") {
      throw error;
    }
  }

  const headers: Record<string, string> = {};

  // Only set Content-Type for JSON, not for FormData
  const isFormData = body instanceof FormData;
  if (!isFormData) {
    headers["Content-Type"] = "application/json";
  }

  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }

  // Log the full resolved URL for debugging (this is critical for finding 404 issues)
  if (process.env.NODE_ENV === "development") {
    console.log(`[apiRequest] ${method} ${url}`, {
      baseUrl: baseUrl || "(proxy)",
      path: requestPath,
      hasToken: !!token,
      isFormData,
      body: isFormData ? "[FormData]" : (body ? JSON.stringify(body).substring(0, 100) : undefined),
    });
    
    // Store last request URL for debug widget
    if (typeof window !== "undefined") {
      (window as any).__lastApiRequestUrl = url;
    }
  }

  // Add timeout to prevent hanging requests
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 10000); // 10 second timeout

  let res: Response;
  try {
    res = await fetch(url, {
      method,
      headers,
      body: isFormData ? body : (body ? JSON.stringify(body) : undefined),
      signal: controller.signal,
      cache: "no-store", // No caching for ticket/list/messages; use apiGetNoStore for GETs
      credentials: "include", // Cookie-based auth (CORS)
    });
    clearTimeout(timeoutId);
  } catch (error: any) {
    clearTimeout(timeoutId);
    
    // If using proxy, the proxy handles fallback automatically
    // Only retry with direct connection if not using proxy
    if (
      process.env.NODE_ENV === "development" &&
      !USE_PROXY &&
      (error.name === "AbortError" ||
       error.message?.includes("Failed to fetch") ||
       error.message?.includes("NetworkError") ||
       error.name === "TypeError") &&
      baseUrl === "http://localhost:8080" &&
      !isDetectionInProgress()
    ) {
      console.warn(`[api-client] Request to ${baseUrl} failed, clearing cache and retrying...`);
      
      // Clear cache and retry with fresh resolution
      API_BASE_URL = null;
      clearApiBaseUrlCache();
      
      // Retry once with detected URL
      const fallbackUrl = await getApiBaseUrl();
      if (fallbackUrl !== baseUrl) {
        console.log(`[api-client] Retrying request with ${fallbackUrl}`);
        return apiRequest<TResponse>(path, options);
      }
    }
    
    if (error.name === "AbortError") {
      const networkError = new Error("Request timeout: Backend server may not be responding. Please check if the backend is running on " + baseUrl);
      (networkError as any).isNetworkError = true;
      (networkError as any).isTimeout = true;
      (networkError as any).status = 0;
      (networkError as any).code = "REQUEST_TIMEOUT";
      (networkError as any).requestPath = requestPath;
      (networkError as any).resolvedUrl = url;
      throw networkError;
    }
    // Network errors (Failed to fetch, CORS, etc.)
    // Only treat as network error if it's actually a connection issue, not an HTTP error response
    const isActualNetworkError = 
      (error.message?.includes("Failed to fetch") || 
       error.message?.includes("NetworkError") || 
       error.name === "TypeError") &&
      !error.status; // If we have a status code, it's an HTTP error, not a network error
    
    if (isActualNetworkError) {
      // Get tried URLs for diagnostics
      const candidates = getBackendUrlCandidates();
      const triedUrls = candidates.slice(0, Math.min(6, candidates.length)); // Show first 6 candidates
      const networkError = new Error(
        "Cannot connect to backend server. Please ensure the backend is running. Start with: .\\tools\\run-backend.ps1"
      );
      (networkError as any).isNetworkError = true;
      (networkError as any).status = 0;
      (networkError as any).code = "BACKEND_UNREACHABLE";
      (networkError as any).originalError = error;
      (networkError as any).triedUrls = triedUrls;
      (networkError as any).lastAttemptedUrl = url;
      (networkError as any).requestPath = requestPath;
      (networkError as any).resolvedUrl = url;
      // Log detailed diagnostics once per endpoint per window
      if (process.env.NODE_ENV === "development") {
        const logKey = `${method}:${requestPath}`;
        if (shouldLogDedup(logKey)) {
          console.error(`[api-client] Connection failed. Last attempted URL: ${url}`);
          console.error(`[api-client] Tried URLs: ${triedUrls.join(", ")}`);
          console.error(`[api-client] Error: ${error.message || error.name}`);
        }
      }
      
      throw networkError;
    }
    
    // For other errors (including HTTP errors with status codes), re-throw as-is
    throw error;
  }

  // Log response status (dev; always when NEXT_PUBLIC_DEBUG_API=1)
  if (process.env.NODE_ENV === "development" || (typeof process !== "undefined" && (process.env.NEXT_PUBLIC_DEBUG_API === "true" || process.env.NEXT_PUBLIC_DEBUG_API === "1"))) {
    console.log(`[apiRequest] ${method} ${url} → ${res.status} ${res.statusText}`);
  }

  if (!res.ok) {
    let errorBody: unknown = null;
    let errorMessage = `API request failed with status ${res.status}`;
    let responseText: string | null = null;
    const contentType = res.headers.get("content-type") || "";
    
    try {
      // Clone the response to read body (response can only be read once)
      const clonedRes = res.clone();
      // Try to read as text first to capture everything
      responseText = await clonedRes.text();
      
      // Try to parse as JSON
      if (responseText && responseText.trim()) {
        try {
          errorBody = JSON.parse(responseText);
        } catch (parseErr) {
          // Not JSON, use text as message
          errorMessage = responseText;
          // Store the text as the body for debugging
          errorBody = { rawText: responseText };
        }
      } else {
        errorBody = { empty: true };
      }
      
      if (errorBody && typeof errorBody === "object" && responseText) {
        (errorBody as Record<string, unknown>).rawText = responseText;
      }

      // Debug mode: surface failure quickly (console + window for toast/debug UI)
      const debugApi = typeof process !== "undefined" && (process.env.NEXT_PUBLIC_DEBUG_API === "true" || process.env.NEXT_PUBLIC_DEBUG_API === "1");
      if (debugApi && typeof window !== "undefined") {
        console.error(`[api-client] Mutation failed: ${method} ${url} → ${res.status} ${res.statusText}`, { body: errorBody, snippet: responseText?.slice(0, 300) });
        (window as any).__lastApiError = { url, status: res.status, statusText: res.statusText, method, body: errorBody };
      }

      // Extract error message from JSON body
      if (errorBody && typeof errorBody === "object") {
        const body = errorBody as Record<string, unknown>;
        if (body.errors && typeof body.errors === "object") {
          // ModelState errors
          const errors = body.errors as Record<string, unknown>;
          const firstError = Object.values(errors)[0];
          if (Array.isArray(firstError) && firstError.length > 0) {
            errorMessage = String(firstError[0]);
          }
        } else if (body.detail && typeof body.detail === "string") {
          // ProblemDetails.detail (primary for 403 Forbidden messages)
          errorMessage = body.detail;
        } else if (body.title && typeof body.title === "string") {
          errorMessage = body.title;
        } else if (body.message && typeof body.message === "string") {
          errorMessage = body.message;
        }
      }
    } catch (parseError) {
      // If all parsing fails, use responseText if available
      if (responseText) {
        errorMessage = responseText;
      }
    }
    
    // Handle 401 Unauthorized - clear session and redirect to login (cookie or token auth)
    if (res.status === 401 && typeof window !== "undefined") {
      localStorage.removeItem("ticketing.auth.token");
      localStorage.removeItem("ticketing.auth.user");
      localStorage.removeItem("userEmail");
      localStorage.removeItem("userName");
      const pathname = window.location.pathname ?? "";
      if (!pathname.startsWith("/login")) {
        const errCode = (errorBody && typeof errorBody === "object")
          ? ((errorBody as Record<string, unknown>).error ?? (errorBody as Record<string, unknown>).authError)
          : undefined;
        const query = errCode === "missing_role" ? "?error=missing_role" : "";
        console.warn("[apiRequest] 401 Unauthorized - redirecting to login" + (query ? " " + query : ""));
        window.location.href = "/login" + query;
      }
    }

    // 403 = permission denied (expected), not a server crash — log as warning; preserve first ~200 chars for debugging
    const isForbidden = res.status === 403;
    const isServerError = res.status >= 500;
    if (!silent) {
      const logSnippet = responseText ? responseText.substring(0, 200) : "(empty)";
      const errorInfo = {
        status: res.status,
        statusText: res.statusText,
        url: url,
        method: method,
        message: errorMessage,
        responseSnippet: logSnippet,
      };
      if (isForbidden) {
        if (process.env.NODE_ENV === "development" && shouldLogError(method, url, res.status)) {
          console.warn(`[apiRequest] 403 Forbidden (permission denied) ${method} ${url}`, errorInfo);
        }
      } else if (isServerError) {
        console.error(`[apiRequest] Server error ${res.status} ${method} ${url}`, errorInfo);
        console.error(`  Response: ${logSnippet}`);
      } else {
        console.warn(`[apiRequest] ${res.status} ${method} ${url}`, errorInfo);
      }
    }

    // Create typed error
    const apiError = new ApiError(
      errorMessage,
      method,
      url,
      res.status,
      res.statusText,
      contentType,
      errorBody,
      responseText,
      requestPath
    );

    apiError.isForbidden = isForbidden;
    apiError.isServerError = isServerError;

    if (errorBody && typeof errorBody === "object") {
      const body = errorBody as Record<string, unknown>;
      (apiError as any).traceId = body.traceId || body.traceID || (body as any).TraceId;
    }

    throw apiError;
  }

  if (res.status === 204) {
    // No Content
    return undefined as TResponse;
  }

  // In dev, read as text first to log raw body (proves URL/status/body for supervisor technician debugging)
  if (process.env.NODE_ENV === "development") {
    const rawText = await res.text();
    console.log(`[apiRequest] ${method} ${url} → ${res.status} response (first 800 chars):`, rawText.slice(0, 800));
    try {
      return (rawText ? JSON.parse(rawText) : undefined) as TResponse;
    } catch (e) {
      console.warn("[apiRequest] JSON parse failed for success response", e);
      return undefined as TResponse;
    }
  }

  return (await res.json()) as TResponse;
}
// lib/url.ts
// Canonical URL construction utilities to prevent double-adding /api and trailing slashes
// Production must set NEXT_PUBLIC_API_BASE_URL (e.g. in .env.production); dev default is used only in development.

/**
 * Default API base URL: only used in development/test (e.g. next dev, e2e). Production must set NEXT_PUBLIC_API_BASE_URL.
 * Dev default matches tools/run-backend.ps1 (port 5000). For IIS (port 8080), set NEXT_PUBLIC_API_BASE_URL.
 */
export function getDefaultApiBaseUrl(): string {
  const env = typeof process !== "undefined" ? process.env.NODE_ENV : undefined;
  if (env === "development" || env === "test") {
    return "http://localhost:5000";
  }
  return "";
}

/**
 * Normalizes a base URL by:
 * - Trimming whitespace
 * - Removing trailing slashes
 * - Removing trailing "/api" suffix if present
 */
export function normalizeBaseUrl(base: string | null | undefined): string {
  if (!base) {
    return "";
  }

  let normalized = base.trim();

  // Remove trailing slashes
  normalized = normalized.replace(/\/+$/, "");

  // Remove trailing "/api" if present
  if (normalized.endsWith("/api")) {
    normalized = normalized.slice(0, -4);
  }

  return normalized;
}

/**
 * Joins a base URL with an API path, ensuring exactly one slash between them.
 * The path must start with "/api/..."
 *
 * @param base - Base URL (e.g. from getEffectiveApiBaseUrl(); empty uses dev default only in development)
 * @param path - API path that must start with "/api/..." (e.g. "/api/health", "/api/tickets")
 * @returns Full URL with exactly one slash between base and path
 */
export function joinApi(base: string, path: string): string {
  const normalizedBase = normalizeBaseUrl(base) || getDefaultApiBaseUrl();

  // GUARD: Base must be absolute URL
  if (normalizedBase && !normalizedBase.startsWith("http://") && !normalizedBase.startsWith("https://")) {
    throw new Error(`[url] joinApi: Base URL must be absolute or empty. Got: "${base}" (normalized: "${normalizedBase}")`);
  }

  if (!normalizedBase) {
    console.warn("[url] joinApi: No API base URL set. Production must set NEXT_PUBLIC_API_BASE_URL.");
  }

  const cleanPath = path.startsWith("/") ? path : `/${path}`;
  if (!cleanPath.startsWith("/api/")) {
    const p = cleanPath.slice(1);
    return `${normalizedBase}/api/${p}`;
  }
  return `${normalizedBase}${cleanPath}`;
}

/**
 * Gets the effective API base URL: env (NEXT_PUBLIC_API_BASE_URL) or dev-only default.
 * Production builds must set NEXT_PUBLIC_API_BASE_URL in .env.production (or build-time env).
 */
export function getEffectiveApiBaseUrl(): string {
  const envUrl = process.env.NEXT_PUBLIC_API_BASE_URL;
  return normalizeBaseUrl(envUrl) || getDefaultApiBaseUrl();
}
/**
 * Reports API client for admin downloadable reports
 */

import { apiRequest } from "./api-client";
import { apiFetch } from "./api";

// Technician Work Report (JSON)
export interface TechnicianWorkReportUser {
  userId: string;
  name: string;
  email: string;
  role: string;
  isSupervisor: boolean;
  ticketsOwned: number;
  ticketsCollaborated: number;
  ticketsTotalInvolved: number;
  openCount: number;
  inProgressCount: number;
  resolvedCount: number;
  repliesCount: number;
  statusChangesCount: number;
  attachmentsCount: number;
  grantsCount: number;
  revokesCount: number;
  lastActivityAt: string | null;
  topTickets: { ticketId: string; title: string; lastActionAt: string | null; actionsCount: number }[];
}

export interface TechnicianWorkReport {
  from: string;
  to: string;
  users: TechnicianWorkReportUser[];
}

export interface TechnicianWorkReportDetail {
  userId: string;
  userName: string;
  from: string;
  to: string;
  byTicket: {
    ticketId: string;
    title: string;
    actions: {
      eventId: string;
      eventType: string;
      actorRole: string;
      oldStatus: string | null;
      newStatus: string | null;
      createdAt: string;
    }[];
  }[];
}

/** Same endpoint and params for table and export; backend uses same dataset for both. */
export async function getTechnicianWorkReport(
  token: string,
  from: string,
  to: string,
  userId?: string
): Promise<TechnicianWorkReport> {
  const params = technicianWorkReportParams(from, to, userId);
  const url = `/api/admin/reports/technician-work?${params.toString()}`;
  return apiRequest<TechnicianWorkReport>(url, { method: "GET", token });
}

export async function getTechnicianWorkReportDetail(
  token: string,
  userId: string,
  from: string,
  to: string
): Promise<TechnicianWorkReportDetail> {
  return apiRequest<TechnicianWorkReportDetail>(
    `/api/admin/reports/technician-work/${userId}/activities?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`,
    { method: "GET", token }
  );
}

/** Build query params for technician-work report (same for table JSON and Excel export). */
function technicianWorkReportParams(from: string, to: string, userId?: string, format?: "xlsx"): URLSearchParams {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (userId && userId !== "all") params.set("userId", userId);
  if (format === "xlsx") params.set("format", "xlsx");
  return params;
}

/**
 * Download Technician Performance report as Excel (.xlsx). Uses same endpoint and from/to/userId as table (getTechnicianWorkReport).
 * When token is null (cookie-based auth), backend uses cookie via credentials: "include".
 */
export async function downloadTechnicianWorkReportExcel(
  token: string | null,
  from: string,
  to: string,
  userId?: string
): Promise<void> {
  const params = technicianWorkReportParams(from, to, userId, "xlsx");
  const path = `/api/admin/reports/technician-work?${params.toString()}`;
  const headers: Record<string, string> = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  const response = await apiFetch(path, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    const serverMessage = await parseServerErrorMessage(response);
    throw new Error(serverMessage || `دانلود گزارش Excel ناموفق بود (${response.status})`);
  }

  const contentDisposition = response.headers.get("Content-Disposition");
  const fileName = extractFileName(contentDisposition) || `technician-work-report_${from}_${to}.xlsx`;
  const blob = await response.blob();
  downloadBlob(blob, fileName);
}

export type ReportRange = "1w" | "1m" | "3m" | "6m" | "1y" | "custom";

export interface ReportParams {
  range?: ReportRange;
  from?: string; // YYYY-MM-DD (Gregorian)
  to?: string; // YYYY-MM-DD (Gregorian)
}

/** Report type for shared download: base = گزارش پایه, analytic = گزارش تحلیلی */
export type ReportDownloadType = "base" | "analytic";

/** base: csv | xlsx. analytic: zip | xlsx */
export type ReportDownloadFormat = "csv" | "xlsx" | "zip";

export interface DownloadReportOptions {
  type: ReportDownloadType;
  /** JWT token; when null (cookie-based auth), request uses credentials only and backend uses cookie. */
  token: string | null;
  params: ReportParams;
  format?: ReportDownloadFormat;
}

/** Thrown on 4xx/5xx so UI can show status-specific message */
export class ReportDownloadError extends Error {
  constructor(
    message: string,
    public readonly statusCode: number
  ) {
    super(message);
    this.name = "ReportDownloadError";
  }
}

/**
 * Single shared download for گزارش پایه and گزارش تحلیلی.
 * Handles query building (range/from/to + format), fetch, and blob download.
 * Use from/to as Gregorian ISO (YYYY-MM-DD); normalize Jalali in the UI before calling.
 */
export async function downloadReport(options: DownloadReportOptions): Promise<void> {
  const { type, token, params, format = "xlsx" } = options;
  const path = type === "base" ? "basic" : "analytic";
  const defaultFileName = type === "base" ? "basic_report" : "analytic_report";
  const validFormats = type === "base" ? ["csv", "xlsx"] : ["zip", "xlsx"];
  const fmt = validFormats.includes(format) ? format : "xlsx";

  const queryParams = buildQueryParams(params, fmt);
  const apiPath = `/api/admin/reports/${path}?${queryParams}`;
  const headers: Record<string, string> = {};
  if (token) headers.Authorization = `Bearer ${token}`;
  const response = await apiFetch(apiPath, {
    method: "GET",
    headers,
  });

  if (!response.ok) {
    const serverMessage = await parseServerErrorMessage(response);
    const message = serverMessage || `دانلود گزارش ناموفق بود (${response.status})`;
    throw new ReportDownloadError(message, response.status);
  }

  const contentDisposition = response.headers.get("Content-Disposition");
  const ext = fmt === "csv" ? "csv" : fmt === "zip" ? "zip" : "xlsx";
  const fileName = extractFileName(contentDisposition) || `${defaultFileName}_${Date.now()}.${ext}`;
  const blob = await response.blob();
  downloadBlob(blob, fileName);
}

/** @deprecated Use downloadReport({ type: "base", token, params, format }) */
export async function downloadBasicReport(token: string, params: ReportParams): Promise<void> {
  return downloadReport({ type: "base", token, params, format: "xlsx" });
}

/** @deprecated Use downloadReport({ type: "analytic", token, params, format }) */
export async function downloadAnalyticReport(token: string, params: ReportParams): Promise<void> {
  return downloadReport({ type: "analytic", token, params, format: "xlsx" });
}

function buildQueryParams(params: ReportParams, format: string): string {
  const searchParams = new URLSearchParams();
  searchParams.set("format", format);

  if (params.range === "custom" && params.from && params.to) {
    searchParams.set("from", params.from);
    searchParams.set("to", params.to);
  } else if (params.range && params.range !== "custom") {
    searchParams.set("range", params.range);
  } else {
    searchParams.set("range", "1m");
  }

  return searchParams.toString();
}

/** Parse JSON error body (message or detail) for user-facing error. */
async function parseServerErrorMessage(response: Response): Promise<string> {
  const text = await response.text();
  if (!text) return "";
  try {
    const j = JSON.parse(text) as { message?: string; detail?: string; title?: string };
    return (j.message ?? j.detail ?? j.title ?? text).trim() || "";
  } catch {
    return text.trim();
  }
}

function extractFileName(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;

  const match = contentDisposition.match(/filename[^;=\n]*=((['"]).*?\2|[^;\n]*)/);
  if (match && match[1]) {
    return match[1].replace(/['"]/g, "");
  }
  return null;
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = window.URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  window.URL.revokeObjectURL(url);
}
















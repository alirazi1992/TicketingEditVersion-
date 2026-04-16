/**
 * Shared date helpers for reporting and date range picker.
 * All conversion/validation in one place; re-exports from date-jalali where needed.
 */

import {
  normalizeSelectedDateToTimestamp,
  formatForApi as formatTimestampForApi,
  ensureGregorianIsoForApi,
} from "./date-jalali";

// Re-export for consumers that want a single import
export { normalizeSelectedDateToTimestamp, ensureGregorianIsoForApi };

/** Format a timestamp as Gregorian YYYY-MM-DD for API query params. */
export function formatForApi(timestamp: number): string {
  return formatTimestampForApi(timestamp);
}

/**
 * Convert a JS Date (from picker) to a normalized timestamp (noon UTC of that calendar day).
 * Uses local date parts so the selected calendar day is preserved.
 */
export function dateToTimestamp(d: Date): number {
  const y = d.getFullYear();
  const m = d.getMonth();
  const day = d.getDate();
  return Date.UTC(y, m, day, 12, 0, 0, 0);
}

/** Convert a timestamp back to a Date for picker value. */
export function timestampToDate(ts: number): Date {
  return new Date(ts);
}

/** Default report range: from 30 days ago to today (noon UTC for each day). */
export function getDefaultReportRange(): { from: number; to: number } {
  const to = new Date();
  const from = new Date();
  from.setDate(from.getDate() - 30);
  return {
    from: dateToTimestamp(from),
    to: dateToTimestamp(to),
  };
}

/**
 * True when range is valid: either both missing, or from <= to.
 * Use to decide if "start must be before end" error should show.
 */
export function isRangeValid(value: { from?: number; to?: number }): boolean {
  const { from, to } = value;
  if (from == null || to == null) return true;
  return from <= to;
}

/** Get inline error message when range is invalid, or null. */
export function getRangeErrorMessage(value: { from?: number; to?: number }): string | null {
  if (isRangeValid(value)) return null;
  return "تاریخ شروع باید قبل از تاریخ پایان باشد. بازه معتبر انتخاب کنید.";
}

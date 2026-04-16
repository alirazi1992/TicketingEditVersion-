/**
 * Jalali (Persian / شمسی) date utilities.
 * Converts between Jalali date strings and ISO (Gregorian) for API.
 */

import dayjs, { Dayjs } from "dayjs";
import jalaliday from "jalaliday/dayjs";

dayjs.extend(jalaliday);

/** Reasonable Jalali year range to avoid invalid calendar state */
export const JALALI_YEAR_MIN = 1200;
export const JALALI_YEAR_MAX = 1600;

/**
 * Returns a dayjs instance in Jalali calendar that is always valid.
 * Use for reading year/month so we never hit "Invalid Jalaali year".
 * - If value is null/empty => today in Jalali.
 * - If value is ISO (YYYY-MM-DD) or Jalali (YYYY/MM/DD) => parse and validate.
 * - Year must be in [JALALI_YEAR_MIN, JALALI_YEAR_MAX], month 0..11.
 * - If invalid => today in Jalali.
 */
export function safeJalaliBase(value?: string | null): Dayjs {
  const today = dayjs().calendar("jalali");
  if (value == null || typeof value !== "string") return today;
  const trimmed = normalizePersianDigits(value.trim());
  if (!trimmed) return today;

  let j: Dayjs;
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) {
    j = dayjs(trimmed).calendar("jalali");
  } else {
    const normalized = trimmed.replace(/-/g, "/");
    j = dayjs(normalized, { jalali: true });
  }

  if (!j.isValid()) return today;
  const year = j.year();
  const month = j.month();
  if (
    typeof year !== "number" ||
    typeof month !== "number" ||
    year < JALALI_YEAR_MIN ||
    year > JALALI_YEAR_MAX ||
    month < 0 ||
    month > 11
  ) {
    return today;
  }
  return j;
}

/**
 * Clamp Jalali year to valid range (for prev/next month navigation).
 */
export function clampJalaliYear(year: number): number {
  return Math.max(JALALI_YEAR_MIN, Math.min(JALALI_YEAR_MAX, year));
}

/**
 * Returns a valid Date for the picker, or undefined if value is empty/invalid.
 * Accepts both Gregorian ISO (YYYY-MM-DD) and Jalali (YYYY/MM/DD or YYYY-MM-DD).
 * Use with fallback: safeDateForPicker(iso) ?? new Date() so the picker never receives invalid state.
 */
export function safeDateForPicker(iso?: string | null): Date | undefined {
  if (iso == null || typeof iso !== "string") return undefined;
  const trimmed = normalizePersianDigits(iso.trim());
  if (!trimmed) return undefined;
  // If input looks like Jalali (year 1200–1500), convert to Gregorian ISO first to avoid wrong year
  const isoForDate = looksLikeJalali(trimmed) ? jalaliToIsoDate(trimmed) : trimmed;
  if (!isoForDate) return undefined;
  const d = new Date(isoForDate + "T12:00:00.000Z");
  if (Number.isNaN(d.getTime())) return undefined;
  const j = dayjs(d).calendar("jalali");
  if (!j.isValid()) return undefined;
  const y = j.year();
  if (y < JALALI_YEAR_MIN || y > JALALI_YEAR_MAX) return undefined;
  return d;
}

/**
 * Convert a Jalali date string to ISO (Gregorian) date YYYY-MM-DD.
 * Accepts "YYYY/MM/DD" or "YYYY-MM-DD"; normalizes to YYYY/MM/DD internally before converting.
 * Backend expects Gregorian yyyy-MM-dd only; UI can stay Jalali.
 * @param j - Jalali date string e.g. "1404/11/16" or "1404-11-16"
 * @returns ISO date string "YYYY-MM-DD" (Gregorian) or empty string if invalid
 * @example jalaliToIsoDate("1404/11/16") => "2026-02-05"
 */
export function jalaliToIsoDate(j: string): string {
  if (!j || typeof j !== "string") return "";
  const trimmed = normalizePersianDigits(j.trim());
  if (!trimmed) return "";
  const normalized = trimmed.replace(/-/g, "/");
  const d = dayjs(normalized, { jalali: true });
  if (!d.isValid()) return "";
  return d.calendar("gregory").format("YYYY-MM-DD");
}

const JALALI_YEAR_FLOOR = 1200;
const JALALI_YEAR_CEIL = 1500;
const ISO_YEAR_MIN = 1900;
const ISO_YEAR_MAX = 2100;

const PERSIAN_DIGITS = "۰۱۲۳۴۵۶۷۸۹";

/**
 * Replace Persian/Arabic-Indic digits (۰-۹) with ASCII 0-9 so parsing and regex work.
 */
export function normalizePersianDigits(s: string): string {
  if (!s || typeof s !== "string") return s;
  return s.replace(/[۰-۹]/g, (c) => String(PERSIAN_DIGITS.indexOf(c)));
}

/**
 * Returns true if the string looks like a Jalali date (year in 1200–1500).
 */
function looksLikeJalali(dateStr: string): boolean {
  const normalized = dateStr.trim().replace(/-/g, "/");
  const m = normalized.match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})$/);
  if (!m) return false;
  const y = parseInt(m[1], 10);
  return y >= JALALI_YEAR_FLOOR && y <= JALALI_YEAR_CEIL;
}

/**
 * Returns true if the string looks like ISO Gregorian (year in 1900–2100, format YYYY-MM-DD or YYYY/MM/DD).
 */
function looksLikeIso(dateStr: string): boolean {
  const normalized = dateStr.trim().replace(/-/g, "/");
  const m = normalized.match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})$/);
  if (!m) return false;
  const y = parseInt(m[1], 10);
  return y >= ISO_YEAR_MIN && y <= ISO_YEAR_MAX;
}

/**
 * Ensure a date string is Gregorian ISO (yyyy-MM-dd) for API query params.
 * - Jalali (e.g. 1404/11/16 or 1404-11-16) => converted via jalaliToIsoDate.
 * - Already ISO => returned as-is (only if format is valid).
 * - Otherwise => empty string (caller should use default).
 */
export function ensureGregorianIsoForApi(dateStr: string): string {
  if (!dateStr || typeof dateStr !== "string") return "";
  const trimmed = normalizePersianDigits(dateStr.trim());
  if (!trimmed) return "";
  if (looksLikeJalali(trimmed)) return jalaliToIsoDate(trimmed);
  if (looksLikeIso(trimmed)) {
    const normalized = trimmed.replace(/-/g, "/");
    const d = dayjs(normalized);
    if (d.isValid()) return d.format("YYYY-MM-DD");
  }
  return "";
}

/**
 * Parse any date string (Jalali or Gregorian, with optional Persian digits) to a numeric timestamp.
 * Use for comparisons; only compare timestamps, not strings.
 * @returns timestamp (ms) or null if empty/invalid
 */
export function normalizeSelectedDateToTimestamp(
  value: string | null | undefined
): number | null {
  if (value == null || typeof value !== "string") return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  const iso = ensureGregorianIsoForApi(trimmed);
  if (!iso) return null;
  const t = new Date(iso + "T12:00:00.000Z").getTime();
  return Number.isNaN(t) ? null : t;
}

/**
 * Format a timestamp as Gregorian YYYY-MM-DD for API query params.
 */
export function formatForApi(timestamp: number): string {
  return dayjs(timestamp).format("YYYY-MM-DD");
}

/**
 * Ensure a date string is in ISO (Gregorian) YYYY-MM-DD for API calls.
 * If the value looks like Jalali (year in 1200–1500), converts via jalaliToIsoDate.
 * Otherwise returns the value as-is (assume already ISO).
 */
export function toIsoForApi(dateStr: string): string {
  if (!dateStr || typeof dateStr !== "string") return dateStr;
  const trimmed = normalizePersianDigits(dateStr.trim());
  if (!trimmed) return trimmed;
  const normalized = trimmed.replace(/-/g, "/");
  const match = normalized.match(/^(\d{4})\/(\d{1,2})\/(\d{1,2})$/);
  if (!match) return trimmed;
  const y = parseInt(match[1], 10);
  if (y >= 1200 && y <= 1500) return jalaliToIsoDate(trimmed);
  return trimmed;
}

/**
 * Convert an ISO date string to Jalali for display.
 * @param iso - ISO date "YYYY-MM-DD"
 * @returns Jalali date string e.g. "1404/11/16" or empty if invalid
 */
export function isoToJalali(iso: string): string {
  if (!iso || typeof iso !== "string") return "";
  const d = dayjs(iso);
  if (!d.isValid()) return "";
  return d.calendar("jalali").format("YYYY/MM/DD");
}

/** Asia/Tehran offset in hours (Iran Standard Time, no DST). */
const TEHRAN_UTC_OFFSET_HOURS = 3.5;

/**
 * Given a UTC ISO datetime string (e.g. ticket UpdatedAt), return the Jalali date key (YYYY-MM-DD)
 * as that moment falls in Asia/Tehran. Single source of truth for calendar day bucketing.
 * @param utcIso - ISO string with Z or offset (e.g. "2026-02-05T10:00:00Z")
 * @returns Jalali key "YYYY-MM-DD" (e.g. "1404-11-16") or empty if invalid
 */
export function getJalaliDayKeyFromUtcIso(utcIso: string): string {
  if (!utcIso || typeof utcIso !== "string") return "";
  const d = new Date(utcIso.trim().endsWith("Z") ? utcIso : utcIso + "Z");
  if (Number.isNaN(d.getTime())) return "";
  const tehranDateStr = d.toLocaleDateString("en-CA", { timeZone: "Asia/Tehran" });
  if (!tehranDateStr) return "";
  const jalali = isoToJalali(tehranDateStr);
  return jalali ? jalali.replace(/\//g, "-") : "";
}

/**
 * Get UTC range (start inclusive, end exclusive) for a Jalali calendar day in Asia/Tehran.
 * Used so backend and frontend share the same day boundaries.
 * @param jalaliDay - "YYYY/MM/DD" or "YYYY-MM-DD" (Jalali)
 * @returns { startUtcIso, endUtcIso } in ISO format, end exclusive
 */
export function getTehranUtcRangeFromJalaliDay(jalaliDay: string): {
  startUtcIso: string;
  endUtcIso: string;
} {
  const isoDate = jalaliToIsoDate(jalaliDay.trim().replace(/-/g, "/"));
  if (!isoDate) {
    const fallback = new Date().toISOString();
    return { startUtcIso: fallback, endUtcIso: fallback };
  }
  const [y, m, d] = isoDate.split("-").map(Number);
  if (!y || !m || !d) {
    const fallback = new Date().toISOString();
    return { startUtcIso: fallback, endUtcIso: fallback };
  }
  const msPerHour = 3600 * 1000;
  const startUtc = new Date(
    Date.UTC(y, m - 1, d, 0, 0, 0) - TEHRAN_UTC_OFFSET_HOURS * msPerHour
  );
  const endUtc = new Date(
    Date.UTC(y, m - 1, d + 1, 0, 0, 0) - TEHRAN_UTC_OFFSET_HOURS * msPerHour
  );
  return {
    startUtcIso: startUtc.toISOString(),
    endUtcIso: endUtc.toISOString(),
  };
}

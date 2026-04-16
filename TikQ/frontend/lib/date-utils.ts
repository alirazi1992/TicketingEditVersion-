/**
 * Report/API date helpers: ensure custom range (Jalali or Date) is always sent as Gregorian ISO (YYYY-MM-DD).
 * Use for "بازه دلخواه" so backend receives 2000-2050 years.
 */

import dayjs from "dayjs";
import { jalaliToIsoDate, ensureGregorianIsoForApi } from "./date-jalali";

const JALALI_YEAR_MIN = 1200;
const JALALI_YEAR_MAX = 1500;

/**
 * Convert any report date input to Gregorian ISO (YYYY-MM-DD) for API query params.
 * - If input is a JS Date: format as YYYY-MM-DD (Gregorian). If the Date's year is in 1200–1500 (Jalali range), treat (year, month+1, day) as Jalali and convert to Gregorian.
 * - If input is a Jalali string (e.g. 1404/10/01 or ۱۴۰۴/۱۰/۰۱): convert to Gregorian and return YYYY-MM-DD.
 * - If input is a timestamp (number): format as YYYY-MM-DD; if the formatted year is in 1200–1500, treat as Jalali and convert.
 * @returns Gregorian ISO date string YYYY-MM-DD, or empty string if invalid
 */
export function toGregorianIsoFromJalaliInput(
  input: string | Date | number | null | undefined
): string {
  if (input == null) return "";

  // Timestamp (from DateRangePicker when user selects Jalali dates; some paths may produce wrong year)
  if (typeof input === "number") {
    const formatted = dayjs(input).format("YYYY-MM-DD");
    const y = parseInt(formatted.slice(0, 4), 10);
    if (y >= JALALI_YEAR_MIN && y <= JALALI_YEAR_MAX) {
      return jalaliToIsoDate(formatted.replace(/-/g, "/"));
    }
    return formatted;
  }

  // JS Date (e.g. from picker.toDate() if it ever returned Jalali year)
  if (input instanceof Date) {
    const y = input.getFullYear();
    const m = input.getMonth();
    const d = input.getDate();
    if (y >= JALALI_YEAR_MIN && y <= JALALI_YEAR_MAX) {
      return jalaliToIsoDate(`${y}/${m + 1}/${d}`);
    }
    return dayjs(input).format("YYYY-MM-DD");
  }

  // String (Jalali like 1404/10/01 or Persian digits ۱۴۰۴/۱۰/۰۱; or already ISO)
  if (typeof input === "string") {
    return ensureGregorianIsoForApi(input);
  }

  return "";
}

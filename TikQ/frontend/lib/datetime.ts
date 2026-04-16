"use client"

export const IRAN_TZ = "Asia/Tehran" as const
export const LOCALE_FA = "fa-IR" as const
export const LOCALE_FA_PERSIAN = "fa-IR-u-ca-persian" as const

export type CanonicalDate = Date | null

export function parseServerDate(input: unknown): CanonicalDate {
  if (input === null || input === undefined || input === "") return null
  if (input instanceof Date) return input
  if (typeof input === "number") return new Date(input)
  if (typeof input === "string") {
    const trimmed = input.trim()
    if (!trimmed) return null
    const hasTimezone = trimmed.includes("Z") || /[+-]\d{2}:\d{2}$/.test(trimmed)
    const parsed = new Date(hasTimezone ? trimmed : `${trimmed}Z`)
    if (Number.isNaN(parsed.getTime())) {
      if (process.env.NODE_ENV === "development") {
        console.warn("[datetime] invalid server date", { input })
      }
      return null
    }
    return parsed
  }
  if (process.env.NODE_ENV === "development") {
    console.warn("[datetime] invalid server date", { input })
  }
  return null
}

function isValidDate(d: Date): boolean {
  return Number.isFinite(d.getTime())
}

export function formatFaTime(d: Date): string {
  if (!isValidDate(d)) return "—"
  return new Intl.DateTimeFormat(LOCALE_FA, {
    timeZone: IRAN_TZ,
    hour: "2-digit",
    minute: "2-digit",
  }).format(d)
}

export function formatFaDate(d: Date): string {
  if (!isValidDate(d)) return "—"
  return new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
    timeZone: IRAN_TZ,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(d)
}

export function formatFaDateTime(d: Date): string {
  if (!isValidDate(d)) return "—"
  return new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
    timeZone: IRAN_TZ,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  }).format(d)
}

export function toFaTime(input: unknown): string {
  const parsed = parseServerDate(input)
  if (!parsed) return "—"
  return formatFaTime(parsed)
}

export function toFaDate(input: unknown): string {
  const parsed = parseServerDate(input)
  if (!parsed) return "—"
  return formatFaDate(parsed)
}

export function toFaDateTime(input: unknown): string {
  const parsed = parseServerDate(input)
  if (!parsed) return "—"
  return formatFaDateTime(parsed)
}

"use client";

import { useEffect, useMemo, useState, useCallback, type ReactNode } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import type { ApiTicketCalendarResponse } from "@/lib/api-types";
import type { Ticket } from "@/types";
import { useAdminTickets, type AdminTicketsFilters } from "@/hooks/use-admin-tickets";
import { useAuth } from "@/lib/auth-context";
import { getAdminTicketsByDayJalali } from "@/lib/admin-tickets-api";
import { getJalaliDayKeyFromUtcIso, jalaliToIsoDate } from "@/lib/date-jalali";
import {
  TICKET_STATUS_LABELS,
  getTicketStatusLabel,
  getEffectiveStatus,
  type TicketStatus,
} from "@/lib/ticket-status";
import { IRAN_TZ, LOCALE_FA_PERSIAN, parseServerDate } from "@/lib/datetime";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Separator } from "@/components/ui/separator";
import { cn } from "@/lib/utils";
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  Users,
  Flag,
  Hash,
  type LucideIcon,
} from "lucide-react";

/* ==== Jalali calendar setup ==== */
import dayjs from "dayjs";
import jalaliday from "jalaliday";
dayjs.extend(jalaliday);
dayjs.calendar("jalali");
dayjs.locale("fa");

type CalendarTicket = ApiTicketCalendarResponse | Ticket;

interface TicketCalendarOverviewProps {
  tickets?: CalendarTicket[]; // Optional - if not provided, will use shared hook
  statusFilter?: string; // Status filter to sync with table
  onStatusFilterChange?: (status: string) => void; // Callback when filter changes
}

type CalendarDay = {
  date: dayjs.Dayjs; // Jalali-aware
  isCurrentMonth: boolean;
  tickets: CalendarTicket[];
};

type StatusBucket = "answered" | "working" | "notResponded";

const weekDays = [
  "شنبه",
  "یکشنبه",
  "دوشنبه",
  "سه‌شنبه",
  "چهارشنبه",
  "پنجشنبه",
  "جمعه",
];

// Persian (Jalali) formatters
const monthFormatter = new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
  timeZone: IRAN_TZ,
  month: "long",
  year: "numeric",
});
const fullDateFormatter = new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
  timeZone: IRAN_TZ,
  weekday: "long",
  year: "numeric",
  month: "long",
  day: "numeric",
});
const dateTimeFormatter = new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
  timeZone: IRAN_TZ,
  year: "numeric",
  month: "long",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});
const dateFormatter = new Intl.DateTimeFormat(LOCALE_FA_PERSIAN, {
  timeZone: IRAN_TZ,
  year: "numeric",
  month: "long",
  day: "numeric",
});

const statusMeta: Record<
  StatusBucket,
  { label: string; description: string; counterClass: string }
> = {
  answered: {
    label: "تیکت‌های پاسخ‌داده‌شده",
    description: "تیکت‌هایی که با موفقیت پاسخ یا بسته شده‌اند",
    counterClass: "bg-emerald-500/15 text-emerald-600",
  },
  working: {
    label: "تیکت‌های در حال پیگیری",
    description: "تیکت‌هایی که تکنسین در حال کار روی آن‌هاست",
    counterClass: "bg-amber-500/15 text-amber-600",
  },
  notResponded: {
    label: "تیکت‌های بی‌پاسخ",
    description: "تیکت‌هایی که هنوز پاسخی دریافت نکرده‌اند",
    counterClass: "bg-rose-500/15 text-rose-600",
  },
};

const statusLabels = TICKET_STATUS_LABELS;

const statusColors: Record<string, string> = {
  Submitted: "bg-blue-100 text-blue-700 border border-blue-200",
  SeenRead: "bg-cyan-100 text-cyan-700 border border-cyan-200",
  Open: "bg-rose-100 text-rose-700 border border-rose-200",
  InProgress: "bg-amber-100 text-amber-700 border border-amber-200",
  Solved: "bg-emerald-100 text-emerald-700 border border-emerald-200",
  // Legacy statuses removed (Resolved/Closed); use Solved instead
  Redo: "bg-orange-100 text-orange-700 border border-orange-200",
  // Legacy Answered removed; use Solved instead
};

const statusCountText: Record<StatusBucket, string> = {
  answered: "تیکت‌های پاسخ داده شده",
  working: "تیکت‌های در حال پیگیری",
  notResponded: "تیکت‌های بی‌پاسخ",
};

const formatDateValue = (
  value?: string | Date | null,
  formatter = dateTimeFormatter
) => {
  if (!value) return "--";
  const date = value instanceof Date ? value : parseServerDate(value);
  if (!date) return "--";
  return formatter.format(date); // Persian calendar formatting
};

const getComparableTime = (value?: string | Date | null) => {
  if (!value) return 0;
  const parsed = value instanceof Date ? value : parseServerDate(value);
  return parsed ? parsed.getTime() : 0;
};

// Persian digits (stabilizes RTL wrapping)
const toFaDigits = (n: number | string) =>
  String(n).replace(/\d/g, (d) => "۰۱۲۳۴۵۶۷۸۹"[Number(d)]);

// Build BiDi-safe RTL badge text (adds RLM around the colon)
const buildRtlBadgeText = (label: string, count: number) =>
  `${label} \u200F:\u200F ${toFaDigits(count)}`;

// --- Use Jalali year/month/day to generate keys like 1404-07-23 (ASCII digits) ---
const formatKeyJalali = (d: dayjs.Dayjs) =>
  `${d.calendar("jalali").year()}-${String(
    d.calendar("jalali").month() + 1
  ).padStart(2, "0")}-${String(d.calendar("jalali").date()).padStart(2, "0")}`;

/** Info row: labels on LEFT, values on RIGHT (as per screenshot) */
const InfoRow = ({
  icon: Icon,
  label,
  value,
}: {
  icon: LucideIcon;
  label: string;
  value: ReactNode;
}) => (
  <div
    className="grid grid-cols-2 gap-6 items-center w-full"
    dir="rtl"
  >
    {/* Labels block — on LEFT */}
    <div className="flex items-center gap-2 text-right text-sm font-iran text-muted-foreground">
      <Icon className="h-4 w-4 text-primary" />
      {label}
    </div>
    {/* Values block — on RIGHT */}
    <div className="text-left text-sm font-iran text-foreground">
      {value}
    </div>
  </div>
);

/** Get Jalali day key (YYYY-MM-DD) in Asia/Tehran for a ticket. Single source of truth for calendar bucketing. Uses UpdatedAt. */
const getTicketJalaliDayKey = (ticket: any): string => {
  const source = ticket?.updatedAt ?? ticket?.createdAt;
  if (!source) return "";
  const iso = typeof source === "string" ? (source.trim().endsWith("Z") ? source : source + "Z") : parseServerDate(source)?.toISOString?.();
  if (!iso) return "";
  return getJalaliDayKeyFromUtcIso(iso);
};

const getTicketDate = (ticket: any): dayjs.Dayjs | null => {
  const key = getTicketJalaliDayKey(ticket);
  if (!key) return null;
  const [jy, jm, jd] = key.split("-").map((n) => parseInt(n, 10));
  if (!jy || !jm || !jd) return null;
  const d = dayjs().calendar("jalali").year(jy).month(jm - 1).date(jd);
  return d.isValid() ? d : null;
};

const getStatusBucket = (status: string): StatusBucket => {
  if (status === "InProgress") return "working";
  if (status === "Open") return "notResponded";
  return "answered";
};

export function TicketCalendarOverview({
  tickets: ticketsProp,
  statusFilter = "all",
  onStatusFilterChange,
}: TicketCalendarOverviewProps) {
  const router = useRouter();
  // Current Jalali month, first day
  const [currentMonth, setCurrentMonth] = useState(() =>
    dayjs().calendar("jalali").startOf("month")
  );
  const [selectedDateKey, setSelectedDateKey] = useState<string | null>(null);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [ticketsForSelectedDay, setTicketsForSelectedDay] = useState<Array<{ ticketId: string; title: string; status: string; updatedAt?: string | null; assignedToName?: string | null; code?: string | null }>>([]);
  const [loadingByDate, setLoadingByDate] = useState(false);
  const [expandedDays, setExpandedDays] = useState<Set<string>>(new Set());
  const [localStatusFilter, setLocalStatusFilter] = useState(statusFilter);
  const { token, user } = useAuth();

  const monthRange = useMemo(() => {
    const start = currentMonth.startOf("month").toDate();
    const end = currentMonth.endOf("month").toDate();
    return {
      start: dayjs(start).format("YYYY-MM-DD"),
      end: dayjs(end).format("YYYY-MM-DD"),
    };
  }, [currentMonth]);

  // Use shared hook for tickets if not provided via props
  const filters: AdminTicketsFilters = {
    status: localStatusFilter !== "all" ? (localStatusFilter as TicketStatus) : undefined,
    start: monthRange.start,
    end: monthRange.end,
  };
  const {
    tickets: ticketsFromHook,
    isLoading,
    error,
    refreshTickets,
  } = useAdminTickets(filters);

  // Use tickets from props if provided, otherwise use hook
  const activeTickets = ticketsProp || ticketsFromHook;
  const filteredTickets = useMemo(() => {
    if (localStatusFilter === "all") return activeTickets;
    return activeTickets.filter((ticket) => (ticket.displayStatus ?? ticket.status) === localStatusFilter);
  }, [activeTickets, localStatusFilter]);

  // Sync local filter with prop
  useEffect(() => {
    if (statusFilter !== localStatusFilter) {
      setLocalStatusFilter(statusFilter);
    }
  }, [statusFilter]);

  // Handle status filter change
  const handleStatusFilterChange = (newStatus: string) => {
    setLocalStatusFilter(newStatus);
    if (onStatusFilterChange) {
      onStatusFilterChange(newStatus);
    }
  };

  const statusFilterOptions = useMemo(
    () => [
      { value: "all", label: "همه" },
      ...Object.entries(TICKET_STATUS_LABELS).map(([value, label]) => ({
        value,
        label,
      })),
    ],
    []
  );

  const statusCounts = useMemo(() => {
    const counts: Record<string, number> = { all: activeTickets.length };
    Object.keys(TICKET_STATUS_LABELS).forEach((key) => {
      counts[key] = 0;
    });
    activeTickets.forEach((ticket) => {
      const displayStatus = ticket.displayStatus ?? ticket.status;
      counts[displayStatus] = (counts[displayStatus] || 0) + 1;
    });
    return counts;
  }, [activeTickets]);

  // Toggle day expansion
  const toggleDayExpansion = (dayKey: string) => {
    setExpandedDays((prev) => {
      const next = new Set(prev);
      if (next.has(dayKey)) {
        next.delete(dayKey);
      } else {
        next.add(dayKey);
      }
      return next;
    });
  };

  // Open day dialog and fetch tickets for that day (same Jalali day → backend Tehran range = same as badge bucketing)
  const handleDayClick = useCallback(
    (dayKey: string) => {
      setSelectedDateKey(dayKey);
      setDialogOpen(true);
      setTicketsForSelectedDay([]);
      if (!user) {
        setLoadingByDate(false);
        return;
      }
      setLoadingByDate(true);
      getAdminTicketsByDayJalali(token, dayKey)
        .then(setTicketsForSelectedDay)
        .catch(() => setTicketsForSelectedDay([]))
        .finally(() => setLoadingByDate(false));
    },
    [token]
  );

  // Group by Jalali date key (Asia/Tehran) — same definition as backend by-date
  const groupedTickets = useMemo(() => {
    const map = new Map<string, any[]>();
    filteredTickets.forEach((ticket) => {
      const key = getTicketJalaliDayKey(ticket);
      if (!key) return;
      if (!map.has(key)) map.set(key, []);
      map.get(key)!.push(ticket);
    });
    return map;
  }, [filteredTickets]);

  // Build Jalali month grid
  const calendarDays = useMemo<CalendarDay[]>(() => {
    const days: CalendarDay[] = [];
    const start = currentMonth; // first day of current Jalali month
    const startOffset = (start.day() + 1) % 7; // make Saturday index 0
    const daysInMonth = start.daysInMonth();

    // previous month spill
    for (let i = startOffset; i > 0; i--) {
      const date = start.subtract(i, "day");
      days.push({
        date,
        isCurrentMonth: false,
        tickets: groupedTickets.get(formatKeyJalali(date)) ?? [],
      });
    }

    // current month days
    for (let d = 0; d < daysInMonth; d++) {
      const date = start.add(d, "day");
      days.push({
        date,
        isCurrentMonth: true,
        tickets: groupedTickets.get(formatKeyJalali(date)) ?? [],
      });
    }

    // next month spill to fill last week
    const remaining = days.length % 7 === 0 ? 0 : 7 - (days.length % 7);
    for (let i = 1; i <= remaining; i++) {
      const date = start.add(daysInMonth - 1 + i, "day");
      days.push({
        date,
        isCurrentMonth: false,
        tickets: groupedTickets.get(formatKeyJalali(date)) ?? [],
      });
    }

    return days;
  }, [currentMonth, groupedTickets]);

  // Monthly summary (within current Jalali month)
  const monthSummary = useMemo(() => {
    const summary: Record<StatusBucket, number> = {
      answered: 0,
      working: 0,
      notResponded: 0,
    };
    activeTickets.forEach((ticket) => {
      const d = getTicketDate(ticket);
      if (!d) return;
      const sameJYear =
        d.calendar("jalali").year() === currentMonth.calendar("jalali").year();
      const sameJMonth =
        d.calendar("jalali").month() ===
        currentMonth.calendar("jalali").month();
      if (!sameJYear || !sameJMonth) return;
      const bucket = getStatusBucket(ticket.displayStatus ?? ticket.status);
      summary[bucket] += 1;
    });
    return summary;
  }, [activeTickets, currentMonth]);

  // Selected day (by Jalali key)
  const selectedDay = useMemo(() => {
    if (!selectedDateKey) return null;
    const [jy, jm, jd] = selectedDateKey.split("-").map((n) => parseInt(n, 10));
    if (!jy || !jm || !jd) return null;
    const date = dayjs()
      .calendar("jalali")
      .year(jy)
      .month(jm - 1)
      .date(jd);
    const ticketsForDay = groupedTickets.get(selectedDateKey) ?? [];
    const sortedTickets = [...ticketsForDay].sort(
      (a, b) =>
        getComparableTime(b.updatedAt || b.createdAt) -
        getComparableTime(a.updatedAt || a.createdAt)
    );
    return { date, tickets: sortedTickets };
  }, [groupedTickets, selectedDateKey]);

  useEffect(() => {
    setSelectedDateKey(null);
    setDialogOpen(false);
  }, [currentMonth]);


  const goToPreviousMonth = () =>
    setCurrentMonth((prev) => prev.subtract(1, "month").startOf("month"));
  const goToNextMonth = () =>
    setCurrentMonth((prev) => prev.add(1, "month").startOf("month"));

  const todayKey = getJalaliDayKeyFromUtcIso(new Date().toISOString());

  return (
    <>
      <Card className="border border-primary/20 bg-gradient-to-br from-background via-background to-primary/5">
        <CardHeader className="space-y-4">
          <div className="flex flex-wrap items-center justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 text-primary">
                <CalendarDays className="h-5 w-5" />
              </div>
              <div className="text-right">
                <CardTitle className="text-lg font-iran">
                  تقویم برنامه‌ریزی تیکت‌ها
                </CardTitle>
                <CardDescription className="font-iran">
                  بررسی توزیع روزانه‌ی تیکت‌ها و وضعیت رسیدگی تکنسین‌ها
                </CardDescription>
              </div>
            </div>
            <div className="flex flex-wrap items-center justify-end gap-2">
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="icon"
                  onClick={goToNextMonth}
                  className="rounded-full"
                  aria-label="ماه بعد"
                >
                  <ChevronRight className="h-4 w-4" />
                </Button>
                <div className="rounded-full bg-primary/10 px-4 py-1 text-sm font-iran text-primary">
                  {monthFormatter.format(currentMonth.toDate())}
                </div>
                <Button
                  variant="outline"
                  size="icon"
                  onClick={goToPreviousMonth}
                  className="rounded-full"
                  aria-label="ماه قبل"
                >
                  <ChevronLeft className="h-4 w-4" />
                </Button>
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2 overflow-x-auto pb-1" dir="rtl">
            {statusFilterOptions.map((option) => (
              <Button
                key={option.value}
                type="button"
                size="sm"
                variant={localStatusFilter === option.value ? "default" : "outline"}
                onClick={() => handleStatusFilterChange(option.value)}
                className="shrink-0 font-iran"
              >
                {option.label}
              </Button>
            ))}
          </div>

          <div className="flex items-stretch gap-2 overflow-x-auto pb-2" dir="rtl">
            {statusFilterOptions.map((option) => (
              <div
                key={`${option.value}-count`}
                className="min-w-[120px] rounded-xl border bg-background/60 px-4 py-3 text-right"
              >
                <div className="text-xs font-iran text-muted-foreground">{option.label}</div>
                <div className="text-lg font-iran font-bold">
                  {statusCounts[option.value] ?? 0}
                </div>
              </div>
            ))}
          </div>
        </CardHeader>

        <CardContent className="space-y-4">
          {error ? (
            <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
              <span>خطا در دریافت تیکت‌ها</span>
              <Button size="sm" variant="outline" onClick={refreshTickets}>
                تلاش دوباره
              </Button>
            </div>
          ) : null}
          {isLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : null}
          <div className="grid grid-cols-7 gap-1 sm:gap-2 text-[10px] sm:text-xs text-muted-foreground">
            {weekDays.map((day) => (
              <div
                key={day}
                className="rounded-lg bg-muted/40 py-1.5 sm:py-2 text-center font-iran min-w-0 truncate"
              >
                {day}
              </div>
            ))}
          </div>

          <div className="grid grid-cols-7 gap-1 sm:gap-2 min-w-0">
            {calendarDays.map((day) => {
              const key = formatKeyJalali(day.date);
              const statusCounts: Record<StatusBucket, number> = {
                answered: 0,
                working: 0,
                notResponded: 0,
              };

              day.tickets.forEach((ticket) => {
                const bucket = getStatusBucket(ticket.displayStatus ?? ticket.status);
                statusCounts[bucket] += 1;
              });

              const isToday = key === todayKey;
              const isExpanded = expandedDays.has(key);
              const maxVisibleTickets = 3;
              const visibleTickets = isExpanded ? day.tickets : day.tickets.slice(0, maxVisibleTickets);
              const remainingCount = day.tickets.length - maxVisibleTickets;

              // Get status color mapping
              const getStatusColor = (status: string) => {
                const statusMap: Record<string, string> = {
                  Submitted: "bg-blue-100 text-blue-800 border-blue-200",
                  SeenRead: "bg-purple-100 text-purple-800 border-purple-200",
                  Open: "bg-red-100 text-red-800 border-red-200",
                  InProgress: "bg-yellow-100 text-yellow-800 border-yellow-200",
                  Solved: "bg-green-100 text-green-800 border-green-200",
                  Redo: "bg-orange-100 text-orange-800 border-orange-200",
                };
                return statusMap[status] || "bg-gray-100 text-gray-800 border-gray-200";
              };

              return (
                <div
                  key={`${key}-${day.isCurrentMonth ? "current" : "other"}`}
                  role="button"
                  tabIndex={0}
                  onClick={() => handleDayClick(key)}
                  onKeyDown={(e) => e.key === "Enter" && handleDayClick(key)}
                  className={cn(
                    "relative flex min-h-[100px] sm:min-h-[120px] flex-col rounded-xl sm:rounded-2xl border p-2 sm:p-3 text-right transition-all cursor-pointer min-w-0 overflow-hidden",
                    "hover:-translate-y-0.5 hover:border-primary/40 hover:shadow-sm",
                    day.isCurrentMonth
                      ? "bg-background"
                      : "bg-muted/50 text-muted-foreground",
                    isToday && "border-primary/60 shadow-inner"
                  )}
                >
                  <div className="flex items-center justify-between text-xs font-medium font-iran mb-2">
                    <span>{day.date.calendar("jalali").date()}</span>
                    {isToday && (
                      <span className="rounded-full bg-primary/10 px-2 py-0.5 text-[10px] text-primary">
                        امروز
                      </span>
                    )}
                  </div>

                  {/* Tickets list */}
                  {day.tickets.length > 0 ? (
                    <div className="flex-1 space-y-1 overflow-y-auto overflow-x-hidden max-h-[72px] sm:max-h-[80px] min-h-0">
                      {visibleTickets.map((ticket, idx) => {
                        const ticketId = (ticket as { id?: string; ticketId?: string }).id ?? (ticket as { id?: string; ticketId?: string }).ticketId;
                        return (
                        <div
                          key={ticket.id || idx}
                          role="button"
                          tabIndex={0}
                          onClick={(e) => {
                            e.stopPropagation();
                            if (ticketId) router.push(`/tickets/${ticketId}`);
                          }}
                          onKeyDown={(e) => {
                            if (e.key === "Enter" && ticketId) {
                              e.stopPropagation();
                              router.push(`/tickets/${ticketId}`);
                            }
                          }}
                          className={cn(
                            "text-[10px] px-2 py-1 rounded border truncate cursor-pointer hover:opacity-90",
                            getStatusColor(ticket.displayStatus ?? ticket.status)
                          )}
                          title={ticket.title || ticket.ticketNumber}
                        >
                          <div className="flex items-center justify-between gap-1">
                            <span className="truncate font-iran">
                              {ticket.ticketNumber || `T-${ticket.id?.substring(0, 8)?.toUpperCase() || ""}`}
                            </span>
                            <Badge
                              variant="outline"
                              className={cn("text-[9px] px-1 py-0 h-auto font-iran", getStatusColor(ticket.displayStatus ?? ticket.status))}
                            >
                              {getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "admin")}
                            </Badge>
                          </div>
                        </div>
                      );})}
                      {!isExpanded && remainingCount > 0 && (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            toggleDayExpansion(key);
                          }}
                          className="w-full text-[10px] text-primary hover:underline font-iran py-1"
                        >
                          +{remainingCount} مورد دیگر
                        </button>
                      )}
                      {isExpanded && remainingCount > 0 && (
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            toggleDayExpansion(key);
                          }}
                          className="w-full text-[10px] text-muted-foreground hover:underline font-iran py-1"
                        >
                          نمایش کمتر
                        </button>
                      )}
                    </div>
                  ) : (
                    <div className="flex-1 flex items-center justify-center text-xs text-muted-foreground font-iran">
                      بدون تیکت
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </CardContent>
      </Card>

      <Dialog
        open={dialogOpen && Boolean(selectedDateKey)}
        onOpenChange={(open) => {
          setDialogOpen(open);
          if (!open) setSelectedDateKey(null);
        }}
      >
        {selectedDay && (
          <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl space-y-4 font-iran" dir="rtl">
            <DialogHeader className="space-y-2 text-right">
              <DialogTitle className="font-iran text-xl">
                تیکت‌های این روز — {fullDateFormatter.format(selectedDay.date.toDate())}
              </DialogTitle>
              <DialogDescription className="font-iran text-muted-foreground">
                {loadingByDate
                  ? "در حال بارگذاری..."
                  : `در این روز ${ticketsForSelectedDay.length} تیکت بروزرسانی شده است. روی تیکت کلیک کنید.`}
              </DialogDescription>
            </DialogHeader>

            <Separator />

            <ScrollArea className="max-h-[60vh] pr-4">
              <div className="space-y-2">
                {loadingByDate ? (
                  <div className="flex items-center justify-center py-12 text-muted-foreground font-iran">
                    در حال بارگذاری...
                  </div>
                ) : ticketsForSelectedDay.length === 0 ? (
                  <div className="text-center py-8 text-muted-foreground font-iran">
                    تیکتی در این روز وجود ندارد
                  </div>
                ) : (
                  ticketsForSelectedDay.map((ticket) => {
                    const statusLabel = statusLabels[ticket.status as TicketStatus] ?? ticket.status;
                    const statusClass = statusColors[ticket.status as TicketStatus] ?? "bg-slate-100 text-slate-700 border";
                    const updatedDisplay = formatDateValue(ticket.updatedAt, dateTimeFormatter);
                    return (
                      <Link
                        key={ticket.ticketId}
                        href={`/tickets/${ticket.ticketId}`}
                        className="flex flex-col gap-1 rounded-xl border bg-muted/40 p-4 shadow-sm hover:bg-muted/70 transition-colors text-right font-iran block"
                      >
                        <div className="flex items-center justify-between gap-2 flex-wrap">
                          <span className="font-medium text-foreground truncate">{ticket.title}</span>
                          <Badge className={cn("font-iran shrink-0", statusClass)}>{statusLabel}</Badge>
                        </div>
                        <div className="flex items-center gap-4 text-sm text-muted-foreground">
                          {ticket.code && <span>{ticket.code}</span>}
                          <span>آخرین بروزرسانی: {updatedDisplay}</span>
                          {ticket.assignedToName && <span>تکنسین: {ticket.assignedToName}</span>}
                        </div>
                      </Link>
                    );
                  })
                )}
              </div>
            </ScrollArea>
          </DialogContent>
        )}
      </Dialog>
    </>
  );
}
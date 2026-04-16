"use client";

import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Separator } from "@/components/ui/separator";
import { TwoStepTicketForm } from "@/components/two-step-ticket-form";
import {
  Plus,
  Search,
  Filter,
  Eye,
  Clock,
  AlertCircle,
  CheckCircle,
  Ticket as TicketIcon,
  MessageSquare,
  Calendar,
  Paperclip,
  FileText,
} from "lucide-react";
import type { CategoriesData } from "@/services/categories-types";
import type { Ticket, TicketPriority, TicketStatus, TicketCategory } from "@/types";
import { getEffectiveStatus, getTicketStatusLabel, getTicketStatusColor, type TicketStatus as TicketStatusType } from "@/lib/ticket-status";
import { formatFileSize } from "@/lib/file-upload";
import { apiGetNoStore } from "@/lib/api-client";
import { getEffectiveApiBaseUrl } from "@/lib/url";
import type { ApiTicketListItemResponse, ApiTicketMessageDto, ApiTicketResponse } from "@/lib/api-types";
import { mapApiMessageToResponse, mapApiTicketToUi } from "@/lib/ticket-mappers";
import { formatFaDate, formatFaDateTime, formatFaTime, parseServerDate } from "@/lib/datetime";

/* =========================
   Strong Types & Dictionaries
   ========================= */

interface CurrentUser {
  id?: string;
  email: string;
  name?: string;
}

// Status colors and labels are now handled by getTicketStatusColor and getTicketStatusLabel
// These are kept for backward compatibility but should not be used directly for client role
const statusColors: Record<TicketStatus, string> = {
  Submitted: "bg-blue-100 text-blue-800 border-blue-200",
  SeenRead: "bg-purple-100 text-purple-800 border-purple-200",
  Open: "bg-red-100 text-red-800 border-red-200",
  InProgress: "bg-yellow-100 text-yellow-800 border-yellow-200",
  Solved: "bg-green-100 text-green-800 border-green-200",
  Redo: "bg-red-100 text-red-800 border-red-200",
};

const priorityColors: Record<TicketPriority, string> = {
  low: "bg-blue-100 text-blue-800 border-blue-200",
  medium: "bg-orange-100 text-orange-800 border-orange-200",
  high: "bg-red-100 text-red-800 border-red-200",
  urgent: "bg-purple-100 text-purple-800 border-purple-200",
};

const priorityLabels: Record<TicketPriority, string> = {
  low: "کم",
  medium: "متوسط",
  high: "بالا",
  urgent: "فوری",
};

const categoryLabels: Record<TicketCategory, string> = {
  hardware: "سخت‌افزار",
  software: "نرم‌افزار",
  network: "شبکه",
  email: "ایمیل",
  security: "امنیت",
  access: "دسترسی",
};
const getCategoryLabel = (cat: string, categoriesData: CategoriesData) =>
  categoriesData?.[cat]?.label ?? categoryLabels[cat as TicketCategory] ?? cat;

/* =========================
   Component Props
   ========================= */

interface ClientDashboardProps {
  tickets: Ticket[];
  onTicketCreate: (ticket: Ticket) => void;
  onTicketSeen: (ticketId: string) => void;
  authToken?: string | null;
  currentUser: CurrentUser | null;
  categoriesData: CategoriesData;
  activeSection?: "tickets" | "create";
}

/* =========================
   Component
   ========================= */

export function ClientDashboard({
  tickets,
  onTicketCreate,
  onTicketSeen,
  authToken,
  currentUser,
  categoriesData,
  activeSection = "tickets",
}: ClientDashboardProps) {
  const [searchQuery, setSearchQuery] = useState("");
  const [filterStatus, setFilterStatus] = useState<TicketStatus | "all">("all");
  const [filterPriority, setFilterPriority] = useState<TicketPriority | "all">("all");
  const [selectedTicket, setSelectedTicket] = useState<Ticket | null>(null);
  const [viewDialogOpen, setViewDialogOpen] = useState(false);
  const [messagesLoading, setMessagesLoading] = useState(false);
  const [ticketDialogOpen, setTicketDialogOpen] = useState(activeSection === "create");
  const [statsDialogOpen, setStatsDialogOpen] = useState(false);
  const [statsDialogData, setStatsDialogData] = useState<{ key: string; title: string } | null>(null);
  const [statsTickets, setStatsTickets] = useState<Ticket[]>([]);
  const [statsLoading, setStatsLoading] = useState(false);
  const [statsError, setStatsError] = useState<string | null>(null);
  const statsRequestRef = useRef(0);
  const [apiBaseUrl, setApiBaseUrl] = useState<string>("");

  // Get API base URL on mount (dev: may use default; production: env only — no localhost)
  useEffect(() => {
    setApiBaseUrl(getEffectiveApiBaseUrl() || "");
  }, []);

  useEffect(() => {
    if (activeSection === "create") {
      setTicketDialogOpen(true);
    } else if (activeSection === "tickets") {
      setTicketDialogOpen(false);
    }
  }, [activeSection]);

  // Filter tickets by clientId (userId) for reliable matching
  // Fall back to email comparison if clientId is not available
  const userTickets = tickets.filter((t) => {
    // For clients, the backend already filters by CreatedByUserId
    // This is a safety filter to ensure we only show the user's own tickets
    // Use clientId if available, otherwise fall back to email comparison
    if (t.clientId && currentUser?.id) {
      return t.clientId === currentUser.id;
    }
    // Fallback to case-insensitive email comparison
    return (
      t.clientEmail?.toLowerCase() === currentUser?.email?.toLowerCase()
    );
  });

  const filteredTickets = userTickets.filter((ticket) => {
    const idStr = String(ticket.id ?? "");
    const matchesSearch =
      (ticket.title ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
      (ticket.description ?? "")
        .toLowerCase()
        .includes(searchQuery.toLowerCase()) ||
      idStr.toLowerCase().includes(searchQuery.toLowerCase());

    // Use effective status for filtering (clients see Redo as Open)
    const effectiveStatus = getEffectiveStatus(ticket.displayStatus ?? ticket.status, "client");
    const matchesStatus =
      filterStatus === "all" || effectiveStatus === filterStatus;
    const matchesPriority =
      filterPriority === "all" || ticket.priority === filterPriority;

    return matchesSearch && matchesStatus && matchesPriority;
  });

  // Single source of truth for status in UI (counts + stat-card modal list).
  const statusForUi = (t: Ticket) => getEffectiveStatus(t.displayStatus ?? t.status, "client");
  const openCount = userTickets.filter((t) => statusForUi(t) === "Open").length;
  const inProgressCount = userTickets.filter((t) => statusForUi(t) === "InProgress").length;
  const resolvedCount = userTickets.filter((t) => statusForUi(t) === "Solved").length;
  const totalCount = userTickets.length;
  const unseenTickets = userTickets.filter((t) => t.isUnseen);

  const handleViewTicket = (ticket: Ticket) => {
    setSelectedTicket(ticket);
    setViewDialogOpen(true);
    if (ticket.id) {
      onTicketSeen(ticket.id);
    }
  };

  // Ensure ticket details and messages are loaded when viewing (works with cookie or token auth).
  useEffect(() => {
    if (!viewDialogOpen || !selectedTicket?.id) return;

    let cancelled = false;
    const loadTicketAndMessages = async () => {
      try {
        setMessagesLoading(true);
        const [details, messages] = await Promise.all([
          apiGetNoStore<ApiTicketResponse>(`/api/tickets/${selectedTicket.id}`, { token: authToken ?? undefined, silent: true }),
          apiGetNoStore<ApiTicketMessageDto[]>(`/api/tickets/${selectedTicket.id}/messages`, { token: authToken ?? undefined, silent: true }),
        ]);
        if (cancelled) return;
        const mapped = mapApiTicketToUi(details, categoriesData, (messages ?? []).map(mapApiMessageToResponse));
        setSelectedTicket(mapped);
      } catch (err) {
        // Non-fatal: allow details dialog without messages if endpoint fails
      } finally {
        if (!cancelled) setMessagesLoading(false);
      }
    };

    void loadTicketAndMessages();
    return () => {
      cancelled = true;
    };
  }, [viewDialogOpen, selectedTicket?.id, authToken, categoriesData]);

  const fetchTicketsForCard = async (key: string): Promise<Ticket[]> => {
    if (!authToken) {
      return userTickets;
    }

    const params = new URLSearchParams();
    if (key === "inProgress") {
      params.set("status", "InProgress");
    } else if (key === "solved") {
      params.set("status", "Solved");
    } else if (key === "unseen") {
      params.set("unseen", "true");
    }

    const endpoint = params.toString()
      ? `/api/tickets?${params.toString()}`
      : "/api/tickets";
    const apiTickets = await apiGetNoStore<ApiTicketListItemResponse[]>(endpoint, { token: authToken });
    return apiTickets.map((apiTicket) => mapApiTicketToUi(apiTicket, categoriesData, []));
  };

  const applyCardFilter = (key: string, items: Ticket[]) => {
    if (key === "all") return items;
    if (key === "open") return items.filter((t) => statusForUi(t) === "Open");
    if (key === "inProgress") return items.filter((t) => statusForUi(t) === "InProgress");
    if (key === "solved") return items.filter((t) => statusForUi(t) === "Solved");
    if (key === "unseen") return items.filter((t) => t.isUnseen === true);
    return items;
  };

  const handleStatsClick = async (key: string, title: string) => {
    setStatsDialogOpen(true);
    setStatsDialogData({ key, title });
    setStatsLoading(true);
    setStatsError(null);

    const requestId = ++statsRequestRef.current;
    try {
      const items = await fetchTicketsForCard(key);
      if (statsRequestRef.current !== requestId) {
        return;
      }
      setStatsTickets(applyCardFilter(key, items));
    } catch (error: any) {
      if (statsRequestRef.current !== requestId) {
        return;
      }
      setStatsError(error?.message || "خطا در بارگذاری تیکت‌ها");
      setStatsTickets([]);
    } finally {
      if (statsRequestRef.current === requestId) {
        setStatsLoading(false);
      }
    }
  };

  const handleTicketCreate = (ticket: Ticket) => {
    onTicketCreate(ticket);
    setTicketDialogOpen(false);
  };

  const handleCancelTicketForm = () => {
    setTicketDialogOpen(false);
  };

  const formatSafeDateTime = (input?: string | number | Date) => {
    const parsed = parseServerDate(input ?? null);
    if (!parsed || Number.isNaN(parsed.getTime())) {
      if (process.env.NODE_ENV === "development") {
        console.warn("[ClientDashboard] Invalid date value:", input);
      }
      return { date: "—", time: "—", dateTime: "—" };
    }
    return {
      date: formatFaDate(parsed),
      time: formatFaTime(parsed),
      dateTime: formatFaDateTime(parsed),
    };
  };

  const faDate = (d?: string | number | Date) => formatSafeDateTime(d).date;
  const faDateTime = (d?: string | number | Date) => formatSafeDateTime(d).dateTime;

  return (
    <div className="space-y-6 font-iran" dir="rtl">
      <div className="flex justify-between items-center">
        <div className="text-right">
          <h2 className="text-2xl font-bold font-iran">پنل کاربری</h2>
          <p className="text-muted-foreground font-iran">
            مدیریت درخواست‌های خدمات IT
          </p>
        </div>
        <Dialog open={ticketDialogOpen} onOpenChange={setTicketDialogOpen}>
          <DialogTrigger asChild>
            <Button className="gap-2 font-iran">
              <Plus className="w-4 h-4" />
              ایجاد تیکت جدید
            </Button>
          </DialogTrigger>
          <DialogContent
            className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl font-iran"
            dir="rtl"
          >
            <DialogHeader>
              <DialogTitle className="text-left font-iran">
                ایجاد درخواست جدید
              </DialogTitle>
            </DialogHeader>
            <TwoStepTicketForm
              onSubmit={handleTicketCreate}
              onClose={handleCancelTicketForm}
              categoriesData={categoriesData}
            />
          </DialogContent>
        </Dialog>
      </div>

      {/* Statistics Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card
          role="button"
          onClick={() => handleStatsClick("all", "کل تیکت‌ها")}
          className="cursor-pointer hover:border-primary transition"
        >
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-right font-iran">
              کل تیکت‌ها
            </CardTitle>
            <TicketIcon className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-right font-iran">
              {totalCount}
            </div>
          </CardContent>
        </Card>
        <Card
          role="button"
          onClick={() => handleStatsClick("open", "در انتظار پاسخ")}
          className="cursor-pointer hover:border-primary transition"
        >
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-right font-iran">
              در انتظار پاسخ
            </CardTitle>
            <AlertCircle className="h-4 w-4 text-red-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-right font-iran">
              {openCount}
            </div>
          </CardContent>
        </Card>
        <Card
          role="button"
          onClick={() => handleStatsClick("inProgress", "در حال انجام")}
          className="cursor-pointer hover:border-primary transition"
        >
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-right font-iran">
              در حال انجام
            </CardTitle>
            <Clock className="h-4 w-4 text-blue-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-right font-iran">
              {inProgressCount}
            </div>
          </CardContent>
        </Card>
        <Card
          role="button"
          onClick={() => handleStatsClick("solved", "حل شده")}
          className="cursor-pointer hover:border-primary transition"
        >
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-right font-iran">
              حل شده
            </CardTitle>
            <CheckCircle className="h-4 w-4 text-green-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-right font-iran">
              {resolvedCount}
            </div>
          </CardContent>
        </Card>
        <Card
          role="button"
          onClick={() => handleStatsClick("unseen", "دیده‌نشده")}
          className="cursor-pointer hover:border-primary transition"
        >
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-sm font-medium text-right font-iran">
              دیده‌نشده
            </CardTitle>
            <Eye className="h-4 w-4 text-blue-500" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold text-right font-iran">
              {unseenTickets.length}
            </div>
          </CardContent>
        </Card>
      </div>

      <Dialog
        open={statsDialogOpen}
        onOpenChange={(open) => {
          setStatsDialogOpen(open);
          if (!open) {
            statsRequestRef.current += 1;
            setStatsDialogData(null);
            setStatsTickets([]);
            setStatsLoading(false);
            setStatsError(null);
          }
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl font-iran" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right font-iran">
              {statsDialogData?.title ?? "جزئیات"}
            </DialogTitle>
          </DialogHeader>
          {statsLoading ? (
            <div className="py-8 text-center text-sm text-muted-foreground">
              در حال بارگذاری...
            </div>
          ) : statsError ? (
            <div className="space-y-3 text-right">
              <p className="text-sm text-muted-foreground">{statsError}</p>
              <Button
                size="sm"
                variant="outline"
                className="font-iran"
                onClick={() => {
                  if (statsDialogData) {
                    handleStatsClick(statsDialogData.key, statsDialogData.title);
                  }
                }}
              >
                تلاش دوباره
              </Button>
            </div>
          ) : statsTickets.length ? (
            <div className="space-y-3 max-h-[70vh] overflow-y-auto pr-1">
              {statsTickets.map((ticket) => {
                const idStr = String(ticket.id ?? "");
                return (
                  <div
                    key={idStr}
                    className="w-full border rounded-lg p-3 space-y-2 text-right cursor-default"
                  >
                    <div className="flex items-center justify-between">
                      <div className="space-y-1 text-right">
                        <div className="font-semibold">{ticket.title}</div>
                        <div className="text-xs text-muted-foreground">شناسه: {idStr}</div>
                      </div>
                      <div className="flex gap-2">
                        <Badge className={`${getTicketStatusColor(ticket.displayStatus ?? ticket.status, "client")} font-iran`}>
                          {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "client")}
                        </Badge>
                        <Badge className={`${priorityColors[ticket.priority]} font-iran`}>
                          {priorityLabels[ticket.priority]}
                        </Badge>
                      </div>
                    </div>
                    <div className="flex items-center justify-between text-sm text-muted-foreground">
                      <span>تاریخ ایجاد: {faDate(ticket.createdAt)}</span>
                      {ticket.isUnseen ? (
                        <span className="flex items-center gap-2 text-blue-600">
                          <span className="h-2 w-2 rounded-full bg-blue-500" />
                          دیده‌نشده
                        </span>
                      ) : null}
                    </div>
                  </div>
                );
              })}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground text-right font-iran">
              تیکتی برای نمایش وجود ندارد.
            </p>
          )}
        </DialogContent>
      </Dialog>

      {/* Tickets Management */}
      <Card>
        <CardHeader>
          <CardTitle className="text-right font-iran">تیکت‌های من</CardTitle>
        </CardHeader>
        <CardContent>
          {/* Filters */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4 mb-6">
            <div className="relative">
              <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
              <Input
                placeholder="جستجو در تیکت‌ها..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pr-10 text-right font-iran"
                dir="rtl"
              />
            </div>

            <Select
              value={filterStatus}
              onValueChange={(value) => setFilterStatus(value as TicketStatus | "all")}
              dir="rtl"
            >
              <SelectTrigger className="text-right font-iran">
                <SelectValue placeholder="وضعیت" />
              </SelectTrigger>
              <SelectContent className="font-iran">
                <SelectItem value="all">همه وضعیت‌ها</SelectItem>
                <SelectItem value="Submitted">{getTicketStatusLabel("Submitted", "client")}</SelectItem>
                <SelectItem value="SeenRead">{getTicketStatusLabel("SeenRead", "client")}</SelectItem>
                <SelectItem value="Open">{getTicketStatusLabel("Open", "client")}</SelectItem>
                <SelectItem value="InProgress">{getTicketStatusLabel("InProgress", "client")}</SelectItem>
                <SelectItem value="Solved">{getTicketStatusLabel("Solved", "client")}</SelectItem>
                {/* Redo is not shown to clients - they see it as InProgress */}
              </SelectContent>
            </Select>

            <Select
              value={filterPriority}
              onValueChange={(value) => setFilterPriority(value as TicketPriority | "all")}
              dir="rtl"
            >
              <SelectTrigger className="text-right font-iran">
                <SelectValue placeholder="اولویت" />
              </SelectTrigger>
              <SelectContent className="font-iran">
                <SelectItem value="all">همه اولویت‌ها</SelectItem>
                <SelectItem value="low">کم</SelectItem>
                <SelectItem value="medium">متوسط</SelectItem>
                <SelectItem value="high">بالا</SelectItem>
                <SelectItem value="urgent">فوری</SelectItem>
              </SelectContent>
            </Select>

            <Button
              variant="outline"
              onClick={() => {
                setSearchQuery("");
                setFilterStatus("all");
                setFilterPriority("all");
              }}
              className="gap-2 font-iran"
            >
              <Filter className="w-4 h-4" />
              پاک کردن فیلترها
            </Button>
          </div>

          {/* Tickets: card list on mobile, table on md+ */}
          <div className="md:hidden space-y-2">
            {filteredTickets.length > 0 ? (
              filteredTickets.map((ticket) => {
                const idStr = String(ticket.id ?? "")
                const isUnseen = ticket.isUnseen === true
                return (
                  <Card key={idStr} className="cursor-pointer" onClick={() => handleViewTicket(ticket)}>
                    <CardContent className="p-3">
                      <div className="flex items-center gap-2">
                        {isUnseen ? <span className="h-2 w-2 rounded-full bg-blue-500 shrink-0" /> : null}
                        <p className="font-mono text-xs text-muted-foreground break-words">{idStr}</p>
                      </div>
                      <p className="font-medium truncate mt-0.5">{ticket.title}</p>
                      <div className="flex flex-wrap gap-1 mt-1">
                        <Badge className={getTicketStatusColor(ticket.displayStatus ?? ticket.status, "client")}>
                          {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "client")}
                        </Badge>
                        <Badge className={priorityColors[ticket.priority]}>{priorityLabels[ticket.priority]}</Badge>
                        <span className="text-xs text-muted-foreground">{faDate(ticket.createdAt)}</span>
                      </div>
                      <Button variant="ghost" size="sm" className="mt-2 w-full gap-1" onClick={(e) => { e.stopPropagation(); handleViewTicket(ticket) }}>
                        <Eye className="w-3 h-3" />
                        مشاهده
                      </Button>
                    </CardContent>
                  </Card>
                )
              })
            ) : (
              <div className="border rounded-lg p-8 text-center text-muted-foreground">
                <Search className="w-8 h-8 mx-auto mb-2" />
                <p className="font-iran">تیکتی یافت نشد</p>
              </div>
            )}
          </div>
          <div className="hidden md:block border rounded-lg overflow-x-auto">
            <Table className="min-w-[700px]">
              <TableHeader>
                <TableRow>
                  <TableHead className="text-right font-iran">
                    شماره تیکت
                  </TableHead>
                  <TableHead className="text-right font-iran">عنوان</TableHead>
                  <TableHead className="text-right font-iran">وضعیت</TableHead>
                  <TableHead className="text-right font-iran">اولویت</TableHead>
                  <TableHead className="text-right font-iran">
                    دسته‌بندی
                  </TableHead>
                  <TableHead className="text-right font-iran">تکنسین</TableHead>
                  <TableHead className="text-right font-iran">
                    تاریخ ایجاد
                  </TableHead>
                  <TableHead className="text-right font-iran">عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTickets.length > 0 ? (
                  filteredTickets.map((ticket) => {
                    const idStr = String(ticket.id ?? "");
                    const isUnseen = ticket.isUnseen === true;
                    return (
                      <TableRow
                        key={idStr}
                        className="cursor-pointer hover:bg-muted/50"
                        onClick={() => handleViewTicket(ticket)}
                      >
                        <TableCell className="font-mono text-sm font-iran">
                          <div className="flex items-center gap-2">
                            {isUnseen ? (
                              <span
                                className="h-2 w-2 rounded-full bg-blue-500"
                                aria-label="به‌روزرسانی جدید"
                              />
                            ) : null}
                            <span className={isUnseen ? "font-bold" : ""}>{idStr}</span>
                          </div>
                        </TableCell>
                        <TableCell className="max-w-xs">
                          <div
                            className="truncate font-iran"
                            title={ticket.title}
                          >
                            {ticket.title}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge
                            className={`${
                              getTicketStatusColor(ticket.displayStatus ?? ticket.status, "client")
                            } font-iran`}
                          >
                            {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "client")}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge
                            className={`${
                              priorityColors[ticket.priority]
                            } font-iran`}
                          >
                            {priorityLabels[ticket.priority]}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <span className="text-sm font-iran">
                            {getCategoryLabel(ticket.category, categoriesData)}
                          </span>
                        </TableCell>
                        <TableCell>
                          {ticket.assignedTechnicianName ? (
                            <div className="flex items-center gap-2">
                              <Avatar className="w-6 h-6">
                                <AvatarFallback className="text-xs font-iran">
                                  {ticket.assignedTechnicianName.charAt(0)}
                                </AvatarFallback>
                              </Avatar>
                              <span className="text-sm font-iran">
                                {ticket.assignedTechnicianName}
                              </span>
                            </div>
                          ) : (
                            <span className="text-sm text-muted-foreground font-iran">
                              تعیین نشده
                            </span>
                          )}
                        </TableCell>
                        <TableCell className="text-sm font-iran">
                          {faDate(ticket.createdAt)}
                        </TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => {
                              e.stopPropagation()
                              handleViewTicket(ticket)
                            }}
                            className="gap-1 font-iran"
                          >
                            <Eye className="w-3 h-3" />
                            مشاهده
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })
                ) : (
                  <TableRow>
                    <TableCell colSpan={8} className="text-center py-8">
                      <div className="flex flex-col items-center gap-2">
                        <Search className="w-8 h-8 text-muted-foreground" />
                        <p className="text-muted-foreground font-iran">
                          تیکتی یافت نشد
                        </p>
                      </div>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {/* View Ticket Dialog */}
      <Dialog open={viewDialogOpen} onOpenChange={setViewDialogOpen}>
        <DialogContent
          className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl font-iran"
          dir="rtl"
        >
          <DialogHeader>
            <DialogTitle className="text-left font-iran">
              جزئیات تیکت {selectedTicket ? String(selectedTicket.id) : ""}
            </DialogTitle>
          </DialogHeader>

          {selectedTicket && (
            <div className="space-y-6">
              {/* Ticket Header */}
              <div className="flex justify-between items-start">
                <div className="text-right space-y-2">
                  <h3 className="text-xl font-semibold font-iran">
                    {selectedTicket.title}
                  </h3>
                  <div className="flex gap-2">
                    <Badge
                      className={`${
                        getTicketStatusColor(selectedTicket.status, "client")
                      } font-iran`}
                    >
                      {getTicketStatusLabel(selectedTicket.status, "client")}
                    </Badge>
                    <Badge
                      className={`${
                        priorityColors[selectedTicket.priority]
                      } font-iran`}
                    >
                      {priorityLabels[selectedTicket.priority]}
                    </Badge>
                  </div>
                </div>
                <div className="text-left space-y-1">
                  <p className="text-sm text-muted-foreground font-iran">
                    شماره تیکت
                  </p>
                  <p className="font-mono text-lg font-iran">
                    {String(selectedTicket.id)}
                  </p>
                </div>
              </div>

              <Separator />

              {/* Ticket Details */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <div className="space-y-4">
                  <div>
                    <h4 className="font-medium mb-2 font-iran">اطلاعات کلی</h4>
                    <div className="space-y-2 text-sm">
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          دسته‌بندی:
                        </span>
                        <span className="font-iran">
                          {getCategoryLabel(selectedTicket.category, categoriesData)}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          زیر دسته:
                        </span>
                        <span className="font-iran">
                          {selectedTicket.subcategory ?? "-"}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          تاریخ ایجاد:
                        </span>
                        <span className="font-iran">
                          {faDate(selectedTicket.createdAt)}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          آخرین به‌روزرسانی:
                        </span>
                        <span className="font-iran">
                          {faDate(selectedTicket.updatedAt)}
                        </span>
                      </div>
                    </div>
                  </div>

                  {selectedTicket.assignedTechnicianName && (
                    <div>
                      <h4 className="font-medium mb-2 font-iran">
                        تکنسین مسئول
                      </h4>
                      <div className="flex items-center gap-2">
                        <Avatar className="w-8 h-8">
                          <AvatarFallback className="font-iran">
                            {selectedTicket.assignedTechnicianName.charAt(0)}
                          </AvatarFallback>
                        </Avatar>
                        <span className="font-iran">
                          {selectedTicket.assignedTechnicianName}
                        </span>
                      </div>
                    </div>
                  )}
                </div>

                <div className="space-y-4">
                  <div>
                    <h4 className="font-medium mb-2 font-iran">اطلاعات تماس</h4>
                    <div className="space-y-2 text-sm">
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          نام:
                        </span>
                        <span className="font-iran">
                          {selectedTicket.clientName}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          ایمیل:
                        </span>
                        <span className="font-iran">
                          {selectedTicket.clientEmail}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          تلفن:
                        </span>
                        <span className="font-iran">
                          {selectedTicket.clientPhone ?? "-"}
                        </span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-muted-foreground font-iran">
                          بخش:
                        </span>
                        <span className="font-iran">
                          {selectedTicket.department ?? "-"}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>

              <Separator />

              {/* Description */}
              <div>
                <h4 className="font-medium mb-2 font-iran">شرح مشکل</h4>
                <div className="bg-muted p-4 rounded-lg text-right">
                  <p className="whitespace-pre-wrap font-iran">
                    {selectedTicket.description}
                  </p>
                </div>
              </div>

              {/* Attachments */}
              {selectedTicket.attachments && selectedTicket.attachments.length > 0 && (
                <div>
                  <h4 className="font-medium mb-2 font-iran flex items-center gap-2">
                    <Paperclip className="w-4 h-4" />
                    فایل‌های پیوست شده
                  </h4>
                  <div className="space-y-2">
                    {selectedTicket.attachments.map((attachment: any) => {
                      const fileUrl = attachment.fileUrl?.startsWith('http') 
                        ? attachment.fileUrl 
                        : `${apiBaseUrl}${attachment.fileUrl}`;
                      return (
                        <a
                          key={attachment.id}
                          href={fileUrl}
                          target="_blank"
                          rel="noopener noreferrer"
                          className="flex items-center gap-2 p-3 border rounded-lg hover:bg-muted transition-colors text-right"
                        >
                          <FileText className="w-4 h-4 text-muted-foreground flex-shrink-0" />
                          <span className="font-iran text-sm flex-1">{attachment.fileName}</span>
                          {attachment.fileSize && (
                            <span className="text-xs text-muted-foreground font-iran">
                              ({formatFileSize(attachment.fileSize)})
                            </span>
                          )}
                        </a>
                      );
                    })}
                  </div>
                </div>
              )}

              {/* Responses / مکالمه (read-only) */}
              {messagesLoading && (!selectedTicket.responses || selectedTicket.responses.length === 0) ? (
                <p className="text-sm text-muted-foreground text-right font-iran">
                  در حال بارگذاری پیام‌ها...
                </p>
              ) : null}
              {selectedTicket.responses && selectedTicket.responses.length > 0 && (
                <div>
                  <h4 className="font-medium mb-4 flex items-center gap-2 font-iran">
                    <MessageSquare className="w-4 h-4" />
                    مکالمه
                  </h4>
                  <p className="text-sm text-muted-foreground text-right font-iran mb-4">
                    این بخش فقط برای مشاهده است. پاسخ‌دهی توسط واحد IT انجام می‌شود.
                  </p>
                  <div className="space-y-4">
                    {selectedTicket.responses.map((response, index) => (
                      <div key={response.id ?? index} className="border rounded-lg p-4">
                        <div className="flex justify-between items-start mb-2">
                          <div className="flex items-center gap-2">
                            <Avatar className="w-6 h-6">
                              <AvatarFallback className="text-xs font-iran">
                                {response.authorName?.charAt(0) || "T"}
                              </AvatarFallback>
                            </Avatar>
                            <span className="font-medium text-sm font-iran">
                              {response.authorName ?? "—"}
                            </span>
                          </div>
                          <div className="text-left">
                            <Badge
                              className={`${
                                getTicketStatusColor(response.status, "client")
                              } mb-1 font-iran`}
                            >
                              {getTicketStatusLabel(response.status, "client")}
                            </Badge>
                            <p className="text-xs text-muted-foreground flex items-center gap-1 font-iran">
                              <Calendar className="w-3 h-3" />
                              {faDateTime(response.timestamp)}
                            </p>
                          </div>
                        </div>
                        <div className="bg-muted/50 p-3 rounded text-right">
                          <p className="whitespace-pre-wrap font-iran">
                            {response.message}
                          </p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

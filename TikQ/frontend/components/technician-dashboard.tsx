"use client"

import type React from "react"
import { useMemo, useState } from "react"
import { useRouter } from "next/navigation"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { apiRequest } from "@/lib/api-client"
import type { ApiTechnicianResponse } from "@/lib/api-types"
import type { Ticket } from "@/types"
import { parseServerDate, toFaDate } from "@/lib/datetime"
import {
  getEffectiveStatus,
  getTicketStatusLabel,
  getTicketStatusColor,
  TICKET_STATUS_OPTIONS,
  type TicketStatus,
} from "@/lib/ticket-status"
import {
  Clock,
  AlertCircle,
  CheckCircle,
  Ticket as TicketIcon,
  Search,
  Filter,
  MessageSquare,
} from "lucide-react"

export type TechnicianDashboardProps = {
  tickets: Ticket[]
  onTicketUpdate?: (ticketId: string, updates: Partial<Ticket>) => void
  onTicketRespond?: (ticketId: string, message: string, status: TicketStatus) => void
  onTicketSeen?: (ticketId: string) => void
  isSupervisor?: boolean
  currentUser?: ApiTechnicianResponse | { name?: string; fullName?: string } | null
  authToken?: string | null
  activeSection?: "assigned" | "available" | "closed" | "history"
  onSectionChange?: (section: "assigned" | "available" | "closed" | "history") => void
}

export function TechnicianDashboard({
  tickets,
  currentUser,
  authToken,
  onTicketRespond,
  onTicketSeen,
  isSupervisor,
}: TechnicianDashboardProps) {
  const router = useRouter()
  const [searchQuery, setSearchQuery] = useState("")
  const [filterStatus, setFilterStatus] = useState<TicketStatus | "all">("all")
  const [filterPriority, setFilterPriority] = useState<Ticket["priority"] | "all">("all")
  const [responseDialogOpen, setResponseDialogOpen] = useState(false)
  const [responseTicket, setResponseTicket] = useState<Ticket | null>(null)
  const [responseStatus, setResponseStatus] = useState<TicketStatus>("Open")
  const [responseMessage, setResponseMessage] = useState("")
  const [responseSubmitting, setResponseSubmitting] = useState(false)
  const [cardDialogOpen, setCardDialogOpen] = useState(false)
  const [cardDialogTitle, setCardDialogTitle] = useState("")
  const [cardDialogTickets, setCardDialogTickets] = useState<Ticket[]>([])
  const [cardDialogLoading, setCardDialogLoading] = useState(false)
  const [cardDialogError, setCardDialogError] = useState<string | null>(null)

  const priorityLabels: Record<Ticket["priority"], string> = {
    low: "پایین",
    medium: "متوسط",
    high: "بالا",
    urgent: "بحرانی",
  }

  const priorityColors: Record<Ticket["priority"], string> = {
    low: "bg-green-100 text-green-700 border border-green-200",
    medium: "bg-blue-100 text-blue-700 border border-blue-200",
    high: "bg-orange-100 text-orange-700 border border-orange-200",
    urgent: "bg-red-100 text-red-700 border border-red-200",
  }

  const sortedTickets = useMemo(() => {
    return [...tickets].sort((a, b) => {
      const aTime = parseServerDate(a.updatedAt ?? a.createdAt)?.getTime() ?? 0
      const bTime = parseServerDate(b.updatedAt ?? b.createdAt)?.getTime() ?? 0
      return bTime - aTime
    })
  }, [tickets])

  const filteredTickets = useMemo(() => {
    return sortedTickets.filter((ticket) => {
      const idStr = String(ticket.id ?? "")
      const matchesSearch =
        (ticket.title ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
        (ticket.description ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
        idStr.toLowerCase().includes(searchQuery.toLowerCase())

      const effectiveStatus = getEffectiveStatus(ticket.displayStatus ?? ticket.status, "technician")
      const matchesStatus = filterStatus === "all" || effectiveStatus === filterStatus
      const matchesPriority = filterPriority === "all" || ticket.priority === filterPriority

      return matchesSearch && matchesStatus && matchesPriority
    })
  }, [sortedTickets, searchQuery, filterStatus, filterPriority])

  const openTickets = sortedTickets.filter(
    (ticket) => getEffectiveStatus(ticket.displayStatus ?? ticket.status, "technician") === "Open"
  )
  const inProgressTickets = sortedTickets.filter(
    (ticket) => getEffectiveStatus(ticket.displayStatus ?? ticket.status, "technician") === "InProgress"
  )
  const redoTickets = sortedTickets.filter(
    (ticket) => getEffectiveStatus(ticket.displayStatus ?? ticket.status, "technician") === "Redo"
  )
  const unreadTickets = sortedTickets.filter((ticket) => ticket.isUnseen)
  const solvedTickets = sortedTickets.filter(
    (ticket) => getEffectiveStatus(ticket.displayStatus ?? ticket.status, "technician") === "Solved"
  )

  const markTicketSeen = async (ticketId: string) => {
    if (onTicketSeen) {
      onTicketSeen(ticketId)
      return
    }

    if (authToken) {
      try {
        await apiRequest(`/api/tickets/${ticketId}/seen`, {
          method: "POST",
          token: authToken,
          silent: true,
        })
      } catch (error) {
        console.warn("Failed to mark ticket as seen", error)
      }
    }
  }

  const handleViewTicket = async (ticketId: string) => {
    await markTicketSeen(ticketId)
    if (ticketId) {
      router.push(`/tickets/${ticketId}`)
    }
  }

  const handleOpenResponse = async (ticket: Ticket) => {
    setResponseTicket(ticket)
    setResponseStatus((ticket.displayStatus ?? ticket.status) ?? "Open")
    setResponseMessage("")
    setResponseDialogOpen(true)
    if (ticket.id) {
      await markTicketSeen(ticket.id)
    }
  }

  // Define valid card keys as a type
  type CardKey = "all" | "unread" | "inprogress" | "redo" | "solved";
  
  const openCardDialog = async (key: CardKey) => {
    // Validate key input
    if (!key) {
      console.error("[TechnicianDashboard] openCardDialog: invalid key (empty)");
      return;
    }

    const mapping: Record<CardKey, { title: string; items: Ticket[] }> = {
      all: { title: "همه تیکت‌ها", items: sortedTickets },
      unread: { title: "خوانده نشده", items: unreadTickets },
      inprogress: { title: "در حال انجام", items: inProgressTickets },
      redo: { title: "بازبینی", items: redoTickets },
      solved: { title: "حل شده", items: solvedTickets },
    }

    const selected = mapping[key];
    
    // Guard against undefined mapping
    if (!selected) {
      console.error("[TechnicianDashboard] openCardDialog: unknown key", {
        key,
        validKeys: Object.keys(mapping),
      });
      
      // Show dialog with error state instead of crashing
      setCardDialogTitle("جزئیات");
      setCardDialogOpen(true);
      setCardDialogLoading(false);
      setCardDialogError(`نوع کارت نامعتبر است: ${String(key)}`);
      setCardDialogTickets([]);
      return;
    }

    setCardDialogTitle(selected.title)
    setCardDialogOpen(true)
    setCardDialogLoading(true)
    setCardDialogError(null)
    try {
      setCardDialogTickets(selected.items)
    } catch (error: any) {
      setCardDialogError(error?.message || "خطا در بارگذاری تیکت‌ها")
      setCardDialogTickets([])
    } finally {
      setCardDialogLoading(false)
    }
  }

  const handleSubmitResponse = async () => {
    if (!responseTicket?.id || !onTicketRespond) return
    if (!responseMessage.trim()) return

    try {
      setResponseSubmitting(true)
      await onTicketRespond(responseTicket.id, responseMessage.trim(), responseStatus)
      setResponseDialogOpen(false)
      setResponseTicket(null)
      setResponseMessage("")
    } catch (error) {
      // Keep dialog open so the user can retry or adjust message
    } finally {
      setResponseSubmitting(false)
    }
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-right">
          داشبورد تکنسین {isSupervisor ? "(سرپرست)" : ""}
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-6 text-right">
        <div className="text-sm text-muted-foreground">
          <p>تعداد تیکت‌های اختصاص‌یافته: {tickets.length}</p>
          <p>نام کاربر: {currentUser?.fullName ?? currentUser?.name ?? "نامشخص"}</p>
        </div>

        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-5">
          <Card
            role="button"
            onClick={() => openCardDialog("all")}
            className="cursor-pointer hover:border-primary transition"
          >
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-right">همه تیکت‌ها</CardTitle>
              <TicketIcon className="h-4 w-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-right">{tickets.length}</div>
            </CardContent>
          </Card>
          <Card
            role="button"
            onClick={() => openCardDialog("unread")}
            className="cursor-pointer hover:border-primary transition"
          >
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-right">خوانده نشده</CardTitle>
              <AlertCircle className="h-4 w-4 text-red-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-right">{unreadTickets.length}</div>
            </CardContent>
          </Card>
          <Card
            role="button"
            onClick={() => openCardDialog("inprogress")}
            className="cursor-pointer hover:border-primary transition"
          >
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-right">در حال انجام</CardTitle>
              <Clock className="h-4 w-4 text-blue-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-right">{inProgressTickets.length}</div>
            </CardContent>
          </Card>
          <Card
            role="button"
            onClick={() => openCardDialog("redo")}
            className="cursor-pointer hover:border-primary transition"
          >
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-right">بازبینی</CardTitle>
              <CheckCircle className="h-4 w-4 text-green-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-right">{redoTickets.length}</div>
            </CardContent>
          </Card>
          <Card
            role="button"
            onClick={() => openCardDialog("solved")}
            className="cursor-pointer hover:border-primary transition"
          >
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-right">پاسخ داده شد</CardTitle>
              <MessageSquare className="h-4 w-4 text-emerald-500" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-right">{solvedTickets.length}</div>
            </CardContent>
          </Card>
        </div>

        <Card>
          <CardHeader>
            <CardTitle className="text-right">تیکت‌های واگذار شده</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <div className="relative">
                <Search className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 w-4 h-4" />
                <Input
                  placeholder="جستجو در تیکت‌ها..."
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  className="pr-10 text-right"
                  dir="rtl"
                />
              </div>
              <Select
                value={filterStatus}
                onValueChange={(value) => setFilterStatus(value as TicketStatus | "all")}
              >
                <SelectTrigger className="text-right">
                  <SelectValue placeholder="همه وضعیت‌ها" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">همه وضعیت‌ها</SelectItem>
                  <SelectItem value="Open">نیاز به پاسخ</SelectItem>
                  <SelectItem value="InProgress">در حال انجام</SelectItem>
                  <SelectItem value="Solved">حل شده</SelectItem>
                </SelectContent>
              </Select>
              <Select
                value={filterPriority}
                onValueChange={(value) => setFilterPriority(value as Ticket["priority"] | "all")}
              >
                <SelectTrigger className="text-right">
                  <SelectValue placeholder="همه اولویت‌ها" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">همه اولویت‌ها</SelectItem>
                  <SelectItem value="low">پایین</SelectItem>
                  <SelectItem value="medium">متوسط</SelectItem>
                  <SelectItem value="high">بالا</SelectItem>
                  <SelectItem value="urgent">بحرانی</SelectItem>
                </SelectContent>
              </Select>
              <Button
                variant="outline"
                onClick={() => {
                  setSearchQuery("")
                  setFilterStatus("all")
                  setFilterPriority("all")
                }}
                className="gap-2"
              >
                <Filter className="w-4 h-4" />
                پاک کردن فیلترها
              </Button>
            </div>

            {/* Tickets: card list on mobile, table on md+ */}
            <div className="md:hidden space-y-2">
              {filteredTickets.length > 0 ? (
                filteredTickets.map((ticket) => {
                  const canAct = ticket.canAct !== false
                  return (
                    <Card key={ticket.id} className="cursor-pointer" onClick={() => handleViewTicket(ticket.id)}>
                      <CardContent className="p-3">
                        <p className="font-mono text-xs text-muted-foreground break-words">{ticket.id}</p>
                        <p className="font-medium truncate mt-0.5">{ticket.title}</p>
                        <div className="flex flex-wrap gap-1 mt-1">
                          <Badge className={getTicketStatusColor(ticket.displayStatus ?? ticket.status, "technician")}>
                            {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "technician")}
                          </Badge>
                          <Badge className={priorityColors[ticket.priority]}>{priorityLabels[ticket.priority]}</Badge>
                          <span className="text-xs text-muted-foreground">{toFaDate(ticket.createdAt)}</span>
                        </div>
                        <div className="flex gap-2 mt-2">
                          <Button variant="ghost" size="sm" className="flex-1" onClick={(e) => { e.stopPropagation(); handleViewTicket(ticket.id) }}>
                            مشاهده
                          </Button>
                          {canAct && (
                            <Button variant="outline" size="sm" className="flex-1" onClick={(e) => { e.stopPropagation(); handleOpenResponse(ticket) }}>
                              پاسخ
                            </Button>
                          )}
                        </div>
                      </CardContent>
                    </Card>
                  )
                })
              ) : (
                <div className="border rounded-lg p-8 text-center text-muted-foreground">
                  تیکتی برای نمایش وجود ندارد.
                </div>
              )}
            </div>
            <div className="hidden md:block border rounded-lg overflow-x-auto">
              <Table className="min-w-[700px]">
                <TableHeader>
                  <TableRow>
                    <TableHead className="text-right">شماره تیکت</TableHead>
                    <TableHead className="text-right">عنوان</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                    <TableHead className="text-right">اولویت</TableHead>
                    <TableHead className="text-right">دسته‌بندی</TableHead>
                    <TableHead className="text-right">تاریخ ایجاد</TableHead>
                    <TableHead className="text-right">عملیات</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredTickets.length === 0 ? (
                    <TableRow>
                      <TableCell colSpan={7} className="text-center text-sm text-muted-foreground">
                        تیکتی برای نمایش وجود ندارد.
                      </TableCell>
                    </TableRow>
                  ) : (
                    filteredTickets.map((ticket) => {
                      const isFaded = Boolean(ticket.isFaded)
                      const canAct = ticket.canAct !== false
                      return (
                      <TableRow
                        key={ticket.id}
                        className={`cursor-pointer hover:bg-muted/50 ${isFaded ? "opacity-60 bg-muted/30" : ""}`}
                        onClick={() => handleViewTicket(ticket.id)}
                      >
                        <TableCell className="font-mono text-sm">
                          <div className="flex items-center gap-2">
                            {ticket.isUnseen ? (
                              <span className="h-2 w-2 rounded-full bg-blue-500" />
                            ) : null}
                            <span className={ticket.isUnseen ? "font-bold" : ""}>
                              {ticket.id}
                            </span>
                          </div>
                        </TableCell>
                        <TableCell className="max-w-xs">
                          <div className="truncate" title={ticket.title}>
                            {ticket.title}
                          </div>
                          {isFaded && (
                            <span className="text-xs text-muted-foreground block mt-0.5">
                              فقط مشاهده / بدون دسترسی
                            </span>
                          )}
                        </TableCell>
                        <TableCell>
                          <Badge className={`${getTicketStatusColor(ticket.displayStatus ?? ticket.status, "technician")}`}>
                            {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "technician")}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge className={`${priorityColors[ticket.priority]}`}>
                            {priorityLabels[ticket.priority]}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <span className="text-sm">
                            {ticket.categoryLabel ?? ticket.category}
                          </span>
                        </TableCell>
                        <TableCell>
                          <span className="text-sm text-muted-foreground">
                            {toFaDate(ticket.createdAt)}
                          </span>
                        </TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <div className="flex items-center gap-2">
                            <Button
                              size="sm"
                              variant="ghost"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleViewTicket(ticket.id)
                              }}
                            >
                              مشاهده
                            </Button>
                            {canAct && (
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={(e) => {
                                  e.stopPropagation()
                                  handleOpenResponse(ticket)
                                }}
                              >
                                پاسخ
                              </Button>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    )})
                  )}
                </TableBody>
              </Table>
            </div>
          </CardContent>
        </Card>
      <Dialog
        open={responseDialogOpen}
        onOpenChange={(open) => {
          setResponseDialogOpen(open)
          if (!open) {
            setResponseTicket(null)
            setResponseMessage("")
            setResponseStatus("Open")
            setResponseSubmitting(false)
          }
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">
              پاسخ به تیکت {responseTicket?.id}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-4 text-right">
            <div>
              <label className="text-sm text-muted-foreground">وضعیت جدید تیکت</label>
              <Select value={responseStatus} onValueChange={(value) => setResponseStatus(value as TicketStatus)}>
                <SelectTrigger className="mt-2 text-right">
                  <SelectValue placeholder="انتخاب وضعیت" />
                </SelectTrigger>
                <SelectContent>
                  {TICKET_STATUS_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <label className="text-sm text-muted-foreground">پیام پاسخ</label>
              <Textarea
                value={responseMessage}
                onChange={(e) => setResponseMessage(e.target.value)}
                placeholder="پاسخ خود را اینجا بنویسید..."
                rows={5}
                className="mt-2"
              />
            </div>
            <div className="flex justify-end gap-2">
              <Button
                variant="outline"
                onClick={() => setResponseDialogOpen(false)}
                disabled={responseSubmitting}
              >
                انصراف
              </Button>
              <Button
                onClick={handleSubmitResponse}
                disabled={responseSubmitting || !responseMessage.trim()}
              >
                {responseSubmitting ? "در حال ارسال..." : "ارسال پاسخ"}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={cardDialogOpen} onOpenChange={setCardDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">{cardDialogTitle}</DialogTitle>
          </DialogHeader>
          {cardDialogLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : cardDialogError ? (
            <div className="space-y-3 text-right">
              <p className="text-sm text-muted-foreground">{cardDialogError}</p>
              <Button size="sm" variant="outline" onClick={() => openCardDialog("all")}>
                تلاش دوباره
              </Button>
            </div>
          ) : cardDialogTickets.length === 0 ? (
            <div className="text-sm text-muted-foreground">تیکتی برای نمایش وجود ندارد.</div>
          ) : (
            <div className="max-h-[70vh] overflow-y-auto space-y-2">
              {cardDialogTickets.map((ticket) => (
                <div
                  key={ticket.id}
                  className="w-full border rounded-lg p-3 text-right cursor-default"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {ticket.isUnseen ? (
                        <span className="h-2 w-2 rounded-full bg-blue-500" />
                      ) : null}
                      <span className={ticket.isUnseen ? "font-bold" : ""}>
                        {ticket.id}
                      </span>
                    </div>
                    <Badge className={getTicketStatusColor(ticket.displayStatus ?? ticket.status, "technician")}>
                      {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "technician")}
                    </Badge>
                  </div>
                  <div className="mt-2 text-sm text-muted-foreground truncate" title={ticket.title}>
                    {ticket.title}
                  </div>
                </div>
              ))}
            </div>
          )}
        </DialogContent>
      </Dialog>
      </CardContent>
    </Card>
  )
}


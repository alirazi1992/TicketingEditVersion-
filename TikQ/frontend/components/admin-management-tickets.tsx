"use client"

import { useEffect, useMemo, useState } from "react"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Separator } from "@/components/ui/separator"
import { TicketCalendarOverview } from "@/components/ticket-calendar-overview"
import { AdminTicketList } from "@/components/admin-ticket-list"
import type {
  ApiAdminTicketDetailsDto,
  ApiAdminTicketListItemDto,
} from "@/lib/api-types"
import { getAdminArchiveTickets, getAdminTicketDetails, getAdminTickets } from "@/lib/admin-tickets-api"
import { apiRequest } from "@/lib/api-client"
import {
  getTicketStatusLabel,
  getTicketStatusColor,
  TICKET_STATUS_OPTIONS,
  type TicketStatus,
} from "@/lib/ticket-status"
import { mapUiStatusToApi } from "@/lib/ticket-mappers"
import { toast } from "@/hooks/use-toast"
import type { Ticket } from "@/types"
import { parseServerDate, toFaDate, toFaDateTime } from "@/lib/datetime"

interface AdminManagementTicketsProps {
  authToken?: string | null
  tickets?: Ticket[]
  onRefreshTickets?: () => void | Promise<void>
}

type TabKey = "recent" | "archive"

const formatDate = (value?: string | null) => toFaDate(value)

const formatDateTime = (value?: string | null) => toFaDateTime(value)

export function AdminManagementTickets({ authToken, tickets, onRefreshTickets }: AdminManagementTicketsProps) {
  const [activeTab, setActiveTab] = useState<TabKey>("recent")
  const [recentTickets, setRecentTickets] = useState<Ticket[]>([])
  const [archiveTickets, setArchiveTickets] = useState<ApiAdminTicketListItemDto[]>([])
  const [recentPage, setRecentPage] = useState(1)
  const [archivePage, setArchivePage] = useState(1)
  const [recentTotal, setRecentTotal] = useState(0)
  const [archiveTotal, setArchiveTotal] = useState(0)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [recentSearch, setRecentSearch] = useState("")
  const [archiveSearch, setArchiveSearch] = useState("")
  const [recentStatus, setRecentStatus] = useState<string>("all")
  const [archiveStatus, setArchiveStatus] = useState<string>("all")

  const [detailsOpen, setDetailsOpen] = useState(false)
  const [detailsLoading, setDetailsLoading] = useState(false)
  const [detailsError, setDetailsError] = useState<string | null>(null)
  const [details, setDetails] = useState<ApiAdminTicketDetailsDto | null>(null)

  const pageSize = 20
  const recentPages = Math.max(1, Math.ceil(recentTotal / pageSize))
  const archivePages = Math.max(1, Math.ceil(archiveTotal / pageSize))

  const loadTickets = async (tab: TabKey) => {
    // Cookie-based auth: token can be null; apiRequest uses credentials: 'include'
    setLoading(true)
    setError(null)
    try {
      if (tab === "archive") {
        const data = await getAdminArchiveTickets(authToken, 30, archivePage, pageSize)
        setArchiveTickets(data.items)
        setArchiveTotal(data.totalCount)
      } else {
        const data = await getAdminTickets(authToken, 30, recentPage, pageSize)
        setRecentTotal(data.totalCount)
      }
    } catch (err: any) {
      setError(err?.message || "خطا در دریافت تیکت‌ها")
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    loadTickets(activeTab)
  }, [activeTab, recentPage, archivePage, authToken])

  useEffect(() => {
    if (!tickets) return
    const cutoff = new Date()
    cutoff.setDate(cutoff.getDate() - 30)
    const recent = tickets.filter((ticket) => {
      const created = parseServerDate(ticket.createdAt)
      if (!created) return false
      return created >= cutoff
    })
    setRecentTickets(recent)
  }, [tickets])

  const handleOpenDetails = async (ticketId: string) => {
    setDetailsOpen(true)
    setDetails(null)
    setDetailsError(null)
    setDetailsLoading(true)
    try {
      const data = await getAdminTicketDetails(authToken, ticketId)
      setDetails(data)
    } catch (err: any) {
      setDetailsError(err?.message || "خطا در دریافت جزئیات")
    } finally {
      setDetailsLoading(false)
    }
  }

  const activeTickets = activeTab === "recent" ? recentTickets : archiveTickets

  const filteredTickets = useMemo(() => {
    const search = activeTab === "recent" ? recentSearch.trim().toLowerCase() : archiveSearch.trim().toLowerCase()
    const status = activeTab === "recent" ? recentStatus : archiveStatus
    return activeTickets.filter((ticket) => {
      const matchesSearch =
        !search ||
        ticket.title.toLowerCase().includes(search) ||
        ticket.id.toLowerCase().includes(search) ||
        ticket.categoryName.toLowerCase().includes(search) ||
        (ticket.subcategoryName ?? "").toLowerCase().includes(search)

      const matchesStatus = status === "all" || (ticket.displayStatus ?? ticket.status) === status
      return matchesSearch && matchesStatus
    })
  }, [activeTickets, activeTab, recentSearch, archiveSearch, recentStatus, archiveStatus])

  const renderTable = (tickets: ApiAdminTicketListItemDto[]) => (
    <div className="border rounded-lg overflow-hidden">
      <table className="w-full text-sm">
        <thead className="bg-muted/50">
          <tr className="text-right">
            <th className="p-3">شناسه</th>
            <th className="p-3">عنوان</th>
            <th className="p-3">وضعیت</th>
            <th className="p-3">دسته‌بندی</th>
            <th className="p-3">تاریخ ایجاد</th>
            <th className="p-3">آخرین فعالیت</th>
            <th className="p-3">تکنسین‌ها</th>
            <th className="p-3">عملیات</th>
          </tr>
        </thead>
        <tbody>
          {tickets.length === 0 ? (
            <tr>
              <td colSpan={8} className="p-6 text-center text-muted-foreground">
                تیکتی برای نمایش وجود ندارد.
              </td>
            </tr>
          ) : (
            tickets.map((ticket) => (
              <tr
                key={ticket.id}
                className="border-t cursor-pointer hover:bg-muted/50"
                onClick={() => handleOpenDetails(ticket.id)}
              >
                <td className="p-3 font-mono">{ticket.id.slice(0, 8)}</td>
                <td className="p-3">{ticket.title}</td>
                <td className="p-3">
                  <Badge className={getTicketStatusColor((ticket.displayStatus ?? ticket.status) as TicketStatus, "admin")}>
                    {getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "admin")}
                  </Badge>
                </td>
                <td className="p-3">
                  <div className="text-xs text-muted-foreground">{ticket.categoryName}</div>
                  {ticket.subcategoryName && (
                    <div className="text-xs">{ticket.subcategoryName}</div>
                  )}
                </td>
                <td className="p-3">{formatDate(ticket.createdAt)}</td>
                <td className="p-3">{formatDate(ticket.lastActivityAt)}</td>
                <td className="p-3">
                  {ticket.assignedTechnicians && ticket.assignedTechnicians.length > 0
                    ? ticket.assignedTechnicians.map((t) => t.name).join("، ")
                    : "—"}
                </td>
                <td className="p-3" onClick={(e) => e.stopPropagation()}>
                  <Button
                    type="button"
                    size="sm"
                    variant="outline"
                    className="font-iran"
                    onClick={(event) => {
                      event.stopPropagation()
                      handleOpenDetails(ticket.id)
                    }}
                  >
                    مشاهده
                  </Button>
                </td>
              </tr>
            ))
          )}
        </tbody>
      </table>
    </div>
  )

  const handleAdminTicketUpdate = async (ticketId: string, updates: Partial<Ticket>) => {
    if (updates.assignedTechnicians || updates.assignedTechnicianName) {
      setRecentTickets((prev) =>
        prev.map((ticket) => (ticket.id === ticketId ? { ...ticket, ...updates } : ticket))
      )
      await loadTickets(activeTab)
      await onRefreshTickets?.()
      return
    }
    const payload: Record<string, unknown> = {}
    if (updates.status) {
      payload.status = mapUiStatusToApi(updates.status as TicketStatus)
    }
    if (Object.keys(payload).length === 0) return
    await apiRequest(`/api/tickets/${ticketId}`, {
      method: "PATCH",
      token: authToken,
      body: payload,
    })
    setRecentTickets((prev) =>
      prev.map((ticket) => (ticket.id === ticketId ? { ...ticket, ...updates } : ticket))
    )
    await loadTickets(activeTab)
    await onRefreshTickets?.()
  }

  return (
    <Tabs value={activeTab} onValueChange={(val) => setActiveTab(val as TabKey)} dir="rtl">
      <Card>
        <CardHeader className="space-y-4">
          <CardTitle className="text-right font-iran">مدیریت تیکت‌ها</CardTitle>
          <TabsList>
            <TabsTrigger value="recent">۳۰ روز اخیر</TabsTrigger>
            <TabsTrigger value="archive">آرشیو</TabsTrigger>
          </TabsList>
        </CardHeader>
        <CardContent className="space-y-4">
          {error && (
            <div className="flex items-center justify-between rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">
              <span>{error}</span>
              <Button size="sm" variant="outline" onClick={() => loadTickets(activeTab)}>
                تلاش دوباره
              </Button>
            </div>
          )}
          {activeTab === "recent" ? (
            <TicketCalendarOverview tickets={recentTickets} />
          ) : null}
          {loading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : (
            <>
              <TabsContent value="recent">
                <AdminTicketList
                  tickets={recentTickets}
                  onTicketUpdate={handleAdminTicketUpdate}
                  authToken={authToken}
                />
              </TabsContent>
              <TabsContent value="archive">
                <div className="flex flex-wrap gap-3 items-center">
                  <Input
                    value={archiveSearch}
                    onChange={(event) => setArchiveSearch(event.target.value)}
                    placeholder="جستجو بر اساس عنوان، شناسه یا دسته‌بندی..."
                    className="text-right font-iran"
                  />
                  <Select
                    value={archiveStatus}
                    onValueChange={(value) => setArchiveStatus(value)}
                  >
                    <SelectTrigger className="min-w-[180px] text-right font-iran">
                      <SelectValue placeholder="فیلتر وضعیت" />
                    </SelectTrigger>
                    <SelectContent dir="rtl" className="font-iran">
                      <SelectItem value="all">همه وضعیت‌ها</SelectItem>
                      {TICKET_STATUS_OPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {renderTable(filteredTickets)}
              </TabsContent>
            </>
          )}

          {activeTab === "archive" ? (
            <div className="flex items-center justify-between text-sm">
              <div>
                صفحه {archivePage} از {archivePages}
              </div>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setArchivePage((p) => Math.max(1, p - 1))}
                  disabled={archivePage <= 1}
                >
                  قبلی
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => setArchivePage((p) => Math.min(archivePages, p + 1))}
                  disabled={archivePage >= archivePages}
                >
                  بعدی
                </Button>
              </div>
            </div>
          ) : null}
        </CardContent>
      </Card>

      {activeTab === "archive" ? (
        <Dialog open={detailsOpen} onOpenChange={setDetailsOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-5xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">جزئیات تیکت</DialogTitle>
          </DialogHeader>
          {detailsLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : detailsError ? (
            <div className="text-sm text-red-700">{detailsError}</div>
          ) : details ? (
            <div className="space-y-6">
              <div className="flex flex-wrap items-center gap-2">
                <Badge className={getTicketStatusColor(details.status as TicketStatus, "admin")}>
                  {getTicketStatusLabel(details.status as TicketStatus, "admin")}
                </Badge>
                <span className="text-sm text-muted-foreground">شناسه:</span>
                <span className="font-mono">{details.id}</span>
              </div>

              <div>
                <h3 className="text-lg font-iran">{details.title}</h3>
                <p className="text-sm text-muted-foreground mt-1">{details.description}</p>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <div className="space-y-1">
                  <div className="text-sm text-muted-foreground">دسته‌بندی</div>
                  <div>{details.categoryName}</div>
                  {details.subcategoryName && <div className="text-sm">{details.subcategoryName}</div>}
                </div>
                <div className="space-y-1">
                  <div className="text-sm text-muted-foreground">تاریخ ایجاد</div>
                  <div>{formatDateTime(details.createdAt)}</div>
                  <div className="text-sm text-muted-foreground">آخرین فعالیت</div>
                  <div>{formatDateTime(details.lastActivityAt)}</div>
                </div>
              </div>

              <div className="grid gap-3 md:grid-cols-3">
                <Card>
                  <CardContent className="pt-4 space-y-1 text-sm">
                    <div className="text-muted-foreground">زمان پاسخ اول</div>
                    <div>{details.timeToFirstResponse?.display ?? "N/A"}</div>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="pt-4 space-y-1 text-sm">
                    <div className="text-muted-foreground">زمان تا پاسخ نهایی</div>
                    <div>{details.timeToAnswered?.display ?? "N/A"}</div>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="pt-4 space-y-1 text-sm">
                    <div className="text-muted-foreground">زمان تا بستن</div>
                    <div>{details.timeToClosed?.display ?? "N/A"}</div>
                  </CardContent>
                </Card>
              </div>

              <div className="grid gap-3 md:grid-cols-2">
                <Card>
                  <CardContent className="pt-4 space-y-1 text-sm">
                    <div className="text-muted-foreground">اطلاعات درخواست‌کننده</div>
                    <div>{details.clientName}</div>
                    <div className="text-xs">{details.clientEmail}</div>
                    <div className="text-xs">{details.clientPhone ?? "—"}</div>
                  </CardContent>
                </Card>
                <Card>
                  <CardContent className="pt-4 space-y-1 text-sm">
                    <div className="text-muted-foreground">تکنسین‌ها</div>
                    <div>
                      {details.assignedTechnicians?.length
                        ? details.assignedTechnicians.map((t) => t.name).join("، ")
                        : "—"}
                    </div>
                    <div className="text-muted-foreground mt-2">پاسخ‌دهندگان</div>
                    <div>
                      {details.responders?.length
                        ? details.responders.map((r) => `${r.name} (${r.role})`).join("، ")
                        : "—"}
                    </div>
                  </CardContent>
                </Card>
              </div>

              <Separator />

              <div className="space-y-2">
                <div className="text-sm text-muted-foreground">تاریخچه پیام‌ها</div>
                <ScrollArea className="max-h-[40vh]">
                  <div className="space-y-3">
                    {details.messages?.length ? (
                      details.messages.map((msg) => (
                        <div key={msg.id} className="rounded-lg border p-3 text-sm">
                          <div className="flex items-center justify-between">
                            <span>{msg.authorName}</span>
                            <span className="text-xs text-muted-foreground">{formatDateTime(msg.createdAt)}</span>
                          </div>
                          <div className="text-xs text-muted-foreground">{msg.authorRole}</div>
                          <div className="mt-2">{msg.message}</div>
                        </div>
                      ))
                    ) : (
                      <div className="text-sm text-muted-foreground">پیامی ثبت نشده است.</div>
                    )}
                  </div>
                </ScrollArea>
              </div>
            </div>
          ) : null}
        </DialogContent>
        </Dialog>
      ) : null}
    </Tabs>
  )
}


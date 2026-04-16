"use client"

import { useEffect, useMemo, useRef, useState } from "react"
import { Button } from "@/components/ui/button"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Progress } from "@/components/ui/progress"
import { Badge } from "@/components/ui/badge"
import { toast } from "@/hooks/use-toast"
import { useAuth } from "@/lib/auth-context"
import {
  assignSupervisorTicket,
  getSupervisorAvailableTechnicians,
  getSupervisorAvailableTickets,
  getSupervisorTechnicianReport,
  getSupervisorTechnicianSummary,
  getSupervisorTechnicians,
  linkSupervisorTechnician,
  removeSupervisorAssignment,
  unlinkSupervisorTechnician,
} from "@/lib/supervisor-api"
import type {
  ApiSupervisorTechnicianWorkloadDto,
  ApiSupervisorTechnicianSummaryDto,
  ApiSupervisorTicketSummaryDto,
  ApiTechnicianResponse,
} from "@/lib/api-types"
import { Download, Plus, Trash2 } from "lucide-react"
import { getTicketStatusLabel, type TicketStatus } from "@/lib/ticket-status"

export function SupervisorTechnicianManagement() {
  const { token, user } = useAuth()
  const [listOpen, setListOpen] = useState(false) // Changed to false - don't auto-open
  const [detailOpen, setDetailOpen] = useState(false)
  const [assignOpen, setAssignOpen] = useState(false)
  const [linkOpen, setLinkOpen] = useState(false)
  const [loading, setLoading] = useState(false)
  const [items, setItems] = useState<ApiSupervisorTechnicianWorkloadDto[]>([])
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selectedTech, setSelectedTech] = useState<ApiSupervisorTechnicianWorkloadDto | null>(null)
  const [summary, setSummary] = useState<ApiSupervisorTechnicianSummaryDto | null>(null)
  const [summaryLoading, setSummaryLoading] = useState(false)
  const [summaryError, setSummaryError] = useState<string | null>(null)
  const [availableTickets, setAvailableTickets] = useState<ApiSupervisorTicketSummaryDto[]>([])
  const [availableTechs, setAvailableTechs] = useState<ApiTechnicianResponse[]>([])
  const [availableTechsError, setAvailableTechsError] = useState<string | null>(null)
  const [assignLoading, setAssignLoading] = useState(false)
  const [linkLoading, setLinkLoading] = useState(false)
  const [reportLoading, setReportLoading] = useState(false)
  const hasLoadedRef = useRef(false)

  const canUseSupervisorEndpoints =
    user?.role === "admin" || (user?.role === "technician" && user?.isSupervisor);

  const loadList = async () => {
    if (!user || !canUseSupervisorEndpoints) return
    try {
      setLoading(true)
      setLoadError(null)
      const raw = await getSupervisorTechnicians(token)
      if (process.env.NODE_ENV === "development") {
        const lastUrl = typeof window !== "undefined" ? (window as { __lastApiRequestUrl?: string }).__lastApiRequestUrl : undefined
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians resolved URL:", lastUrl, "raw:", typeof raw, Array.isArray(raw) ? `array[${(raw as unknown[]).length}]` : "", JSON.stringify(raw).slice(0, 400))
      }
      const data = Array.isArray(raw) ? raw : (raw as { items?: unknown[]; data?: unknown[] })?.items ?? (raw as { items?: unknown[]; data?: unknown[] })?.data ?? []
      const list = Array.isArray(data) ? data : []
      setItems(list as ApiSupervisorTechnicianWorkloadDto[])
      if (process.env.NODE_ENV === "development") {
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians normalized length:", list.length)
      }
    } catch (err: any) {
      if (process.env.NODE_ENV === "development") {
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians failed HTTP status:", err?.status, err?.statusText, err?.message)
      }
      const statusInfo = err?.status ? ` (${err.status} ${err.statusText || ""})` : ""
      const errorMsg = err?.message || "لطفاً دوباره تلاش کنید"
      setLoadError(`خطا در بارگذاری تکنسین‌ها${statusInfo}: ${errorMsg}`)
      if (err?.status !== 404) {
        toast({
          title: "خطا در بارگذاری تکنسین‌ها",
          description: errorMsg,
          variant: "destructive",
        })
      }
    } finally {
      setLoading(false)
    }
  }

  const loadSummary = async (technicianUserId: string) => {
    if (!user || !canUseSupervisorEndpoints) return
    try {
      setSummaryLoading(true)
      setSummaryError(null)
      const data = await getSupervisorTechnicianSummary(token, technicianUserId)
      setSummary(data)
    } catch (err: any) {
      const statusInfo = err?.status ? ` (${err.status} ${err.statusText || ""})` : ""
      setSummaryError(`خطا در دریافت اطلاعات${statusInfo}: ${err?.message || "لطفاً دوباره تلاش کنید"}`)
      setSummary(null)
    } finally {
      setSummaryLoading(false)
    }
  }

  const loadAvailableTickets = async () => {
    if (!user || !canUseSupervisorEndpoints) return
    try {
      const data = await getSupervisorAvailableTickets(token)
      setAvailableTickets(data)
    } catch (err: any) {
      const statusInfo = err?.status ? ` (${err.status})` : ""
      toast({
        title: "خطا در بارگذاری تیکت‌ها",
        description: `${err?.message || "لطفاً دوباره تلاش کنید"}${statusInfo}`,
        variant: "destructive",
      })
    }
  }

  const loadAvailableTechs = async () => {
    if (!user || !canUseSupervisorEndpoints) return
    try {
      setLinkLoading(true)
      setAvailableTechsError(null)
      const raw = await getSupervisorAvailableTechnicians(token)
      if (process.env.NODE_ENV === "development") {
        const lastUrl = typeof window !== "undefined" ? (window as { __lastApiRequestUrl?: string }).__lastApiRequestUrl : undefined
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians/available resolved URL:", lastUrl, "raw:", typeof raw, Array.isArray(raw) ? `array[${(raw as unknown[]).length}]` : "", JSON.stringify(raw).slice(0, 400))
      }
      // Normalize: Array | { items: Array } | { data: Array }
      const data = Array.isArray(raw) ? raw : (raw as { items?: unknown[]; data?: unknown[] })?.items ?? (raw as { items?: unknown[]; data?: unknown[] })?.data ?? []
      const list = Array.isArray(data) ? data : []
      setAvailableTechs(list as ApiTechnicianResponse[])
      if (process.env.NODE_ENV === "development") {
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians/available normalized length:", list.length)
      }
    } catch (err: any) {
      if (process.env.NODE_ENV === "development") {
        console.warn("[SUPERVISOR_DEV] GET /api/supervisor/technicians/available failed HTTP status:", err?.status, err?.statusText, err?.message)
      }
      const statusInfo = err?.status ? ` (${err.status} ${err.statusText || ""})` : ""
      const errorMsg = `خطا در بارگذاری تکنسین‌های قابل انتخاب${statusInfo}: ${err?.message || "لطفاً دوباره تلاش کنید"}`
      setAvailableTechsError(errorMsg)
      if (err?.status !== 404) {
        toast({
          title: "خطا در بارگذاری تکنسین‌های قابل انتخاب",
          description: err?.message || "لطفاً دوباره تلاش کنید",
          variant: "destructive",
        })
      }
    } finally {
      setLinkLoading(false)
    }
  }

  // Load list once when session is ready (user set via cookie auth; token may be null)
  useEffect(() => {
    if (user && !hasLoadedRef.current) {
      hasLoadedRef.current = true
      if (canUseSupervisorEndpoints) {
        void loadList()
      } else {
        setLoadError("دسترسی فقط برای سرپرست یا مدیر سیستم مجاز است.")
      }
    }
  }, [user, canUseSupervisorEndpoints])

  // Load available techs only when dialog opens
  useEffect(() => {
    if (linkOpen && user && canUseSupervisorEndpoints) {
      void loadAvailableTechs()
    }
  }, [linkOpen, user, canUseSupervisorEndpoints])

  const handleOpenDetail = async (tech: ApiSupervisorTechnicianWorkloadDto) => {
    setSelectedTech(tech)
    setDetailOpen(true)
    await loadSummary(tech.technicianUserId)
  }

  const handleAssign = async (ticketId: string) => {
    if (!user || !selectedTech) return
    
    // Validate ticketId (must be a Guid, not empty)
    if (!ticketId || ticketId.trim() === "") {
      toast({
        title: "خطا",
        description: "لطفاً یک تیکت انتخاب کنید",
        variant: "destructive",
      })
      return
    }
    
    try {
      setAssignLoading(true)
      
      // Log the payload being sent
      console.log("[handleAssign] Assigning ticket:", {
        ticketId,
        technicianUserId: selectedTech.technicianUserId,
      });
      
      await assignSupervisorTicket(token, selectedTech.technicianUserId, ticketId)
      toast({ title: "تیکت واگذار شد" })
      await loadSummary(selectedTech.technicianUserId)
      await loadList()
      setAssignOpen(false)
    } catch (err: any) {
      console.error("[handleAssign] Error assigning ticket:", {
        ticketId,
        technicianUserId: selectedTech?.technicianUserId,
        status: err?.status,
        statusText: err?.statusText,
        message: err?.message,
        body: err?.body,
        rawText: err?.rawText?.substring(0, 200),
        details: err?.body?.details,
      });
      
      const errorMessage = err?.body?.message || err?.message || "لطفاً دوباره تلاش کنید";
      const errorDetails = err?.body?.details;
      
      toast({
        title: "خطا در واگذاری تیکت",
        description: errorDetails ? `${errorMessage}\n${errorDetails}` : errorMessage,
        variant: "destructive",
      })
    } finally {
      setAssignLoading(false)
    }
  }

  const handleRemoveAssignment = async (ticketId: string) => {
    if (!user || !selectedTech) return
    try {
      await removeSupervisorAssignment(token, selectedTech.technicianUserId, ticketId)
      toast({ title: "واگذاری حذف شد" })
      await loadSummary(selectedTech.technicianUserId)
      await loadList()
    } catch (err: any) {
      toast({
        title: "خطا در حذف واگذاری",
        description: err?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const handleDownloadReport = async () => {
    if (!user || !selectedTech) return;
    
    try {
      setReportLoading(true);
      
      console.log("[handleDownloadReport] Downloading report for technician:", {
        technicianUserId: selectedTech.technicianUserId,
        technicianName: selectedTech.technicianName,
      });
      
      const blob = await getSupervisorTechnicianReport(token, selectedTech.technicianUserId);
      
      // Generate filename with timestamp
      const timestamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
      const filename = `technician-report-${selectedTech.technicianName}-${timestamp}.csv`;
      
      // Create download link
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = filename;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
      
      toast({
        title: "گزارش دانلود شد",
        description: `فایل ${filename} با موفقیت دانلود شد`,
      });
      
      console.log("[handleDownloadReport] Report downloaded successfully:", filename);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : String(err);
      const status = (err as { status?: number })?.status;
      const statusText = (err as { statusText?: string })?.statusText;
      console.error("[handleDownloadReport] Failed to download report:", {
        message,
        status,
        statusText,
        technicianUserId: selectedTech?.technicianUserId,
      });
      toast({
        title: "خطا در دریافت گزارش",
        description: message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      });
    } finally {
      setReportLoading(false);
    }
  }

  const handleLinkTechnician = async (technicianUserId: string) => {
    if (!user) return
    try {
      // POST /api/supervisor/technicians/{technicianUserId}/link must use user id, not technician entity id
      await linkSupervisorTechnician(token, technicianUserId)
      toast({ title: "تکنسین اضافه شد" })
      await loadList()
      await loadAvailableTechs()
      setLinkOpen(false)
    } catch (err: any) {
      toast({
        title: "خطا در افزودن تکنسین",
        description: err?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const handleUnlinkTechnician = async (technicianUserId: string) => {
    if (!user) return
    try {
      await unlinkSupervisorTechnician(token, technicianUserId)
      toast({ title: "تکنسین حذف شد" })
      await loadList()
    } catch (err: any) {
      toast({
        title: "خطا در حذف تکنسین",
        description: err?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const assignedTicketIds = useMemo(() => {
    if (!summary) return new Set<string>()
    return new Set(summary.activeTickets.map((t) => t.ticketId))
  }, [summary])

  const linkedTechnicianIds = useMemo(() => {
    return new Set(items.map((tech) => tech.technicianUserId))
  }, [items])

  /** Use for any date display; avoids rendering DateTime.MinValue from backend. */
  const formatOptionalDate = (value: string | undefined | null): string => {
    if (value == null || value === "" || value.startsWith("0001-01-01")) return "—"
    return value
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold">مدیریت تکنسین‌ها</h2>
        <div className="flex gap-2">
          <Button variant="outline" onClick={() => setLinkOpen(true)}>
            افزودن تکنسین
          </Button>
          <Button onClick={() => setListOpen(true)}>نمایش لیست</Button>
        </div>
      </div>

      {/* Main page content */}
      <div className="rounded-lg border p-6">
        {!user ? (
          <div className="text-center text-muted-foreground">
            لطفاً وارد شوید
          </div>
        ) : loading ? (
          <div className="text-center text-muted-foreground">
            در حال بارگذاری...
          </div>
        ) : loadError ? (
          <div className="space-y-3 text-center">
            <div className="text-sm text-destructive">{loadError}</div>
            <Button size="sm" variant="outline" onClick={() => void loadList()}>
              تلاش مجدد
            </Button>
          </div>
        ) : items.length === 0 ? (
          <div className="text-center space-y-3">
            <div className="text-muted-foreground">تکنسینی تحت مدیریت شما نیست. با «افزودن تکنسین» تکنسین‌های قابل واگذاری را لینک کنید.</div>
            <Button variant="outline" onClick={() => setLinkOpen(true)}>
              افزودن تکنسین
            </Button>
          </div>
        ) : (
          <div className="space-y-2">
            <div className="text-sm text-muted-foreground mb-4">
              {items.length} تکنسین تحت مدیریت
            </div>
            {items.map((tech) => (
              <div
                key={tech.technicianUserId}
                className="flex items-center justify-between p-3 border rounded hover:bg-accent cursor-pointer"
                onClick={() => {
                  setSelectedTech(tech)
                  setDetailOpen(true)
                  void loadSummary(tech.technicianUserId)
                }}
              >
                <div>
                  <div className="font-medium">{tech.technicianName}</div>
                  <div className="text-xs text-muted-foreground">
                    {tech.inboxLeft} باقی مانده از {tech.inboxTotal}
                  </div>
                </div>
                <div className="text-sm text-muted-foreground">
                  {tech.workloadPercent}%
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      <Dialog open={listOpen} onOpenChange={setListOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl" dir="rtl">
          <DialogHeader>
            <DialogTitle>تکنسین‌های تحت مدیریت</DialogTitle>
          </DialogHeader>
          {loading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : loadError ? (
            <div className="space-y-3">
              <div className="text-sm text-destructive">{loadError}</div>
              <Button size="sm" variant="outline" onClick={() => void loadList()}>
                تلاش مجدد
              </Button>
            </div>
          ) : items.length === 0 ? (
            <div className="space-y-1">
              <div className="text-sm text-muted-foreground">تکنسینی تحت مدیریت شما نیست. از «افزودن تکنسین» برای لینک کردن تکنسین‌ها استفاده کنید.</div>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>نام</TableHead>
                  <TableHead>بار کاری</TableHead>
                  <TableHead>عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {items.map((tech) => (
                  <TableRow key={tech.technicianUserId}>
                    <TableCell>{tech.technicianName}</TableCell>
                    <TableCell className="space-y-2">
                      <Progress value={tech.workloadPercent} />
                      <div className="text-xs text-muted-foreground">
                        {tech.inboxLeft} باقی مانده از {tech.inboxTotal}
                      </div>
                    </TableCell>
                    <TableCell className="flex gap-2">
                      <Button size="sm" variant="outline" onClick={() => handleOpenDetail(tech)}>
                        جزئیات
                      </Button>
                      <Button
                        size="sm"
                        variant="ghost"
                        onClick={() => handleUnlinkTechnician(tech.technicianUserId)}
                      >
                        <Trash2 className="h-4 w-4" />
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={detailOpen} onOpenChange={setDetailOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-5xl" dir="rtl">
          <DialogHeader>
            <DialogTitle>{selectedTech?.technicianName ?? "جزئیات تکنسین"}</DialogTitle>
          </DialogHeader>
          {summaryLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : summaryError ? (
            <div className="text-sm text-muted-foreground">{summaryError}</div>
          ) : summary ? (
            <div className="space-y-6">
              <div className="flex justify-end">
                <Button 
                  variant="outline" 
                  onClick={handleDownloadReport} 
                  disabled={reportLoading}
                  className="gap-2"
                >
                  <Download className="h-4 w-4" />
                  {reportLoading ? "در حال دانلود..." : "دانلود گزارش"}
                </Button>
              </div>

              <div>
                <h3 className="font-medium mb-2">آرشیو تیکت‌ها</h3>
                {summary.archiveTickets.length === 0 ? (
                  <div className="text-sm text-muted-foreground">آرشیوی وجود ندارد.</div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>شناسه</TableHead>
                        <TableHead>عنوان</TableHead>
                        <TableHead>وضعیت</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {summary.archiveTickets.map((ticket, index) => {
                        const key = ticket.id ?? (ticket.ticketId ? `${ticket.ticketId}-${index}` : `archive-${index}`);
                        return (
                          <TableRow key={key}>
                            <TableCell className="font-mono text-sm">{ticket.ticketId}</TableCell>
                            <TableCell>{ticket.title}</TableCell>
                            <TableCell>
                              <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                )}
              </div>

              <div>
                <div className="flex items-center justify-between mb-2">
                  <h3 className="font-medium">تیکت‌های فعال</h3>
                  <Button
                    size="sm"
                    variant="outline"
                    onClick={async () => {
                      await loadAvailableTickets()
                      setAssignOpen(true)
                    }}
                  >
                    <Plus className="h-4 w-4 mr-1" />
                    افزودن تیکت
                  </Button>
                </div>
                {summary.activeTickets.length === 0 ? (
                  <div className="text-sm text-muted-foreground">تیکت فعالی وجود ندارد.</div>
                ) : (
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>شناسه</TableHead>
                        <TableHead>عنوان</TableHead>
                        <TableHead>وضعیت</TableHead>
                        <TableHead>عملیات</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {summary.activeTickets.map((ticket, index) => {
                        const key = ticket.id ?? (ticket.ticketId ? `${ticket.ticketId}-${index}` : `active-${index}`);
                        return (
                          <TableRow key={key}>
                            <TableCell className="font-mono text-sm">{ticket.ticketId}</TableCell>
                            <TableCell>{ticket.title}</TableCell>
                            <TableCell>
                              <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                            </TableCell>
                            <TableCell>
                              <Button
                                size="sm"
                                variant="ghost"
                                onClick={() => handleRemoveAssignment(ticket.id ?? ticket.ticketId)}
                              >
                                <Trash2 className="h-4 w-4" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        );
                      })}
                    </TableBody>
                  </Table>
                )}
              </div>
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      <Dialog open={assignOpen} onOpenChange={setAssignOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl" dir="rtl">
          <DialogHeader>
            <DialogTitle>انتخاب تیکت برای واگذاری</DialogTitle>
          </DialogHeader>
          {assignLoading ? (
            <div className="text-sm text-muted-foreground">در حال ثبت...</div>
          ) : availableTickets.length === 0 ? (
            <div className="text-sm text-muted-foreground">تیکتی برای واگذاری وجود ندارد.</div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>شناسه</TableHead>
                  <TableHead>عنوان</TableHead>
                  <TableHead>وضعیت</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {availableTickets
                  .filter((ticket) => !assignedTicketIds.has(ticket.ticketId))
                  .map((ticket, index) => (
                    <TableRow key={ticket.id || ticket.ticketId || `ticket-${index}`}>
                      <TableCell className="font-mono text-sm">{ticket.ticketId}</TableCell>
                      <TableCell>{ticket.title}</TableCell>
                      <TableCell>
                        <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                      </TableCell>
                      <TableCell>
                        <Button size="sm" onClick={() => handleAssign(ticket.id)}>
                          انتخاب
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={linkOpen} onOpenChange={setLinkOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle>افزودن تکنسین</DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          {linkLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : availableTechsError ? (
            <div className="space-y-3">
              <div className="text-sm text-destructive">{availableTechsError}</div>
              <Button size="sm" variant="outline" onClick={() => void loadAvailableTechs()}>
                تلاش مجدد
              </Button>
            </div>
          ) : availableTechs.length === 0 ? (
            <div className="space-y-1">
              <div className="text-sm text-muted-foreground">همهٔ تکنسین‌های فعال قبلاً به شما لینک شده‌اند یا در سیستم تکنسینی با نقش «تکنسین» وجود ندارد. از بخش مدیریت کاربران (ادمین) تکنسین اضافه کنید.</div>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>نام</TableHead>
                  <TableHead>ایمیل</TableHead>
                  <TableHead />
                </TableRow>
              </TableHeader>
              <TableBody>
                {availableTechs
                  .filter((tech) => {
                    const uid = (tech as ApiTechnicianResponse & { technicianUserId?: string }).userId ?? (tech as ApiTechnicianResponse & { technicianUserId?: string }).technicianUserId ?? tech.id
                    return !linkedTechnicianIds.has(uid)
                  })
                  .map((tech) => {
                    const selectedTechnicianUserId =
                      (tech as ApiTechnicianResponse & { technicianUserId?: string }).userId ??
                      (tech as ApiTechnicianResponse & { technicianUserId?: string }).technicianUserId ??
                      tech.id
                    const displayName =
                      (tech as ApiTechnicianResponse & { technicianName?: string }).fullName ??
                      (tech as ApiTechnicianResponse & { technicianName?: string }).technicianName ??
                      (tech as ApiTechnicianResponse).email ??
                      "(unknown)"
                    if (process.env.NODE_ENV === "development" && !(tech as { userId?: string | null }).userId) {
                      console.warn("[SUPERVISOR_DEV] Available technician missing userId; using fallback:", {
                        id: tech.id,
                        fullName: (tech as ApiTechnicianResponse).fullName,
                        selectedTechnicianUserId,
                      })
                    }
                    return (
                      <TableRow key={String(selectedTechnicianUserId)}>
                        <TableCell>{displayName}</TableCell>
                        <TableCell>{(tech as ApiTechnicianResponse).email ?? ""}</TableCell>
                        <TableCell>
                          <Button size="sm" onClick={() => handleLinkTechnician(selectedTechnicianUserId)}>
                            افزودن
                          </Button>
                        </TableCell>
                      </TableRow>
                    )
                  })}
              </TableBody>
            </Table>
          )}
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}


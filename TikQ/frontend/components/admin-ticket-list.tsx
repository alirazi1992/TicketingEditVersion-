"use client"

import React from "react"

import { useEffect, useMemo, useState, useRef } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { ScrollArea } from "@/components/ui/scroll-area"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Checkbox } from "@/components/ui/checkbox"
import { toast } from "@/hooks/use-toast"
import {
  Search,
  Filter,
  Eye,
  Download,
  Printer,
  Calendar,
  MessageSquare,
  FileText,
  User,
  Phone,
  Clock,
  AlertCircle,
  CheckCircle,
  XCircle,
  Paperclip,
  Settings,
  Mail,
  ChevronDown,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"
import {
  TICKET_STATUS_LABELS,
  TICKET_STATUS_OPTIONS,
  getTicketStatusLabel,
  getStatusOptionsForRole,
  statusForUi,
  type TicketStatus,
} from "@/lib/ticket-status"
import { apiRequest, apiGetNoStore } from "@/lib/api-client"
import {
  autoAssignAdminTicket,
  getAdminTechnicianDirectory,
  manualAssignAdminTicket,
} from "@/lib/admin-tickets-api"
import type { ApiAdminTechnicianDirectoryItemDto, ApiTicketMessageDto } from "@/lib/api-types"
import { mapUiStatusToApi } from "@/lib/ticket-mappers"
import { formatFaDate, formatFaDateTime, formatFaTime, parseServerDate } from "@/lib/datetime"

const statusColors: Record<string, string> = {
  Submitted: "bg-blue-100 text-blue-800 border-blue-200",
  SeenRead: "bg-purple-100 text-purple-800 border-purple-200",
  Open: "bg-red-100 text-red-800 border-red-200",
  InProgress: "bg-yellow-100 text-yellow-800 border-yellow-200",
  Solved: "bg-green-100 text-green-800 border-green-200",
  Redo: "bg-orange-100 text-orange-800 border-orange-200",
  Answered: "bg-teal-100 text-teal-800 border-teal-200",
  Resolved: "bg-green-100 text-green-800 border-green-200",
  Closed: "bg-gray-100 text-gray-800 border-gray-200",
}

const statusLabels: Record<TicketStatus, string> = TICKET_STATUS_LABELS

const statusIcons: Record<string, LucideIcon> = {
  Submitted: AlertCircle,
  SeenRead: Eye,
  Open: AlertCircle,
  InProgress: Clock,
  Solved: CheckCircle,
  Redo: AlertCircle,
  Answered: CheckCircle,
  Resolved: CheckCircle,
  Closed: XCircle,
}

const priorityColors: Record<string, string> = {
  low: "bg-blue-100 text-blue-800 border-blue-200",
  medium: "bg-orange-100 text-orange-800 border-orange-200",
  high: "bg-red-100 text-red-800 border-red-200",
  urgent: "bg-purple-100 text-purple-800 border-purple-200",
}

const priorityLabels: Record<string, string> = {
  low: "کم",
  medium: "متوسط",
  high: "بالا",
  urgent: "فوری",
}

const categoryLabels: Record<string, string> = {
  hardware: "سخت‌افزار",
  software: "نرم‌افزار",
  network: "شبکه",
  email: "ایمیل",
  security: "امنیت",
  access: "دسترسی",
}
const getCategoryLabel = (ticketOrId: any) => {
  const id = typeof ticketOrId === "string" ? ticketOrId : ticketOrId?.category
  if (typeof ticketOrId === "object" && ticketOrId?.categoryLabel) {
    return ticketOrId.categoryLabel
  }
  return categoryLabels[id] ?? id
}

type CanonicalStatus = "open" | "seen" | "review" | "in_progress" | "solved" | "other"

const normalizeStatus = (input: unknown): CanonicalStatus => {
  if (!input) return "other"
  const raw = String(input).toLowerCase()
  if (raw.includes("open") || raw === "submitted") return "open"
  if (raw.includes("seen")) return "seen"
  if (raw.includes("redo") || raw.includes("review")) return "review"
  if (raw.includes("progress")) return "in_progress"
  if (raw.includes("resolved") || raw.includes("solved") || raw.includes("closed")) return "solved"
  return "other"
}

type AssignedTechnicianItem = {
  id?: string
  userId?: string
  name?: string
  fullName?: string
  role?: string
  isSupervisor?: boolean
  isActive?: boolean
}

const normalizeNameList = (value: unknown): string[] => {
  if (Array.isArray(value)) {
    return value
      .flatMap((item) => (typeof item === "string" ? [item] : []))
      .map((item) => item.trim())
      .filter(Boolean)
  }
  if (typeof value === "string") {
    const trimmed = value.trim()
    if (!trimmed) return []
    return trimmed
      .split(/[\n,]/g)
      .map((item) => item.trim())
      .filter(Boolean)
  }
  return []
}

const normalizeAssignedTechnicians = (ticket: any): AssignedTechnicianItem[] => {
  const raw = ticket?.assignedTechnicians ?? ticket?.technicians ?? ticket?.technician

  if (Array.isArray(raw)) {
    return raw
      .map((item: any) => {
        if (typeof item === "string") {
          return { name: item }
        }
        if (item && typeof item === "object") {
          return {
            id: item.id,
            userId: item.userId ?? item.technicianUserId,
            name: item.name ?? item.technicianName,
            fullName: item.fullName,
            role: item.role,
            isSupervisor: item.isSupervisor,
            isActive: item.isActive,
          }
        }
        return null
      })
      .filter(Boolean)
  }

  if (raw && typeof raw === "object") {
    return [
      {
        id: raw.id,
        userId: raw.userId ?? raw.technicianUserId,
        name: raw.name ?? raw.technicianName,
        fullName: raw.fullName,
        role: raw.role,
        isSupervisor: raw.isSupervisor,
        isActive: raw.isActive,
      },
    ]
  }

  const rawNames = normalizeNameList(
    ticket?.assignedTechnicianName ??
      ticket?.technicianName ??
      ticket?.assignedTechnicians ??
      ticket?.technicians ??
      ticket?.technician
  )
  if (rawNames.length > 0) {
    return rawNames.map((name) => ({ name }))
  }

  return []
}

interface AdminTicketListProps {
  tickets: any[]
  onTicketUpdate: (ticketId: string, updates: any) => void
  authToken?: string | null
}

export function AdminTicketList({ tickets, onTicketUpdate, authToken }: AdminTicketListProps) {
  const [searchQuery, setSearchQuery] = useState("")
  const [filterStatus, setFilterStatus] = useState("all")
  const [filterPriority, setFilterPriority] = useState("all")
  const [filterCategory, setFilterCategory] = useState("all")
  const [selectedTickets, setSelectedTickets] = useState<string[]>([])
  const [selectedTicket, setSelectedTicket] = useState<any>(null)
  const [viewDialogOpen, setViewDialogOpen] = useState(false)
  const [previewMessages, setPreviewMessages] = useState<ApiTicketMessageDto[] | null>(null)
  const [previewMessagesLoading, setPreviewMessagesLoading] = useState(false)
  const [replyMessage, setReplyMessage] = useState("")
  const [replyStatus, setReplyStatus] = useState<TicketStatus>("Open")
  const [replySubmitting, setReplySubmitting] = useState(false)
  const [assignDialogOpen, setAssignDialogOpen] = useState(false)
  const [assignTicket, setAssignTicket] = useState<any>(null)
  const [assignLoading, setAssignLoading] = useState(false)
  const [assignError, setAssignError] = useState<string | null>(null)
  const [directoryLoading, setDirectoryLoading] = useState(false)
  const [directoryError, setDirectoryError] = useState<string | null>(null)
  const [directoryItems, setDirectoryItems] = useState<ApiAdminTechnicianDirectoryItemDto[]>([])
  const [directorySearch, setDirectorySearch] = useState("")
  const [directoryFilter, setDirectoryFilter] = useState<"all" | "Free" | "Busy" | "expertise">("all")
  const [expertiseCategoryId, setExpertiseCategoryId] = useState<number | null>(null)
  const [expertiseSubcategoryId, setExpertiseSubcategoryId] = useState<number | null>(null)
  const [selectedTechnicians, setSelectedTechnicians] = useState<string[]>([])
  const [autoAssigning, setAutoAssigning] = useState<Record<string, boolean>>({})

  const [techListOpen, setTechListOpen] = useState(false)
  const [techListTicketId, setTechListTicketId] = useState<string | null>(null)
  const [techList, setTechList] = useState<AssignedTechnicianItem[]>([])

  // Prevent preview modal from closing right after reply/status mutation (Radix may fire onOpenChange(false) when focus moves to toast)
  const previewCloseGuardUntilRef = useRef(0)

  // Filter tickets: use statusForUi so dropdown (API enum values) matches API response
  const filteredTickets = tickets.filter((ticket) => {
    const matchesSearch =
      (ticket.title ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
      (ticket.description ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
      (ticket.id ?? "").toLowerCase().includes(searchQuery.toLowerCase()) ||
      (ticket.clientName ?? "").toLowerCase().includes(searchQuery.toLowerCase())

    const matchesStatus = filterStatus === "all" || statusForUi(ticket) === filterStatus
    const matchesPriority = filterPriority === "all" || ticket.priority === filterPriority
    const matchesCategory = filterCategory === "all" || ticket.category === filterCategory

    return matchesSearch && matchesStatus && matchesPriority && matchesCategory
  })

  const loadTechnicianDirectory = async () => {
    if (!authToken) return
    setDirectoryLoading(true)
    setDirectoryError(null)
    try {
      const items = await getAdminTechnicianDirectory(authToken, {
        search: directorySearch.trim() || undefined,
        availability: directoryFilter === "expertise" ? "all" : directoryFilter,
        categoryId: directoryFilter === "expertise" ? expertiseCategoryId ?? undefined : undefined,
        subcategoryId: directoryFilter === "expertise" ? expertiseSubcategoryId ?? undefined : undefined,
      })
      setDirectoryItems(items)
    } catch (error: any) {
      setDirectoryError(error?.message || "خطا در دریافت فهرست تکنسین‌ها")
    } finally {
      setDirectoryLoading(false)
    }
  }

  useEffect(() => {
    if (!assignDialogOpen) return
    loadTechnicianDirectory()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [assignDialogOpen, directorySearch, directoryFilter, expertiseCategoryId, expertiseSubcategoryId])

  const directoryCategories = useMemo(() => {
    const map = new Map<number, { id: number; name: string }>()
    directoryItems.forEach((item) => {
      item.expertise?.forEach((tag) => {
        map.set(tag.categoryId, { id: tag.categoryId, name: tag.categoryName })
      })
    })
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name))
  }, [directoryItems])

  const directorySubcategories = useMemo(() => {
    if (!expertiseCategoryId) return []
    const map = new Map<number, { id: number; name: string }>()
    directoryItems.forEach((item) => {
      item.expertise?.forEach((tag) => {
        if (tag.categoryId === expertiseCategoryId) {
          map.set(tag.subcategoryId, { id: tag.subcategoryId, name: tag.subcategoryName })
        }
      })
    })
    return Array.from(map.values()).sort((a, b) => a.name.localeCompare(b.name))
  }, [directoryItems, expertiseCategoryId])

  const handleViewTicket = (ticket: any) => {
    setSelectedTicket(ticket)
    setReplyMessage("")
    setPreviewMessages(null)
    setReplyStatus((statusForUi(ticket) as TicketStatus) || "Open")
    setViewDialogOpen(true)
  }

  const getTechRoleLabel = (tech: AssignedTechnicianItem) => {
    if (tech.isSupervisor || tech.role === "SupervisorTechnician") return "سرپرست"
    if (tech.role === "Technician") return "تکنسین"
    return tech.role || "تکنسین"
  }

  const openTechListDialog = (ticket: any) => {
    const list = normalizeAssignedTechnicians(ticket)
    setTechListTicketId(ticket.id)
    setTechList(list)
    setTechListOpen(true)
  }

  // Fetch full conversation when preview dialog opens (cookie or token auth)
  useEffect(() => {
    if (!viewDialogOpen || !selectedTicket?.id) return
    setPreviewMessagesLoading(true)
    apiGetNoStore<ApiTicketMessageDto[]>(`/api/tickets/${selectedTicket.id}/messages`, {
      token: authToken ?? undefined,
    })
      .then((list) => setPreviewMessages(list ?? []))
      .catch(() => setPreviewMessages([]))
      .finally(() => setPreviewMessagesLoading(false))
  }, [viewDialogOpen, selectedTicket?.id, authToken])

  const handleOpenAssignDialog = (ticket: any) => {
    setAssignTicket(ticket)
    setAssignError(null)
    setSelectedTechnicians([])
    setDirectoryFilter("all")
    setDirectorySearch("")
    setExpertiseCategoryId(null)
    setExpertiseSubcategoryId(null)
    setAssignDialogOpen(true)
  }

  const handleAutoAssign = async (ticket: any) => {
    if (!authToken) {
      toast({
        title: "عدم دسترسی",
        description: "لطفاً دوباره وارد شوید.",
        variant: "destructive",
      })
      return
    }
    setAutoAssigning((prev) => ({ ...prev, [ticket.id]: true }))
    try {
      const existingIds = (ticket.assignedTechnicians ?? []).map((t: any) => t.technicianUserId)
      const result = await autoAssignAdminTicket(authToken, ticket.id, {
        categoryId: ticket.categoryId,
        subcategoryId: ticket.subcategoryId,
        existingTechnicianUserIds: existingIds,
      })
      onTicketUpdate(ticket.id, {
        assignedTechnicians: result.assignees,
        assignedTechnicianName: result.assignees?.[0]?.name ?? null,
      })
      toast({
        title: "تخصیص انجام شد",
        description: result.addedTechnicians?.length
          ? `${result.addedTechnicians.length} تکنسین به تیکت اضافه شد.`
          : "تکنسین جدیدی برای تخصیص یافت نشد.",
      })
    } catch (error: any) {
      const status = error?.status ?? (error as any)?.status;
      const message = error?.body?.message || error?.message || "خطا در تخصیص خودکار";
      toast({
        title: "تخصیص ناموفق بود",
        description: status ? `(${status}) ${message}` : message,
        variant: "destructive",
      });
    } finally {
      setAutoAssigning((prev) => ({ ...prev, [ticket.id]: false }))
    }
  }

  const handleManualAssign = async () => {
    if (!assignTicket?.id) return
    if (!authToken) {
      toast({
        title: "عدم دسترسی",
        description: "لطفاً دوباره وارد شوید.",
        variant: "destructive",
      })
      return
    }
    const alreadyAssigned = new Set(
      (assignTicket.assignedTechnicians ?? []).map((t: any) => t.technicianUserId)
    )
    const targetIds = selectedTechnicians.filter((id) => !alreadyAssigned.has(id))
    if (targetIds.length === 0) {
      toast({
        title: "انتخابی وجود ندارد",
        description: "تکنسین جدیدی برای تخصیص انتخاب نشده است.",
      })
      return
    }
    setAssignLoading(true)
    setAssignError(null)
    try {
      const result = await manualAssignAdminTicket(
        authToken,
        assignTicket.id,
        targetIds,
        Array.from(alreadyAssigned)
      )
      onTicketUpdate(assignTicket.id, {
        assignedTechnicians: result.assignees,
        assignedTechnicianName: result.assignees?.[0]?.name ?? null,
      })
      toast({
        title: "تخصیص انجام شد",
        description: `${result.addedTechnicians?.length ?? 0} تکنسین به تیکت اضافه شد.`,
      })
      setAssignDialogOpen(false)
    } catch (error: any) {
      const status = error?.status ?? (error as any)?.status;
      const message = error?.body?.message || error?.message || "خطا در تخصیص دستی";
      setAssignError(status ? `(${status}) ${message}` : message);
    } finally {
      setAssignLoading(false)
    }
  }

  // Refetch ticket details + messages and update modal state. Do NOT close the preview modal after this.
  // We do NOT call onTicketUpdate here: the parent would run loadTickets(activeTab) which sets loading=true,
  // replacing the table with a loading div and unmounting this component (and thus closing the modal).
  const reloadPreviewTicketAndMessages = async () => {
    if (!selectedTicket?.id) return
    try {
      const [refreshed, list] = await Promise.all([
        apiGetNoStore<Record<string, unknown>>(`/api/tickets/${selectedTicket.id}`, { token: authToken ?? undefined }),
        apiGetNoStore<ApiTicketMessageDto[]>(`/api/tickets/${selectedTicket.id}/messages`, { token: authToken ?? undefined }),
      ])
      const newStatus = (refreshed?.displayStatus ?? refreshed?.status ?? selectedTicket?.status) as TicketStatus
      setSelectedTicket((prev: any) =>
        prev ? { ...prev, displayStatus: newStatus, status: newStatus, updatedAt: refreshed?.updatedAt ?? prev.updatedAt } : prev
      )
      setPreviewMessages(list ?? [])
      previewCloseGuardUntilRef.current = Date.now() + 600
    } catch (err) {
      // non-fatal: modal stays open with previous data
    }
  }

  const handleReplySubmit = async () => {
    if (!selectedTicket?.id) return
    if (!replyMessage.trim()) {
      toast({
        title: "پیام خالی است",
        description: "لطفاً متن پاسخ را وارد کنید.",
        variant: "destructive",
      })
      return
    }

    try {
      setReplySubmitting(true)
      // POST message with status (API enum: Open/InProgress/Solved etc.). credentials: "include" in api-client for cookie auth.
      await apiRequest(`/api/tickets/${selectedTicket.id}/messages`, {
        method: "POST",
        token: authToken ?? undefined,
        body: {
          message: replyMessage.trim(),
          status: mapUiStatusToApi(replyStatus),
        },
      })
      setReplyMessage("")
      await reloadPreviewTicketAndMessages()
      toast({
        title: "پاسخ ارسال شد",
        description: "پاسخ و وضعیت با موفقیت ثبت شد.",
      })
    } catch (error: any) {
      const status = error?.status ?? (error as any)?.status;
      const body = error?.body && typeof error.body === "object" ? (error.body as Record<string, unknown>) : null;
      const message = body?.message ?? body?.detail ?? error?.message ?? "لطفاً دوباره تلاش کنید.";
      const desc = status ? `(${status}) ${String(message)}` : String(message);
      toast({
        title: "ارسال پاسخ ناموفق بود",
        description: desc,
        variant: "destructive",
      });
    } finally {
      setReplySubmitting(false)
    }
  }

  const handleStatusUpdate = async () => {
    if (!selectedTicket?.id) return
    const currentStatus = statusForUi(selectedTicket)
    if (replyStatus === currentStatus) {
      toast({
        title: "بدون تغییر",
        description: "وضعیت جدید با وضعیت فعلی یکسان است.",
      })
      return
    }
    try {
      // PATCH ticket with API enum (Open/InProgress/Solved etc.). credentials: "include" in api-client for cookie auth.
      await apiRequest(`/api/tickets/${selectedTicket.id}`, {
        method: "PATCH",
        token: authToken ?? undefined,
        body: { status: mapUiStatusToApi(replyStatus) },
      })
      await reloadPreviewTicketAndMessages()
      toast({
        title: "وضعیت ثبت شد",
        description: "وضعیت تیکت با موفقیت به‌روزرسانی شد.",
      })
    } catch (error: any) {
      const status = error?.status ?? (error as any)?.status
      const body = error?.body && typeof error.body === "object" ? (error.body as Record<string, unknown>) : null
      const message = body?.message ?? body?.detail ?? error?.message ?? "لطفاً دوباره تلاش کنید."
      const desc = status ? `(${status}) ${String(message)}` : String(message)
      toast({
        title: "ثبت وضعیت ناموفق بود",
        description: desc,
        variant: "destructive",
      })
    }
  }

  const handleSelectTicket = (ticketId: string, checked: boolean) => {
    if (checked) {
      setSelectedTickets([...selectedTickets, ticketId])
    } else {
      setSelectedTickets(selectedTickets.filter((id) => id !== ticketId))
    }
  }

  const handleSelectAll = (checked: boolean) => {
    if (checked) {
      setSelectedTickets(filteredTickets.map((ticket) => ticket.id))
    } else {
      setSelectedTickets([])
    }
  }

  const handleBulkStatusUpdate = (newStatus: string) => {
    selectedTickets.forEach((ticketId) => {
      onTicketUpdate(ticketId, { status: newStatus })
    })
    setSelectedTickets([])
    toast({
      title: "به‌روزرسانی انجام شد",
      description: `وضعیت ${selectedTickets.length} تیکت به‌روزرسانی شد`,
    })
  }

  const handlePrint = () => {
    const reportRows = filteredTickets
    const counts = reportRows.reduce(
      (acc, ticket) => {
        const statusKey = normalizeStatus(ticket.displayStatus ?? ticket.status)
        if (statusKey === "open") acc.open += 1
        else if (statusKey === "seen") acc.seen += 1
        else if (statusKey === "review") acc.review += 1
        else if (statusKey === "in_progress") acc.inProgress += 1
        else if (statusKey === "solved") acc.solved += 1
        return acc
      },
      { open: 0, seen: 0, review: 0, inProgress: 0, solved: 0 },
    )

    const printContent = `
      <!DOCTYPE html>
      <html dir="rtl" lang="fa">
      <head>
        <meta charset="UTF-8">
        <title>گزارش تیکت‌ها</title>
        <style>
          body { font-family: 'IRANYekan', Tahoma, Arial, sans-serif; margin: 20px; direction: rtl; }
          .header { text-align: center; margin-bottom: 30px; border-bottom: 2px solid #333; padding-bottom: 10px; }
          .stats { display: flex; justify-content: space-around; margin: 20px 0; }
          .stat-box { text-align: center; padding: 10px; border: 1px solid #ddd; border-radius: 5px; }
          table { width: 100%; border-collapse: collapse; margin-top: 20px; }
          th, td { border: 1px solid #ddd; padding: 8px; text-align: right; }
          th { background-color: #f5f5f5; font-weight: bold; }
          .status-open { background-color: #fee2e2; color: #991b1b; }
          .status-seen { background-color: #e0f2fe; color: #075985; }
          .status-review { background-color: #fde68a; color: #92400e; }
          .status-in_progress { background-color: #fef3c7; color: #92400e; }
          .status-solved { background-color: #d1fae5; color: #065f46; }
          .status-other { background-color: #f3f4f6; color: #374151; }
          .priority-urgent { background-color: #fce7f3; color: #be185d; }
          .priority-high { background-color: #fee2e2; color: #991b1b; }
          .priority-medium { background-color: #fed7aa; color: #c2410c; }
          .priority-low { background-color: #dbeafe; color: #1e40af; }
          @media print { body { margin: 0; } }
        </style>
      </head>
      <body>
        <div class="header">
          <h1>گزارش تیکت‌های سیستم مدیریت خدمات IT</h1>
          <p>تاریخ تولید گزارش: ${formatFaDate(new Date())}</p>
        </div>
        
        <div class="stats">
          <div class="stat-box">
            <h3>کل تیکت‌ها</h3>
            <p>${reportRows.length}</p>
          </div>
          <div class="stat-box">
            <h3>باز</h3>
            <p>${counts.open}</p>
          </div>
          <div class="stat-box">
            <h3>مشاهده شده</h3>
            <p>${counts.seen}</p>
          </div>
          <div class="stat-box">
            <h3>بازبینی</h3>
            <p>${counts.review}</p>
          </div>
          <div class="stat-box">
            <h3>در حال انجام</h3>
            <p>${counts.inProgress}</p>
          </div>
          <div class="stat-box">
            <h3>حل شده</h3>
            <p>${counts.solved}</p>
          </div>
        </div>

        <table>
          <thead>
            <tr>
              <th>شماره تیکت</th>
              <th>عنوان</th>
              <th>وضعیت</th>
              <th>اولویت</th>
              <th>دسته‌بندی</th>
              <th>درخواست‌کننده</th>
              <th>تکنسین</th>
              <th>تاریخ ایجاد</th>
            </tr>
          </thead>
          <tbody>
            ${reportRows
              .map(
                (ticket) => {
                  const normalizedStatus = normalizeStatus(ticket.displayStatus ?? ticket.status)
                  return `
              <tr>
                <td>${ticket.id}</td>
                <td>${ticket.title}</td>
                <td class="status-${normalizedStatus}">${getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "admin")}</td>
                <td class="priority-${ticket.priority}">${priorityLabels[ticket.priority]}</td>
                <td>${getCategoryLabel(ticket)}</td>
                <td>${ticket.clientName}</td>
                <td>${ticket.assignedTechnicianName || "تعیین نشده"}</td>
                <td>${formatSafeDate(ticket.createdAt)}</td>
              </tr>
            `
                },
              )
              .join("")}
          </tbody>
        </table>
      </body>
      </html>
    `

    const printWindow = window.open("", "_blank")
    if (printWindow) {
      printWindow.document.write(printContent)
      printWindow.document.close()
      printWindow.print()
    }
  }

  const handleExportCSV = () => {
    const headers = [
      "شماره تیکت",
      "عنوان",
      "وضعیت",
      "اولویت",
      "دسته‌بندی",
      "درخواست‌کننده",
      "ایمیل",
      "تکنسین",
      "تاریخ ایجاد",
      "آخرین به‌روزرسانی",
    ]

    const csvContent = [
      headers.join(","),
      ...filteredTickets.map((ticket) =>
        [
          ticket.id,
          `"${ticket.title}"`,
          getTicketStatusLabel(ticket.displayStatus ?? ticket.status, "admin"),
          priorityLabels[ticket.priority],
          getCategoryLabel(ticket),
          `"${ticket.clientName}"`,
          ticket.clientEmail,
          `"${ticket.assignedTechnicianName || "تعیین نشده"}"`,
          formatSafeDate(ticket.createdAt),
          formatSafeDate(ticket.updatedAt),
        ].join(","),
      ),
    ].join("\n")

    const blob = new Blob(["\ufeff" + csvContent], { type: "text/csv;charset=utf-8;" })
    const link = document.createElement("a")
    const url = URL.createObjectURL(blob)
    link.setAttribute("href", url)
    link.setAttribute("download", `tickets-report-${new Date().toISOString().split("T")[0]}.csv`)
    link.style.visibility = "hidden"
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)

    toast({
      title: "فایل CSV ایجاد شد",
      description: "گزارش تیکت‌ها با موفقیت دانلود شد",
    })
  }

  const formatDateTime = (input: unknown) => {
    const parsed = parseServerDate(input)
    if (!parsed || Number.isNaN(parsed.getTime())) {
      if (process.env.NODE_ENV === "development") {
        console.warn("[AdminTicketList] Invalid date value:", input)
      }
      return { date: "—", time: "—", dateTime: "—" }
    }
    return {
      date: formatFaDate(parsed),
      time: formatFaTime(parsed),
      dateTime: formatFaDateTime(parsed),
    }
  }

  const formatSafeDate = (input: unknown) => formatDateTime(input).date

  return (
    <div className="space-y-6 font-iran" dir="rtl">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <CardTitle className="text-right font-iran">مدیریت کامل تیکت‌ها</CardTitle>
            <div className="flex gap-2">
              <Button variant="outline" onClick={handlePrint} className="gap-2 bg-transparent font-iran">
                <Printer className="w-4 h-4" />
                چاپ گزارش
              </Button>
              <Button variant="outline" onClick={handleExportCSV} className="gap-2 bg-transparent font-iran">
                <Download className="w-4 h-4" />
                خروجی CSV
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {/* Filters */}
          <div className="grid grid-cols-1 md:grid-cols-5 gap-4 mb-6">
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

            <Select value={filterStatus} onValueChange={setFilterStatus} dir="rtl">
              <SelectTrigger className="text-right font-iran">
                <SelectValue placeholder="وضعیت" />
              </SelectTrigger>
              <SelectContent className="font-iran">
                <SelectItem value="all">همه وضعیت‌ها</SelectItem>
                {getStatusOptionsForRole("admin").map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>

            <Select value={filterPriority} onValueChange={setFilterPriority} dir="rtl">
              <SelectTrigger className="text-right font-iran">
                <SelectValue placeholder="اولویت" />
              </SelectTrigger>
              <SelectContent className="font-iran">
                <SelectItem value="all">همه اولویت‌ها</SelectItem>
                <SelectItem value="urgent">فوری</SelectItem>
                <SelectItem value="high">بالا</SelectItem>
                <SelectItem value="medium">متوسط</SelectItem>
                <SelectItem value="low">کم</SelectItem>
              </SelectContent>
            </Select>

            <Select value={filterCategory} onValueChange={setFilterCategory} dir="rtl">
              <SelectTrigger className="text-right font-iran">
                <SelectValue placeholder="دسته‌بندی" />
              </SelectTrigger>
              <SelectContent className="font-iran">
                <SelectItem value="all">همه دسته‌ها</SelectItem>
                <SelectItem value="hardware">سخت‌افزار</SelectItem>
                <SelectItem value="software">نرم‌افزار</SelectItem>
                <SelectItem value="network">شبکه</SelectItem>
                <SelectItem value="email">ایمیل</SelectItem>
                <SelectItem value="security">امنیت</SelectItem>
                <SelectItem value="access">دسترسی</SelectItem>
              </SelectContent>
            </Select>

            <Button
              variant="outline"
              onClick={() => {
                setSearchQuery("")
                setFilterStatus("all")
                setFilterPriority("all")
                setFilterCategory("all")
              }}
              className="gap-2 font-iran"
            >
              <Filter className="w-4 h-4" />
              پاک کردن فیلترها
            </Button>
          </div>

          {/* Bulk Actions */}
          {selectedTickets.length > 0 && (
            <div className="flex items-center gap-4 mb-4 p-3 bg-muted rounded-lg">
              <span className="text-sm font-medium font-iran">{selectedTickets.length} تیکت انتخاب شده</span>
              <div className="flex gap-2">
                <Button
                  size="sm"
                  onClick={() => handleBulkStatusUpdate("InProgress")}
                  variant="outline"
                  className="font-iran"
                >
                  در حال انجام
                </Button>
                <Button
                  size="sm"
                  onClick={() => handleBulkStatusUpdate("Resolved")}
                  variant="outline"
                  className="font-iran"
                >
                  حل شده
                </Button>
                <Button
                  size="sm"
                  onClick={() => handleBulkStatusUpdate("Closed")}
                  variant="outline"
                  className="font-iran"
                >
                  بسته
                </Button>
              </div>
            </div>
          )}

          {/* Tickets Table - horizontal scroll on small screens */}
          <div className="border rounded-lg overflow-x-auto">
            <Table className="min-w-[700px]">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">
                    <Checkbox
                      checked={selectedTickets.length === filteredTickets.length && filteredTickets.length > 0}
                      onCheckedChange={handleSelectAll}
                    />
                  </TableHead>
                  <TableHead className="text-right font-iran">شماره تیکت</TableHead>
                  <TableHead className="text-right font-iran">عنوان</TableHead>
                  <TableHead className="text-right font-iran">وضعیت</TableHead>
                  <TableHead className="text-right font-iran">اولویت</TableHead>
                  <TableHead className="text-right font-iran">دسته‌بندی</TableHead>
                  <TableHead className="text-right font-iran">درخواست‌کننده</TableHead>
                  <TableHead className="text-right font-iran">تکنسین</TableHead>
                  <TableHead className="text-right font-iran">تاریخ ایجاد</TableHead>
                  <TableHead className="text-right font-iran">عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTickets.length > 0 ? (
                  filteredTickets.map((ticket) => {
                    const isSelected = selectedTickets.includes(ticket.id)
                    const assignedTechnicians = normalizeAssignedTechnicians(ticket)

                    return (
                      <TableRow
                        key={ticket.id}
                        className={`cursor-pointer hover:bg-muted/50 ${isSelected ? "bg-muted/50" : ""}`}
                        onClick={() => handleViewTicket(ticket)}
                      >
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={isSelected}
                            onCheckedChange={(checked) => handleSelectTicket(ticket.id, checked as boolean)}
                          />
                        </TableCell>
                        <TableCell className="font-mono text-sm font-iran">{ticket.id}</TableCell>
                        <TableCell className="max-w-xs">
                          <div className="truncate font-iran" title={ticket.title}>
                            {ticket.title}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge className={`${statusColors[statusForUi(ticket)] ?? statusColors.Open} font-iran`}>
                            {getTicketStatusLabel(statusForUi(ticket), "admin")}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <Badge className={`${priorityColors[ticket.priority]} font-iran`}>
                            {priorityLabels[ticket.priority]}
                          </Badge>
                        </TableCell>
                        <TableCell>
                          <span className="text-sm font-iran">{getCategoryLabel(ticket)}</span>
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Avatar className="w-6 h-6">
                              <AvatarFallback className="text-xs font-iran">
                                {ticket.clientName.charAt(0)}
                              </AvatarFallback>
                            </Avatar>
                            <div>
                              <div className="text-sm font-medium font-iran">{ticket.clientName}</div>
                              <div className="text-xs text-muted-foreground font-iran">{ticket.clientEmail}</div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <Button
                            variant="outline"
                            size="sm"
                            className="gap-1 font-iran"
                            onClick={(e) => {
                              e.stopPropagation()
                              openTechListDialog(ticket)
                            }}
                          >
                            مشاهده ({assignedTechnicians.length})
                            <ChevronDown className="h-3 w-3" />
                          </Button>
                        </TableCell>
                        <TableCell className="text-sm font-iran">
                          {formatSafeDate(ticket.createdAt)}
                        </TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <div className="flex flex-col gap-2">
                            <Button
                              variant="outline"
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
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleAutoAssign(ticket)
                              }}
                              disabled={autoAssigning[ticket.id]}
                              className="gap-1 font-iran"
                            >
                              {autoAssigning[ticket.id] ? "در حال تخصیص..." : "خودکار"}
                            </Button>
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleOpenAssignDialog(ticket)
                              }}
                              className="gap-1 font-iran"
                            >
                              دستی
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    )
                  })
                ) : (
                  <TableRow>
                    <TableCell colSpan={10} className="text-center py-8">
                      <div className="flex flex-col items-center gap-2">
                        <Search className="w-8 h-8 text-muted-foreground" />
                        <p className="text-muted-foreground font-iran">تیکتی یافت نشد</p>
                      </div>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {/* Enhanced View Ticket Dialog — stays open after reply/status/assign; close only via X or overlay */}
      <Dialog
        open={viewDialogOpen}
        onOpenChange={(open) => {
          if (!open) {
            if (Date.now() < previewCloseGuardUntilRef.current) return
            if (selectedTicket) {
              const status = statusForUi(selectedTicket)
              if (status) {
                onTicketUpdate(selectedTicket.id, { displayStatus: status, status })
              }
            }
            setSelectedTicket(null)
          }
          setViewDialogOpen(open)
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-6xl font-iran" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right font-iran text-xl">پیش‌نمایش تیکت {selectedTicket?.id}</DialogTitle>
          </DialogHeader>
          {selectedTicket && (
            <div className="space-y-6">
              {/* Ticket Header with Status */}
              <div className="bg-gradient-to-r from-blue-50 to-indigo-50 border border-blue-200 rounded-lg p-6">
                <div className="flex justify-between items-start mb-4">
                  <div className="text-right space-y-3">
                    <h2 className="text-2xl font-bold font-iran text-gray-900">{selectedTicket.title}</h2>
                    <div className="flex gap-3 items-center">
                      <Badge
                        className={`${statusColors[statusForUi(selectedTicket)] ?? statusColors.Open} font-iran text-sm px-3 py-1`}
                      >
                        {React.createElement(
                          statusIcons[statusForUi(selectedTicket)] ?? AlertCircle,
                          { className: "w-4 h-4 ml-1" }
                        )}
                        {getTicketStatusLabel(statusForUi(selectedTicket), "admin")}
                      </Badge>
                      <Badge className={`${priorityColors[selectedTicket.priority]} font-iran text-sm px-3 py-1`}>
                        {priorityLabels[selectedTicket.priority]}
                      </Badge>
                      <div className="flex items-center gap-2 bg-white px-3 py-1 rounded-full border">
                        <span className="text-sm font-iran">{getCategoryLabel(selectedTicket)}</span>
                      </div>
                    </div>
                  </div>
                  <div className="text-left bg-white p-4 rounded-lg border shadow-sm">
                    <p className="text-sm text-muted-foreground font-iran mb-1">شماره تیکت</p>
                    <p className="font-mono text-2xl font-bold text-blue-600">{selectedTicket.id}</p>
                  </div>
                </div>
              </div>

              {/* Main Content Grid */}
              <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Left Column - Ticket Details */}
                <div className="lg:col-span-2 space-y-6">
                  {/* Description */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <FileText className="w-5 h-5 text-blue-600" />
                        شرح مشکل
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="bg-gray-50 border rounded-lg p-4">
                        <p className="whitespace-pre-wrap text-right font-iran leading-relaxed">
                          {selectedTicket.description}
                        </p>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Dynamic Fields */}
                  {selectedTicket.dynamicFields && Object.keys(selectedTicket.dynamicFields).length > 0 && (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-right font-iran">
                          <FileText className="w-5 h-5 text-green-600" />
                          اطلاعات تکمیلی
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          {Object.entries(selectedTicket.dynamicFields).map(([key, value]) => (
                            <div key={key} className="bg-gray-50 border rounded-lg p-3">
                              <div className="flex justify-between items-center">
                                <span className="text-sm text-muted-foreground font-iran">{key}:</span>
                                <span className="text-sm font-medium font-iran text-right">{value as string}</span>
                              </div>
                            </div>
                          ))}
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Attachments */}
                  {selectedTicket.attachments && selectedTicket.attachments.length > 0 && (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-right font-iran">
                          <Paperclip className="w-5 h-5 text-purple-600" />
                          فایل‌های پیوست
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                          {selectedTicket.attachments.map((file: any, index: number) => (
                            <div key={index} className="flex items-center gap-3 p-3 bg-gray-50 border rounded-lg">
                              <Paperclip className="w-4 h-4 text-gray-500" />
                              <div className="flex-1 text-right">
                                <p className="text-sm font-medium font-iran">{file.name}</p>
                                <p className="text-xs text-muted-foreground font-iran">
                                  {(file.size / 1024).toFixed(1)} KB
                                </p>
                              </div>
                            </div>
                          ))}
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Conversation: full thread (client + technician + admin + supervisor) */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <MessageSquare className="w-5 h-5 text-orange-600" />
                        مکالمه
                        {previewMessages && previewMessages.length > 0 && (
                          <span className="text-sm font-normal text-muted-foreground">
                            ({previewMessages.length})
                          </span>
                        )}
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      {previewMessagesLoading ? (
                        <div className="py-8 text-center text-muted-foreground font-iran">
                          در حال بارگذاری مکالمه...
                        </div>
                      ) : !previewMessages || previewMessages.length === 0 ? (
                        <div className="py-8 text-center text-muted-foreground font-iran border border-dashed rounded-lg">
                          هنوز پیامی ثبت نشده است
                        </div>
                      ) : (
                        <div className="space-y-4">
                          {previewMessages.map((msg) => {
                            const msgDateTime = formatDateTime(msg.createdAt)
                            const roleLabel =
                              msg.authorRole === "Client"
                                ? "درخواست‌کننده"
                                : msg.authorRole === "Technician"
                                  ? "تکنسین"
                                  : msg.authorRole === "Admin"
                                    ? "مدیر"
                                    : msg.authorRole === "Supervisor"
                                      ? "سرپرست"
                                      : msg.authorRole ?? "—"
                            return (
                              <div key={msg.id} className="border rounded-lg p-4 bg-white shadow-sm">
                                <div className="flex justify-between items-start mb-3">
                                  <div className="flex items-center gap-3">
                                    <Avatar className="w-8 h-8">
                                      <AvatarFallback className="text-sm font-iran">
                                        {msg.authorName?.charAt(0) || "?"}
                                      </AvatarFallback>
                                    </Avatar>
                                    <div className="text-right">
                                      <p className="font-medium text-sm font-iran">{msg.authorName}</p>
                                      <Badge variant="secondary" className="text-xs font-iran mt-1">
                                        {roleLabel}
                                      </Badge>
                                    </div>
                                  </div>
                                  <div className="text-left text-xs text-muted-foreground font-iran">
                                    <div className="flex items-center gap-1 justify-end">
                                      <Calendar className="w-3 h-3" />
                                      <span>{msgDateTime.date}</span>
                                    </div>
                                    <div className="flex items-center gap-1 justify-end mt-1">
                                      <Clock className="w-3 h-3" />
                                      <span>{msgDateTime.time}</span>
                                    </div>
                                  </div>
                                </div>
                                <div className="bg-muted/50 border rounded-lg p-3">
                                  <p className="whitespace-pre-wrap text-right font-iran text-sm leading-relaxed">
                                    {msg.message}
                                  </p>
                                </div>
                              </div>
                            )
                          })}
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </div>

                {/* Right Column - Sidebar Info */}
                <div className="space-y-6">
                  {/* Ticket Information */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <FileText className="w-5 h-5 text-blue-600" />
                        اطلاعات تیکت
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="space-y-3">
                        <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                          <span className="text-sm text-muted-foreground font-iran">دسته‌بندی:</span>
                          <span className="text-sm font-medium font-iran">
                            {getCategoryLabel(selectedTicket)}
                          </span>
                        </div>

                        {selectedTicket.subcategory && (
                          <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                            <span className="text-sm text-muted-foreground font-iran">زیر دسته:</span>
                            <span className="text-sm font-medium font-iran">{selectedTicket.subcategory}</span>
                          </div>
                        )}

                        <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                          <span className="text-sm text-muted-foreground font-iran">تاریخ ایجاد:</span>
                          <span className="text-sm font-medium font-iran">
                            {formatDateTime(selectedTicket.createdAt).date}
                          </span>
                        </div>

                        <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                          <span className="text-sm text-muted-foreground font-iran">زمان ایجاد:</span>
                          <span className="text-sm font-medium font-iran">
                            {formatDateTime(selectedTicket.createdAt).time}
                          </span>
                        </div>

                        <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                          <span className="text-sm text-muted-foreground font-iran">آخرین به‌روزرسانی:</span>
                          <span className="text-sm font-medium font-iran">
                            {formatDateTime(selectedTicket.updatedAt).date}
                          </span>
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Client Information */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <User className="w-5 h-5 text-green-600" />
                        اطلاعات درخواست‌کننده
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-4">
                        <div className="flex items-center gap-3 p-3 bg-green-50 border border-green-200 rounded-lg">
                          <Avatar className="w-10 h-10">
                            <AvatarFallback className="font-iran bg-green-100 text-green-700">
                              {selectedTicket.clientName.charAt(0)}
                            </AvatarFallback>
                          </Avatar>
                          <div className="text-right flex-1">
                            <p className="font-medium font-iran">{selectedTicket.clientName}</p>
                            <p className="text-sm text-muted-foreground font-iran">درخواست‌کننده</p>
                          </div>
                        </div>

                        <div className="space-y-3">
                          <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                            <span className="text-sm text-muted-foreground font-iran">ایمیل:</span>
                            <span className="text-sm font-medium font-iran">{selectedTicket.clientEmail}</span>
                          </div>

                          {selectedTicket.clientPhone && (
                            <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                              <span className="text-sm text-muted-foreground font-iran">تلفن:</span>
                              <span className="text-sm font-medium font-iran">{selectedTicket.clientPhone}</span>
                            </div>
                          )}

                          {selectedTicket.department && (
                            <div className="flex justify-between items-center p-2 bg-gray-50 rounded">
                              <span className="text-sm text-muted-foreground font-iran">بخش:</span>
                              <span className="text-sm font-medium font-iran">{selectedTicket.department}</span>
                            </div>
                          )}
                        </div>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Assigned Technician */}
                  {selectedTicket.assignedTechnicianName && (
                    <Card>
                      <CardHeader>
                        <CardTitle className="flex items-center gap-2 text-right font-iran">
                          <User className="w-5 h-5 text-purple-600" />
                          تکنسین مسئول
                        </CardTitle>
                      </CardHeader>
                      <CardContent>
                        <div className="flex items-center gap-3 p-3 bg-purple-50 border border-purple-200 rounded-lg">
                          <Avatar className="w-10 h-10">
                            <AvatarFallback className="font-iran bg-purple-100 text-purple-700">
                              {selectedTicket.assignedTechnicianName.charAt(0)}
                            </AvatarFallback>
                          </Avatar>
                          <div className="text-right flex-1">
                            <p className="font-medium font-iran">{selectedTicket.assignedTechnicianName}</p>
                            <p className="text-sm text-muted-foreground font-iran">تکنسین مسئول</p>
                          </div>
                        </div>
                      </CardContent>
                    </Card>
                  )}

                  {/* Reply + Status */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <MessageSquare className="w-5 h-5 text-blue-600" />
                        پاسخ و تغییر وضعیت
                      </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                      <div className="space-y-2">
                        <label className="text-sm font-iran text-muted-foreground">وضعیت</label>
                        <Select
                          value={replyStatus}
                          onValueChange={(value) => setReplyStatus(value as TicketStatus)}
                        >
                          <SelectTrigger className="text-right font-iran">
                            <SelectValue placeholder="انتخاب وضعیت" />
                          </SelectTrigger>
                          <SelectContent dir="rtl" className="font-iran">
                            {TICKET_STATUS_OPTIONS.map((option) => (
                              <SelectItem key={option.value} value={option.value}>
                                {option.label}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                      </div>

                      <div className="space-y-2">
                        <label className="text-sm font-iran text-muted-foreground">متن پاسخ</label>
                        <Textarea
                          value={replyMessage}
                          onChange={(event) => setReplyMessage(event.target.value)}
                          className="min-h-[120px] text-right font-iran"
                          placeholder="پاسخ خود را بنویسید..."
                        />
                      </div>

                      <div className="flex flex-wrap gap-2">
                        <Button
                          type="button"
                          onClick={handleReplySubmit}
                          disabled={replySubmitting}
                          className="font-iran gap-2"
                        >
                          <MessageSquare className="w-4 h-4" />
                          {replySubmitting ? "در حال ارسال..." : "ارسال پاسخ"}
                        </Button>
                        <Button
                          type="button"
                          variant="outline"
                          onClick={handleStatusUpdate}
                          className="font-iran gap-2"
                        >
                          <Settings className="w-4 h-4" />
                          ثبت وضعیت
                        </Button>
                      </div>
                    </CardContent>
                  </Card>

                  {/* Quick Actions */}
                  <Card>
                    <CardHeader>
                      <CardTitle className="flex items-center gap-2 text-right font-iran">
                        <Settings className="w-5 h-5 text-gray-600" />
                        عملیات سریع
                      </CardTitle>
                    </CardHeader>
                    <CardContent>
                      <div className="space-y-2">
                        <Button
                          variant="outline"
                          className="w-full justify-start gap-2 font-iran bg-transparent"
                          onClick={() => {
                            navigator.clipboard.writeText(selectedTicket.id)
                            toast({
                              title: "کپی شد",
                              description: "شماره تیکت در کلیپ‌بورد کپی شد",
                            })
                          }}
                        >
                          <FileText className="w-4 h-4" />
                          کپی شماره تیکت
                        </Button>

                        <Button
                          variant="outline"
                          className="w-full justify-start gap-2 font-iran bg-transparent"
                          onClick={() => {
                            const mailtoLink = `mailto:${selectedTicket.clientEmail}?subject=پاسخ به تیکت ${selectedTicket.id}&body=سلام ${selectedTicket.clientName}،%0A%0Aدر خصوص تیکت ${selectedTicket.id} با عنوان "${selectedTicket.title}"%0A%0A`
                            window.open(mailtoLink)
                          }}
                        >
                          <Mail className="w-4 h-4" />
                          ارسال ایمیل به کاربر
                        </Button>

                        {selectedTicket.clientPhone && (
                          <Button
                            variant="outline"
                            className="w-full justify-start gap-2 font-iran bg-transparent"
                            onClick={() => {
                              window.open(`tel:${selectedTicket.clientPhone}`)
                            }}
                          >
                            <Phone className="w-4 h-4" />
                            تماس با کاربر
                          </Button>
                        )}
                      </div>
                    </CardContent>
                  </Card>
                </div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      <Dialog open={techListOpen} onOpenChange={setTechListOpen}>
        <DialogContent className="max-w-md font-iran text-right" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right font-iran">تکنسین‌های این تیکت</DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            {techList.length === 0 ? (
              <p className="text-muted-foreground text-sm py-4 text-center font-iran">
                برای این تیکت تکنسینی ثبت نشده است
              </p>
            ) : (
              <ul className="space-y-3 max-h-[60vh] overflow-y-auto">
                {techList.map((tech, index) => {
                  const displayName = tech.fullName || tech.name || "نامشخص"
                  const roleLabel = getTechRoleLabel(tech)
                  const key = tech.id || tech.userId || `${displayName}-${index}`
                  return (
                    <li key={key} className="flex items-center justify-between gap-3 border rounded-lg p-3">
                      <div className="flex flex-col text-right">
                        <span className="text-sm font-medium font-iran">{displayName}</span>
                        <span className="text-xs text-muted-foreground font-iran">{roleLabel}</span>
                      </div>
                      <Badge variant="outline" className="text-xs font-iran">
                        {roleLabel}
                      </Badge>
                    </li>
                  )
                })}
              </ul>
            )}
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={assignDialogOpen} onOpenChange={setAssignDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl font-iran text-right" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right font-iran">تخصیص تکنسین</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            {assignTicket ? (
              <div className="rounded-lg border bg-muted/30 p-3 text-sm">
                <div className="font-iran">{assignTicket.title}</div>
                <div className="text-xs text-muted-foreground">
                  شناسه: <span className="font-mono">{assignTicket.id}</span>
                </div>
                <div className="text-xs text-muted-foreground">
                  دسته‌بندی: {getCategoryLabel(assignTicket)} {assignTicket.subcategory ? `- ${assignTicket.subcategory}` : ""}
                </div>
              </div>
            ) : null}

            <Input
              value={directorySearch}
              onChange={(event) => setDirectorySearch(event.target.value)}
              placeholder="جستجوی تکنسین..."
              className="text-right font-iran"
            />

            <div className="flex gap-2 overflow-x-auto pb-1">
              <Button
                type="button"
                variant={directoryFilter === "all" ? "default" : "outline"}
                className="font-iran"
                onClick={() => {
                  setDirectoryFilter("all")
                  setExpertiseCategoryId(null)
                  setExpertiseSubcategoryId(null)
                }}
              >
                همه
              </Button>
              <Button
                type="button"
                variant={directoryFilter === "Free" ? "default" : "outline"}
                className="font-iran"
                onClick={() => {
                  setDirectoryFilter("Free")
                  setExpertiseCategoryId(null)
                  setExpertiseSubcategoryId(null)
                }}
              >
                آزاد
              </Button>
              <Button
                type="button"
                variant={directoryFilter === "Busy" ? "default" : "outline"}
                className="font-iran"
                onClick={() => {
                  setDirectoryFilter("Busy")
                  setExpertiseCategoryId(null)
                  setExpertiseSubcategoryId(null)
                }}
              >
                پرمشغله
              </Button>
              <Button
                type="button"
                variant={directoryFilter === "expertise" ? "default" : "outline"}
                className="font-iran"
                onClick={() => setDirectoryFilter("expertise")}
              >
                تخصص
              </Button>
            </div>

            {directoryFilter === "expertise" ? (
              <div className="space-y-2">
                <div className="flex gap-2 overflow-x-auto pb-1">
                  {directoryCategories.length === 0 ? (
                    <span className="text-xs text-muted-foreground">دسته‌بندی‌ای یافت نشد.</span>
                  ) : (
                    directoryCategories.map((cat) => (
                      <Button
                        key={cat.id}
                        type="button"
                        variant={expertiseCategoryId === cat.id ? "default" : "outline"}
                        className="text-xs font-iran"
                        onClick={() => {
                          setExpertiseCategoryId(cat.id)
                          setExpertiseSubcategoryId(null)
                        }}
                      >
                        {cat.name}
                      </Button>
                    ))
                  )}
                </div>
                {expertiseCategoryId ? (
                  <div className="flex gap-2 overflow-x-auto pb-1">
                    <Button
                      type="button"
                      variant={expertiseSubcategoryId === null ? "default" : "outline"}
                      className="text-xs font-iran"
                      onClick={() => setExpertiseSubcategoryId(null)}
                    >
                      همه زیردسته‌ها
                    </Button>
                    {directorySubcategories.map((sub) => (
                      <Button
                        key={sub.id}
                        type="button"
                        variant={expertiseSubcategoryId === sub.id ? "default" : "outline"}
                        className="text-xs font-iran"
                        onClick={() => setExpertiseSubcategoryId(sub.id)}
                      >
                        {sub.name}
                      </Button>
                    ))}
                  </div>
                ) : null}
              </div>
            ) : null}

            {directoryLoading ? (
              <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
            ) : directoryError ? (
              <div className="text-sm text-red-700">{directoryError}</div>
            ) : directoryItems.length === 0 ? (
              <div className="text-sm text-muted-foreground">تکنسینی برای نمایش وجود ندارد.</div>
            ) : (
              <ScrollArea className="max-h-[60vh]">
                <div className="space-y-2">
                  {directoryItems.map((tech) => {
                    const assignedSet = new Set(
                      (assignTicket?.assignedTechnicians ?? []).map((t: any) => t.technicianUserId)
                    )
                    const isAlreadyAssigned = assignedSet.has(tech.technicianUserId)
                    const isSelected = selectedTechnicians.includes(tech.technicianUserId)

                    return (
                      <div
                        key={tech.technicianUserId}
                        className="flex flex-col gap-2 rounded-lg border p-3"
                      >
                        <div className="flex items-center justify-between">
                          <div className="flex items-center gap-3">
                            <Checkbox
                              checked={isSelected}
                              disabled={isAlreadyAssigned}
                              onCheckedChange={(checked) => {
                                const isChecked = Boolean(checked)
                                setSelectedTechnicians((prev) =>
                                  isChecked
                                    ? [...prev, tech.technicianUserId]
                                    : prev.filter((id) => id !== tech.technicianUserId)
                                )
                              }}
                            />
                            <div>
                              <div className="font-iran">{tech.name}</div>
                              <div className="text-xs text-muted-foreground">{tech.email}</div>
                            </div>
                          </div>
                          <div className="flex items-center gap-2">
                            <Badge
                              variant="outline"
                              className={`text-xs ${tech.availability === "Free" ? "text-emerald-600" : "text-amber-600"}`}
                            >
                              {tech.availability === "Free" ? "آزاد" : "پرمشغله"}
                            </Badge>
                            <span className="text-xs text-muted-foreground">
                              {tech.inboxLeftActiveNonTerminal}/{tech.inboxTotalActive}
                            </span>
                          </div>
                        </div>
                        {isAlreadyAssigned ? (
                          <div className="text-xs text-muted-foreground">قبلاً به این تیکت تخصیص داده شده است.</div>
                        ) : null}
                        {tech.expertise?.length ? (
                          <div className="flex flex-wrap gap-2">
                            {tech.expertise.map((tag) => (
                              <Badge key={`${tag.categoryId}-${tag.subcategoryId}`} variant="secondary" className="text-xs font-iran">
                                {tag.categoryName} / {tag.subcategoryName}
                              </Badge>
                            ))}
                          </div>
                        ) : (
                          <div className="text-xs text-muted-foreground">تخصصی ثبت نشده است.</div>
                        )}
                      </div>
                    )
                  })}
                </div>
              </ScrollArea>
            )}

            {assignError ? <div className="text-sm text-red-700">{assignError}</div> : null}

            <div className="flex justify-end gap-2">
              <Button variant="outline" onClick={() => setAssignDialogOpen(false)} className="font-iran">
                انصراف
              </Button>
              <Button onClick={handleManualAssign} disabled={assignLoading} className="font-iran">
                {assignLoading ? "در حال تخصیص..." : "تخصیص"}
              </Button>
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

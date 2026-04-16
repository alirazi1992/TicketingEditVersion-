"use client"

import { useEffect, useMemo, useState } from "react"
import { useRouter } from "next/navigation"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Input } from "@/components/ui/input"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Avatar, AvatarFallback } from "@/components/ui/avatar"
import { Separator } from "@/components/ui/separator"
import { Checkbox } from "@/components/ui/checkbox"
import { toast } from "@/hooks/use-toast"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { AssignedTechniciansCell } from "@/components/assigned-technicians-cell"
import { autoAssignAdminTicket, getAdminTechnicianDirectory, manualAssignAdminTicket } from "@/lib/admin-tickets-api"
import type { ApiAdminTechnicianDirectoryItemDto } from "@/lib/api-types"
import { parseServerDate, toFaDate, toFaDateTime } from "@/lib/datetime"
import { cn } from "@/lib/utils"
import {
  Search,
  Filter,
  UserPlus,
  Users,
  Star,
  Clock,
  CheckCircle,
  AlertTriangle,
  HardDrive,
  ComputerIcon as Software,
  Network,
  Mail,
  Shield,
  Key,
  Wrench,
  Zap,
  Target,
} from "lucide-react"
import type { LucideIcon } from "lucide-react"
import type { TechnicianProfile } from "@/data/technician-profiles"

const getAutomaticAssignment = (ticket: any, technicians: any[]) => {
  const availableTechnicians = technicians.filter((tech) => tech.status === "available")

  if (availableTechnicians.length === 0) {
    const leastBusyTech = technicians
      .filter((tech) => tech.activeTickets < 8) 
      .sort((a, b) => a.activeTickets - b.activeTickets)[0]

    return leastBusyTech || null
  }

  const scoredTechnicians = availableTechnicians.map((tech) => ({
    ...tech,
    score: calculateComprehensiveScore(tech, ticket),
    matchReasons: getMatchReasons(tech, ticket),
  }))

  return scoredTechnicians.sort((a, b) => b.score - a.score)[0]
}

const calculateComprehensiveScore = (technician: any, ticket: any) => {
  let score = 0
  const weights = {
    specialty: 40,
    priority: 25,
    rating: 20,
    workload: 10,
    experience: 5,
  }

  if (technician.specialties.includes(ticket.category)) {
    score += weights.specialty
    if (technician.specialties[0] === ticket.category) {
      score += 10
    }
  } else {
    score -= 15
  }

  const priorityScore = getPriorityScore(technician, ticket.priority)
  score += (priorityScore / 100) * weights.priority

  score += (technician.rating / 5) * weights.rating

  const workloadScore = Math.max(0, ((8 - technician.activeTickets) / 8) * 100)
  score += (workloadScore / 100) * weights.workload

  const experienceScore = Math.min(100, (technician.completedTickets / 100) * 100)
  score += (experienceScore / 100) * weights.experience

  score += getBonusScore(technician, ticket)

  return Math.round(score * 10) / 10 
}

const getPriorityScore = (technician: any, priority: string) => {
  const priorityWeights: Record<string, { rating: number; experience: number }> = {
    urgent: { rating: 4.5, experience: 30 },
    high: { rating: 4.0, experience: 20 },
    medium: { rating: 3.5, experience: 10 },
    low: { rating: 3.0, experience: 5 },
  }

  const requirement = priorityWeights[priority] || priorityWeights.medium
  let score = 0

  if (technician.rating >= requirement.rating) {
    score += 60
  } else {
    score += (technician.rating / requirement.rating) * 60
  }

  if (technician.completedTickets >= requirement.experience) {
    score += 40
  } else {
    score += (technician.completedTickets / requirement.experience) * 40
  }

  return Math.min(100, score)
}

const getBonusScore = (technician: any, ticket: any) => {
  let bonus = 0

  if (technician.avgResponseTime && Number.parseFloat(technician.avgResponseTime) < 2.0) {
    bonus += 5
  }

  if (technician.rating >= 4.8 && technician.completedTickets >= 50) {
    bonus += 8
  }

  const relatedSpecialties = getRelatedSpecialties(ticket.category)
  const matchingSpecialties = technician.specialties.filter((s: string) => relatedSpecialties.includes(s))
  if (matchingSpecialties.length > 1) {
    bonus += 3
  }

  if (technician.activeTickets <= 1) {
    bonus += 5
  }

  return bonus
}

const getRelatedSpecialties = (category: string) => {
  const relations: Record<string, string[]> = {
    hardware: ["hardware", "network"],
    software: ["software", "access"],
    network: ["network", "hardware", "security"],
    email: ["email", "software", "security"],
    security: ["security", "network", "access"],
    access: ["access", "security", "software"],
  }
  return relations[category] || [category]
}

const getMatchReasons = (technician: any, ticket: any) => {
  const reasons: string[] = []

  if (technician.specialties.includes(ticket.category)) {
    reasons.push(`متخصص ${getCategoryLabel(ticket)}`)
  }

  if (technician.rating >= 4.5) {
    reasons.push("امتیاز بالا")
  }

  if (technician.activeTickets <= 2) {
    reasons.push("بار کاری کم")
  }

  if (technician.completedTickets >= 50) {
    reasons.push("تجربه بالا")
  }

  const priorityRequirements: Record<string, number> = {
    urgent: 4.5,
    high: 4.0,
    medium: 3.5,
    low: 3.0,
  }

  if (technician.rating >= priorityRequirements[ticket.priority]) {
    reasons.push(`مناسب برای اولویت ${priorityLabels[ticket.priority]}`)
  }

  return reasons
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

const statusColors: Record<string, string> = {
  open: "bg-red-100 text-red-800 border-red-200",
  Open: "bg-red-100 text-red-800 border-red-200",
  "in-progress": "bg-yellow-100 text-yellow-800 border-yellow-200",
  InProgress: "bg-yellow-100 text-yellow-800 border-yellow-200",
  solved: "bg-green-100 text-green-800 border-green-200",
  Solved: "bg-green-100 text-green-800 border-green-200",
  closed: "bg-gray-100 text-gray-800 border-gray-200",
  Closed: "bg-gray-100 text-gray-800 border-gray-200",
  answered: "bg-blue-100 text-blue-800 border-blue-200",
  Answered: "bg-blue-100 text-blue-800 border-blue-200",
}

const statusLabels: Record<string, string> = {
  open: "باز",
  Open: "باز",
  Submitted: "ثبت شده",
  SeenRead: "مشاهده شد",
  "in-progress": "در حال انجام",
  InProgress: "در حال انجام",
  solved: "حل شده",
  Solved: "حل شده",
  closed: "بسته",
  Closed: "بسته",
  answered: "پاسخ داده شد",
  Answered: "پاسخ داده شد",
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

const categoryIcons: Record<string, LucideIcon> = {
  hardware: HardDrive,
  software: Software,
  network: Network,
  email: Mail,
  security: Shield,
  access: Key,
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

const getSubcategoryLabel = (ticket: any) => {
  if (!ticket) return ""
  return ticket.subcategoryLabel || ticket.subcategory || ""
}

const formatDateTime = (value?: string | Date | null) => toFaDateTime(value)

const formatDuration = (start?: string | Date | null, end?: string | Date | null) => {
  if (!start || !end) return "—"
  const s = start instanceof Date ? start : parseServerDate(start)
  const e = end instanceof Date ? end : parseServerDate(end)
  if (!s || !e) return "—"
  const diffMs = Math.max(0, e.getTime() - s.getTime())
  const totalMinutes = Math.floor(diffMs / 60000)
  const days = Math.floor(totalMinutes / (60 * 24))
  const hours = Math.floor((totalMinutes % (60 * 24)) / 60)
  const minutes = totalMinutes % 60
  const parts: string[] = []
  if (days) parts.push(`${days} روز`)
  if (hours) parts.push(`${hours} ساعت`)
  if (minutes || parts.length === 0) parts.push(`${minutes} دقیقه`)
  return parts.join(" ")
}

const getSuggestedTechnicians = (ticket: any, technicians: TechnicianProfile[]) => {
  const category = ticket?.category
  const subcategory = getSubcategoryLabel(ticket)
  if (!category) return []

  return technicians.filter((tech) => {
    const hasCategory = tech.specialties?.includes(category)
    if (!subcategory) return hasCategory
    const expertise = tech.expertise ?? []
    return hasCategory && expertise.includes(subcategory)
  })
}

const pickAutomaticTechnician = (ticket: any, technicians: TechnicianProfile[]) => {
  const candidates = getSuggestedTechnicians(ticket, technicians)
  if (candidates.length === 0) return null
  return [...candidates].sort((a, b) => {
    if (a.activeTickets !== b.activeTickets) {
      return a.activeTickets - b.activeTickets
    }
    return b.rating - a.rating
  })[0]
}

/** 
 * Timeline item interface for proper event representation.
 * Includes actor attribution and deduplication support.
 */
interface TimelineItem {
  label: string
  time: string
  actorName?: string
  actorRole?: string
  eventType: string
  fromStatus?: string | null
  toStatus?: string | null
  preview?: string | null
}

/**
 * Event type labels for display
 */
const eventTypeLabels: Record<string, string> = {
  Created: "ایجاد تیکت",
  TechnicianOpened: "مشاهده شد",
  StartWork: "شروع کار",
  ReplyAdded: "پاسخ",
  StatusChanged: "تغییر وضعیت",
  Revision: "بازنگری",
  AssignedTechnicians: "تخصیص تکنسین",
  Assigned: "تخصیص",
  Handoff: "واگذاری",
  Closed: "بسته شد",
  Viewed: "مشاهده شد",
}

/**
 * Actor role labels for display badges
 */
const actorRoleLabels: Record<string, string> = {
  Admin: "مدیر",
  Technician: "تکنسین",
  SupervisorTechnician: "سرپرست",
  Client: "کاربر",
}

/**
 * Builds a normalized, deduplicated timeline from ticket activity events.
 * 
 * Rules:
 * - Single-assignee tickets: Dedupe consecutive same-status events from same actor
 * - Multi-assignee tickets: Show separate events when different actors perform actions
 * - Always show actor name and role
 * - Separate workflow status changes from view/seen events
 */
const buildTimeline = (ticket: any): TimelineItem[] => {
  const events = Array.isArray(ticket?.activityEvents) ? ticket.activityEvents : []
  const assignedTechnicians = ticket?.assignedTechnicians ?? []
  const isMultiAssignee = assignedTechnicians.length > 1

  // Start with the Created event
  const timeline: TimelineItem[] = [
    {
      label: "ایجاد تیکت",
      time: ticket?.createdAt,
      actorName: ticket?.clientName || ticket?.createdByName || "کاربر",
      actorRole: "Client",
      eventType: "Created",
      fromStatus: null,
      toStatus: "Submitted",
    },
  ]

  // Sort events chronologically
  const sortedEvents = events
    .slice()
    .filter((e: any) => e?.createdAt)
    .sort((a: any, b: any) => {
      const aTime = parseServerDate(a.createdAt)?.getTime() ?? 0
      const bTime = parseServerDate(b.createdAt)?.getTime() ?? 0
      return aTime - bTime
    })

  // Track seen status changes for deduplication (single-assignee only)
  const seenStatusChanges = new Set<string>()
  // Track view events per actor for proper multi-assignee handling
  const viewedByActor = new Set<string>()

  sortedEvents.forEach((event: any) => {
    const actorName = event.actorName || "نامشخص"
    const actorRole = event.actorRole || "Unknown"
    const eventType = event.eventType || ""
    const newStatus = event.newStatus
    const oldStatus = event.oldStatus

    // Generate dedup key based on event type
    const getStatusKey = () => `status:${newStatus}`
    const getViewKey = () => `view:${event.actorUserId || actorName}`
    const getReplyKey = () => `reply:${event.actorUserId || actorName}:${event.createdAt}`

    // Normalize "seen" events: TechnicianOpened, Viewed, or StatusChanged → SeenRead all show as "مشاهده شد" and dedupe
    const isSeenEvent =
      eventType === "TechnicianOpened" ||
      eventType === "Viewed" ||
      (eventType === "StatusChanged" && newStatus === "SeenRead")
    if (isSeenEvent) {
      const viewKey = getViewKey()
      const minuteKey = `${viewKey}:${parseServerDate(event.createdAt)?.getTime() ? Math.floor(parseServerDate(event.createdAt)!.getTime() / 60000) : event.createdAt}`
      if (isMultiAssignee) {
        if (!viewedByActor.has(minuteKey)) {
          viewedByActor.add(minuteKey)
          timeline.push({
            label: "مشاهده شد",
            time: event.createdAt,
            actorName,
            actorRole,
            eventType: "Viewed",
            fromStatus: null,
            toStatus: null,
          })
        }
      } else {
        if (!viewedByActor.has(minuteKey)) {
          viewedByActor.add(minuteKey)
          timeline.push({
            label: "مشاهده شد",
            time: event.createdAt,
            actorName,
            actorRole,
            eventType: "Viewed",
            fromStatus: null,
            toStatus: null,
          })
        }
      }
      return
    }

    if (eventType === "StartWork") {
      // StartWork is a workflow event - dedupe by status for single-assignee
      const statusKey = "status:InProgress"
      if (!isMultiAssignee && seenStatusChanges.has(statusKey)) {
        return // Skip duplicate
      }
      seenStatusChanges.add(statusKey)
      timeline.push({
        label: "شروع کار",
        time: event.createdAt,
        actorName,
        actorRole,
        eventType: "StartWork",
        fromStatus: oldStatus,
        toStatus: "InProgress",
      })
      return
    }

    if (eventType === "ReplyAdded" || eventType === "MessageAdded") {
      // Replies are always shown (not deduped) - each reply is unique
      let preview: string | null = null
      if (event.metadataJson) {
        try {
          const meta = typeof event.metadataJson === "string" 
            ? JSON.parse(event.metadataJson) 
            : event.metadataJson
          preview = meta.preview || meta.message || null
        } catch {
          // Ignore parse errors
        }
      }
      timeline.push({
        label: "پاسخ",
        time: event.createdAt,
        actorName,
        actorRole,
        eventType: "Reply",
        fromStatus: oldStatus,
        toStatus: newStatus,
        preview,
      })
      return
    }

    if (eventType === "StatusChanged" || eventType === "Revision") {
      // Status changes: dedupe by toStatus for single-assignee
      if (newStatus) {
        const statusKey = getStatusKey()
        if (!isMultiAssignee && seenStatusChanges.has(statusKey)) {
          return // Skip duplicate status
        }
        seenStatusChanges.add(statusKey)
        timeline.push({
          label: eventType === "Revision" ? "بازنگری" : "تغییر وضعیت",
          time: event.createdAt,
          actorName,
          actorRole,
          eventType: eventType,
          fromStatus: oldStatus,
          toStatus: newStatus,
        })
      }
      return
    }

    if (eventType === "AssignedTechnicians" || eventType === "Assigned" || eventType === "Handoff") {
      // Assignment events - always show with actor info
      let assignedTo: string | null = null
      if (event.metadataJson) {
        try {
          const meta = typeof event.metadataJson === "string" 
            ? JSON.parse(event.metadataJson) 
            : event.metadataJson
          assignedTo = meta.assignedTo || meta.technicianNames || null
        } catch {
          // Ignore parse errors
        }
      }
      timeline.push({
        label: eventType === "Handoff" ? "واگذاری" : "تخصیص تکنسین",
        time: event.createdAt,
        actorName,
        actorRole,
        eventType: eventType,
        fromStatus: null,
        toStatus: null,
        preview: assignedTo,
      })
      return
    }

    if (eventType === "Closed") {
      timeline.push({
        label: "بسته شد",
        time: event.createdAt,
        actorName,
        actorRole,
        eventType: "Closed",
        fromStatus: oldStatus,
        toStatus: "Closed",
      })
      return
    }

    // Fallback: if event has newStatus, treat as status change
    if (newStatus) {
      const statusKey = getStatusKey()
      if (!isMultiAssignee && seenStatusChanges.has(statusKey)) {
        return
      }
      seenStatusChanges.add(statusKey)
      timeline.push({
        label: statusLabels[newStatus] || newStatus,
        time: event.createdAt,
        actorName,
        actorRole,
        eventType: "StatusChanged",
        fromStatus: oldStatus,
        toStatus: newStatus,
      })
    }
  })

  // Final dedupe: one "مشاهده شد" per (actor, minute) so no duplicate seen lines
  const seenKeys = new Set<string>()
  const deduped = timeline.filter((item) => {
    const isSeen =
      item.label === "مشاهده شد" ||
      item.eventType === "Viewed" ||
      item.toStatus === "SeenRead"
    if (!isSeen) return true
    const t = parseServerDate(item.time)
    const minute = t ? Math.floor(t.getTime() / 60000) : item.time
    const key = `${item.actorName ?? ""}:${minute}`
    if (seenKeys.has(key)) return false
    seenKeys.add(key)
    return true
  })

  return deduped
}

// Mock technicians data
const mockTechnicians = [
  {
    id: "tech-001",
    name: "علی احمدی",
    email: "ali@company.com",
    specialties: ["network", "hardware"],
    rating: 4.8,
    activeTickets: 3,
    completedTickets: 45,
    status: "available",
    avgResponseTime: "1.5",
  },
  {
    id: "tech-002",
    name: "سارا محمدی",
    email: "sara@company.com",
    specialties: ["software", "security"],
    rating: 4.9,
    activeTickets: 2,
    completedTickets: 52,
    status: "available",
    avgResponseTime: "1.2",
  },
  {
    id: "tech-003",
    name: "حسن رضایی",
    email: "hassan@company.com",
    specialties: ["hardware", "email"],
    rating: 4.7,
    activeTickets: 4,
    completedTickets: 38,
    status: "busy",
    avgResponseTime: "2.5",
  },
  {
    id: "tech-004",
    name: "مریم کریمی",
    email: "maryam@company.com",
    specialties: ["software", "access"],
    rating: 4.6,
    activeTickets: 1,
    completedTickets: 29,
    status: "available",
    avgResponseTime: "1.8",
  },
]

interface AdminTechnicianAssignmentProps {
  tickets: any[]
  technicians: TechnicianProfile[]
  onTicketUpdate: (ticketId: string, updates: any) => void
  authToken?: string | null
}

export function AdminTechnicianAssignment({
  tickets,
  technicians: technicianOptions,
  onTicketUpdate,
  authToken,
}: AdminTechnicianAssignmentProps) {
  const router = useRouter()
  // Filter to only show active technicians
  const technicians = (technicianOptions && technicianOptions.length > 0 ? technicianOptions : []).filter((tech: any) => {
    // If technician has isActive property, use it; otherwise assume active
    return tech.isActive !== false;
  })
  const [searchQuery, setSearchQuery] = useState("")
  const [filterStatus, setFilterStatus] = useState("unassigned")
  const [filterPriority, setFilterPriority] = useState("all")
  const [selectedTickets, setSelectedTickets] = useState<string[]>([])
  const [selectedTicket, setSelectedTicket] = useState<any>(null)
  const [assignDialogOpen, setAssignDialogOpen] = useState(false)
  const [bulkAssignDialogOpen, setBulkAssignDialogOpen] = useState(false)
  const [autoAssignEnabled, setAutoAssignEnabled] = useState(false)
  const [autoAssignDialogOpen, setAutoAssignDialogOpen] = useState(false)
  const [pendingAutoAssignments, setPendingAutoAssignments] = useState<any[]>([])
  const [criteriaDialogOpen, setCriteriaDialogOpen] = useState(false)
  const [selectedTicketForCriteria, setSelectedTicketForCriteria] = useState<any>(null)
  const [directoryItems, setDirectoryItems] = useState<ApiAdminTechnicianDirectoryItemDto[]>([])
  const [directoryLoading, setDirectoryLoading] = useState(false)
  const [directoryError, setDirectoryError] = useState<string | null>(null)

  const filteredTickets = tickets.filter((ticket) => {
    const matchesSearch =
      ticket.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
      ticket.description.toLowerCase().includes(searchQuery.toLowerCase()) ||
      ticket.id.toLowerCase().includes(searchQuery.toLowerCase()) ||
      ticket.clientName.toLowerCase().includes(searchQuery.toLowerCase())

    const matchesStatus =
      filterStatus === "all" ||
      (filterStatus === "unassigned" && !ticket.assignedTo) ||
      (filterStatus === "assigned" && ticket.assignedTo)

    const matchesPriority = filterPriority === "all" || ticket.priority === filterPriority

    return matchesSearch && matchesStatus && matchesPriority
  })

  useEffect(() => {
    if (!assignDialogOpen || !authToken) return
    const loadDirectory = async () => {
      setDirectoryLoading(true)
      setDirectoryError(null)
      try {
        const items = await getAdminTechnicianDirectory(authToken)
        setDirectoryItems(items)
      } catch (error: any) {
        setDirectoryError(error?.message || "خطا در دریافت لیست تکنسین‌ها")
      } finally {
        setDirectoryLoading(false)
      }
    }
    void loadDirectory()
  }, [assignDialogOpen, authToken])

  const suggestedDirectoryTechnicians = useMemo(() => {
    if (!selectedTicket) return []
    if (!directoryItems.length) return []
    const subcategoryId = selectedTicket.subcategoryId
    const categoryId = selectedTicket.categoryId
    return directoryItems.filter((tech) =>
      tech.expertise?.some((tag) =>
        subcategoryId ? tag.subcategoryId === subcategoryId : categoryId ? tag.categoryId === categoryId : false
      )
    )
  }, [directoryItems, selectedTicket])

  const suggestedTechnicians = selectedTicket
    ? directoryItems.length
      ? suggestedDirectoryTechnicians
      : getSuggestedTechnicians(selectedTicket, technicians)
    : []

  const handleAssignTicket = (ticket: any) => {
    setSelectedTicket(ticket)
    setAssignDialogOpen(true)
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

  const handleAssignToTechnician = async (
    technicianUserId: string,
    technicianName: string,
    useLegacyAssign = false
  ) => {
    if (!selectedTicket) return
    if (!authToken || useLegacyAssign) {
      try {
        await onTicketUpdate(selectedTicket.id, {
          assignedTo: technicianUserId,
          assignedTechnicianName: technicianName,
        })
        toast({
          title: "تکنسین تعیین شد",
          description: `تیکت ${selectedTicket.id} به ${technicianName} واگذار شد`,
        })
        setAssignDialogOpen(false)
        setSelectedTicket(null)
      } catch (error) {
        console.error("Failed to assign technician", error)
        toast({
          title: "خطا در تعیین تکنسین",
          description: "لطفاً دوباره تلاش کنید",
          variant: "destructive",
        })
      }
      return
    }
    if (!authToken) {
      toast({
        title: "عدم دسترسی",
        description: "لطفاً دوباره وارد شوید.",
        variant: "destructive",
      })
      return
    }
    try {
      const existingIds = (selectedTicket.assignedTechnicians ?? []).map((t: any) => t.technicianUserId)
      const result = await manualAssignAdminTicket(
        authToken,
        selectedTicket.id,
        [technicianUserId],
        existingIds
      )
      onTicketUpdate(selectedTicket.id, {
        assignedTechnicians: result.assignees,
        assignedTechnicianName: result.assignees?.[0]?.name ?? null,
      })
      toast({
        title: "تکنسین تعیین شد",
        description: `تیکت ${selectedTicket.id} به ${technicianName} واگذار شد`,
      })
      setAssignDialogOpen(false)
      setSelectedTicket(null)
    } catch (error: any) {
      console.error("Failed to assign technician", error)
      toast({
        title: "خطا در تعیین تکنسین",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const handleBulkAssign = async (technicianId: string, technicianName: string) => {
    try {
      await Promise.all(
        selectedTickets.map((ticketId) => {
          const ticket = tickets.find((t) => t.id === ticketId)
          return onTicketUpdate(ticketId, {
            assignedTo: technicianId,
            assignedTechnicianName: technicianName,
            status: ticket?.status === "open" ? "in-progress" : ticket?.status,
          })
        })
      )

      toast({
        title: "تکنسین تعیین شد",
        description: `${selectedTickets.length} تیکت به ${technicianName} واگذار شد`,
      })

      setBulkAssignDialogOpen(false)
      setSelectedTickets([])
    } catch (error) {
      console.error("Failed to bulk assign technicians", error)
      toast({
        title: "خطا در تعیین تکنسین",
        description: "برخی تیکت‌ها ممکن است واگذار نشده باشند",
        variant: "destructive",
      })
    }
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
        title: "تکنسین به صورت خودکار تعیین شد",
        description: result.addedTechnicians?.length
          ? `${result.addedTechnicians.length} تکنسین به تیکت اضافه شد.`
          : "تکنسین جدیدی برای تخصیص یافت نشد.",
      })
    } catch (error: any) {
      console.error("Failed to auto-assign technician", error)
      toast({
        title: "خطا در تعیین خودکار",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const handleBulkAutoAssign = () => {
    const unassignedTickets = selectedTickets
      .map((id) => filteredTickets.find((t) => t.id === id))
      .filter((ticket) => ticket && !ticket.assignedTo)

    const assignments = unassignedTickets.map((ticket) => {
      const recommendedTech = pickAutomaticTechnician(ticket, technicians)
      return {
        ticket,
        technician: recommendedTech,
        success: !!recommendedTech,
      }
    })

    setPendingAutoAssignments(assignments)
    setAutoAssignDialogOpen(true)
  }

  const confirmAutoAssignments = () => {
    let successCount = 0

    pendingAutoAssignments.forEach(({ ticket, technician, success }) => {
      if (success && technician) {
        const currentStatus = (ticket.displayStatus ?? ticket.status) as string
        onTicketUpdate(ticket.id, {
          assignedTo: technician.id,
          assignedTechnicianName: technician.name,
          status: currentStatus === "Open" ? "InProgress" : currentStatus,
        })
        successCount++
      }
    })

    toast({
      title: "تعیین خودکار تکمیل شد",
      description: `${successCount} تیکت به صورت خودکار واگذار شد`,
    })

    setAutoAssignDialogOpen(false)
    setPendingAutoAssignments([])
    setSelectedTickets([])
  }

  const getRecommendedTechnicians = (ticket: any) => {
    return technicians
      .map((tech) => ({
        ...tech,
        score: calculateRecommendationScore(tech, ticket),
      }))
      .sort((a, b) => b.score - a.score)
  }

  const calculateRecommendationScore = (technician: any, ticket: any) => {
    let score = 0

    if (technician.specialties.includes(ticket.category)) {
      score += 50
    }

    if (technician.status === "available") {
      score += 30
    }

    score += Math.max(0, 20 - technician.activeTickets * 5)

    score += technician.rating * 10

    return score
  }

  const getStatusIcon = (status: string) => {
    switch (status) {
      case "available":
        return <CheckCircle className="w-3 h-3 text-green-500" />
      case "busy":
        return <Clock className="w-3 h-3 text-yellow-500" />
      default:
        return <AlertTriangle className="w-3 h-3 text-red-500" />
    }
  }

  const getStatusLabel = (status: string) => {
    switch (status) {
      case "available":
        return "آماده"
      case "busy":
        return "مشغول"
      default:
        return "غیرفعال"
    }
  }

  return (
    <div className="space-y-6" dir="rtl">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <CardTitle className="text-right">تعیین تکنسین</CardTitle>
            <div className="flex items-center gap-3">
              {/* Auto-assign toggle */}
              <div className="flex items-center gap-2">
                <Switch checked={autoAssignEnabled} onCheckedChange={setAutoAssignEnabled} id="auto-assign" />
                <Label htmlFor="auto-assign" className="text-sm">
                  تعیین خودکار
                </Label>
              </div>

              {selectedTickets.length > 0 && (
                <div className="flex gap-2">
                  <Button variant="outline" onClick={handleBulkAutoAssign} className="gap-2 bg-transparent">
                    <Zap className="w-4 h-4" />
                    تعیین خودکار ({selectedTickets.length})
                  </Button>
                  <Button onClick={() => setBulkAssignDialogOpen(true)} className="gap-2">
                    <Users className="w-4 h-4" />
                    تعیین دستی ({selectedTickets.length})
                  </Button>
                </div>
              )}
            </div>
          </div>
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
                className="pr-10 text-right"
                dir="rtl"
              />
            </div>

            <Select value={filterStatus} onValueChange={setFilterStatus} dir="rtl">
              <SelectTrigger className="text-right">
                <SelectValue placeholder="وضعیت واگذاری" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">همه تیکت‌ها</SelectItem>
                <SelectItem value="unassigned">واگذار نشده</SelectItem>
                <SelectItem value="assigned">واگذار شده</SelectItem>
              </SelectContent>
            </Select>

            <Select value={filterPriority} onValueChange={setFilterPriority} dir="rtl">
              <SelectTrigger className="text-right">
                <SelectValue placeholder="اولویت" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">همه اولویت‌ها</SelectItem>
                <SelectItem value="urgent">فوری</SelectItem>
                <SelectItem value="high">بالا</SelectItem>
                <SelectItem value="medium">متوسط</SelectItem>
                <SelectItem value="low">کم</SelectItem>
              </SelectContent>
            </Select>

            <Button
              variant="outline"
              onClick={() => {
                setSearchQuery("")
                setFilterStatus("unassigned")
                setFilterPriority("all")
              }}
              className="gap-2"
            >
              <Filter className="w-4 h-4" />
              پاک کردن فیلترها
            </Button>
          </div>

          {/* Bulk Actions */}
          {selectedTickets.length > 0 && (
            <div className="flex items-center gap-4 mb-4 p-3 bg-muted rounded-lg">
              <span className="text-sm font-medium">{selectedTickets.length} تیکت انتخاب شده</span>
              <Button size="sm" onClick={() => setBulkAssignDialogOpen(true)} className="gap-2">
                <UserPlus className="w-4 h-4" />
                واگذاری گروهی
              </Button>
            </div>
          )}

          {/* Tickets: card list on mobile, table on md+ */}
          <div className="md:hidden space-y-2">
            {filteredTickets.length > 0 ? (
              filteredTickets.map((ticket) => {
                const CategoryIcon = categoryIcons[ticket.category] || HardDrive
                const isSelected = selectedTickets.includes(ticket.id)
                return (
                  <Card
                    key={ticket.id}
                    className={cn("cursor-pointer", isSelected && "ring-2 ring-primary")}
                    onClick={() => router.push(`/tickets/${ticket.id}`)}
                  >
                    <CardContent className="p-3">
                      <div className="flex items-start justify-between gap-2">
                        <div className="min-w-0 flex-1">
                          <p className="font-mono text-xs text-muted-foreground break-words">{ticket.id}</p>
                          <p className="font-medium truncate">{ticket.title}</p>
                          <div className="flex flex-wrap gap-1 mt-1">
                            <Badge className={priorityColors[ticket.priority]}>{priorityLabels[ticket.priority]}</Badge>
                            <span className="text-xs text-muted-foreground">{toFaDate(ticket.createdAt)}</span>
                          </div>
                        </div>
                        <div className="flex gap-1 shrink-0" onClick={(e) => e.stopPropagation()}>
                          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={(e) => { e.stopPropagation(); handleAssignTicket(ticket) }}>
                            <UserPlus className="w-3 h-3" />
                          </Button>
                          <Button variant="ghost" size="icon" className="h-8 w-8" onClick={(e) => { e.stopPropagation(); setSelectedTicketForCriteria(ticket); setCriteriaDialogOpen(true) }}>
                            <Target className="w-3 h-3" />
                          </Button>
                        </div>
                      </div>
                    </CardContent>
                  </Card>
                )
              })
            ) : (
              <div className="border rounded-lg p-8 text-center text-muted-foreground">
                <Search className="w-8 h-8 mx-auto mb-2" />
                <p>تیکتی یافت نشد</p>
              </div>
            )}
          </div>
          <div className="hidden md:block border rounded-lg overflow-x-auto">
            <Table className="min-w-[700px]">
              <TableHeader>
                <TableRow>
                  <TableHead className="w-12">
                    <Checkbox
                      checked={selectedTickets.length === filteredTickets.length && filteredTickets.length > 0}
                      onCheckedChange={handleSelectAll}
                    />
                  </TableHead>
                  <TableHead className="text-right">شماره تیکت</TableHead>
                  <TableHead className="text-right">عنوان</TableHead>
                  <TableHead className="text-right">اولویت</TableHead>
                  <TableHead className="text-right">دسته‌بندی</TableHead>
                  <TableHead className="text-right">درخواست‌کننده</TableHead>
                  <TableHead className="text-right">تکنسین فعلی</TableHead>
                  <TableHead className="text-right">تاریخ ایجاد</TableHead>
                  <TableHead className="text-right">عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {filteredTickets.length > 0 ? (
                  filteredTickets.map((ticket) => {
                    const CategoryIcon = categoryIcons[ticket.category] || HardDrive // Fallback to HardDrive if category not found
                    const isSelected = selectedTickets.includes(ticket.id)

                    return (
                      <TableRow
                        key={ticket.id}
                        className={`cursor-pointer hover:bg-muted/50 ${isSelected ? "bg-muted/50" : ""}`}
                        onClick={() => router.push(`/tickets/${ticket.id}`)}
                      >
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <Checkbox
                            checked={isSelected}
                            onCheckedChange={(checked) => handleSelectTicket(ticket.id, checked as boolean)}
                          />
                        </TableCell>
                        <TableCell className="font-mono text-sm">{ticket.id}</TableCell>
                        <TableCell className="max-w-xs">
                          <div className="truncate" title={ticket.title}>
                            {ticket.title}
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge className={priorityColors[ticket.priority]}>{priorityLabels[ticket.priority]}</Badge>
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <CategoryIcon className="w-4 h-4" />
                            <span className="text-sm">{getCategoryLabel(ticket)}</span>
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <Avatar className="w-6 h-6">
                              <AvatarFallback className="text-xs">{ticket.clientName.charAt(0)}</AvatarFallback>
                            </Avatar>
                            <div>
                              <div className="text-sm font-medium">{ticket.clientName}</div>
                              <div className="text-xs text-muted-foreground">{ticket.clientEmail}</div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          <AssignedTechniciansCell technicians={normalizeAssignedTechnicians(ticket)} />
                        </TableCell>
                        <TableCell className="text-sm">
                          {toFaDate(ticket.createdAt)}
                        </TableCell>
                        <TableCell onClick={(e) => e.stopPropagation()}>
                          <div className="flex gap-1">
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleAssignTicket(ticket)
                              }}
                              className="gap-1"
                            >
                              <UserPlus className="w-3 h-3" />
                              انتخاب دستی
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={(e) => {
                                e.stopPropagation()
                                handleAutoAssign(ticket)
                              }}
                              className="gap-1 text-blue-600 hover:text-blue-700"
                            >
                              <Zap className="w-3 h-3" />
                              خودکار
                            </Button>
                            <Button
                              variant="ghost"
                              size="sm"
                              onClick={(e) => {
                                e.stopPropagation()
                                setSelectedTicketForCriteria(ticket)
                                setCriteriaDialogOpen(true)
                              }}
                              className="gap-1 text-purple-600 hover:text-purple-700"
                            >
                              <Target className="w-3 h-3" />
                              تحلیل
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    )
                  })
                ) : (
                  <TableRow>
                    <TableCell colSpan={9} className="text-center py-8">
                      <div className="flex flex-col items-center gap-2">
                        <Search className="w-8 h-8 text-muted-foreground" />
                        <p className="text-muted-foreground">تیکتی یافت نشد</p>
                      </div>
                    </TableCell>
                  </TableRow>
                )}
              </TableBody>
            </Table>
          </div>
        </CardContent>
      </Card>

      {/* Assign Technician Dialog - responsive */}
      <Dialog open={assignDialogOpen} onOpenChange={setAssignDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl text-right" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">واگذاری تیکت {selectedTicket?.id}</DialogTitle>
          </DialogHeader>
          {selectedTicket && (
            <div className="space-y-6">
              {/* Ticket Info */}
              <div className="bg-muted p-4 rounded-lg">
                <div className="text-sm text-muted-foreground mb-2">شناسه: {selectedTicket.id}</div>
                <h4 className="font-medium mb-2">{selectedTicket.title}</h4>
                <div className="flex flex-wrap gap-2 mb-2">
                  <Badge className={priorityColors[selectedTicket.priority]}>
                    {priorityLabels[selectedTicket.priority]}
                  </Badge>
                  <Badge variant="outline">{getCategoryLabel(selectedTicket)}</Badge>
                  {getSubcategoryLabel(selectedTicket) ? (
                    <Badge variant="outline">{getSubcategoryLabel(selectedTicket)}</Badge>
                  ) : null}
                </div>
                <p className="text-sm text-muted-foreground">درخواست‌کننده: {selectedTicket.clientName}</p>
              </div>

              <Separator />

              {/* Suggested Technicians */}
              <div>
                <h4 className="font-medium mb-4 flex items-center gap-2">
                  <Star className="w-4 h-4 text-yellow-500" />
                  تکنسین‌های پیشنهادی
                </h4>
                <div className="grid gap-3">
                  {directoryLoading ? (
                    <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
                  ) : directoryError ? (
                    <div className="text-sm text-red-600">{directoryError}</div>
                  ) : suggestedTechnicians.length === 0 ? (
                    <div className="text-sm text-muted-foreground">
                      تکنسینی با دسته‌بندی و زیر‌دسته‌بندی دقیق یافت نشد.
                    </div>
                  ) : (
                    suggestedTechnicians.map((technician: any) =>
                      "technicianUserId" in technician ? (
                        <div
                          key={technician.technicianUserId}
                          className="flex items-center justify-between p-3 border rounded-lg hover:bg-muted/50"
                        >
                          <div className="flex items-center gap-3">
                            <Avatar className="w-10 h-10">
                              <AvatarFallback>{technician.name.charAt(0)}</AvatarFallback>
                            </Avatar>
                            <div>
                              <div className="flex items-center gap-2">
                                <span className="font-medium">{technician.name}</span>
                                <span className="text-xs text-muted-foreground">
                                  {technician.availability === "Free" ? "آزاد" : "پرمشغله"}
                                </span>
                              </div>
                              <div className="flex items-center gap-4 text-sm text-muted-foreground">
                                <span>
                                  {technician.inboxLeftActiveNonTerminal}/{technician.inboxTotalActive} فعال
                                </span>
                              </div>
                              <div className="flex flex-wrap gap-1 mt-1">
                                {technician.expertise?.map((tag: any) => (
                                  <div
                                    key={`${tag.categoryId}-${tag.subcategoryId}`}
                                    className="flex items-center gap-1 px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs"
                                  >
                                    <span>{tag.categoryName}</span>
                                    <span>/</span>
                                    <span>{tag.subcategoryName}</span>
                                  </div>
                                ))}
                              </div>
                            </div>
                          </div>
                          <Button
                            onClick={() => handleAssignToTechnician(technician.technicianUserId, technician.name)}
                            className="gap-2"
                          >
                            <UserPlus className="w-4 h-4" />
                            واگذاری
                          </Button>
                        </div>
                      ) : (
                        <div
                          key={technician.id}
                          className="flex items-center justify-between p-3 border rounded-lg hover:bg-muted/50"
                        >
                          <div className="flex items-center gap-3">
                            <Avatar className="w-10 h-10">
                              <AvatarFallback>{technician.name.charAt(0)}</AvatarFallback>
                            </Avatar>
                            <div>
                              <div className="flex items-center gap-2">
                                <span className="font-medium">{technician.name}</span>
                                {getStatusIcon(technician.status)}
                                <span className="text-xs text-muted-foreground">{getStatusLabel(technician.status)}</span>
                              </div>
                              <div className="flex items-center gap-4 text-sm text-muted-foreground">
                                <div className="flex items-center gap-1">
                                  <Star className="w-3 h-3 text-yellow-500" />
                                  <span>{technician.rating}</span>
                                </div>
                                <span>تیکت‌های فعال: {technician.activeTickets}</span>
                                <span>تکمیل شده: {technician.completedTickets}</span>
                              </div>
                              <div className="flex gap-1 mt-1">
                                {technician.specialties.map((specialty: string) => {
                                  const SpecialtyIcon = categoryIcons[specialty] || HardDrive
                                  return (
                                    <div
                                      key={specialty}
                                      className="flex items-center gap-1 px-2 py-1 bg-blue-100 text-blue-800 rounded text-xs"
                                    >
                                      <SpecialtyIcon className="w-3 h-3" />
                                      <span>{categoryLabels[specialty] ?? specialty}</span>
                                    </div>
                                  )
                                })}
                              </div>
                            </div>
                          </div>
                          <Button
                            onClick={() => handleAssignToTechnician(technician.id, technician.name, true)}
                            className="gap-2"
                          >
                            <UserPlus className="w-4 h-4" />
                            واگذاری
                          </Button>
                        </div>
                      )
                    )
                  )}
                </div>
              </div>

              <Separator />

              {/* All Technicians */}
              <div>
                <h4 className="font-medium mb-4 flex items-center gap-2">
                  <Wrench className="w-4 h-4" />
                  همه تکنسین‌ها
                </h4>
                <div className="grid gap-2 max-h-60 overflow-y-auto">
                  {directoryLoading ? (
                    <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
                  ) : directoryError ? (
                    <div className="text-sm text-red-600">{directoryError}</div>
                  ) : directoryItems.length > 0 ? (
                    directoryItems.map((technician) => (
                      <div
                        key={technician.technicianUserId}
                        className="flex items-center justify-between p-2 border rounded hover:bg-muted/50"
                      >
                        <div className="flex items-center gap-2">
                          <Avatar className="w-8 h-8">
                            <AvatarFallback className="text-sm">{technician.name.charAt(0)}</AvatarFallback>
                          </Avatar>
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-medium">{technician.name}</span>
                              <span className="text-xs text-muted-foreground">
                                {technician.availability === "Free" ? "آزاد" : "پرمشغله"}
                              </span>
                            </div>
                            <div className="text-xs text-muted-foreground">
                              فعال: {technician.inboxLeftActiveNonTerminal} | کل: {technician.inboxTotalActive}
                            </div>
                          </div>
                        </div>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleAssignToTechnician(technician.technicianUserId, technician.name)}
                        >
                          انتخاب
                        </Button>
                      </div>
                    ))
                  ) : (
                    technicians.map((technician) => (
                      <div
                        key={technician.id}
                        className="flex items-center justify-between p-2 border rounded hover:bg-muted/50"
                      >
                        <div className="flex items-center gap-2">
                          <Avatar className="w-8 h-8">
                            <AvatarFallback className="text-sm">{technician.name.charAt(0)}</AvatarFallback>
                          </Avatar>
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="text-sm font-medium">{technician.name}</span>
                              {getStatusIcon(technician.status)}
                            </div>
                            <div className="text-xs text-muted-foreground">
                              فعال: {technician.activeTickets} | تکمیل: {technician.completedTickets}
                            </div>
                          </div>
                        </div>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => handleAssignToTechnician(technician.id, technician.name, true)}
                        >
                          انتخاب
                        </Button>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Bulk Assign Dialog */}
      <Dialog open={bulkAssignDialogOpen} onOpenChange={setBulkAssignDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">واگذاری گروهی ({selectedTickets.length} تیکت)</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div>
              <h4 className="font-medium mb-3">انتخاب تکنسین</h4>
              <div className="grid gap-2 max-h-60 overflow-y-auto">
                {technicians.map((technician) => (
                  <div
                    key={technician.id}
                    className="flex items-center justify-between p-3 border rounded hover:bg-muted/50"
                  >
                    <div className="flex items-center gap-3">
                      <Avatar className="w-8 h-8">
                        <AvatarFallback>{technician.name.charAt(0)}</AvatarFallback>
                      </Avatar>
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{technician.name}</span>
                          {getStatusIcon(technician.status)}
                          <span className="text-xs text-muted-foreground">{getStatusLabel(technician.status)}</span>
                        </div>
                        <div className="text-sm text-muted-foreground">
                          فعال: {technician.activeTickets} | امتیاز: {technician.rating}
                        </div>
                      </div>
                    </div>
                    <Button onClick={() => handleBulkAssign(technician.id, technician.name)} className="gap-2">
                      <Users className="w-4 h-4" />
                      واگذاری همه
                    </Button>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* Auto Assignment Confirmation Dialog */}
      <Dialog open={autoAssignDialogOpen} onOpenChange={setAutoAssignDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">تأیید تعیین خودکار تکنسین</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="bg-blue-50 p-4 rounded-lg border border-blue-200">
              <div className="flex items-center gap-2 mb-2">
                <Zap className="w-5 h-5 text-blue-600" />
                <h4 className="font-medium text-blue-900">پیش‌نمایش تعیین خودکار</h4>
              </div>
              <p className="text-sm text-blue-700">
                سیستم بر اساس تخصص، امتیاز، و بار کاری تکنسین‌ها، بهترین گزینه را انتخاب کرده است.
              </p>
            </div>

            <div className="space-y-3 max-h-96 overflow-y-auto">
              {pendingAutoAssignments.map(({ ticket, technician, success }, index) => (
                <div
                  key={ticket.id}
                  className={`p-4 border rounded-lg ${success ? "bg-green-50 border-green-200" : "bg-red-50 border-red-200"}`}
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-2">
                        <span className="font-medium">{ticket.title}</span>
                        <Badge className={priorityColors[ticket.priority]} variant="outline">
                          {priorityLabels[ticket.priority]}
                        </Badge>
                        <Badge variant="outline">{getCategoryLabel(ticket)}</Badge>
                      </div>
                      <p className="text-sm text-muted-foreground">درخواست‌کننده: {ticket.clientName}</p>
                    </div>

                    <div className="text-left">
                      {success && technician ? (
                        <div className="flex items-center gap-2">
                          <CheckCircle className="w-4 h-4 text-green-600" />
                          <div>
                            <p className="font-medium text-green-800">{technician.name}</p>
                            <div className="flex items-center gap-1 text-xs text-green-600">
                              <Star className="w-3 h-3" />
                              <span>{technician.rating}</span>
                              <span>• {technician.activeTickets} فعال</span>
                            </div>
                          </div>
                        </div>
                      ) : (
                        <div className="flex items-center gap-2 text-red-600">
                          <AlertTriangle className="w-4 h-4" />
                          <span className="text-sm">تکنسین مناسب یافت نشد</span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>

            <div className="flex justify-between items-center pt-4 border-t">
              <div className="text-sm text-muted-foreground">
                {pendingAutoAssignments.filter((a) => a.success).length} از {pendingAutoAssignments.length} تیکت قابل
                واگذاری
              </div>
              <div className="flex gap-2">
                <Button variant="outline" onClick={() => setAutoAssignDialogOpen(false)}>
                  انصراف
                </Button>
                <Button
                  onClick={confirmAutoAssignments}
                  disabled={pendingAutoAssignments.filter((a) => a.success).length === 0}
                  className="gap-2"
                >
                  <CheckCircle className="w-4 h-4" />
                  تأیید و اجرا
                </Button>
              </div>
            </div>
          </div>
        </DialogContent>
      </Dialog>

      {/* Analyze Dialog */}
      <Dialog open={criteriaDialogOpen} onOpenChange={setCriteriaDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">تحلیل تیکت</DialogTitle>
          </DialogHeader>
          {selectedTicketForCriteria ? (() => {
            const t = selectedTicketForCriteria
            const isAccepted = t.isAccepted === true
            const canonicalStatus = t.canonicalStatus ?? t.displayStatus ?? t.status
            const isActuallyClosed = canonicalStatus === "Solved" || canonicalStatus === "Closed"
            const closeTimeFromEvent = t.activityEvents
              ?.slice()
              ?.reverse()
              ?.find((e: { newStatus?: string }) => e.newStatus === "Closed" || e.newStatus === "Solved")
              ?.createdAt
            const endTimeForDuration = isAccepted && isActuallyClosed && closeTimeFromEvent
              ? closeTimeFromEvent
              : t.updatedAt ?? t.createdAt
            return (
            <div className="space-y-6">
              <div className="bg-muted p-4 rounded-lg space-y-2">
                <div className="text-sm text-muted-foreground">شناسه: {t.id}</div>
                <div className="text-lg font-medium">{t.title}</div>
                <div className="flex flex-wrap gap-2">
                  {!isAccepted ? (
                    <Badge variant="secondary" className="font-iran">در انتظار پذیرش</Badge>
                  ) : isActuallyClosed ? (
                    <Badge className={priorityColors[t.priority]}>{priorityLabels[t.priority]}</Badge>
                  ) : (
                    <Badge className={priorityColors[t.priority]}>{priorityLabels[t.priority]}</Badge>
                  )}
                  <Badge variant="outline">{getCategoryLabel(t)}</Badge>
                  {getSubcategoryLabel(t) ? (
                    <Badge variant="outline">{getSubcategoryLabel(t)}</Badge>
                  ) : null}
                  <Badge variant="secondary">{t.clientName}</Badge>
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <Card>
                  <CardHeader>
                    <CardTitle className="text-right text-sm">زمان‌بندی</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-2 text-sm">
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">ایجاد:</span>
                      <span>{formatDateTime(t.createdAt)}</span>
                    </div>
                    {isAccepted && isActuallyClosed && closeTimeFromEvent ? (
                      <>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">بستن:</span>
                          <span>{formatDateTime(closeTimeFromEvent)}</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">مدت زمان کل:</span>
                          <span>{formatDuration(t.createdAt, closeTimeFromEvent)}</span>
                        </div>
                      </>
                    ) : (
                      <>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">آخرین بروزرسانی:</span>
                          <span>{formatDateTime(t.updatedAt ?? t.createdAt)}</span>
                        </div>
                        <div className="flex justify-between">
                          <span className="text-muted-foreground">{isAccepted ? "مدت تا آخرین بروزرسانی:" : "زمان از ایجاد:"}</span>
                          <span>{formatDuration(t.createdAt, endTimeForDuration)}</span>
                        </div>
                      </>
                    )}
                  </CardContent>
                </Card>
                <Card>
                  <CardHeader>
                    <CardTitle className="text-right text-sm">پاسخ‌دهندگان</CardTitle>
                  </CardHeader>
                  <CardContent className="text-sm">
                    {!isAccepted
                      ? "در انتظار پذیرش / بدون پذیرش تکنسین"
                      : (t.assignedTechnicians && t.assignedTechnicians.length > 0
                          ? t.assignedTechnicians.map((tech: { technicianName?: string }) => tech.technicianName).join("، ")
                          : t.assignedTechnicianName || "—")}
                  </CardContent>
                </Card>
              </div>

              <Card>
                <CardHeader>
                  <CardTitle className="text-right text-sm">Timeline وضعیت‌ها</CardTitle>
                </CardHeader>
                <CardContent>
                  {!isAccepted ? (
                    <div className="space-y-3">
                      <p className="text-sm text-muted-foreground">هنوز پذیرش نشده — فقط رویداد ایجاد.</p>
                      <div className="flex items-start gap-3">
                        <span className="w-2 h-2 rounded-full mt-2 bg-blue-500" />
                        <div className="text-sm">
                          <span className="font-medium">ایجاد تیکت</span>
                          <span className="text-muted-foreground mx-2">—</span>
                          <span className="text-muted-foreground text-xs">{formatDateTime(t.createdAt)}</span>
                        </div>
                      </div>
                    </div>
                  ) : (
                  <div className="space-y-3">
                    {buildTimeline(selectedTicketForCriteria).map((item, index) => {
                      // Determine dot color based on event type
                      const getDotColor = () => {
                        if (item.eventType === "Created") return "bg-blue-500"
                        if (item.eventType === "Viewed") return "bg-gray-400"
                        if (item.eventType === "Reply") return "bg-green-500"
                        if (item.eventType === "Closed") return "bg-slate-600"
                        if (item.toStatus === "Solved") return "bg-emerald-500"
                        return "bg-primary"
                      }

                      // Format status change label (never show internal codes like SeenRead)
                      const getEventLabel = () => {
                        if (item.eventType === "StatusChanged" && item.fromStatus && item.toStatus) {
                          if (item.toStatus === "SeenRead") return "مشاهده شد"
                          const from = statusLabels[item.fromStatus] ?? item.fromStatus
                          const to = statusLabels[item.toStatus] ?? item.toStatus
                          return `${from} → ${to}`
                        }
                        if (item.eventType === "StartWork") return "شروع کار"
                        return statusLabels[item.label] ?? statusLabels[item.toStatus] ?? item.label
                      }

                      // Get role badge variant
                      const getRoleBadgeVariant = () => {
                        if (item.actorRole === "Admin") return "default"
                        if (item.actorRole === "SupervisorTechnician") return "secondary"
                        if (item.actorRole === "Technician") return "outline"
                        return "secondary"
                      }

                      return (
                        <div key={`${item.eventType}-${item.actorName}-${index}`} className="flex items-start gap-3">
                          <span className={`w-2 h-2 rounded-full mt-2 ${getDotColor()}`} />
                          <div className="text-sm flex-1">
                            <div className="flex items-center flex-wrap gap-2">
                              <span className="font-medium">{getEventLabel()}</span>
                              {item.actorName && (
                                <>
                                  <span className="text-muted-foreground">—</span>
                                  <span className="text-muted-foreground">{item.actorName}</span>
                                  {item.actorRole && (
                                    <Badge variant={getRoleBadgeVariant()} className="text-xs py-0 px-1.5">
                                      {actorRoleLabels[item.actorRole] ?? item.actorRole}
                                    </Badge>
                                  )}
                                </>
                              )}
                              <span className="text-muted-foreground">—</span>
                              <span className="text-muted-foreground text-xs">{formatDateTime(item.time)}</span>
                            </div>
                            {item.preview && (
                              <p className="text-xs text-muted-foreground mt-1 truncate max-w-md">
                                {item.preview}
                              </p>
                            )}
                          </div>
                        </div>
                      )
                    })}
                  </div>
                  )}
                </CardContent>
              </Card>
            </div>
            )
          })() : (
            <div className="text-sm text-muted-foreground">تیکتی انتخاب نشده است.</div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}


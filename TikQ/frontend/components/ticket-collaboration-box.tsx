"use client"

import { useState, useEffect, useRef } from "react"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Separator } from "@/components/ui/separator"
import { Users, Clock, AlertCircle, CheckCircle, MessageSquare } from "lucide-react"
import { useAuth } from "@/lib/auth-context"
import { getTicketCollaboration, updateWorkSession } from "@/lib/tickets-api"
import { getStatusLabel, getStatusColor } from "@/lib/ticket-status"
import type { ApiTicketCollaborationResponse, ApiUpdateWorkSessionRequest } from "@/lib/api-types"
import { toast } from "@/hooks/use-toast"
import { useSignalR } from "@/hooks/use-signalr"
import { parseServerDate } from "@/lib/datetime"

const stateLabels: Record<string, string> = {
  Idle: "بیکار",
  Investigating: "در حال بررسی",
  Responding: "در حال پاسخ",
  WaitingForClient: "در انتظار مشتری",
  Done: "انجام شده",
}

const activityLabels: Record<string, string> = {
  StatusChanged: "تغییر وضعیت",
  CommentAdded: "افزودن نظر",
  AssignmentChanged: "تغییر اختصاص",
  TechnicianStateChanged: "تغییر وضعیت تکنسین",
  WorkNoteAdded: "بروزرسانی کار",
}

interface TicketCollaborationBoxProps {
  ticketId: string
}

import { getEffectiveApiBaseUrl, joinApi } from "@/lib/url";

const API_BASE_URL = getEffectiveApiBaseUrl()
// PHASE 3: Use correct SignalR hub URL (matches backend TicketHub at /hubs/tickets)
const SIGNALR_HUB_URL = joinApi(API_BASE_URL, "/hubs/tickets")

export function TicketCollaborationBox({ ticketId }: TicketCollaborationBoxProps) {
  const { token, user } = useAuth()
  const [collaboration, setCollaboration] = useState<ApiTicketCollaborationResponse | null>(null)
  const [loading, setLoading] = useState(true)
  const [submitting, setSubmitting] = useState(false)
  const [workingOn, setWorkingOn] = useState("")
  const [note, setNote] = useState("")
  const [state, setState] = useState<"Idle" | "Investigating" | "Responding" | "WaitingForClient" | "Done">("Idle")
  const pollIntervalRef = useRef<NodeJS.Timeout | null>(null)
  const lastUpdateRef = useRef<string | null>(null)

  const isTechnician = user?.role === "technician"
  const isAdmin = user?.role === "admin"

  // SignalR connection
  const { connection, connected, invoke, on } = useSignalR(SIGNALR_HUB_URL)

  useEffect(() => {
    if (!user) return

    // PHASE 3: Subscribe to ticket updates when SignalR is connected
    // Uses backend method names: SubscribeToTicket/UnsubscribeFromTicket
    if (connected && connection && invoke) {
      invoke("SubscribeToTicket", ticketId).catch((err) => {
        console.error("Failed to subscribe to ticket:", err)
      })
    }

    // PHASE 3: Listen for real-time ticket updates (TicketUpdated event from backend)
    const unsubscribeUpdated = on?.("TicketUpdated", (data: { ticketId: string; updateType: string; metadata?: any }) => {
      if (data.ticketId === ticketId) {
        // Refresh collaboration data when ticket is updated
        loadCollaboration()
        lastUpdateRef.current = new Date().toISOString()
        // Show toast for updates
        if (data.metadata?.authorName && data.metadata?.authorName !== user?.fullName) {
          toast({
            title: "بروزرسانی تیکت",
            description: data.updateType === "ReplyAdded" 
              ? `${data.metadata.authorName} پاسخی اضافه کرد`
              : `${data.metadata.authorName} وضعیت را بروزرسانی کرد`,
          })
        }
      }
    })

    // Also listen for status changes
    const unsubscribeStatus = on?.("TicketStatusUpdated", (data: { ticketId: string; newStatus: string; actorRole: string }) => {
      if (data.ticketId === ticketId) {
        loadCollaboration()
        lastUpdateRef.current = new Date().toISOString()
      }
    })

    loadCollaboration()

    // Setup polling as fallback (every 15 seconds) when SignalR is disconnected
    pollIntervalRef.current = setInterval(() => {
      if (!connected) {
        loadCollaboration()
      }
    }, 15000)

    // Poll on window focus
    const handleFocus = () => {
      loadCollaboration()
    }
    window.addEventListener("focus", handleFocus)

    return () => {
      if (pollIntervalRef.current) {
        clearInterval(pollIntervalRef.current)
      }
      window.removeEventListener("focus", handleFocus)
      // Unsubscribe from ticket when leaving
      if (connected && connection && invoke) {
        invoke("UnsubscribeFromTicket", ticketId).catch(() => {})
      }
      unsubscribeUpdated?.()
      unsubscribeStatus?.()
    }
  }, [token, ticketId, connected, connection, user?.id, user?.fullName])

  const loadCollaboration = async () => {
    if (!user) return
    try {
      const data = await getTicketCollaboration(token, ticketId)
      setCollaboration(data)
      lastUpdateRef.current = new Date().toISOString()
    } catch (error: any) {
      console.error("Failed to load collaboration data:", error)
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (!user || !workingOn.trim()) return

    try {
      setSubmitting(true)
      const request: ApiUpdateWorkSessionRequest = {
        workingOn: workingOn.trim(),
        note: note.trim() || null,
        state,
      }
      await updateWorkSession(token, ticketId, request)
      toast({
        title: "موفق",
        description: "بروزرسانی کار با موفقیت ثبت شد",
      })
      setWorkingOn("")
      setNote("")
      // Reload to get updated data
      await loadCollaboration()
    } catch (error: any) {
      console.error("Failed to update work session:", error)
      toast({
        title: "خطا",
        description: "خطا در ثبت بروزرسانی کار",
        variant: "destructive",
      })
    } finally {
      setSubmitting(false)
    }
  }

  const formatRelativeTime = (dateString: string) => {
    const date = parseServerDate(dateString)
    if (!date) return "—"
    const now = new Date()
    const diffMs = now.getTime() - date.getTime()
    const diffMins = Math.floor(diffMs / 60000)
    const diffHours = Math.floor(diffMs / 3600000)
    const diffDays = Math.floor(diffMs / 86400000)

    if (diffMins < 1) return "همین الان"
    if (diffMins < 60) return `${diffMins} دقیقه پیش`
    if (diffHours < 24) return `${diffHours} ساعت پیش`
    return `${diffDays} روز پیش`
  }

  if (loading) {
    return (
      <Card>
        <CardContent className="pt-6">
          <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
        </CardContent>
      </Card>
    )
  }

  if (!collaboration) {
    return null
  }

  // Check if someone is actively working (Responding or Investigating)
  const activeWorkers = collaboration.activeTechnicians.filter(
    (t) => t.state === "Responding" || t.state === "Investigating"
  )
  const otherActiveWorker = activeWorkers.find((t) => t.technicianUserId !== user?.id)

  return (
    <Card dir="rtl">
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Users className="h-5 w-5" />
          همکاری و بروزرسانی‌های زنده
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-6">
        {/* Current Status */}
        <div>
          <Label className="text-sm font-medium mb-2 block">وضعیت فعلی تیکت</Label>
          <Badge className={getStatusColor(collaboration.status)}>
            {getStatusLabel(collaboration.status)}
          </Badge>
        </div>

        <Separator />

        {/* Latest Update */}
        {collaboration.lastActivity && (
          <div>
            <Label className="text-sm font-medium mb-2 block">آخرین بروزرسانی</Label>
            <div className="p-3 bg-muted rounded-lg">
              <div className="flex items-center justify-between mb-1">
                <span className="font-medium">{collaboration.lastActivity.actorName}</span>
                <span className="text-xs text-muted-foreground">
                  {formatRelativeTime(collaboration.lastActivity.createdAt)}
                </span>
              </div>
              <p className="text-sm text-muted-foreground">{collaboration.lastActivity.message}</p>
              <Badge variant="outline" className="text-xs mt-2">
                {activityLabels[collaboration.lastActivity.type] || collaboration.lastActivity.type}
              </Badge>
            </div>
          </div>
        )}

        <Separator />

        {/* Active Technicians */}
        <div>
          <Label className="text-sm font-medium mb-2 block">چه کسی روی چی کار می‌کند؟</Label>
          {collaboration.activeTechnicians.length === 0 ? (
            <p className="text-sm text-muted-foreground">فعلا هیچ کس در حال کار نیست</p>
          ) : (
            <div className="space-y-3">
              {otherActiveWorker && (
                <div className="p-3 bg-amber-50 border border-amber-200 rounded-lg">
                  <div className="flex items-center gap-2 mb-1">
                    <AlertCircle className="h-4 w-4 text-amber-600" />
                    <span className="text-sm font-medium text-amber-900">
                      این تیکت توسط {otherActiveWorker.name} در حال پیگیری است
                    </span>
                  </div>
                </div>
              )}
              {collaboration.activeTechnicians.map((tech) => (
                <div
                  key={tech.technicianUserId}
                  className="p-3 border rounded-lg hover:bg-muted/50 transition-colors"
                >
                  <div className="flex items-start justify-between mb-2">
                    <div className="flex-1">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="font-medium">{tech.name}</span>
                        <Badge variant="outline" className="text-xs">
                          {stateLabels[tech.state] || tech.state}
                        </Badge>
                      </div>
                      <p className="text-sm text-foreground mb-1">{tech.workingOn}</p>
                      {tech.note && <p className="text-xs text-muted-foreground">{tech.note}</p>}
                    </div>
                    <span className="text-xs text-muted-foreground">
                      {formatRelativeTime(tech.updatedAt)}
                    </span>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>

        {/* My Work Form (for technicians) */}
        {isTechnician && (
          <>
            <Separator />
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <Label htmlFor="state">وضعیت کار</Label>
                <Select value={state} onValueChange={(v: any) => setState(v)}>
                  <SelectTrigger id="state">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Idle">{stateLabels.Idle}</SelectItem>
                    <SelectItem value="Investigating">{stateLabels.Investigating}</SelectItem>
                    <SelectItem value="Responding">{stateLabels.Responding}</SelectItem>
                    <SelectItem value="WaitingForClient">{stateLabels.WaitingForClient}</SelectItem>
                    <SelectItem value="Done">{stateLabels.Done}</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div>
                <Label htmlFor="workingOn">دارم روی این بخش کار می‌کنم...</Label>
                <Input
                  id="workingOn"
                  value={workingOn}
                  onChange={(e) => setWorkingOn(e.target.value)}
                  placeholder="مثال: شبکه، درایور پرینتر، مشکل دسترسی..."
                  required
                />
              </div>

              <div>
                <Label htmlFor="note">یادداشت (اختیاری)</Label>
                <Textarea
                  id="note"
                  value={note}
                  onChange={(e) => setNote(e.target.value)}
                  placeholder="یادداشت کوتاه..."
                  rows={2}
                />
              </div>

              <Button type="submit" disabled={submitting || !workingOn.trim()}>
                {submitting ? "در حال ثبت..." : "ثبت بروزرسانی"}
              </Button>
            </form>
          </>
        )}

        {/* Recent Activities */}
        {collaboration.recentActivities.length > 0 && (
          <>
            <Separator />
            <div>
              <Label className="text-sm font-medium mb-2 block">آخرین فعالیت‌ها</Label>
              <div className="space-y-2 max-h-64 overflow-y-auto">
                {collaboration.recentActivities.map((activity) => (
                  <div key={activity.id} className="flex items-start gap-2 p-2 rounded hover:bg-muted/50">
                    <MessageSquare className="h-4 w-4 text-muted-foreground mt-0.5" />
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="text-sm font-medium">{activity.actorName}</span>
                        <Badge variant="outline" className="text-xs">
                          {activityLabels[activity.type] || activity.type}
                        </Badge>
                      </div>
                      <p className="text-xs text-muted-foreground mb-1">{activity.message}</p>
                      <p className="text-xs text-muted-foreground">
                        {formatRelativeTime(activity.createdAt)}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  )
}
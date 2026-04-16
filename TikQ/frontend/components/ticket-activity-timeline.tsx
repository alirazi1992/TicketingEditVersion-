"use client"

import { useState, useEffect } from "react"
import { Card, CardContent } from "@/components/ui/card"
import { Badge } from "@/components/ui/badge"
import { Clock, MessageSquare, UserPlus, CheckCircle, XCircle } from "lucide-react"
import { useAuth } from "@/lib/auth-context"
import { toFaDateTime } from "@/lib/datetime"
import { getTicketActivities } from "@/lib/tickets-api"
import type { ApiTicketActivityDto } from "@/lib/api-types"

const activityLabels: Record<string, string> = {
  StatusChanged: "تغییر وضعیت",
  CommentAdded: "افزودن نظر",
  AssignmentChanged: "تغییر اختصاص",
  TechnicianStateChanged: "تغییر وضعیت تکنسین",
  WorkNoteAdded: "بروزرسانی کار",
}

const activityIcons: Record<string, any> = {
  StatusChanged: CheckCircle,
  CommentAdded: MessageSquare,
  AssignmentChanged: UserPlus,
  TechnicianStateChanged: Clock,
}

interface TicketActivityTimelineProps {
  ticketId: string
}

export function TicketActivityTimeline({ ticketId }: TicketActivityTimelineProps) {
  const { token, user } = useAuth()
  const [activities, setActivities] = useState<ApiTicketActivityDto[]>([])
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!user) return
    loadActivities()
  }, [user, ticketId])

  const loadActivities = async () => {
    if (!user) return
    try {
      setLoading(true)
      const data = await getTicketActivities(token, ticketId)
      setActivities(data)
    } catch (error: any) {
      console.error("Failed to load activities:", error)
    } finally {
      setLoading(false)
    }
  }

  if (loading) {
    return <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
  }

  if (activities.length === 0) {
    return <p className="text-sm text-muted-foreground">فعلا فعالیتی ثبت نشده است</p>
  }

  return (
    <div className="space-y-4" dir="rtl">
      <h3 className="text-lg font-semibold">زمان‌خط فعالیت‌ها</h3>
      <div className="space-y-3">
        {activities.map((activity) => {
          const Icon = activityIcons[activity.type] || Clock
          return (
            <Card key={activity.id}>
              <CardContent className="pt-6">
                <div className="flex items-start gap-3">
                  <div className="mt-1">
                    <Icon className="h-5 w-5 text-muted-foreground" />
                  </div>
                  <div className="flex-1">
                    <div className="flex items-center gap-2 mb-1">
                      <p className="font-medium">{activity.actorName}</p>
                      <Badge variant="outline" className="text-xs">
                        {activityLabels[activity.type] || activity.type}
                      </Badge>
                    </div>
                    <p className="text-sm text-muted-foreground mb-1">{activity.message}</p>
                    <p className="text-xs text-muted-foreground">{toFaDateTime(activity.createdAt)}</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          )
        })}
      </div>
    </div>
  )
}

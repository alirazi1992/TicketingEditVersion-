"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { apiRequest, apiGetNoStore } from "@/lib/api-client";
import type { ApiTicketResponse, ApiTicketMessageDto } from "@/lib/api-types";
import { mapApiTicketToUi, mapApiMessageToResponse, mapUiStatusToApi } from "@/lib/ticket-mappers";
import { useCategories } from "@/services/useCategories";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/hooks/use-toast";
import { ArrowRight, Calendar, Hash, User, Flag, MessageSquare, Users, Clock } from "lucide-react";
import type { Ticket } from "@/types";
import { TICKET_STATUS_LABELS, getTicketStatusLabel, getEffectiveStatus, type TicketStatus } from "@/lib/ticket-status";
import { claimTicket } from "@/lib/ticket-api";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { useTicketUpdates } from "@/lib/realtime-context";
import { toFaDate, toFaDateTime } from "@/lib/datetime";

const statusColors: Record<TicketStatus, string> = {
  Submitted: "bg-blue-100 text-blue-700 border border-blue-200",
  SeenRead: "bg-purple-100 text-purple-700 border border-purple-200",
  Open: "bg-rose-100 text-rose-700 border border-rose-200",
  InProgress: "bg-amber-100 text-amber-700 border border-amber-200",
  Solved: "bg-emerald-100 text-emerald-700 border border-emerald-200",
  Redo: "bg-slate-100 text-slate-700 border border-slate-200",
};

const getActivityEventLabel = (
  eventType: string,
  oldStatus?: string | null,
  newStatus?: string | null,
  metadataJson?: string | null
): string => {
  // Prefer Persian message from backend (e.g. grant/revoke with technician name)
  if ((eventType === "AccessGranted" || eventType === "AccessRevoked") && metadataJson) {
    try {
      const meta = JSON.parse(metadataJson) as { messageFa?: string };
      if (meta.messageFa) return meta.messageFa;
    } catch {
      /* ignore */
    }
  }
  const labels: Record<string, string> = {
    Created: "تیکت ایجاد شد",
    AssignedTechnicians: "تکنسین‌ها به تیکت واگذار شدند",
    TechnicianOpened: "تکنسین تیکت را مشاهده کرد",
    StartWork: "تکنسین شروع به کار کرد",
    ReplyAdded: "پاسخ جدید اضافه شد",
    StatusChanged: oldStatus && newStatus
      ? `وضعیت از ${TICKET_STATUS_LABELS[oldStatus as TicketStatus] || oldStatus} به ${TICKET_STATUS_LABELS[newStatus as TicketStatus] || newStatus} تغییر کرد`
      : "وضعیت تغییر کرد",
    Handoff: "تیکت به تکنسین دیگری واگذار شد",
    AssignedToTechnicianBySupervisor: "تیکت توسط سرپرست به تکنسین واگذار شد",
    UnassignedTechnicianBySupervisor: "واگذاری تکنسین توسط سرپرست لغو شد",
    Closed: "تیکت بسته شد",
    Revision: "تیکت بازگشایی شد",
    AccessGranted: "دسترسی همکاری به تکنسین داده شد",
    AccessRevoked: "دسترسی همکاری از تکنسین لغو شد",
    TicketUpdated: "اطلاعات تیکت به‌روزرسانی شد",
  };
  return labels[eventType] || eventType;
};

const priorityLabels: Record<string, string> = {
  low: "پایین",
  medium: "متوسط",
  high: "بالا",
  urgent: "بحرانی",
};

export default function TicketDetailPage() {
  const params = useParams();
  const router = useRouter();
  const { token, user } = useAuth();
  const { categories } = useCategories();
  const [ticket, setTicket] = useState<Ticket | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [replyMessage, setReplyMessage] = useState("");
  const [replyStatus, setReplyStatus] = useState<TicketStatus>("Open");
  const [statusDraft, setStatusDraft] = useState<TicketStatus>("Open");
  const [replySubmitting, setReplySubmitting] = useState(false);
  const [statusSubmitting, setStatusSubmitting] = useState(false);
  const [claimSubmitting, setClaimSubmitting] = useState(false);

  const ticketId = params.id as string;

  // PHASE 4: Real-time updates via SignalR
  // Automatically refresh ticket when updates are received from other users
  const { connected: realtimeConnected } = useTicketUpdates(ticketId, (updateType) => {
    console.log(`[Realtime] Ticket ${ticketId} received update: ${updateType}`);
    // Reload ticket data when any update is received
    loadTicket();
    
    // Show toast notification
    if (updateType === "ReplyAdded") {
      toast({
        title: "پاسخ جدید",
        description: "پاسخ جدیدی به این تیکت اضافه شد",
      });
    } else if (updateType === "StatusChanged") {
      toast({
        title: "تغییر وضعیت",
        description: "وضعیت این تیکت تغییر کرد",
      });
    }
  });

  const loadTicket = async () => {
    if (!user || !ticketId) {
      setError("دسترسی غیرمجاز");
      setLoading(false);
      return;
    }

    try {
      const [ticketDetails, messages] = await Promise.all([
        apiGetNoStore<ApiTicketResponse>(`/api/tickets/${ticketId}`, { token }),
        apiGetNoStore<ApiTicketMessageDto[]>(`/api/tickets/${ticketId}/messages`, { token }),
      ]);

      const mapped = mapApiTicketToUi(ticketDetails, categories, messages.map(mapApiMessageToResponse));
      setTicket(mapped);
      setReplyStatus(mapped.status);
      setStatusDraft(mapped.status);

      try {
        await apiRequest(`/api/tickets/${ticketId}/seen`, {
          method: "POST",
          token,
          silent: true,
        });
      } catch (seenError) {
        console.warn("Failed to mark ticket as seen", seenError);
        toast({
          title: "به‌روزرسانی خوانده‌شده ثبت نشد",
          description: "اتصال یا سرور بررسی شود.",
        });
      }

    } catch (err: any) {
      console.error("Failed to load ticket:", err);
      const statusCode = err?.status as number | undefined;
      if (statusCode === 403) {
        setError("دسترسی به این تیکت برای شما مجاز نیست");
      } else if (statusCode === 404) {
        setError("تیکت یافت نشد");
      } else {
        setError(err?.message || "خطا در بارگذاری تیکت");
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadTicket();
  }, [token, ticketId, categories, user?.role]);

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center space-y-2">
          <div className="w-8 h-8 border-2 border-current border-t-transparent rounded-full animate-spin mx-auto" />
          <p className="text-sm text-muted-foreground">در حال بارگذاری...</p>
        </div>
      </div>
    );
  }

  if (error || !ticket) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background">
        <div className="text-center space-y-4">
          <p className="text-lg text-destructive">{error || "تیکت یافت نشد"}</p>
          <Button onClick={() => router.push("/")}>بازگشت به داشبورد</Button>
        </div>
      </div>
    );
  }

  const isAdmin = user?.role === "admin";
  const isTechnician = user?.role === "technician";
  const canAct = ticket.canAct !== false;
  const canChangeStatus = Boolean((ticket.canEdit || isAdmin) && canAct);
  const canReply = Boolean((ticket.canReply || isAdmin) && canAct);
  const isReadOnly = Boolean(isTechnician && !isAdmin && !canAct);
  const readOnlyReason =
    (ticket.readOnlyReason as string | null | undefined) ??
    "فقط مشاهده / بدون دسترسی";
  const showClaim = Boolean(isTechnician && ticket.accessMode === "Candidate" && !ticket.assignedTo);
  const canClaim = Boolean(isTechnician && ticket.canClaim);
  const claimDisabledReason = ticket.claimDisabledReason ?? "امکان قبول مسئولیت وجود ندارد";
  const canReopenToRedo = canChangeStatus;
  const isFaded = Boolean(ticket.isFaded);

  const latestActivity = ticket.latestActivity
    ? {
        actionType: ticket.latestActivity.actionType,
        actorName: ticket.latestActivity.actorName,
        actorRole: ticket.latestActivity.actorRole,
        createdAt: ticket.latestActivity.createdAt,
        fromStatus: ticket.latestActivity.fromStatus,
        toStatus: ticket.latestActivity.toStatus,
        summary: ticket.latestActivity.summary,
      }
    : ticket.activityEvents?.[0]
    ? {
        actionType: ticket.activityEvents[0].eventType,
        actorName: ticket.activityEvents[0].actorName,
        actorRole: ticket.activityEvents[0].actorRole,
        createdAt: ticket.activityEvents[0].createdAt,
        fromStatus: ticket.activityEvents[0].oldStatus ?? null,
        toStatus: ticket.activityEvents[0].newStatus ?? null,
        summary: null,
      }
    : null;

  const roleLabel = (role: string) =>
    role === "Admin"
      ? "مدیر"
      : role === "Supervisor"
      ? "سرپرست"
      : role === "Technician"
      ? "تکنسین"
      : "مشتری";

  const formatActivitySummary = () => {
    if (!latestActivity) return "—";
    if (latestActivity.summary) return latestActivity.summary;
    return getActivityEventLabel(
      latestActivity.actionType,
      latestActivity.fromStatus,
      latestActivity.toStatus
    );
  };

  const statusOptions: TicketStatus[] =
    user?.role === "client"
      ? ["Open", "InProgress", "Solved"]
      : ["Open", "InProgress", "Solved", "Redo"];

  const handleStatusUpdate = async (nextStatus: TicketStatus) => {
    if (!user) return;
    try {
      setStatusSubmitting(true);
      await apiRequest(`/api/tickets/${ticketId}`, {
        method: "PATCH",
        token,
        body: { status: mapUiStatusToApi(nextStatus) },
      });
      toast({
        title: "وضعیت به‌روزرسانی شد",
        description: "تغییر وضعیت با موفقیت ثبت شد",
      });
      await loadTicket();
      router.refresh();
    } catch (err: any) {
      const status = err?.status ?? (err as any)?.status;
      const msg = err?.message || "لطفا دوباره تلاش کنید";
      if (process.env.NODE_ENV === "development" || (typeof window !== "undefined" && (window as any).__lastApiError)) {
        console.error("[TicketDetail] Status update failed", { status, url: err?.url, message: msg });
      }
      toast({
        title: "خطا در تغییر وضعیت",
        description: status ? `(${status}) ${msg}` : msg,
        variant: "destructive",
      });
    } finally {
      setStatusSubmitting(false);
    }
  };

  const handleReplySubmit = async () => {
    if (!user || !replyMessage.trim()) return;
    try {
      setReplySubmitting(true);
      await apiRequest(`/api/tickets/${ticketId}/messages`, {
        method: "POST",
        token,
        body: {
          message: replyMessage.trim(),
          status: mapUiStatusToApi(replyStatus),
        },
      });
      toast({
        title: "پاسخ ثبت شد",
        description: "پاسخ با موفقیت ارسال شد",
      });
      setReplyMessage("");
      await loadTicket();
      router.refresh();
    } catch (err: any) {
      const status = err?.status ?? (err as any)?.status;
      const msg = err?.message || "لطفا دوباره تلاش کنید";
      if (process.env.NODE_ENV === "development" || (typeof window !== "undefined" && (window as any).__lastApiError)) {
        console.error("[TicketDetail] Reply failed", { status, url: err?.url, message: msg });
      }
      toast({
        title: "خطا در ارسال پاسخ",
        description: status ? `(${status}) ${msg}` : msg,
        variant: "destructive",
      });
    } finally {
      setReplySubmitting(false);
    }
  };

  const handleClaim = async () => {
    if (!user || !ticketId) return;
    try {
      setClaimSubmitting(true);
      await claimTicket(token, ticketId);
      await loadTicket();
      router.refresh();
      toast({
        title: "قبول مسئولیت انجام شد",
        description: "تیکت با موفقیت به شما واگذار شد.",
      });
    } catch (err: any) {
      const status = err?.status ?? (err as any)?.status;
      const message = err?.body?.message || err?.message || "خطا در قبول مسئولیت";
      if (process.env.NODE_ENV === "development") {
        console.error("[TicketDetail] Claim failed", { status, message });
      }
      toast({
        title: "قبول مسئولیت ناموفق بود",
        description: status ? `(${status}) ${message}` : message,
        variant: "destructive",
      });
    } finally {
      setClaimSubmitting(false);
    }
  };

  const handleCollaboratorUpdate = async (technicianUserId: string, action: "grant" | "revoke") => {
    if (!user) return;
    try {
      const url =
        action === "grant"
          ? `/api/tickets/${ticketId}/access/grant`
          : `/api/tickets/${ticketId}/access/revoke`;
      await apiRequest(url, {
        method: "POST",
        token,
        body: { technicianUserId },
      });
      toast({
        title: "دسترسی بروزرسانی شد",
        description: action === "grant" ? "دادن دسترسی همکاری انجام شد" : "لغو دسترسی انجام شد",
      });
      await loadTicket();
      router.refresh();
    } catch (err: any) {
      const status = err?.status ?? (err as any)?.status;
      const msg = err?.body?.message || err?.message || "لطفا دوباره تلاش کنید";
      if (process.env.NODE_ENV === "development") {
        console.error("[TicketDetail] Collaborator update failed", { status, message: msg });
      }
      toast({
        title: "خطا در بروزرسانی دسترسی",
        description: status ? `(${status}) ${msg}` : msg,
        variant: "destructive",
      });
    }
  };

  return (
    <div className="min-h-screen bg-background px-3 sm:px-6 py-4 sm:py-6 overflow-x-hidden" dir="rtl">
      <div className="max-w-4xl mx-auto space-y-6 min-w-0">
        <div className="flex items-center justify-between">
          <Button variant="ghost" onClick={() => router.push("/")} className="gap-2">
            <ArrowRight className="h-4 w-4" />
            بازگشت به داشبورد
          </Button>
        </div>

        <Card className={isReadOnly || isFaded ? "opacity-90" : ""}>
          <CardHeader>
            <div className="flex items-start justify-between">
              <div className="space-y-2">
                <CardTitle className="text-2xl">{ticket.title}</CardTitle>
                <div className="flex gap-2">
                  <Badge className={statusColors[ticket.displayStatus ?? ticket.status] || statusColors.Submitted}>
                    {getTicketStatusLabel(ticket.displayStatus ?? ticket.status, user?.role === "client" ? "client" : user?.role === "technician" ? "technician" : "admin")}
                  </Badge>
                  <Badge>{priorityLabels[ticket.priority] || ticket.priority}</Badge>
                </div>
                <div className="text-sm text-muted-foreground space-y-1">
                  <div>آخرین اقدام: {formatActivitySummary()}</div>
                  {latestActivity && (
                    <div className="flex items-center gap-2">
                      <span>{latestActivity.actorName}</span>
                      <Badge variant="outline" className="text-xs">
                        {roleLabel(latestActivity.actorRole)}
                      </Badge>
                      <span className="text-xs">{toFaDateTime(latestActivity.createdAt)}</span>
                    </div>
                  )}
                </div>
              </div>
              <div className="text-left min-w-0">
                <p className="text-sm text-muted-foreground">شماره تیکت</p>
                <p className="font-mono text-lg break-words">{ticket.id}</p>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-6 relative">
            {isReadOnly && (
              <div className="absolute inset-0 pointer-events-none">
                <div className="absolute inset-0 bg-background/40 backdrop-blur-[1px] rounded-md" />
                <div className="absolute top-4 left-4 right-4 pointer-events-none">
                  <div className="text-xs text-muted-foreground bg-background/80 border rounded px-3 py-2">
                    {readOnlyReason}
                  </div>
                </div>
              </div>
            )}
            {(canChangeStatus || canReply || showClaim) && (
              <>
                <Card>
                  <CardHeader>
                    <CardTitle className="text-lg">عملیات</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    {showClaim && (
                      <div className="space-y-2">
                        <label className="text-sm text-muted-foreground">قبول مسئولیت</label>
                        <div className="flex items-center gap-3">
                          <Button onClick={handleClaim} disabled={!canClaim || claimSubmitting}>
                            {claimSubmitting ? "در حال ثبت..." : "قبول مسئولیت"}
                          </Button>
                          {!canClaim && (
                            <span className="text-xs text-muted-foreground">{claimDisabledReason}</span>
                          )}
                        </div>
                      </div>
                    )}

                    {canChangeStatus && (
                      <div className="space-y-2">
                        <label className="text-sm text-muted-foreground">تغییر وضعیت</label>
                        <div className="flex items-center gap-3">
                          <Select value={statusDraft} onValueChange={(value) => setStatusDraft(value as TicketStatus)}>
                            <SelectTrigger className="w-56" disabled={isReadOnly}>
                              <SelectValue placeholder="انتخاب وضعیت" />
                            </SelectTrigger>
                            <SelectContent>
                              {statusOptions.map((status) => (
                                <SelectItem key={status} value={status}>
                                  {TICKET_STATUS_LABELS[status]}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                          <Button
                            onClick={() => handleStatusUpdate(statusDraft)}
                            disabled={statusSubmitting || isReadOnly}
                          >
                            {statusSubmitting ? "در حال ثبت..." : "ثبت وضعیت"}
                          </Button>
                          {canReopenToRedo && (
                            <Button
                              variant="outline"
                              onClick={() => handleStatusUpdate("Redo")}
                              disabled={statusSubmitting || isReadOnly}
                            >
                              بازگشایی به بازبینی
                            </Button>
                          )}
                        </div>
                      </div>
                    )}

                    {canReply && (
                      <div className="space-y-2">
                        <label className="text-sm text-muted-foreground">پاسخ</label>
                        <div className="flex items-center gap-3">
                          <Select value={replyStatus} onValueChange={(value) => setReplyStatus(value as TicketStatus)}>
                            <SelectTrigger className="w-56" disabled={isReadOnly}>
                              <SelectValue placeholder="وضعیت بعد از پاسخ" />
                            </SelectTrigger>
                            <SelectContent>
                              {statusOptions.map((status) => (
                                <SelectItem key={status} value={status}>
                                  {TICKET_STATUS_LABELS[status]}
                                </SelectItem>
                              ))}
                            </SelectContent>
                          </Select>
                        </div>
                        <Textarea
                          value={replyMessage}
                          onChange={(e) => setReplyMessage(e.target.value)}
                          placeholder="متن پاسخ را وارد کنید..."
                          rows={4}
                          disabled={isReadOnly}
                        />
                        <Button
                          onClick={handleReplySubmit}
                          disabled={replySubmitting || !replyMessage.trim() || isReadOnly}
                        >
                          {replySubmitting ? "در حال ارسال..." : "ارسال پاسخ"}
                        </Button>
                      </div>
                    )}
                  </CardContent>
                </Card>
                <Separator />
              </>
            )}
            <div>
              <h3 className="text-lg font-semibold mb-2">توضیحات</h3>
              <p className="text-muted-foreground whitespace-pre-wrap">{ticket.description}</p>
            </div>

            <Separator />

            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="flex items-center gap-2">
                <Hash className="h-4 w-4 text-muted-foreground" />
                <span className="text-sm text-muted-foreground">دسته‌بندی:</span>
                <span className="text-sm font-medium">{ticket.category}</span>
              </div>
              <div className="flex items-center gap-2">
                <User className="h-4 w-4 text-muted-foreground" />
                <span className="text-sm text-muted-foreground">تکنسین:</span>
                <span className="text-sm font-medium">
                  {ticket.assignedTechnicians && ticket.assignedTechnicians.length > 0
                    ? ticket.assignedTechnicians.filter((at: any) => at.canAct !== false).map((at: any) => at.technicianName).join(", ")
                    : ticket.assignedTechnicianName || "اختصاص نیافته"}
                </span>
              </div>
              <div className="flex items-center gap-2">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span className="text-sm text-muted-foreground">تاریخ ایجاد:</span>
                <span className="text-sm font-medium">{toFaDate(ticket.createdAt)}</span>
              </div>
              <div className="flex items-center gap-2">
                <Flag className="h-4 w-4 text-muted-foreground" />
                <span className="text-sm text-muted-foreground">اولویت:</span>
                <span className="text-sm font-medium">{priorityLabels[ticket.priority] || ticket.priority}</span>
              </div>
            </div>

            {/* Assigned Technicians: Owner + Collaborators (active) + Candidates (inactive when claimed) */}
            {ticket.assignedTechnicians && ticket.assignedTechnicians.length > 0 && (
              <>
                <Separator />
                <div>
                  <div className="mb-4">
                    <h3 className="text-lg font-semibold flex items-center gap-2">
                      <Users className="h-5 w-5" />
                      تکنسین‌های واگذار شده ({ticket.assignedTechnicians.filter((at: any) => at.canAct === true || (at.canAct !== false && at.isActive)).length} فعال)
                    </h3>
                  </div>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                    {ticket.assignedTechnicians.map((at: any, index: number) => (
                      <div
                        key={at.id || at.technicianId || `${at.technicianUserId}-${at.assignedAt}-${index}`}
                        className={`p-3 border rounded-lg ${(at.canAct === true || (at.canAct !== false && at.isActive)) ? "border-blue-300 bg-blue-50" : "opacity-60"}`}
                      >
                        <div className="flex items-center justify-between">
                          <div>
                            <div className="flex items-center gap-2 flex-wrap">
                              <span className="font-medium">{at.technicianName}</span>
                              {(at.accessMode === "Owner" || at.role === "Owner" || at.role === "Lead") && <Badge variant="default" className="text-xs">مسئول</Badge>}
                              {(at.accessMode === "Collaborator" || at.role === "Collaborator") && <Badge variant="outline" className="text-xs">همکار</Badge>}
                              {(at.accessMode === "Candidate" || at.role === "Candidate") && <Badge variant="secondary" className="text-xs">کاندید</Badge>}
                              {(at.canAct === true || (at.canAct !== false && at.isActive)) && <Badge variant="default" className="text-xs bg-green-600">فعال</Badge>}
                              {(at.canAct === false || (!at.canAct && !at.isActive)) && <Badge variant="secondary" className="text-xs">غیرفعال / فقط مشاهده</Badge>}
                            </div>
                            {at.technicianEmail && (
                              <p className="text-xs text-muted-foreground mt-1">{at.technicianEmail}</p>
                            )}
                            <p className="text-xs text-muted-foreground mt-1">{toFaDate(at.assignedAt)}</p>
                          </div>
                          {(ticket.canGrantAccess || isAdmin) && at.isActive && at.technicianUserId && (
                            <div className="flex flex-col gap-2">
                              {at.role === "Candidate" && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => handleCollaboratorUpdate(String(at.technicianUserId), "grant")}
                                >
                                  دادن دسترسی همکاری
                                </Button>
                              )}
                              {at.role === "Collaborator" && (
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => handleCollaboratorUpdate(String(at.technicianUserId), "revoke")}
                                >
                                  لغو دسترسی
                                </Button>
                              )}
                            </div>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </>
            )}

            {/* Activity Events */}
            {ticket.activityEvents && ticket.activityEvents.length > 0 && (
              <>
                <Separator />
                <div>
                  <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                    <Clock className="h-5 w-5" />
                    تاریخچه فعالیت‌ها ({ticket.activityEvents.length})
                  </h3>
                  <div className="space-y-3 max-h-96 overflow-y-auto">
                    {ticket.activityEvents.map((event: any) => (
                      <div key={event.id} className="flex gap-3 p-3 border rounded-lg bg-gray-50">
                        <Avatar className="w-8 h-8 flex-shrink-0">
                          <AvatarFallback className="text-xs">{event.actorName.charAt(0)}</AvatarFallback>
                        </Avatar>
                        <div className="flex-1 text-right">
                          <div className="flex items-center gap-2 mb-1">
                            <span className="text-sm font-medium">{event.actorName}</span>
                            <Badge variant="outline" className="text-xs">
                              {event.actorRole === "Admin"
                                ? "ادمین"
                                : event.actorRole === "Supervisor"
                                ? "سرپرست"
                                : event.actorRole === "Owner"
                                ? "مسئول"
                                : event.actorRole === "Collaborator"
                                ? "همکار"
                                : event.actorRole === "Technician"
                                ? "تکنسین"
                                : "مشتری"}
                            </Badge>
                          </div>
                          <p className="text-sm text-muted-foreground">
                            {getActivityEventLabel(event.eventType, event.oldStatus, event.newStatus, event.metadataJson)}
                          </p>
                          <p className="text-xs text-muted-foreground mt-1">{toFaDateTime(event.createdAt)}</p>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              </>
            )}

            {ticket.responses && ticket.responses.length > 0 && (
              <>
                <Separator />
                <div>
                  <h3 className="text-lg font-semibold mb-4 flex items-center gap-2">
                    <MessageSquare className="h-5 w-5" />
                    پیام‌ها ({ticket.responses.length})
                  </h3>
                  <div className="space-y-4">
                    {ticket.responses.map((message) => (
                      <Card key={message.id || message.timestamp} className="min-w-0 overflow-hidden">
                        <CardContent className="pt-6">
                          <div className="flex items-start justify-between gap-2 mb-2 flex-wrap">
                            <div className="min-w-0">
                              <p className="font-medium break-words">{message.authorName}</p>
                              <p className="text-xs text-muted-foreground">{toFaDateTime(message.timestamp)}</p>
                            </div>
                          </div>
                          <p className="text-sm whitespace-pre-wrap break-words max-w-full">{message.message}</p>
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                </div>
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );

}


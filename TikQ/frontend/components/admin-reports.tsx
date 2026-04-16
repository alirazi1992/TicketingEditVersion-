"use client";

import { useState, useCallback, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { toast } from "@/hooks/use-toast";
import { useAuth } from "@/lib/auth-context";
import {
  downloadReport,
  ReportDownloadError,
  getTechnicianWorkReport,
  getTechnicianWorkReportDetail,
  downloadTechnicianWorkReportExcel,
  type ReportRange,
  type TechnicianWorkReport as TechWorkReportType,
  type TechnicianWorkReportDetail as TechWorkDetailType,
} from "@/lib/reports-api";
import { FileDown, FileSpreadsheet, BarChart3, Loader2, Calendar, Users } from "lucide-react";
import { toFaDate } from "@/lib/datetime";
import {
  getDefaultReportRange,
  isRangeValid,
  getRangeErrorMessage,
} from "@/lib/date";
import { toGregorianIsoFromJalaliInput } from "@/lib/date-utils";
import { DateRangePicker } from "@/components/date-range-picker";

type ReportType = "basic" | "analytic";
type BasicFormat = "csv" | "xlsx";
type AnalyticFormat = "zip" | "xlsx";

export function AdminReports() {
  const { token, user } = useAuth();
  const [reportType, setReportType] = useState<ReportType>("basic");
  const [basicFormat, setBasicFormat] = useState<BasicFormat>("xlsx");
  const [analyticFormat, setAnalyticFormat] = useState<AnalyticFormat>("xlsx");
  const [range, setRange] = useState<ReportRange>("1m");
  const [customRange, setCustomRange] = useState<{ from?: number; to?: number }>({});
  const [loading, setLoading] = useState(false);

  const [techReportRange, setTechReportRange] = useState<{ from?: number; to?: number }>(() => getDefaultReportRange());
  const [techReport, setTechReport] = useState<TechWorkReportType | null>(null);
  const [techReportLoading, setTechReportLoading] = useState(false);
  const [techReportExcelLoading, setTechReportExcelLoading] = useState(false);
  const [techReportError, setTechReportError] = useState<string | null>(null);
  const [detailUserId, setDetailUserId] = useState<string | null>(null);
  const [detailData, setDetailData] = useState<TechWorkDetailType | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState<string | undefined>(undefined);

  const handleDownload = async () => {
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد سیستم شوید",
        variant: "destructive",
      });
      return;
    }

    // Build Gregorian ISO for custom range (so API always gets 2000-2050); validate before calling API.
    let fromIso: string | undefined;
    let toIso: string | undefined;
    if (range === "custom") {
      const from = customRange.from;
      const to = customRange.to;
      if (from == null || to == null) {
        toast({
          title: "خطا",
          description: "لطفاً تاریخ شروع و پایان را وارد کنید",
          variant: "destructive",
        });
        return;
      }
      if (!isRangeValid(customRange)) {
        toast({
          title: "خطا",
          description: "تاریخ شروع باید قبل از تاریخ پایان باشد",
          variant: "destructive",
        });
        return;
      }
      fromIso = toGregorianIsoFromJalaliInput(from);
      toIso = toGregorianIsoFromJalaliInput(to);
      if (!fromIso || !toIso) {
        toast({
          title: "خطا",
          description: "تاریخ شروع یا پایان معتبر نیست. لطفاً تاریخ شمسی معتبر وارد کنید.",
          variant: "destructive",
        });
        return;
      }
    }

    setLoading(true);
    try {
      const params = {
        range,
        from: fromIso,
        to: toIso,
      };
      const type = reportType === "basic" ? "base" : "analytic";
      const format = reportType === "basic" ? basicFormat : analyticFormat;
      await downloadReport({ type, token, params, format });
      toast({
        title: "موفق",
        description: type === "base" ? "گزارش پایه دانلود شد" : "گزارش تحلیلی دانلود شد",
      });
    } catch (error: unknown) {
      console.error("Failed to download report:", error);
      let title = "خطا در دانلود گزارش";
      let description: string;
      if (error instanceof ReportDownloadError) {
        if (error.statusCode >= 500) {
          title = "خطای سرور (۵۰۰)";
          description = error.message;
        } else if (error.statusCode >= 400) {
          title = "درخواست نامعتبر (۴۰۰)";
          description = error.message;
        } else {
          description = error.message;
        }
      } else {
        description = error instanceof Error ? error.message : "لطفاً دوباره تلاش کنید";
      }
      toast({
        title,
        description,
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  };

  const rangeLabels: Record<ReportRange, string> = {
    "1w": "یک هفته اخیر",
    "1m": "یک ماه اخیر",
    "3m": "سه ماه اخیر",
    "6m": "شش ماه اخیر",
    "1y": "یک سال اخیر",
    custom: "بازه دلخواه",
  };

  const loadTechnicianWorkReport = useCallback(
    async (skipToast?: boolean) => {
      if (!user) {
        if (!skipToast) toast({ title: "خطا", description: "لطفاً ابتدا وارد شوید", variant: "destructive" });
        return;
      }
      const defaultRange = getDefaultReportRange();
      const fromTs = techReportRange.from ?? defaultRange.from;
      const toTs = techReportRange.to ?? defaultRange.to;
      if (!isRangeValid(techReportRange)) {
        setTechReportError("تاریخ شروع باید قبل از تاریخ پایان باشد");
        setTechReport(null);
        if (!skipToast) toast({ title: "خطا", description: "بازه تاریخ معتبر انتخاب کنید", variant: "destructive" });
        return;
      }
      setTechReportError(null);
      setTechReport(null);
      setTechReportLoading(true);
      try {
        const fromIso = toGregorianIsoFromJalaliInput(fromTs);
        const toIso = toGregorianIsoFromJalaliInput(toTs);
        const data = await getTechnicianWorkReport(token, fromIso, toIso, selectedUserId);
        setTechReport(data ?? { from: fromIso, to: toIso, users: [] });
        setTechReportError(null);
        if (!skipToast) toast({ title: "موفق", description: "گزارش عملکرد تکنسین‌ها بارگذاری شد" });
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : "بارگذاری گزارش ناموفق بود";
        setTechReportError(msg);
        setTechReport(null);
        if (!skipToast) toast({ title: "خطا", description: msg, variant: "destructive" });
      } finally {
        setTechReportLoading(false);
      }
    },
    [token, user, techReportRange, selectedUserId]
  );

  useEffect(() => {
    if (!user) return;
    loadTechnicianWorkReport(true);
  }, [techReportRange, selectedUserId, token, loadTechnicianWorkReport]);

  const openDetail = useCallback(
    async (userId: string) => {
      const from = techReportRange.from;
      const to = techReportRange.to;
      if (!user || from == null || to == null) return;
      setDetailUserId(userId);
      setDetailData(null);
      setDetailLoading(true);
      try {
        const fromIso = toGregorianIsoFromJalaliInput(from);
        const toIso = toGregorianIsoFromJalaliInput(to);
        const data = await getTechnicianWorkReportDetail(token, userId, fromIso, toIso);
        setDetailData(data);
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : "بارگذاری جزئیات ناموفق بود";
        toast({ title: "خطا", description: msg, variant: "destructive" });
      } finally {
        setDetailLoading(false);
      }
    },
    [token, techReportRange]
  );

  const eventTypeLabel: Record<string, string> = {
    ReplyAdded: "پاسخ",
    StatusChanged: "تغییر وضعیت",
    Revision: "بازگشایی",
    AccessGranted: "اعطای دسترسی",
    AccessRevoked: "لغو دسترسی",
    TicketUpdated: "بروزرسانی تیکت",
    Created: "ایجاد",
  };

  return (<div className="space-y-6" dir="rtl">
      <div className="text-right">
        <h3 className="text-xl font-bold font-iran">گزارش‌گیری</h3>
        <p className="text-muted-foreground font-iran">
          دانلود گزارش‌های تیکت‌ها به فرمت CSV یا ZIP
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Basic Report Card */}
        <Card
          className={`cursor-pointer transition-all ${
            reportType === "basic"
              ? "ring-2 ring-primary border-primary"
              : "hover:border-primary/50"
          }`}
          onClick={() => setReportType("basic")}
        >
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-right font-iran">
              <FileSpreadsheet className="w-5 h-5" />
              گزارش پایه
            </CardTitle>
            <CardDescription className="text-right font-iran">
              لیست تیکت‌ها با جزئیات اصلی
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <ul className="text-sm text-muted-foreground space-y-1 font-iran text-right">
              <li>• شماره تیکت</li>
              <li>• عنوان و وضعیت</li>
              <li>• دسته‌بندی و زیر دسته</li>
              <li>• نام مشتری و تکنسین</li>
              <li>• تاریخ ایجاد و آخرین بروزرسانی</li>
            </ul>
            <p className="text-xs text-muted-foreground font-iran">
              برای دانلود، بازهٔ زمانی را در پایین انتخاب کنید و دکمهٔ «دانلود گزارش» را بزنید.
            </p>
            <div className="flex items-center gap-2 flex-wrap">
              <Label className="text-xs font-iran">فرمت:</Label>
              <Select value={basicFormat} onValueChange={(v: BasicFormat) => setBasicFormat(v)} dir="rtl">
                <SelectTrigger className="w-[100px] h-8 text-xs font-iran">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="csv">CSV</SelectItem>
                  <SelectItem value="xlsx">XLSX</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>

        {/* Analytic Report Card */}
        <Card
          className={`cursor-pointer transition-all ${
            reportType === "analytic"
              ? "ring-2 ring-primary border-primary"
              : "hover:border-primary/50"
          }`}
          onClick={() => setReportType("analytic")}
        >
          <CardHeader>
            <CardTitle className="flex items-center gap-2 text-right font-iran">
              <BarChart3 className="w-5 h-5" />
              گزارش تحلیلی
            </CardTitle>
            <CardDescription className="text-right font-iran">
              آمار و تحلیل جامع تیکت‌ها
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-3">
            <ul className="text-sm text-muted-foreground space-y-1 font-iran text-right">
              <li>• فراوانی بر حسب دسته‌بندی</li>
              <li>• فراوانی بر حسب مشتری</li>
              <li>• زمان حل تیکت‌ها</li>
              <li>• تاریخچه تغییر وضعیت</li>
              <li>• مدت زمان هر مرحله</li>
            </ul>
            <p className="text-xs text-muted-foreground font-iran">
              برای دانلود، بازهٔ زمانی را در پایین انتخاب کنید و دکمهٔ «دانلود گزارش» را بزنید.
            </p>
            <div className="flex items-center gap-2 flex-wrap">
              <Label className="text-xs font-iran">فرمت:</Label>
              <Select value={analyticFormat} onValueChange={(v: AnalyticFormat) => setAnalyticFormat(v)} dir="rtl">
                <SelectTrigger className="w-[100px] h-8 text-xs font-iran">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="zip">ZIP</SelectItem>
                  <SelectItem value="xlsx">XLSX</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Time Range Selection */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-right font-iran">
            <Calendar className="w-5 h-5" />
            بازه زمانی
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <div className="space-y-2">
              <Label className="font-iran text-right">انتخاب بازه</Label>
              <Select
                value={range || "1m"}
                onValueChange={(value: ReportRange) => setRange(value)}
                dir="rtl"
              >
                <SelectTrigger className="text-right font-iran">
                  <SelectValue placeholder="انتخاب بازه زمانی" />
                </SelectTrigger>
                <SelectContent className="font-iran">
                  {Object.entries(rangeLabels)
                    .filter(([v]) => Boolean(v))
                    .map(([value, label]) => (
                      <SelectItem key={value} value={value}>
                        {label}
                      </SelectItem>
                    ))}
                </SelectContent>
              </Select>
            </div>

            {range === "custom" && (
              <DateRangePicker
                value={customRange}
                onChange={setCustomRange}
                error={!isRangeValid(customRange) ? getRangeErrorMessage(customRange) ?? undefined : undefined}
                labels={{ from: "از تاریخ", to: "تا تاریخ" }}
              />
            )}
          </div>

          <div className="flex justify-end items-center gap-3 pt-4">
            <span className="text-sm text-muted-foreground font-iran">
              {reportType === "basic" ? "گزارش پایه" : "گزارش تحلیلی"}
            </span>
            <Button
              onClick={handleDownload}
              disabled={loading}
              className="gap-2 font-iran"
            >
              {loading ? (
                <Loader2 className="w-4 h-4 animate-spin" />
              ) : (
                <FileDown className="w-4 h-4" />
              )}
              {loading ? "در حال آماده‌سازی..." : "دانلود گزارش"}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Technician Work Report */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-right font-iran">
            <Users className="w-5 h-5" />
            گزارش عملکرد تکنسین‌ها
          </CardTitle>
          <CardDescription className="text-right font-iran">
            آمار کار هر تکنسین و سرپرست: تیکت‌های مسئول/همکار، پاسخ‌ها، تغییر وضعیت، و جزئیات فعالیت
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {techReportError && (
            <p className="text-destructive text-sm font-iran text-right rounded-md bg-destructive/10 p-3 flex items-center justify-between gap-2">
              <span>{techReportError}</span>
              <Button variant="outline" size="sm" onClick={() => loadTechnicianWorkReport(false)} className="font-iran">
                تلاش مجدد
              </Button>
            </p>
          )}
          <div className="flex flex-wrap items-end gap-4">
            <DateRangePicker
              value={techReportRange}
              onChange={setTechReportRange}
              error={getRangeErrorMessage(techReportRange) ?? undefined}
              labels={{ from: "از تاریخ (شمسی)", to: "تا تاریخ (شمسی)" }}
              size="sm"
            />
            <Button onClick={() => loadTechnicianWorkReport()} disabled={techReportLoading} className="gap-2 font-iran">
              {techReportLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <BarChart3 className="w-4 h-4" />}
              {techReportLoading ? "در حال بارگذاری..." : "بارگذاری گزارش"}
            </Button>
            {techReport && techReport.users.length > 0 && (
              <Select
                value={selectedUserId ?? "all"}
                onValueChange={(v) => setSelectedUserId(v === "all" ? undefined : v)}
                dir="rtl"
              >
                <SelectTrigger className="w-[200px] text-right font-iran">
                  <SelectValue placeholder="همه کاربران" />
                </SelectTrigger>
                <SelectContent className="font-iran">
                  <SelectItem value="all">همه</SelectItem>
                  {techReport.users.map((u) => (
                    <SelectItem key={u.userId} value={u.userId}>
                      {u.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
            {techReport && (
              <>
                <Button
                  variant="outline"
                  onClick={() => {
                    const blob = new Blob([JSON.stringify(techReport, null, 2)], { type: "application/json" });
                    const url = URL.createObjectURL(blob);
                    const a = document.createElement("a");
                    a.href = url;
                    a.download = `technician-work-report-${techReportRange.from != null ? toGregorianIsoFromJalaliInput(techReportRange.from) : "from"}-${techReportRange.to != null ? toGregorianIsoFromJalaliInput(techReportRange.to) : "to"}.json`;
                    a.click();
                    URL.revokeObjectURL(url);
                    toast({ title: "موفق", description: "فایل JSON دانلود شد" });
                  }}
                  className="gap-2 font-iran"
                >
                  <FileDown className="w-4 h-4" />
                  خروجی JSON
                </Button>
                <Button
                  variant="outline"
                  disabled={techReportExcelLoading}
                  onClick={async () => {
                    if (!user) return;
                    const defaultRange = getDefaultReportRange();
                    const fromIso = toGregorianIsoFromJalaliInput(techReportRange.from ?? defaultRange.from);
                    const toIso = toGregorianIsoFromJalaliInput(techReportRange.to ?? defaultRange.to);
                    setTechReportExcelLoading(true);
                    try {
                      await downloadTechnicianWorkReportExcel(token, fromIso, toIso, selectedUserId);
                      toast({ title: "موفق", description: "فایل Excel دانلود شد" });
                    } catch (err: unknown) {
                      const msg = err instanceof Error ? err.message : "دانلود Excel ناموفق بود";
                      toast({ title: "خطا", description: msg, variant: "destructive" });
                    } finally {
                      setTechReportExcelLoading(false);
                    }
                  }}
                  className="gap-2 font-iran"
                >
                  {techReportExcelLoading ? <Loader2 className="w-4 h-4 animate-spin" /> : <FileDown className="w-4 h-4" />}
                  خروجی Excel
                </Button>
              </>
            )}
          </div>

          {techReport && (
            <div className="overflow-x-auto rounded-md border">
              <table className="w-full text-right font-iran text-sm">
                <thead className="bg-muted">
                  <tr>
                    <th className="p-3">نام</th>
                    <th className="p-3">نقش</th>
                    <th className="p-3">مسئول</th>
                    <th className="p-3">همکار</th>
                    <th className="p-3">پاسخ</th>
                    <th className="p-3">تغییر وضعیت</th>
                    <th className="p-3">باز / حل‌شده</th>
                    <th className="p-3">آخرین فعالیت</th>
                    <th className="p-3"></th>
                  </tr>
                </thead>
                <tbody>
                  {techReport.users.map((u) => (
                    <tr key={u.userId} className="border-t hover:bg-muted/50">
                      <td className="p-3 font-medium">{u.name}</td>
                      <td className="p-3">{u.isSupervisor ? "سرپرست" : "تکنسین"}</td>
                      <td className="p-3">{u.ticketsOwned}</td>
                      <td className="p-3">{u.ticketsCollaborated}</td>
                      <td className="p-3">{u.repliesCount}</td>
                      <td className="p-3">{u.statusChangesCount}</td>
                      <td className="p-3">
                        {u.openCount + u.inProgressCount} / {u.resolvedCount}
                      </td>
                      <td className="p-3 text-muted-foreground">
                        {u.lastActivityAt ? toFaDate(u.lastActivityAt) : "—"}
                      </td>
                      <td className="p-3">
                        <Button variant="ghost" size="sm" onClick={() => openDetail(u.userId)}>
                          جزئیات
                        </Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {techReport.users.length === 0 && (
                <p className="p-6 text-center text-muted-foreground font-iran">در این بازه فعالیتی ثبت نشده است.</p>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Drilldown Modal */}
      <Dialog open={!!detailUserId} onOpenChange={(open) => !open && setDetailUserId(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl flex flex-col" dir="rtl">
          <DialogHeader>
            <DialogTitle className="font-iran">
              {detailData ? detailData.userName : "جزئیات فعالیت"}
            </DialogTitle>
          </DialogHeader>
          {detailLoading ? (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="w-8 h-8 animate-spin text-muted-foreground" />
            </div>
          ) : detailData ? (
            <div className="overflow-y-auto space-y-4 pr-2">
              {detailData.byTicket.map((t) => (
                <Card key={t.ticketId}>
                  <CardHeader className="py-3">
                    <CardTitle className="text-base font-iran">
                      <a href={`/tickets/${t.ticketId}`} className="hover:underline text-primary">
                        {t.title || t.ticketId}
                      </a>
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="pt-0">
                    <ul className="space-y-2">
                      {t.actions.map((a) => (
                        <li key={a.eventId} className="flex flex-wrap items-center gap-2 text-sm border-b pb-2 last:border-0">
                          <span className="font-medium">{eventTypeLabel[a.eventType] || a.eventType}</span>
                          <span className="text-muted-foreground">({a.actorRole})</span>
                          {a.newStatus && (
                            <span className="text-muted-foreground">
                              {a.oldStatus} → {a.newStatus}
                            </span>
                          )}
                          <span className="text-muted-foreground mr-auto">{toFaDate(a.createdAt)}</span>
                        </li>
                      ))}
                    </ul>
                  </CardContent>
                </Card>
              ))}
              {detailData.byTicket.length === 0 && (
                <p className="text-center text-muted-foreground font-iran py-8">فعالیتی در این بازه ثبت نشده است.</p>
              )}
            </div>
          ) : null}
        </DialogContent>
      </Dialog>

      {/* Info Section */}
      <Card className="border-blue-200 bg-blue-50">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <FileSpreadsheet className="w-5 h-5 text-blue-600 mt-0.5" />
            <div className="text-right">
              <h4 className="font-medium text-blue-800 font-iran">
                راهنمای گزارش‌ها
              </h4>
              <ul className="text-sm text-blue-700 mt-2 space-y-1 font-iran">
                <li>
                  <strong>گزارش پایه:</strong> یک فایل CSV شامل لیست تمام تیکت‌ها
                  با اطلاعات اصلی. مناسب برای بررسی سریع.
                </li>
                <li>
                  <strong>گزارش تحلیلی:</strong> یک فایل ZIP شامل چند CSV:
                  جزئیات تیکت‌ها، آمار دسته‌بندی، فراوانی مشتریان، و تاریخچه
                  تغییر وضعیت.
                </li>
                <li>
                  <strong>گزارش عملکرد تکنسین‌ها:</strong> آمار هر تکنسین/سرپرست:
                  تیکت‌های مسئول و همکار، تعداد پاسخ و تغییر وضعیت، باز/حل‌شده. با کلیک روی «جزئیات» لیست فعالیت به‌تفکیک تیکت نمایش داده می‌شود.
                </li>
              </ul>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
















"use client";

import { useEffect, useMemo, useState } from "react";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Progress } from "@/components/ui/progress";
import { Badge } from "@/components/ui/badge";
import { toast } from "@/hooks/use-toast";
import { useAuth } from "@/lib/auth-context";
import {
  assignSupervisorTicket,
  downloadSupervisorReport,
  getSupervisorAvailableTickets,
  getSupervisorTechnicians,
  getSupervisorTechnicianSummary,
  removeSupervisorAssignment,
} from "@/lib/supervisor-api";
import type {
  ApiSupervisorTechnicianListItemDto,
  ApiSupervisorTechnicianSummaryDto,
  ApiTicketSummaryDto,
} from "@/lib/api-types";
import { getTicketStatusLabel, type TicketStatus } from "@/lib/ticket-status";
import { toFaDate } from "@/lib/datetime";

type Props = {
  open: boolean;
  onOpenChange: (open: boolean) => void;
};

export function SupervisorTechniciansModal({ open, onOpenChange }: Props) {
  const { token, user } = useAuth();
  const [items, setItems] = useState<ApiSupervisorTechnicianListItemDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selected, setSelected] = useState<ApiSupervisorTechnicianListItemDto | null>(null);
  const [summary, setSummary] = useState<ApiSupervisorTechnicianSummaryDto | null>(null);
  const [summaryLoading, setSummaryLoading] = useState(false);
  const [summaryError, setSummaryError] = useState<string | null>(null);
  const [assignDialogOpen, setAssignDialogOpen] = useState(false);
  const [availableTickets, setAvailableTickets] = useState<ApiTicketSummaryDto[]>([]);
  const [availableLoading, setAvailableLoading] = useState(false);

  const fetchList = async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      const data = await getSupervisorTechnicians(token);
      setItems(data);
    } catch (err: any) {
      // Format error message with status code
      const statusInfo = err?.status ? ` (${err.status} ${err.statusText || ""})` : "";
      setError(`${err?.message || "خطا در دریافت لیست تکنسین‌ها"}${statusInfo}`);
    } finally {
      setLoading(false);
    }
  };

  const fetchSummary = async (technicianUserId: string) => {
    if (!user) return;
    setSummaryLoading(true);
    setSummaryError(null);
    try {
      const data = await getSupervisorTechnicianSummary(token, technicianUserId);
      setSummary(data);
    } catch (err: any) {
      const statusInfo = err?.status ? ` (${err.status} ${err.statusText || ""})` : "";
      setSummaryError(`${err?.message || "خطا در دریافت اطلاعات تکنسین"}${statusInfo}`);
      setSummary(null);
    } finally {
      setSummaryLoading(false);
    }
  };

  const fetchAvailableTickets = async () => {
    if (!user) return;
    setAvailableLoading(true);
    try {
      const data = await getSupervisorAvailableTickets(token);
      setAvailableTickets(data);
    } catch (err: any) {
      const statusInfo = err?.status ? ` (${err.status})` : "";
      toast({
        title: "خطا در دریافت تیکت‌ها",
        description: `${err?.message || "لطفا دوباره تلاش کنید"}${statusInfo}`,
        variant: "destructive",
      });
      setAvailableTickets([]);
    } finally {
      setAvailableLoading(false);
    }
  };

  // Only fetch when dialog opens, not on every token change
  useEffect(() => {
    if (open && token) {
      void fetchList();
    } else {
      setSelected(null);
      setSummary(null);
      setSummaryError(null);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open]); // Only depend on 'open', not 'token'

  const handleSelect = async (item: ApiSupervisorTechnicianListItemDto) => {
    setSelected(item);
    await fetchSummary(item.technicianUserId);
  };

  const handleAssign = async (ticketId: string) => {
    if (!user || !selected) return;
    try {
      await assignSupervisorTicket(token, selected.technicianUserId, ticketId);
      toast({
        title: "تخصیص انجام شد",
        description: "تیکت به تکنسین اضافه شد",
      });
      setAssignDialogOpen(false);
      await fetchSummary(selected.technicianUserId);
      await fetchList();
    } catch (err: any) {
      toast({
        title: "خطا در تخصیص",
        description: err?.message || "لطفا دوباره تلاش کنید",
        variant: "destructive",
      });
    }
  };

  const handleRemove = async (ticketId: string) => {
    if (!user || !selected) return;
    try {
      await removeSupervisorAssignment(token, selected.technicianUserId, ticketId);
      toast({
        title: "حذف انجام شد",
        description: "تخصیص تکنسین برداشته شد",
      });
      await fetchSummary(selected.technicianUserId);
      await fetchList();
    } catch (err: any) {
      toast({
        title: "خطا در حذف تخصیص",
        description: err?.message || "لطفا دوباره تلاش کنید",
        variant: "destructive",
      });
    }
  };

  const handleDownloadReport = async () => {
    if (!user || !selected) return;
    try {
      const blob = await downloadSupervisorReport(token, selected.technicianUserId, "csv");
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `technician-report-${selected.technicianUserId}.csv`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);
      window.URL.revokeObjectURL(url);
    } catch (err: any) {
      toast({
        title: "خطا در دریافت گزارش",
        description: err?.message || "لطفا دوباره تلاش کنید",
        variant: "destructive",
      });
    }
  };

  const handlePrintReport = () => {
    if (!summary) return;
    const rows = summary.archiveTickets
      .map(
        (t) => `
        <tr>
          <td>${t.id}</td>
          <td>${t.title}</td>
          <td>${t.status}</td>
          <td>${t.clientName}</td>
          <td>${toFaDate(t.createdAt)}</td>
          <td>${t.updatedAt ? toFaDate(t.updatedAt) : "-"}</td>
        </tr>`
      )
      .join("");

    const html = `
      <html dir="rtl">
        <head>
          <title>Technician Report</title>
          <style>
            body { font-family: Tahoma, Arial, sans-serif; margin: 20px; }
            table { width: 100%; border-collapse: collapse; margin-top: 16px; }
            th, td { border: 1px solid #ccc; padding: 6px; text-align: right; }
            th { background: #f3f4f6; }
          </style>
        </head>
        <body>
          <h2>گزارش تکنسین: ${summary.technicianName}</h2>
          <table>
            <thead>
              <tr>
                <th>شناسه</th>
                <th>عنوان</th>
                <th>وضعیت</th>
                <th>مشتری</th>
                <th>تاریخ ایجاد</th>
                <th>آخرین بروزرسانی</th>
              </tr>
            </thead>
            <tbody>${rows}</tbody>
          </table>
        </body>
      </html>`;

    const printWindow = window.open("", "_blank");
    if (!printWindow) return;
    printWindow.document.write(html);
    printWindow.document.close();
    printWindow.print();
  };

  const activeTickets = summary?.activeTickets ?? [];
  const archiveTickets = summary?.archiveTickets ?? [];

  const availableTicketLookup = useMemo(() => {
    return new Set(activeTickets.map((t) => t.id));
  }, [activeTickets]);

  return (
    <>
      <Dialog open={open} onOpenChange={onOpenChange}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle className="text-right">تکنسین‌ها</DialogTitle>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          {loading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : error ? (
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">{error}</p>
              <Button variant="outline" onClick={fetchList}>تلاش دوباره</Button>
            </div>
          ) : items.length === 0 ? (
            <div className="text-sm text-muted-foreground">تکنسینی برای نمایش وجود ندارد.</div>
          ) : (
            <div className="space-y-3">
              {items.map((item) => (
                <button
                  key={item.technicianUserId}
                  type="button"
                  onClick={() => handleSelect(item)}
                  className="w-full border rounded-lg p-3 text-right hover:border-primary transition"
                >
                  <div className="flex items-center justify-between">
                    <div className="space-y-1">
                      <div className="font-medium">{item.technicianName}</div>
                      <div className="text-xs text-muted-foreground">
                        {item.inboxLeft} باقی مانده از {item.inboxTotal}
                      </div>
                    </div>
                    <div className="w-40">
                      <Progress value={item.workloadPercent} />
                    </div>
                  </div>
                </button>
              ))}
            </div>
          )}
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={Boolean(selected)} onOpenChange={(openValue) => !openValue && setSelected(null)}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-5xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle className="text-right">
              {selected?.technicianName ?? "جزئیات تکنسین"}
            </DialogTitle>
          </DialogHeader>

          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          {summaryLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : summaryError ? (
            <div className="space-y-3">
              <p className="text-sm text-muted-foreground">{summaryError}</p>
              {selected ? (
                <Button variant="outline" onClick={() => fetchSummary(selected.technicianUserId)}>
                  تلاش دوباره
                </Button>
              ) : null}
            </div>
          ) : summary ? (
            <div className="space-y-6">
              <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                  <CardTitle className="text-right">آرشیو تیکت‌ها</CardTitle>
                  <div className="flex gap-2">
                    <Button variant="outline" onClick={handleDownloadReport}>
                      دانلود CSV
                    </Button>
                    <Button variant="outline" onClick={handlePrintReport}>
                      چاپ گزارش
                    </Button>
                  </div>
                </CardHeader>
                <CardContent>
                  {archiveTickets.length === 0 ? (
                    <div className="text-sm text-muted-foreground">موردی وجود ندارد.</div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="text-right">شناسه</TableHead>
                          <TableHead className="text-right">عنوان</TableHead>
                          <TableHead className="text-right">وضعیت</TableHead>
                          <TableHead className="text-right">مشتری</TableHead>
                          <TableHead className="text-right">تاریخ ایجاد</TableHead>
                          <TableHead className="text-right">آخرین بروزرسانی</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {archiveTickets.map((ticket) => (
                          <TableRow key={ticket.id}>
                            <TableCell className="font-mono text-sm">{ticket.id}</TableCell>
                            <TableCell>{ticket.title}</TableCell>
                            <TableCell>
                              <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                            </TableCell>
                            <TableCell>{ticket.clientName}</TableCell>
                            <TableCell>{toFaDate(ticket.createdAt)}</TableCell>
                            <TableCell>
                              {ticket.updatedAt ? toFaDate(ticket.updatedAt) : "-"}
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader className="flex flex-row items-center justify-between">
                  <CardTitle className="text-right">تخصیص‌های فعال</CardTitle>
                  <Button
                    onClick={async () => {
                      await fetchAvailableTickets();
                      setAssignDialogOpen(true);
                    }}
                  >
                    افزودن تیکت
                  </Button>
                </CardHeader>
                <CardContent>
                  {activeTickets.length === 0 ? (
                    <div className="text-sm text-muted-foreground">تخصیص فعالی وجود ندارد.</div>
                  ) : (
                    <Table>
                      <TableHeader>
                        <TableRow>
                          <TableHead className="text-right">شناسه</TableHead>
                          <TableHead className="text-right">عنوان</TableHead>
                          <TableHead className="text-right">وضعیت</TableHead>
                          <TableHead className="text-right">مشتری</TableHead>
                          <TableHead className="text-right">عملیات</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {activeTickets.map((ticket) => (
                          <TableRow key={ticket.id}>
                            <TableCell className="font-mono text-sm">{ticket.id}</TableCell>
                            <TableCell>{ticket.title}</TableCell>
                            <TableCell>
                              <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                            </TableCell>
                            <TableCell>{ticket.clientName}</TableCell>
                            <TableCell>
                              <Button variant="outline" size="sm" onClick={() => handleRemove(ticket.id)}>
                                حذف
                              </Button>
                            </TableCell>
                          </TableRow>
                        ))}
                      </TableBody>
                    </Table>
                  )}
                </CardContent>
              </Card>
            </div>
          ) : null}
          </div>
        </DialogContent>
      </Dialog>

      <Dialog open={assignDialogOpen} onOpenChange={setAssignDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-3xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle className="text-right">تیکت‌های قابل تخصیص</DialogTitle>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          {availableLoading ? (
            <div className="text-sm text-muted-foreground">در حال بارگذاری...</div>
          ) : availableTickets.length === 0 ? (
            <div className="text-sm text-muted-foreground">تیکتی برای تخصیص وجود ندارد.</div>
          ) : (
            <div className="space-y-2">
              {availableTickets.map((ticket) => (
                <button
                  key={ticket.id}
                  type="button"
                  disabled={availableTicketLookup.has(ticket.id)}
                  onClick={() => handleAssign(ticket.id)}
                  className="w-full border rounded-lg p-3 text-right hover:border-primary transition disabled:opacity-50"
                >
                  <div className="flex items-center justify-between">
                    <div className="space-y-1">
                      <div className="font-medium">{ticket.title}</div>
                      <div className="text-xs text-muted-foreground">{ticket.clientName}</div>
                    </div>
                    <Badge variant="outline">{getTicketStatusLabel((ticket.displayStatus ?? ticket.status) as TicketStatus, "technician")}</Badge>
                  </div>
                </button>
              ))}
            </div>
          )}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}


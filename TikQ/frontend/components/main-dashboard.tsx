"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter, usePathname } from "next/navigation";
import {
  FolderTree,
  LayoutDashboard,
  ListChecks,
  Settings2,
  Ticket as TicketIcon,
  UserPlus,
} from "lucide-react";

import { apiRequest, apiGetNoStore } from "@/lib/api-client";
import type {
  ApiCategoryResponse,
  ApiTicketListItemResponse,
  ApiTicketMessageDto,
  ApiTicketResponse,
  ApiUserDto,
} from "@/lib/api-types";
import {
  mapApiMessageToResponse,
  mapApiTicketToUi,
  mapUiPriorityToApi,
  mapUiStatusToApi,
} from "@/lib/ticket-mappers";
import {
  buildTechnicianProfile,
  type TechnicianProfile,
} from "@/data/technician-profiles";
import { ClientDashboard } from "@/components/client-dashboard";
import { TechnicianDashboard } from "@/components/technician-dashboard";
import { AdminDashboard } from "@/components/admin-dashboard";
import {
  DashboardShell,
  type DashboardNavItem,
} from "@/components/dashboard-shell";
import { BackendStatusBanner } from "@/components/backend-status-banner";
import { useAuth } from "@/lib/auth-context";
import { useCategories } from "@/services/useCategories";
import { categoryService } from "@/services/CategoryService";
import type { CategoriesData } from "@/services/categories-types";
import type { Ticket, TicketStatus } from "@/types";
import { toast } from "@/hooks/use-toast";
import { getMyTechnicianProfile } from "@/lib/technicians-api";
import { SupervisorTechnicianManagement } from "@/components/supervisor-technician-management";
import { useTicketListUpdates, useRealtime } from "@/lib/realtime-context";
import { toast as showToast } from "@/hooks/use-toast";
import { parseServerDate, toFaDateTime } from "@/lib/datetime";

export function MainDashboard() {
  const { user, token, isLoading } = useAuth();
  const router = useRouter();
  const pathname = usePathname();

  const [tickets, setTickets] = useState<Ticket[]>([]);
  const [technicians, setTechnicians] = useState<TechnicianProfile[]>([]);
  const { categories: categoriesData, save: saveCategories } = useCategories();
  const categoriesRef = useRef<CategoriesData>(categoriesData);
  const [activeView, setActiveView] = useState<string>("");
  const [isSupervisor, setIsSupervisor] = useState(false);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [isLoadingData, setIsLoadingData] = useState(true);

  useEffect(() => {
    if (process.env.NODE_ENV !== "development") return;
    const raw = "2026-02-02T08:05:00";
    const parsed = parseServerDate(raw);
    console.log("[datetime test]", {
      raw,
      parsed: parsed?.toISOString() ?? null,
      tehran: toFaDateTime(raw),
    });
  }, []);

  const getDefaultViewForRole = (role: "client" | "technician" | "admin") => {
    switch (role) {
      case "admin":
        return "admin.tickets";
      case "technician":
        return "technician.assigned";
      default:
        return "client.tickets";
    }
  };

  const resolvedActiveView =
    user?.role ? activeView || getDefaultViewForRole(user.role) : "";

  const categoriesReady = useMemo(
    () =>
      Object.values(categoriesData).some(
        (cat) => typeof cat.backendId !== "undefined"
      ),
    [categoriesData]
  );

  useEffect(() => {
    categoriesRef.current = categoriesData;
  }, [categoriesData]);

  const loadTickets = async (
    authToken: string,
    categorySnapshot: CategoriesData,
    userRole?: string
  ): Promise<Ticket[]> => {
    setIsLoadingData(true);
    setLoadError(null);
    
    try {
      // Use technician-specific endpoint for technicians
      const endpoint = userRole === "technician" ? "/api/technician/tickets" : "/api/tickets";
      console.log(`[loadTickets] Fetching from ${endpoint} for role ${userRole}`);
      
      const apiTickets = await apiGetNoStore<ApiTicketListItemResponse[]>(endpoint, {
        token: authToken,
      });

      console.log(`[loadTickets] Received ${apiTickets?.length ?? 0} tickets`);

      const mapped = apiTickets.map((apiTicket) =>
        mapApiTicketToUi(apiTicket, categorySnapshot, [])
      );

      setTickets(mapped);
      setLoadError(null);
      return mapped;
    } catch (error: any) {
      console.error("[loadTickets] Failed to load tickets:", error);
      
      // Determine error type for better user feedback
      let errorMsg = "خطا در بارگذاری تیکت‌ها";
      if (error?.isNetworkError || error?.message?.includes("Failed to fetch")) {
        errorMsg = "سرور در دسترس نیست - لطفاً اتصال شبکه و وضعیت سرور را بررسی کنید";
      } else if (error?.status === 401) {
        errorMsg = "نشست شما منقضی شده - لطفاً مجدداً وارد شوید";
      } else if (error?.status === 500) {
        errorMsg = "خطای داخلی سرور - لطفاً با پشتیبانی تماس بگیرید";
      } else if (error?.message) {
        errorMsg = error.message;
      }
      
      setLoadError(errorMsg);
      toast({
        title: "بارگذاری تیکت‌ها ناموفق بود",
        description: errorMsg,
        variant: "destructive",
      });
      setTickets([]);
      return [];
    } finally {
      setIsLoadingData(false);
    }
  };

  const loadTechnicians = async (authToken: string, currentTickets: Ticket[] = tickets) => {
    try {
      const { getAllTechnicians } = await import("@/lib/technicians-api");
      const apiTechnicians = await getAllTechnicians(authToken);
      const technicianItems = apiTechnicians.items ?? [];
      
      // Calculate active tickets for each technician
      const techniciansWithLoad = technicianItems
        .filter((tech) => tech.isActive) // Only show active technicians
        .map((tech) => {
          // Count active tickets assigned to this technician
          const activeTicketsCount = currentTickets.filter(
            (ticket) => ticket.assignedTo === tech.id && 
                      ((ticket.displayStatus ?? ticket.status) === "Open" || (ticket.displayStatus ?? ticket.status) === "InProgress")
          ).length;
          
          return {
            id: tech.id,
            name: tech.fullName,
            email: tech.email,
            phone: tech.phone || "",
            department: tech.department || "",
            status: activeTicketsCount >= 5 ? ("busy" as const) : ("available" as const),
            specialties: [],
            rating: 5,
            activeTickets: activeTicketsCount,
          };
        });
      
      setTechnicians(techniciansWithLoad);
    } catch (error) {
      console.error("Failed to load technicians", error);
      toast({
        title: "خطا در بارگذاری تکنسین‌ها",
        description: "لطفاً صفحه را رفرش کنید",
        variant: "destructive",
      });
      setTechnicians([]);
    }
  };

  // Redirect unauthenticated users to the login page
  useEffect(() => {
    if (!isLoading && !user) {
      router.replace("/login");
    }
  }, [isLoading, user, router]);

  useEffect(() => {
    if (!user) {
      setTickets([]);
      setTechnicians([]);
      setIsSupervisor(false);
      return;
    }

    const loadData = async () => {
      const loadedTickets = await loadTickets(token, categoriesReady ? categoriesRef.current : {}, user?.role);

      if (user.role === "admin") {
        const needsTechnicians =
          resolvedActiveView === "admin.assignment" ||
          resolvedActiveView === "admin.technicians" ||
          resolvedActiveView === "admin.auto-settings";
        if (needsTechnicians) {
          await loadTechnicians(token, loadedTickets);
        }
        setIsSupervisor(false);
      } else {
        setTechnicians([]);
      }
    };

    void loadData();
  }, [token, user?.id, user?.role, categoriesReady, resolvedActiveView]);

  useEffect(() => {
    if (!user || user.role !== "technician") {
      setIsSupervisor(false);
      return;
    }

    // Prefer /api/auth/me isSupervisor flag; fallback to profile if missing
    if (typeof user.isSupervisor === "boolean") {
      setIsSupervisor(user.isSupervisor);
      return;
    }

    const loadSupervisorFlag = async () => {
      try {
        const profile = await getMyTechnicianProfile(token);
        setIsSupervisor(Boolean(profile.isSupervisor));
      } catch (error) {
        console.warn("Failed to load supervisor profile", error);
        setIsSupervisor(false);
      }
    };

    void loadSupervisorFlag();
  }, [token, user?.role, user?.isSupervisor]);

  // -------- PHASE 4: Real-time updates via SignalR --------
  const { addTicketUpdateListener } = useRealtime();

  // Handle real-time ticket updates - refresh immediately when events are received
  useEffect(() => {
    if (!user) return;

    const unsubscribe = addTicketUpdateListener({
      onAnyUpdate: (ticketId, updateType) => {
        console.log(`[Realtime] Ticket ${ticketId} updated: ${updateType}`);
        // Immediately refresh tickets on any update
        loadTickets(token, categoriesRef.current, user?.role).catch(console.warn);
        
        // Also refresh technicians for admin when needed
        if (user.role === "admin") {
          const needsTechnicians =
            resolvedActiveView === "admin.assignment" ||
            resolvedActiveView === "admin.technicians" ||
            resolvedActiveView === "admin.auto-settings";
          if (needsTechnicians) {
            loadTechnicians(token).catch(console.warn);
          }
        }
      },
      onReplyAdded: (payload) => {
        // Show toast notification for new replies (from other users)
        if (payload.metadata?.authorName) {
          showToast({
            title: "پاسخ جدید",
            description: `${payload.metadata.authorName} پاسخی اضافه کرد`,
          });
        }
      },
      onStatusChanged: (payload) => {
        // Show toast notification for status changes
        showToast({
          title: "تغییر وضعیت تیکت",
          description: `وضعیت از ${payload.oldStatus} به ${payload.newStatus} تغییر کرد`,
        });
      },
    });

    return () => {
      unsubscribe();
    };
  }, [token, user?.id, user?.role, addTicketUpdateListener, resolvedActiveView]);

  // -------- Ticket handlers (single definitions) --------

  const ensureBackendCategories = async () => {
    // If we already have backend ids, skip.
    if (
      Object.values(categoriesRef.current).some(
        (cat) => typeof cat.backendId !== "undefined"
      )
    ) {
      return categoriesRef.current;
    }

    try {
      const fresh = await categoryService.list();
      categoriesRef.current = fresh;
      await saveCategories(fresh);
      return fresh;
    } catch (error) {
      console.error("Failed to hydrate categories from backend", error);
      return categoriesRef.current;
    }
  };

  const handleTicketCreate = async (draft: Ticket) => {
    if (!user) return;

    const catMap =
      Object.values(categoriesRef.current).some(
        (cat) => typeof cat.backendId !== "undefined"
      )
        ? categoriesRef.current
        : await ensureBackendCategories();

    const category = catMap[draft.category];
    if (!category || typeof category.backendId === "undefined") {
      console.warn("Missing category mapping for", draft.category, category);
      toast({
        title: "دسته‌بندی نامعتبر است",
        description:
          "دسته‌بندی‌ها هنوز از سرور بارگذاری نشده‌اند. چند لحظه بعد دوباره تلاش کنید.",
        variant: "destructive",
      });
      return;
    }

    const subcategoryId = draft.subcategory
      ? category.subIssues[draft.subcategory]?.backendId
      : undefined;

    if (!subcategoryId) {
      toast({
        title: "زیرشاخه الزامی است",
        description: "لطفاً یک زیرشاخه معتبر انتخاب کنید.",
        variant: "destructive",
      });
      return;
    }

    try {
      const payload = {
        title: draft.title,
        description: draft.description || "",
        categoryId: category.backendId,
        subcategoryId,
        priority: mapUiPriorityToApi(draft.priority),
        dynamicFields: draft.dynamicFields || undefined,
      };

      const hasAttachments = Array.isArray(draft.attachments) && draft.attachments.some((f: any) => f?.file instanceof File);
      if (process.env.NODE_ENV === "development") {
        const maskedToken = token ? `${token.slice(0, 6)}...${token.slice(-4)}` : undefined;
        const attachmentNames = hasAttachments
          ? draft.attachments
              .filter((f: any) => f?.file instanceof File)
              .map((f: any) => f.file.name)
          : [];
        console.log("[TicketCreate] Final payload", {
          payload,
          hasAttachments,
          attachments: attachmentNames,
          headers: {
            Authorization: token ? `Bearer ${maskedToken}` : undefined,
            "Content-Type": hasAttachments ? "multipart/form-data" : "application/json",
          },
        });
      }
      const created = hasAttachments
        ? await apiRequest<ApiTicketResponse>("/api/tickets", {
            method: "POST",
            token,
            body: (() => {
              const formData = new FormData();
              formData.append("ticketData", JSON.stringify(payload));
              draft.attachments.forEach((file: any) => {
                if (file?.file instanceof File) {
                  formData.append("attachments", file.file);
                }
              });
              return formData;
            })(),
          })
        : await apiRequest<ApiTicketResponse>("/api/tickets", {
            method: "POST",
            token,
            body: payload,
          });

      const ticket = mapApiTicketToUi(created, categoriesRef.current, []);
      setTickets((prev) => [ticket, ...prev]);
      await refreshTickets();
    } catch (error) {
      console.error("Failed to create ticket", error);
      toast({
        title: "ثبت تیکت ناموفق بود",
          description: "لطفا اتصال و داده‌ها را بررسی کنید.",
          variant: "destructive",
        });
    }
  };

  const handleTicketSeen = async (ticketId: string) => {
    if (!user) return;

    const nowIso = new Date().toISOString();
    setTickets((prev) =>
      prev.map((ticket) =>
        ticket.id === ticketId
          ? { ...ticket, isUnseen: false, isUnread: false, lastSeenAt: nowIso }
          : ticket
      )
    );

    try {
      await apiRequest(`/api/tickets/${ticketId}/seen`, {
        method: "POST",
        token,
      });
    } catch (error) {
      console.warn("Failed to mark ticket as seen", error);
      toast({
        title: "به‌روزرسانی خوانده‌شده ثبت نشد",
        description: "اتصال یا سرور بررسی شود.",
      });
    }
  };

  const refreshTickets = async () => {
    if (!user) return;
    await loadTickets(token, categoriesRef.current, user?.role);
  };

  // Refetch tickets when returning to dashboard (pathname) or when page becomes visible (e.g. back button, tab focus)
  useEffect(() => {
    if (!user || !token) return;
    const isDashboardPath =
      pathname === "/admin" ||
      pathname === "/client" ||
      pathname === "/technician" ||
      pathname === "/supervisor";
    if (isDashboardPath) {
      refreshTickets();
    }
  }, [pathname, user?.id, token]);

  useEffect(() => {
    if (!user) return;
    const handleVisibility = () => {
      if (typeof document !== "undefined" && document.visibilityState === "visible") {
        refreshTickets();
      }
    };
    document.addEventListener("visibilitychange", handleVisibility);
    return () => document.removeEventListener("visibilitychange", handleVisibility);
  }, [user?.id, token]);

  const handleTicketUpdate = async (
    ticketId: string,
    updates: Partial<Ticket>
  ) => {
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد سیستم شوید",
        variant: "destructive",
      });
      return;
    }

    const payload: Record<string, unknown> = {};

    if (updates.status) {
      payload.status = mapUiStatusToApi(updates.status);
    }
    if (updates.priority) {
      payload.priority = mapUiPriorityToApi(updates.priority);
    }
    
    // Handle technician assignment separately
    if (typeof updates.assignedTo !== "undefined" && updates.assignedTo) {
      // Use the new assign-technician endpoint
      try {
        const { assignTechnicianToTicket } = await import("@/lib/technicians-api");
        const updatedTicket = await assignTechnicianToTicket(token, ticketId, updates.assignedTo);
        
        // Refresh tickets list to get latest data
        const refreshedTickets = await loadTickets(token, categoriesRef.current, user?.role);
        
        // Also refresh technicians to update their active ticket counts
        if (user?.role === "admin") {
          await loadTechnicians(token, refreshedTickets);
        }

        toast({
          title: "تکنسین تعیین شد",
          description: `تیکت ${ticketId} با موفقیت به تکنسین واگذار شد`,
        });
        return;
      } catch (error: any) {
        console.error("Failed to assign technician", error);
        const errorMessage = error?.body?.message || error?.message || "لطفا مجددا تلاش کنید.";
        toast({
          title: "تعیین تکنسین ناموفق بود",
          description: errorMessage,
          variant: "destructive",
        });
        return;
      }
    }

    if (typeof updates.assignedTo !== "undefined" && !updates.assignedTo) {
      // Unassign technician
      payload.assignedToUserId = null;
    }

    if (Object.keys(payload).length === 0) {
      return;
    }

    try {
      const updatedTicket = await apiRequest<ApiTicketResponse>(
        `/api/tickets/${ticketId}`,
        {
          method: "PATCH",
          token,
          body: payload,
        }
      );

      // Refresh tickets list to get latest data
      await refreshTickets();
      
      toast({
        title: "تیکت به‌روزرسانی شد",
        description: "تغییرات با موفقیت اعمال شد",
      });
    } catch (error: any) {
      console.error("Failed to update ticket", error);
      const errorMessage = error?.body?.message || error?.message || "لطفا مجددا تلاش کنید.";
      toast({
        title: "به‌روزرسانی تیکت ناموفق بود",
        description: errorMessage,
        variant: "destructive",
      });
    }
  };

  const handleTicketResponse = async (
    ticketId: string,
    message: string,
    status: TicketStatus
  ) => {
    if (!user) return;

    try {
      await apiRequest<ApiTicketMessageDto>(
        `/api/tickets/${ticketId}/messages`,
        {
          method: "POST",
          token,
          body: {
            message,
            status: mapUiStatusToApi(status),
          },
        }
      );

      const [ticketDetails, messages] = await Promise.all([
        apiGetNoStore<ApiTicketResponse>(`/api/tickets/${ticketId}`, { token }),
        apiGetNoStore<ApiTicketMessageDto[]>(`/api/tickets/${ticketId}/messages`, { token }),
      ]);

      const mapped = mapApiTicketToUi(
        ticketDetails,
        categoriesRef.current,
        messages.map(mapApiMessageToResponse)
      );
      setTickets((prev) =>
        prev.map((ticket) => (ticket.id === ticketId ? mapped : ticket))
      );
      await refreshTickets();
    } catch (error) {
      const errorWithStatus = error as any;
      const statusCode = errorWithStatus?.status as number | undefined;
      const errorBody = errorWithStatus?.body as Record<string, any> | undefined;
      const detail =
        errorBody?.detail ||
        errorBody?.message ||
        errorWithStatus?.message ||
        "لطفا مجددا تلاش کنید.";

      if (process.env.NODE_ENV === "development") {
        console.error("Failed to add response", {
          status: statusCode,
          detail,
          traceId: errorBody?.traceId,
          rawText: errorWithStatus?.rawText,
        });
      }

      let description = "لطفا مجددا تلاش کنید.";
      if (statusCode === 400) {
        description = detail || "درخواست نامعتبر است.";
      } else if (statusCode === 401) {
        description = "نشست شما منقضی شده - لطفاً مجدداً وارد شوید";
      } else if (statusCode === 403) {
        description = "شما مجوز ارسال پاسخ برای این تیکت را ندارید";
      } else if (statusCode === 404) {
        description = "تیکت مورد نظر یافت نشد";
      } else if (statusCode === 500) {
        description = "خطای داخلی سرور - لطفاً مجدداً تلاش کنید";
      } else if (detail) {
        description = detail;
      }

      toast({
        title: "ثبت پاسخ ناموفق بود",
        description,
        variant: "destructive",
      });
      throw error;
    }
  };

  // -------- Active view handling --------

  useEffect(() => {
    if (!user) {
      setActiveView("");
      return;
    }

    setActiveView((current) => {
      if (current && current.startsWith(user.role)) {
        return current;
      }
      return getDefaultViewForRole(user.role);
    });
  }, [user]);

  const handleCategoryUpdate = async (updatedCategories: CategoriesData) => {
    let nextCategories: CategoriesData = { ...updatedCategories };

    // If admin creates a new category (no backendId), sync it to the backend so tickets can use it
    if (token && user?.role === "admin") {
      for (const [key, category] of Object.entries(updatedCategories)) {
        if (typeof category.backendId === "undefined") {
          const name = (category.label ?? category.id ?? key)?.toString?.()?.trim() ?? "";
          if (!name) continue; // never POST with empty name
          try {
            const created = await apiRequest<ApiCategoryResponse>("/api/categories", {
              method: "POST",
              token,
              body: {
                name,
                description: category.description ?? category.label ?? "",
              },
            });

            nextCategories = {
              ...nextCategories,
              [key]: { ...category, backendId: created.id },
            };
          } catch (error) {
            console.error("Failed to sync category to backend", key, error);
          }
        }
      }
    }

    await saveCategories(nextCategories);
  };

  const navItems = useMemo<DashboardNavItem[]>(() => {
    if (!user) {
      return [];
    }

    if (user.role === "client") {
      const userTickets = tickets.filter(
        (ticket) => ticket.clientEmail === user.email
      );
      const newTicketCount = userTickets.filter(
        (ticket) => (ticket.displayStatus ?? ticket.status) === "Open"
      ).length;

      return [
        {
          id: "client-overview",
          title: "داشبورد",
          icon: LayoutDashboard,
          target: "client.tickets",
        },
        {
          id: "client-tickets",
          title: "درخواست‌های من",
          icon: TicketIcon,
          children: [
            {
              id: "client-tickets-list",
              title: "همه درخواست‌ها",
              target: "client.tickets",
              badge: userTickets.length,
            },
            {
              id: "client-tickets-create",
              title: "ثبت درخواست جدید",
              target: "client.create",
              badge: newTicketCount > 0 ? "+" : undefined,
            },
          ],
        },
      ];
    }

    if (user.role === "technician") {
      const technicianTickets = tickets.filter(
        (ticket) => ticket.assignedTechnicianEmail === user.email
      );
      const inProgressCount = technicianTickets.filter(
        (ticket) => (ticket.displayStatus ?? ticket.status) === "InProgress"
      ).length;
      const closedCount = technicianTickets.filter(
        (ticket) => (ticket.displayStatus ?? ticket.status) === "Solved"
      ).length;

      return [
        {
          id: "technician-overview",
          title: "داشبورد پشتیبان",
          icon: LayoutDashboard,
          target: "technician.assigned",
        },
        {
          id: "technician-tickets",
          title: "تیکت‌های محول شده",
          icon: ListChecks,
          children: [
            {
              id: "technician-assigned",
              title: "تیکت‌های من",
              target: "technician.assigned",
              badge: technicianTickets.length,
            },
            {
              id: "technician-progress",
              title: "در حال رسیدگی",
              target: "technician.in-progress",
              badge: inProgressCount,
            },
            {
              id: "technician-history",
              title: "آرشیو / پایان یافته",
              target: "technician.history",
              badge: closedCount,
            },
            ...(isSupervisor
              ? [
                  {
                    id: "technician-technicians",
                    title: "تکنسین‌ها",
                    target: "technician.technicians",
                  },
                ]
              : []),
          ],
        },
      ];
    }

    const openTicketsCount = tickets.filter(
      (ticket) => (ticket.displayStatus ?? ticket.status) === "Open"
    ).length;

    return [
      {
        id: "admin-overview",
        title: "داشبورد مدیر",
        icon: LayoutDashboard,
        target: "admin.tickets",
      },
      {
        id: "admin-tickets",
        title: "مدیریت تیکت‌ها",
        icon: TicketIcon,
        children: [
          {
            id: "admin-tickets-all",
            title: "همه تیکت‌ها",
            target: "admin.tickets",
            badge: tickets.length,
          },
          {
            id: "admin-assignment",
            title: "تخصیص تیکت‌ها",
            target: "admin.assignment",
            badge: openTicketsCount,
          },
        ],
      },
      {
        id: "admin-categories",
        title: "مدیریت دسته‌بندی‌ها",
        icon: FolderTree,
        children: [
          {
            id: "admin-categories-manage",
            title: "مدیریت دسته‌بندی‌ها",
            target: "admin.categories",
            badge: Object.keys(categoriesData).length,
          },
        ],
      },
      {
        id: "admin-automation",
        title: "تنظیمات خودکار",
        icon: Settings2,
        target: "admin.auto-settings",
      },
      {
        id: "admin-technicians",
        title: "مدیریت تکنسین‌ها",
        icon: UserPlus,
        target: "admin.technicians",
      },
    ];
  }, [user, tickets, categoriesData]);

  // -------- Loading & unauthenticated states --------

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-950 text-slate-100">
        <div className="text-center space-y-2">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-slate-300 mx-auto" />
          <p className="text-sm text-slate-300">Loading...</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return null;
  }
// -------- Main dashboard content --------

  const dashboardContent = (() => {
    // Show backend status banner when there's an error or data is empty
    const showStatusBanner = loadError || (tickets.length === 0 && !isLoadingData);
    
    if (user.role === "client") {
      const clientSection: "tickets" | "create" =
        resolvedActiveView === "client.create" ? "create" : "tickets";

      return (
        <>
          {showStatusBanner && <BackendStatusBanner />}
          <ClientDashboard
            tickets={tickets}
            onTicketCreate={handleTicketCreate}
            onTicketSeen={handleTicketSeen}
            authToken={token}
            currentUser={user}
            categoriesData={categoriesData}
            activeSection={clientSection}
          />
        </>
      );
    }

    if (user.role === "technician") {
      const technicianSection: "assigned" | "in-progress" | "history" =
        resolvedActiveView === "technician.in-progress"
          ? "in-progress"
          : resolvedActiveView === "technician.history"
          ? "history"
          : "assigned";

      const handleTechnicianSectionChange = (
        section: "assigned" | "in-progress" | "history"
      ) => {
        const next = `technician.${section}`;
        setActiveView((prev) => (prev === next ? prev : next));
      };


      if (resolvedActiveView === "technician.technicians" && isSupervisor) {
        return (
          <>
            {showStatusBanner && <BackendStatusBanner />}
            <SupervisorTechnicianManagement />
          </>
        );
      }

      return (
        <>
          {showStatusBanner && <BackendStatusBanner />}
          <TechnicianDashboard
            tickets={tickets}
            onTicketUpdate={handleTicketUpdate}
            onTicketRespond={handleTicketResponse}
            onTicketSeen={handleTicketSeen}
            isSupervisor={isSupervisor}
            currentUser={user}
            authToken={token}
            activeSection={technicianSection}
            onSectionChange={handleTechnicianSectionChange}
          />
        </>
      );
    }

    const adminSection:
      | "tickets"
      | "assignment"
      | "technicians"
      | "categories"
      | "auto-settings" =
      resolvedActiveView === "admin.assignment"
        ? "assignment"
        : resolvedActiveView === "admin.technicians"
        ? "technicians"
        : resolvedActiveView === "admin.categories"
        ? "categories"
        : resolvedActiveView === "admin.auto-settings"
        ? "auto-settings"
        : "tickets";

    return (
      <>
        {showStatusBanner && <BackendStatusBanner />}
        <AdminDashboard
          tickets={tickets}
          onTicketUpdate={handleTicketUpdate}
          onRefreshTickets={refreshTickets}
          technicians={technicians}
          categoriesData={categoriesData}
          onCategoryUpdate={handleCategoryUpdate}
          activeSection={adminSection}
          authToken={token}
        />
      </>
    );
  })();

  return (
    <DashboardShell
      user={{
        name: user.name,
        email: user.email,
        role: user.role,
        department: user.department ?? undefined,
        title: user.phone ?? undefined,
        avatar: user.avatar ?? undefined,
      }}
      navItems={navItems}
      activeItem={resolvedActiveView}
      onSelect={setActiveView}
    >
      {dashboardContent}
    </DashboardShell>
  );
}


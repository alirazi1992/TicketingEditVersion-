"use client";

import { useState, useEffect } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { toast } from "@/hooks/use-toast";
import { toFaDateTime } from "@/lib/datetime";
import { useAuth } from "@/lib/auth-context";
import { useCategories } from "@/services/useCategories";
import { categoryService } from "@/services/CategoryService";
import {
  Plus,
  Edit,
  Trash2,
  FolderPlus,
  Settings,
  AlertTriangle,
  Search,
  Loader2,
} from "lucide-react";
import type { FormFieldDef, FieldType } from "@/lib/dynamic-forms";
import { parseOptions } from "@/lib/dynamic-forms";
import { apiRequest } from "@/lib/api-client";
import {
  createCategory,
  updateCategory,
  deleteCategory,
  createSubcategory,
  updateSubcategory,
  deleteSubcategory,
} from "@/lib/categories-api";
import type {
  ApiCategoryResponse,
  ApiSubcategoryResponse,
} from "@/lib/api-types";
import { SubcategoryFieldDesignerDialog } from "@/components/subcategory-field-designer-dialog";

interface CategoryManagementProps {
  categoriesData?: any;
  onCategoryUpdate?: (updatedCategories: any) => void;
}

export function CategoryManagement({
  categoriesData: _legacyCategoriesData,
  onCategoryUpdate: _legacyOnCategoryUpdate,
}: CategoryManagementProps) {
  const { token, user } = useAuth();
  const { save: saveCategories } = useCategories();
  const [categories, setCategories] = useState<ApiCategoryResponse[]>([]);
  const [subcategories, setSubcategories] = useState<ApiSubcategoryResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [subcategoriesLoading, setSubcategoriesLoading] = useState(false);
  const [searchQuery, setSearchQuery] = useState("");
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [editingCategory, setEditingCategory] = useState<ApiCategoryResponse | null>(null);
  const [editingSubCategory, setEditingSubCategory] = useState<ApiSubcategoryResponse | null>(null);
  const [categoryDialogOpen, setCategoryDialogOpen] = useState(false);
  const [subCategoryDialogOpen, setSubCategoryDialogOpen] = useState(false);
  const [lastRefreshedAt, setLastRefreshedAt] = useState<Date | null>(null);
  const [newCategoryData, setNewCategoryData] = useState({
    name: "",
    description: "",
    isActive: true,
  });
  const [newSubCategoryData, setNewSubCategoryData] = useState({
    name: "",
    description: "",
    isActive: true,
  });

  // Field designer state
  const [fieldDesignerOpen, setFieldDesignerOpen] = useState(false);
  const [designingScope, setDesigningScope] = useState<{ type: "category" | "subcategory"; id: number } | null>(null);

  // Load categories on mount and when search query changes (cookie auth; token optional)
  useEffect(() => {
    loadCategories();
  }, [token, searchQuery]);

  // Load subcategories when category is selected (cookie auth; token optional)
  useEffect(() => {
    if (selectedCategoryId != null) {
      loadSubcategories(selectedCategoryId);
    } else {
      setSubcategories([]);
    }
  }, [token, selectedCategoryId]);

  const loadCategories = async () => {
    setLoading(true);
    try {
      const raw = await apiRequest<unknown>("/api/categories", {
        method: "GET",
        token: token ?? undefined,
      });
      console.log("[CATEGORY_MGMT] raw", raw);

      const rawList = Array.isArray(raw)
        ? raw
        : (raw as { items?: unknown[]; data?: unknown[]; totalCount?: number })?.items ??
          (raw as { items?: unknown[]; data?: unknown[]; totalCount?: number })?.data ??
          [];

      const normalize = (c: Record<string, unknown>): ApiCategoryResponse => ({
        id: (c.id as number) ?? (c.Id as number),
        name: (c.name as string) ?? (c.Name as string) ?? "",
        isActive: (c.isActive as boolean) ?? (c.IsActive as boolean) ?? true,
        description: (c.description as string | null) ?? (c.Description as string | null) ?? null,
        subcategories: Array.isArray(c.subcategories ?? c.Subcategories)
          ? ((c.subcategories ?? c.Subcategories) as Record<string, unknown>[]).map((s) => ({
              id: (s.id as number) ?? (s.Id as number),
              categoryId: (s.categoryId as number) ?? (s.CategoryId as number),
              name: (s.name as string) ?? (s.Name as string) ?? "",
              isActive: (s.isActive as boolean) ?? (s.IsActive as boolean) ?? true,
              description: (s.description as string | null) ?? (s.Description as string | null) ?? null,
            }))
          : [],
      });

      const normalized = rawList
        .filter((x): x is Record<string, unknown> => x != null && typeof x === "object")
        .map(normalize);
      console.log("[CATEGORY_MGMT] normalized count", normalized.length);
      if (normalized.length === 0 && process.env.NODE_ENV === "development") {
        console.warn(
          "[CATEGORY_MGMT] categories empty; received shape:",
          typeof raw,
          Array.isArray(raw),
          raw != null && typeof raw === "object" ? Object.keys(raw as object) : null
        );
      }

      const q = searchQuery.trim().toLowerCase();
      const filtered =
        q === ""
          ? normalized
          : normalized.filter((c) => (c.name ?? "").toLowerCase().includes(q));
      setCategories(filtered);
      setLastRefreshedAt(new Date());
    } catch (error: any) {
      console.error("Failed to load categories:", error);
      toast({
        title: "خطا در بارگذاری دسته‌بندی‌ها",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      });
    } finally {
      setLoading(false);
    }
  };

  const refreshCategoryContext = async () => {
    try {
      const data = await categoryService.list();
      await saveCategories(data);
      setLastRefreshedAt(new Date());
    } catch (error) {
      console.error("Failed to refresh category context", error);
    }
  };

  const loadSubcategories = async (categoryId: number) => {
    setSubcategoriesLoading(true);
    try {
      const raw = await apiRequest<unknown>(
        `/api/categories/${categoryId}/subcategories`,
        { method: "GET", token: token ?? undefined }
      );
      console.log("[CATEGORY_MGMT] subcategories raw", raw);

      const rawList = Array.isArray(raw)
        ? raw
        : (raw as { items?: unknown[]; data?: unknown[] })?.items ??
          (raw as { items?: unknown[]; data?: unknown[] })?.data ??
          [];

      const normalizeSub = (s: Record<string, unknown>): ApiSubcategoryResponse => ({
        id: (s.id as number) ?? (s.Id as number),
        categoryId: (s.categoryId as number) ?? (s.CategoryId as number) ?? categoryId,
        name: (s.name as string) ?? (s.Name as string) ?? "",
        isActive: (s.isActive as boolean) ?? (s.IsActive as boolean) ?? true,
        description: (s.description as string | null) ?? (s.Description as string | null) ?? null,
      });

      const normalized = rawList
        .filter((x): x is Record<string, unknown> => x != null && typeof x === "object")
        .map(normalizeSub);
      console.log("[CATEGORY_MGMT] subcategories normalized count", normalized.length);

      setSubcategories(normalized);
    } catch (error: any) {
      console.error("Failed to load subcategories:", error);
      toast({
        title: "خطا در بارگذاری زیر دسته‌ها",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      });
    } finally {
      setSubcategoriesLoading(false);
    }
  };

  const handleCreateCategory = async () => {
    const trimmedName = newCategoryData.name?.trim() ?? "";
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً وارد شوید",
        variant: "destructive",
      });
      return;
    }
    if (!trimmedName) {
      toast({
        title: "خطا",
        description: "لطفاً نام دسته‌بندی را وارد کنید",
        variant: "destructive",
      });
      return;
    }

    const payload = {
      name: trimmedName,
      description: newCategoryData.description?.trim() ?? "",
      isActive: newCategoryData.isActive ?? true,
    };

    try {
      console.log("[CategoryManagement] Creating category:", payload);
      const createdCategory = await createCategory(token, payload);
      console.log("[CategoryManagement] Category created successfully:", createdCategory);
      
      // Verify we got a valid response with an ID
      if (!createdCategory || !createdCategory.id) {
        throw new Error("Server returned invalid response - no category ID");
      }
      
      toast({
        title: "موفق",
        description: `دسته‌بندی "${createdCategory.name}" با شناسه ${createdCategory.id} ایجاد شد`,
      });
      setCategoryDialogOpen(false);
      setNewCategoryData({ name: "", description: "", isActive: true });
      
      // Refresh categories list
      console.log("[CategoryManagement] Refreshing categories list...");
      await loadCategories();
      await refreshCategoryContext();
      console.log("[CategoryManagement] Categories refreshed");
    } catch (error: any) {
      console.error("[CategoryManagement] Failed to create category:", error);
      const errorMessage = error?.message || "لطفاً دوباره تلاش کنید";
      const statusCode = error?.status;
      
      let description = errorMessage;
      if (statusCode === 400) {
        description = `خطای اعتبارسنجی: ${errorMessage}`;
      } else if (statusCode === 401 || statusCode === 403) {
        description = "شما مجوز ایجاد دسته‌بندی را ندارید";
      } else if (statusCode === 409) {
        description = "این دسته‌بندی قبلاً وجود دارد";
      } else if (statusCode >= 500) {
        description = `خطای سرور: ${errorMessage}`;
      }
      
      toast({
        title: "خطا در ایجاد دسته‌بندی",
        description,
        variant: "destructive",
      });
    }
  };

  const handleUpdateCategory = async () => {
    if (!user || !editingCategory || !editingCategory.name.trim()) {
      toast({
        title: "خطا",
        description: "لطفاً نام دسته‌بندی را وارد کنید",
        variant: "destructive",
      });
      return;
    }

    try {
      await updateCategory(token, editingCategory.id, {
        name: editingCategory.name,
        description: editingCategory.description || null,
        isActive: editingCategory.isActive,
      });
      toast({ title: "موفق", description: "دسته‌بندی به‌روزرسانی شد" });
      setEditingCategory(null);
      await loadCategories();
      await refreshCategoryContext();
      if (selectedCategoryId === editingCategory.id) {
        await loadSubcategories(editingCategory.id);
      }
    } catch (error: any) {
      console.error("Failed to update category:", error);
      toast({
        title: "خطا در به‌روزرسانی دسته‌بندی",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      });
    }
  };

  const handleDeleteCategory = async (categoryId: number) => {
    if (!user) return;
    if (!confirm("آیا از حذف این دسته‌بندی اطمینان دارید؟")) return;

    try {
      await deleteCategory(token, categoryId);
      toast({
        title: "موفق",
        description: "دسته‌بندی حذف شد",
      });
      if (selectedCategoryId === categoryId) {
        setSelectedCategoryId(null);
      }
      await loadCategories();
      await refreshCategoryContext();
    } catch (error: any) {
      console.error("Failed to delete category:", error);
      toast({
        title: "خطا در حذف دسته‌بندی",
        description: error?.message || "این دسته‌بندی در حال استفاده است و نمی‌توان آن را حذف کرد",
        variant: "destructive",
      });
    }
  };

  const handleCreateSubCategory = async () => {
    if (!user || !selectedCategoryId || !newSubCategoryData.name.trim()) {
      toast({
        title: "خطا",
        description: "لطفاً نام زیر دسته را وارد کنید",
        variant: "destructive",
      });
      return;
    }

    try {
      console.log("[CategoryManagement] Creating subcategory:", { categoryId: selectedCategoryId, ...newSubCategoryData });
      const createdSubcategory = await createSubcategory(token, selectedCategoryId, newSubCategoryData);
      console.log("[CategoryManagement] Subcategory created successfully:", createdSubcategory);
      
      // Verify we got a valid response with an ID
      if (!createdSubcategory || !createdSubcategory.id) {
        throw new Error("Server returned invalid response - no subcategory ID");
      }
      
      toast({
        title: "موفق",
        description: `زیر دسته "${createdSubcategory.name}" با شناسه ${createdSubcategory.id} ایجاد شد`,
      });
      setSubCategoryDialogOpen(false);
      setNewSubCategoryData({ name: "", description: "", isActive: true });
      
      // Refresh lists
      console.log("[CategoryManagement] Refreshing subcategories list...");
      await loadSubcategories(selectedCategoryId);
      await loadCategories();
      await refreshCategoryContext();
      console.log("[CategoryManagement] Subcategories refreshed");
    } catch (error: any) {
      console.error("[CategoryManagement] Failed to create subcategory:", error);
      const errorMessage = error?.message || "لطفاً دوباره تلاش کنید";
      const statusCode = error?.status;
      
      let description = errorMessage;
      if (statusCode === 400) {
        description = `خطای اعتبارسنجی: ${errorMessage}`;
      } else if (statusCode === 401 || statusCode === 403) {
        description = "شما مجوز ایجاد زیر دسته را ندارید";
      } else if (statusCode === 404) {
        description = "دسته‌بندی مورد نظر یافت نشد";
      } else if (statusCode === 409) {
        description = "این زیر دسته قبلاً وجود دارد";
      } else if (statusCode >= 500) {
        description = `خطای سرور: ${errorMessage}`;
      }
      
      toast({
        title: "خطا در ایجاد زیر دسته",
        description,
        variant: "destructive",
      });
    }
  };

  const handleUpdateSubCategory = async () => {
    if (!user || !editingSubCategory || !editingSubCategory.name.trim()) {
      toast({
        title: "خطا",
        description: "لطفاً نام زیر دسته را وارد کنید",
        variant: "destructive",
      });
      return;
    }

    try {
      await updateSubcategory(token, editingSubCategory.id, {
        name: editingSubCategory.name,
        description: editingSubCategory.description || null,
        isActive: editingSubCategory.isActive,
      });
      toast({ title: "موفق", description: "زیر دسته به‌روزرسانی شد" });
      setEditingSubCategory(null);
      if (selectedCategoryId) {
        await loadSubcategories(selectedCategoryId);
        await loadCategories();
        await refreshCategoryContext();
      }
    } catch (error: any) {
      console.error("Failed to update subcategory:", error);
      toast({
        title: "خطا در به‌روزرسانی زیر دسته",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      });
    }
  };

  const handleDeleteSubCategory = async (subCategoryId: number) => {
    if (!user) return;
    if (!confirm("آیا از حذف این زیر دسته اطمینان دارید؟")) return;

    try {
      await deleteSubcategory(token, subCategoryId);
      toast({
        title: "موفق",
        description: "زیر دسته حذف شد",
      });
      if (selectedCategoryId) {
        await loadSubcategories(selectedCategoryId);
        await loadCategories();
        await refreshCategoryContext();
      }
    } catch (error: any) {
      console.error("Failed to delete subcategory:", error);
      toast({
        title: "خطا در حذف زیر دسته",
        description: error?.message || "این زیر دسته در حال استفاده است و نمی‌توان آن را حذف کرد",
        variant: "destructive",
      });
    }
  };

  // Dynamic Field Designer handlers
  const openFieldDesigner = (type: "category" | "subcategory", id: number) => {
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد سیستم شوید",
        variant: "destructive",
      });
      return;
    }

    setDesigningScope({ type, id });
    setFieldDesignerOpen(true);
  };

  const updateField = (
    index: number,
    patch: Partial<FormFieldDef> & { optionsText?: string }
  ) => {
    setEditingFields((prev) => {
      const copy = [...prev];
      const existing = copy[index];
      let options = existing.options;
      if (patch.optionsText !== undefined) {
        options = parseOptions(patch.optionsText);
      }
      copy[index] = { ...existing, ...patch, options };
      return copy;
    });
  };

  const removeField = (index: number) => {
    setEditingFields((prev) => prev.filter((_, i) => i !== index));
  };

  // Default fields preview for built-in forms
  const getDefaultFieldDefs = (
    catId: string,
    subId?: string
  ): FormFieldDef[] => {
    switch (catId) {
      case "hardware": {
        if (subId === "computer-not-working") {
          return [
            {
              id: "deviceBrand",
              label: "برند دستگاه",
              type: "select",
              required: false,
              options: [],
            },
            {
              id: "deviceModel",
              label: "مدل دستگاه",
              type: "text",
              required: false,
            },
            {
              id: "powerStatus",
              label: "وضعیت روشن شدن",
              type: "select",
              required: false,
              options: [],
            },
          ];
        }
        return [
          {
            id: "deviceType",
            label: "نوع دستگاه",
            type: "select",
            required: true,
            options: [],
          },
          {
            id: "deviceModel",
            label: "مدل دستگاه",
            type: "text",
            required: true,
          },
          {
            id: "serialNumber",
            label: "سریال نامبر",
            type: "text",
            required: false,
          },
          {
            id: "warrantyStatus",
            label: "وضعیت گارانتی",
            type: "select",
            required: true,
            options: [],
          },
        ];
      }
      case "software": {
        if (subId === "os-issues") {
          return [
            {
              id: "operatingSystem",
              label: "سیستم‌عامل",
              type: "select",
              required: false,
              options: [],
            },
            {
              id: "version",
              label: "نسخه/بیلد",
              type: "text",
              required: false,
            },
          ];
        }
        return [
          {
            id: "softwareName",
            label: "نام نرم‌افزار",
            type: "text",
            required: true,
          },
          { id: "version", label: "نسخه", type: "text", required: false },
          {
            id: "licenseInfo",
            label: "اطلاعات لایسنس",
            type: "textarea",
            required: false,
          },
        ];
      }
      case "network": {
        if (subId === "internet-connection") {
          return [
            {
              id: "internetAccess",
              label: "دسترسی اینترنت",
              type: "select",
              required: false,
              options: [],
            },
            {
              id: "affectedServices",
              label: "سرویس‌های متاثر",
              type: "textarea",
              required: true,
            },
          ];
        }
        return [
          {
            id: "connectionType",
            label: "نوع اتصال",
            type: "select",
            required: true,
            options: [],
          },
          {
            id: "networkLocation",
            label: "محل شبکه/اتاق",
            type: "text",
            required: true,
          },
          { id: "ipAddress", label: "آدرس IP", type: "text", required: false },
        ];
      }
      case "email": {
        return [
          {
            id: "emailProvider",
            label: "سرویس‌دهنده ایمیل",
            type: "select",
            required: true,
            options: [],
          },
          {
            id: "emailClient",
            label: "کلاینت ایمیل",
            type: "select",
            required: false,
            options: [],
          },
        ];
      }
      case "security": {
        return [
          {
            id: "incidentType",
            label: "نوع رویداد امنیتی",
            type: "select",
            required: true,
            options: [],
          },
          {
            id: "affectedSystems",
            label: "سامانه‌های متاثر",
            type: "textarea",
            required: false,
          },
        ];
      }
      case "access": {
        return [
          { id: "system", label: "سامانه/سیستم", type: "text", required: true },
          {
            id: "requestedAccess",
            label: "دسترسی درخواستی",
            type: "textarea",
            required: true,
          },
        ];
      }
      case "training": {
        return [
          { id: "topic", label: "موضوع", type: "text", required: true },
          {
            id: "preferredTime",
            label: "زمان پیشنهادی",
            type: "datetime",
            required: false,
          },
        ];
      }
      case "maintenance": {
        return [
          { id: "asset", label: "دارایی/تجهیز", type: "text", required: true },
          {
            id: "maintenanceType",
            label: "نوع نگهداشت",
            type: "select",
            required: false,
            options: [],
          },
        ];
      }
      default:
        return [];
    }
  };

  const defaultPreview: FormFieldDef[] = [];

  return (
    <div className="space-y-6" dir="rtl">
      <div className="flex justify-between items-center">
        <div className="text-right">
          <h3 className="text-xl font-bold font-iran">مدیریت دسته‌بندی‌ها</h3>
          <p className="text-muted-foreground font-iran">
            مدیریت دسته‌بندی‌ها و زیر دسته‌های تیکت‌ها
          </p>
          <p className="text-xs text-muted-foreground font-iran mt-1">
            آخرین بروزرسانی:{" "}
            {lastRefreshedAt ? toFaDateTime(lastRefreshedAt) : "—"}
          </p>
        </div>
        <Dialog open={categoryDialogOpen} onOpenChange={setCategoryDialogOpen}>
          <DialogTrigger asChild>
            <Button className="gap-2 font-iran">
              <Plus className="w-4 h-4" />
              دسته‌بندی جدید
            </Button>
          </DialogTrigger>
          <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-md font-iran" dir="rtl">
            <DialogHeader>
              <DialogTitle className="text-right font-iran">
                ایجاد دسته‌بندی جدید
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="categoryName" className="text-right font-iran">
                  نام دسته‌بندی *
                </Label>
                <Input
                  id="categoryName"
                  placeholder="مثال: مشکلات سخت‌افزاری"
                  value={newCategoryData.name}
                  onChange={(e) =>
                    setNewCategoryData({
                      ...newCategoryData,
                      name: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="space-y-2">
                <Label
                  htmlFor="categoryDescription"
                  className="text-right font-iran"
                >
                  توضیحات
                </Label>
                <Textarea
                  id="categoryDescription"
                  placeholder="توضیحات دسته‌بندی..."
                  value={newCategoryData.description}
                  onChange={(e) =>
                    setNewCategoryData({
                      ...newCategoryData,
                      description: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="categoryIsActive"
                  checked={newCategoryData.isActive}
                  onCheckedChange={(checked) =>
                    setNewCategoryData({ ...newCategoryData, isActive: checked })
                  }
                />
                <Label htmlFor="categoryIsActive" className="text-right font-iran">
                  فعال
                </Label>
              </div>
              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  onClick={() => setCategoryDialogOpen(false)}
                  className="font-iran"
                >
                  انصراف
                </Button>
                <Button onClick={handleCreateCategory} className="font-iran">
                  ایجاد
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Categories List */}
        <Card>
          <CardHeader>
            <CardTitle className="text-right font-iran">
              دسته‌بندی‌های اصلی
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="relative mb-4">
              <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
              <Input
                placeholder="جستجو بر اساس نام یا توضیحات..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pr-10 text-right"
                dir="rtl"
              />
            </div>
            <div className="space-y-2">
              {categories.length === 0 ? (
                <div className="text-center py-8">
                  <FolderPlus className="w-12 h-12 text-muted-foreground mx-auto mb-2" />
                  <p className="text-muted-foreground font-iran">
                    هیچ دسته‌بندی‌ای وجود ندارد
                  </p>
                  <p className="text-sm text-muted-foreground font-iran">
                    برای شروع یک دسته‌بندی جدید ایجاد کنید
                  </p>
                </div>
              ) : (
                categories.map((category) => (
                  <div
                    key={category.id}
                    className={`p-3 border rounded-lg cursor-pointer transition-colors ${
                      selectedCategoryId === category.id
                        ? "bg-primary/10 border-primary"
                        : "hover:bg-muted/50"
                    }`}
                    onClick={() => setSelectedCategoryId(category.id)}
                  >
                    <div className="flex justify-between items-start">
                      <div className="text-right flex-1">
                        <div className="flex items-center gap-2">
                          <h4 className="font-medium font-iran">
                            {category.name}
                          </h4>
                          {!category.isActive && (
                            <Badge variant="destructive" className="text-xs">
                              غیرفعال
                            </Badge>
                          )}
                        </div>
                        {category.description && (
                          <p className="text-sm text-muted-foreground mt-1 font-iran">
                            {category.description}
                          </p>
                        )}
                        <div className="flex items-center gap-2 mt-2">
                          <Badge
                            variant="secondary"
                            className="text-xs font-iran"
                          >
                            {category.subcategories?.length || 0} زیر دسته
                          </Badge>
                        </div>
                      </div>
                      <div className="flex gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => {
                              e.stopPropagation();
                              setEditingCategory(category);
                            }}
                            className="font-iran"
                          >
                            <Edit className="w-3 h-3" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={(e) => {
                              e.stopPropagation();
                              openFieldDesigner("category", category.id);
                            }}
                            className="font-iran"
                          >
                            <Settings className="w-3 h-3" />
                          </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={(e) => {
                            e.stopPropagation();
                            if (
                              confirm("آیا از حذف این دسته‌بندی اطمینان دارید؟")
                            ) {
                              handleDeleteCategory(category.id);
                            }
                          }}
                          className="text-red-600 hover:text-red-700 font-iran"
                        >
                          <Trash2 className="w-3 h-3" />
                        </Button>
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </CardContent>
        </Card>

        {/* Sub Categories */}
        <Card>
          <CardHeader>
            <div className="flex justify-between items-center">
              <CardTitle className="text-right font-iran">
                {selectedCategoryId
                  ? `زیر دسته‌های ${categories.find(c => c.id === selectedCategoryId)?.name || ""}`
                  : "زیر دسته‌ها"}
              </CardTitle>
              {selectedCategoryId && (
                <Dialog
                  open={subCategoryDialogOpen}
                  onOpenChange={setSubCategoryDialogOpen}
                >
                  <DialogTrigger asChild>
                    <Button size="sm" className="gap-2 font-iran">
                      <FolderPlus className="w-4 h-4" />
                      زیر دسته جدید
                    </Button>
                  </DialogTrigger>
                  <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-md font-iran" dir="rtl">
                    <DialogHeader>
                      <DialogTitle className="text-right font-iran">
                        ایجاد زیر دسته جدید
                      </DialogTitle>
                    </DialogHeader>
                    <div className="space-y-4">
                      <div className="space-y-2">
                        <Label
                          htmlFor="subCategoryName"
                          className="text-right font-iran"
                        >
                          نام زیر دسته *
                        </Label>
                        <Input
                          id="subCategoryName"
                          placeholder="مثال: رایانه کار نمی‌کند"
                          value={newSubCategoryData.name}
                          onChange={(e) =>
                            setNewSubCategoryData({
                              ...newSubCategoryData,
                              name: e.target.value,
                            })
                          }
                          className="text-right font-iran"
                          dir="rtl"
                        />
                      </div>
                      <div className="space-y-2">
                        <Label
                          htmlFor="subCategoryDescription"
                          className="text-right font-iran"
                        >
                          توضیحات
                        </Label>
                        <Textarea
                          id="subCategoryDescription"
                          placeholder="توضیحات زیر دسته..."
                          value={newSubCategoryData.description}
                          onChange={(e) =>
                            setNewSubCategoryData({
                              ...newSubCategoryData,
                              description: e.target.value,
                            })
                          }
                          className="text-right font-iran"
                          dir="rtl"
                        />
                      </div>
                      <div className="flex items-center gap-2">
                        <Switch
                          id="subCategoryIsActive"
                          checked={newSubCategoryData.isActive}
                          onCheckedChange={(checked) =>
                            setNewSubCategoryData({ ...newSubCategoryData, isActive: checked })
                          }
                        />
                        <Label htmlFor="subCategoryIsActive" className="text-right font-iran">
                          فعال
                        </Label>
                      </div>
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="outline"
                          onClick={() => setSubCategoryDialogOpen(false)}
                          className="font-iran"
                        >
                          انصراف
                        </Button>
                        <Button
                          onClick={handleCreateSubCategory}
                          className="font-iran"
                        >
                          ایجاد
                        </Button>
                      </div>
                    </div>
                  </DialogContent>
                </Dialog>
              )}
            </div>
          </CardHeader>
          <CardContent>
            {selectedCategoryId ? (
              subcategoriesLoading ? (
                <div className="flex items-center justify-center py-8">
                  <Loader2 className="w-6 h-6 animate-spin text-muted-foreground" />
                  <span className="mr-3 text-sm text-muted-foreground">در حال بارگذاری...</span>
                </div>
              ) : subcategories.length === 0 ? (
                <div className="text-center py-8">
                  <FolderPlus className="w-12 h-12 text-muted-foreground mx-auto mb-2" />
                  <p className="text-muted-foreground font-iran">
                    هیچ زیر دسته‌ای وجود ندارد
                  </p>
                  <p className="text-sm text-muted-foreground font-iran">
                    برای شروع یک زیر دسته جدید ایجاد کنید
                  </p>
                </div>
              ) : (
                <div className="space-y-2">
                  {subcategories.map((subCategory) => (
                    <div key={subCategory.id} className="p-3 border rounded-lg">
                      <div className="flex justify-between items-start">
                        <div className="text-right flex-1">
                          <div className="flex items-center gap-2">
                            <h5 className="font-medium font-iran">
                              {subCategory.name}
                            </h5>
                            {!subCategory.isActive && (
                              <Badge variant="secondary" className="text-xs">
                                غیرفعال
                              </Badge>
                            )}
                          </div>
                          {subCategory.description && (
                            <p className="text-sm text-muted-foreground mt-1 font-iran">
                              {subCategory.description}
                            </p>
                          )}
                        </div>
                        <div className="flex gap-1">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setEditingSubCategory(subCategory)}
                            className="font-iran"
                          >
                            <Edit className="w-3 h-3" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => openFieldDesigner("subcategory", subCategory.id)}
                            className="font-iran"
                          >
                            <Settings className="w-3 h-3" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleDeleteSubCategory(subCategory.id)}
                            className="text-red-600 hover:text-red-700 font-iran"
                          >
                            <Trash2 className="w-3 h-3" />
                          </Button>
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
              )
            ) : (
              <div className="text-center py-8">
                <Settings className="w-12 h-12 text-muted-foreground mx-auto mb-2" />
                <p className="text-muted-foreground font-iran">
                  دسته‌بندی انتخاب کنید
                </p>
                <p className="text-sm text-muted-foreground font-iran">
                  برای مشاهده زیر دسته‌ها، یک دسته‌بندی انتخاب کنید
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Edit Category Dialog */}
      {editingCategory && (
        <Dialog
          open={!!editingCategory}
          onOpenChange={() => setEditingCategory(null)}
        >
          <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-md font-iran" dir="rtl">
            <DialogHeader>
              <DialogTitle className="text-right font-iran">
                ویرایش دسته‌بندی
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label
                  htmlFor="editCategoryName"
                  className="text-right font-iran"
                >
                  نام دسته‌بندی *
                </Label>
                <Input
                  id="editCategoryName"
                  value={editingCategory.name}
                  onChange={(e) =>
                    setEditingCategory({
                      ...editingCategory,
                      name: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="space-y-2">
                <Label
                  htmlFor="editCategoryDescription"
                  className="text-right font-iran"
                >
                  توضیحات
                </Label>
                <Textarea
                  id="editCategoryDescription"
                  value={editingCategory.description}
                  onChange={(e) =>
                    setEditingCategory({
                      ...editingCategory,
                      description: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="editCategoryIsActive"
                  checked={editingCategory.isActive}
                  onCheckedChange={(checked) =>
                    setEditingCategory({ ...editingCategory, isActive: checked })
                  }
                />
                <Label htmlFor="editCategoryIsActive" className="text-right font-iran">
                  فعال
                </Label>
              </div>
              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  onClick={() => setEditingCategory(null)}
                  className="font-iran"
                >
                  انصراف
                </Button>
                <Button onClick={handleUpdateCategory} className="font-iran">
                  به‌روزرسانی
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}

      {editingSubCategory && (
        <Dialog
          open={!!editingSubCategory}
          onOpenChange={() => setEditingSubCategory(null)}
        >
          <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-md font-iran" dir="rtl">
            <DialogHeader>
              <DialogTitle className="text-right font-iran">
                ویرایش زیر دسته
              </DialogTitle>
            </DialogHeader>
            <div className="space-y-4">
              <div className="space-y-2">
                <Label
                  htmlFor="editSubCategoryName"
                  className="text-right font-iran"
                >
                  نام زیر دسته *
                </Label>
                <Input
                  id="editSubCategoryName"
                  value={editingSubCategory.name}
                  onChange={(e) =>
                    setEditingSubCategory({
                      ...editingSubCategory,
                      name: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="space-y-2">
                <Label
                  htmlFor="editSubCategoryDescription"
                  className="text-right font-iran"
                >
                  توضیحات
                </Label>
                <Textarea
                  id="editSubCategoryDescription"
                  value={editingSubCategory.description}
                  onChange={(e) =>
                    setEditingSubCategory({
                      ...editingSubCategory,
                      description: e.target.value,
                    })
                  }
                  className="text-right font-iran"
                  dir="rtl"
                />
              </div>
              <div className="flex items-center gap-2">
                <Switch
                  id="editSubCategoryIsActive"
                  checked={editingSubCategory.isActive}
                  onCheckedChange={(checked) =>
                    setEditingSubCategory({ ...editingSubCategory, isActive: checked })
                  }
                />
                <Label htmlFor="editSubCategoryIsActive" className="text-right font-iran">
                  فعال
                </Label>
              </div>
              <div className="flex justify-end gap-2">
                <Button
                  variant="outline"
                  onClick={() => setEditingSubCategory(null)}
                  className="font-iran"
                >
                  انصراف
                </Button>
                <Button onClick={handleUpdateSubCategory} className="font-iran">
                  به‌روزرسانی
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}

      {/* Field Designer Dialog */}
      {designingScope !== null && (
        <SubcategoryFieldDesignerDialog
          open={fieldDesignerOpen}
          onOpenChange={(open) => {
            setFieldDesignerOpen(open);
            if (!open) {
              setDesigningScope(null);
              void refreshCategoryContext();
            }
          }}
          scopeType={designingScope.type}
          scopeId={designingScope.id}
          token={token}
        />
      )}

      {/* Warning Message */}
      <Card className="border-orange-200 bg-orange-50">
        <CardContent className="pt-6">
          <div className="flex items-start gap-3">
            <AlertTriangle className="w-5 h-5 text-orange-600 mt-0.5" />
            <div className="text-right">
              <h4 className="font-medium text-orange-800 font-iran">
                نکته مهم
              </h4>
              <p className="text-sm text-orange-700 mt-1 font-iran">
                تغییرات در دسته‌بندی‌ها بلافاصله در فرم ایجاد تیکت کاربران اعمال
                می‌شود. حذف دسته‌بندی‌ها ممکن است بر تیکت‌های موجود تأثیر
                بگذارد.
              </p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

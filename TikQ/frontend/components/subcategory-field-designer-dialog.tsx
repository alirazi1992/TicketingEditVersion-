"use client";

import { useState, useEffect, useCallback, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
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
import { Loader2, Trash2, Edit2, Save, X } from "lucide-react";
import type { FormFieldDef, FieldType } from "@/lib/dynamic-forms";
import { parseOptions } from "@/lib/dynamic-forms";
import {
  getFieldDefinitions,
  getCategoryFieldDefinitions,
  createFieldDefinition,
  createCategoryFieldDefinition,
  updateFieldDefinition,
  updateCategoryFieldDefinition,
  deleteFieldDefinition,
  deleteCategoryFieldDefinition,
  type FieldDefinitionResponse,
} from "@/lib/field-definitions-api";

interface SubcategoryFieldDesignerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  scopeType?: "subcategory" | "category";
  scopeId: number;
  token: string | null;
}

export function SubcategoryFieldDesignerDialog({
  open,
  onOpenChange,
  scopeType = "subcategory",
  scopeId,
  token,
}: SubcategoryFieldDesignerDialogProps) {
  const { user } = useAuth();
  const [fields, setFields] = useState<FieldDefinitionResponse[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isBackendDown, setIsBackendDown] = useState(false);
  const [lastTriedUrls, setLastTriedUrls] = useState<string[]>([]);
  const [saving, setSaving] = useState(false);
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [deletingIndex, setDeletingIndex] = useState<number | null>(null);
  const [showDevDiagnostics, setShowDevDiagnostics] = useState(false);
  const errorLogRef = useRef<Set<string>>(new Set());

  // New field form state
  const [newField, setNewField] = useState<{
    key: string;
    label: string;
    type: FieldType;
    isRequired: boolean;
    defaultValue?: string;
    optionsText?: string;
    displayOrder?: number;
  }>({
    key: "",
    label: "",
    type: "text",
    isRequired: false,
    defaultValue: "",
    optionsText: "",
    displayOrder: 0,
  });

  // Validation errors
  const [errors, setErrors] = useState<Record<string, string>>({});

  const loadFields = useCallback(async () => {
    if (!user) return;

    setLoading(true);
    setError(null);
    setIsBackendDown(false);
    setLastTriedUrls([]);
    try {
      const loadedFields =
        scopeType === "category"
          ? await getCategoryFieldDefinitions(token, scopeId)
          : await getFieldDefinitions(token, scopeId);
      setFields(loadedFields);
      setError(null);
      setIsBackendDown(false);
    } catch (error: any) {
      const isNetworkError =
        error?.isNetworkError ||
        error?.message?.includes("Cannot connect to backend server");
      const status = typeof error?.status === "number" ? error.status : undefined;
      const errorMessage = isNetworkError
        ? "Backend not reachable. Start backend with .\\tools\\run-backend.ps1"
        : status === 401
          ? "Unauthorized – please login"
          : status === 403
            ? "Not enough permissions to access this resource"
            : status === 404
              ? (process.env.NODE_ENV === "development" && error?.requestPath
                  ? `Endpoint not found: ${error.requestPath}`
                  : "Endpoint not found")
        : error?.message || `خطا در دریافت فیلدها (${error?.status || "unknown"})`;
      const logKey = `${scopeType}:${scopeId}:${isNetworkError ? "network" : "error"}`;
      if (process.env.NODE_ENV === "development" && !errorLogRef.current.has(logKey)) {
        console.error("[SubcategoryFieldDesigner] Error loading fields:", error);
        errorLogRef.current.add(logKey);
      }
      setError(errorMessage);
      setIsBackendDown(isNetworkError);
      setLastTriedUrls(Array.isArray(error?.triedUrls) ? error.triedUrls : []);
      
      // Check if it's a schema error
      if (errorMessage.includes("schema") || errorMessage.includes("migration") || errorMessage.includes("DefaultValue")) {
        setError("خطای پایگاه داده: لطفاً سرور بک‌اند را راه‌اندازی مجدد کنید تا مایگریشن‌ها اعمال شوند.");
      }
      
      if (!isNetworkError) {
        toast({
          title: "خطا در بارگذاری فیلدها",
          description: errorMessage,
          variant: "destructive",
        });
      }
      setFields([]);
    } finally {
      setLoading(false);
    }
  }, [token, scopeId, scopeType]);

  // Load fields when dialog opens
  useEffect(() => {
    if (open && token && scopeId) {
      loadFields();
    } else {
      // Reset state when dialog closes
      setFields([]);
      setEditingIndex(null);
      setDeletingIndex(null);
      setNewField({
        key: "",
        label: "",
        type: "text",
        isRequired: false,
        defaultValue: "",
        optionsText: "",
        displayOrder: 0,
      });
      setErrors({});
      setError(null);
      setIsBackendDown(false);
      setLastTriedUrls([]);
    }
  }, [open, token, scopeId, loadFields]);

  const validateNewField = (): boolean => {
    const newErrors: Record<string, string> = {};

    if (!newField.key.trim()) {
      newErrors.key = "شناسه فیلد الزامی است";
    } else if (!/^[a-zA-Z][a-zA-Z0-9_]*$/.test(newField.key)) {
      newErrors.key =
        "شناسه باید با حرف انگلیسی شروع شود و فقط شامل حروف، اعداد و _ باشد";
    } else if (fields.some((f) => f.key === newField.key)) {
      newErrors.key = "این شناسه قبلاً استفاده شده است";
    }

    if (!newField.label.trim()) {
      newErrors.label = "عنوان فیلد الزامی است";
    }

    if (
      (newField.type === "select" || newField.type === "multiselect" || newField.type === "radio") &&
      !newField.optionsText?.trim()
    ) {
      newErrors.optionsText = "برای فیلدهای انتخابی، گزینه‌ها الزامی است";
    }

    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleAddField = async () => {
    if (!user || !validateNewField()) {
      return;
    }

    setSaving(true);
    try {
      const options =
        (newField.type === "select" || newField.type === "multiselect" || newField.type === "radio")
          ? parseOptions(newField.optionsText || "")
          : [];

      const backendType = mapFrontendTypeToBackendType(newField.type);
      const created =
        scopeType === "category"
          ? await createCategoryFieldDefinition(token, scopeId, {
              name: newField.key,
              label: newField.label,
              key: newField.key,
              type: backendType,
              isRequired: newField.isRequired,
              defaultValue: newField.defaultValue || undefined,
              options: options.length > 0 ? options : undefined,
              displayOrder: newField.displayOrder || 0,
            })
          : await createFieldDefinition(token, scopeId, {
              name: newField.key,
              label: newField.label,
              key: newField.key,
              type: backendType,
              isRequired: newField.isRequired,
              defaultValue: newField.defaultValue || undefined,
              options: options.length > 0 ? options : undefined,
              displayOrder: newField.displayOrder || 0,
            });

      // Refresh fields list
      await loadFields();

      // Reset form
      setNewField({
        key: "",
        label: "",
        type: "text",
        isRequired: false,
        defaultValue: "",
        optionsText: "",
        displayOrder: 0,
      });
      setErrors({});

      toast({
        title: "موفق",
        description: "فیلد با موفقیت اضافه شد",
      });
    } catch (error: any) {
      console.error("[SubcategoryFieldDesigner] Error creating field:", error);
      toast({
        title: "خطا در ایجاد فیلد",
        description:
          error?.message ||
          `خطا در ایجاد فیلد (${error?.status || "unknown"})`,
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleUpdateField = async (index: number) => {
    if (!user || editingIndex === null) return;

    const field = fields[index];
    const updatedField = { ...field };

    // Validate
    if (!updatedField.label.trim()) {
      toast({
        title: "خطا",
        description: "عنوان فیلد الزامی است",
        variant: "destructive",
      });
      return;
    }

    setSaving(true);
    try {
      const backendType = mapFrontendTypeToBackendType(
        mapBackendTypeToFrontendType(updatedField.type)
      );

      if (scopeType === "category") {
        await updateCategoryFieldDefinition(token, scopeId, field.id, {
          label: updatedField.label,
          type: backendType,
          isRequired: updatedField.isRequired,
          defaultValue: updatedField.defaultValue || undefined,
          options: updatedField.options || undefined,
          displayOrder: updatedField.displayOrder ?? 0,
        });
      } else {
        await updateFieldDefinition(token, scopeId, field.id, {
          label: updatedField.label,
          type: backendType,
          isRequired: updatedField.isRequired,
          defaultValue: updatedField.defaultValue || undefined,
          options: updatedField.options || undefined,
          displayOrder: updatedField.displayOrder ?? 0,
        });
      }

      await loadFields();
      setEditingIndex(null);

      toast({
        title: "موفق",
        description: "فیلد با موفقیت به‌روزرسانی شد",
      });
    } catch (error: any) {
      console.error("[SubcategoryFieldDesigner] Error updating field:", error);
      toast({
        title: "خطا در به‌روزرسانی فیلد",
        description:
          error?.message ||
          `خطا در به‌روزرسانی فیلد (${error?.status || "unknown"})`,
        variant: "destructive",
      });
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteField = async (index: number) => {
    if (!user) return;

    const field = fields[index];
    if (!confirm(`آیا از حذف فیلد "${field.label}" مطمئن هستید؟`)) {
      return;
    }

    setDeletingIndex(index);
    try {
      if (scopeType === "category") {
        await deleteCategoryFieldDefinition(token, scopeId, field.id);
      } else {
        await deleteFieldDefinition(token, scopeId, field.id);
      }
      await loadFields();
      toast({
        title: "موفق",
        description: "فیلد با موفقیت حذف شد",
      });
    } catch (error: any) {
      console.error("[SubcategoryFieldDesigner] Error deleting field:", error);
      toast({
        title: "خطا در حذف فیلد",
        description:
          error?.message ||
          `خطا در حذف فیلد (${error?.status || "unknown"})`,
        variant: "destructive",
      });
    } finally {
      setDeletingIndex(null);
    }
  };

  const mapBackendTypeToFrontendType = (backendType: string): FieldType => {
    const mapping: Record<string, FieldType> = {
      Text: "text",
      TextArea: "textarea",
      Number: "number",
      Email: "email",
      Phone: "tel",
      Date: "date",
      Select: "select",
      MultiSelect: "multiselect",
      Boolean: "checkbox",
    };
    return mapping[backendType] || "text";
  };

  const mapFrontendTypeToBackendType = (frontendType: FieldType): string => {
    const mapping: Record<FieldType, string> = {
      text: "Text",
      textarea: "TextArea",
      number: "Number",
      email: "Email",
      tel: "Phone",
      date: "Date",
      datetime: "Date",
      select: "Select",
      multiselect: "MultiSelect",
      radio: "Select",
      checkbox: "Boolean",
      file: "Text",
    };
    return mapping[frontendType] || "Text";
  };

  const getTypeLabel = (type: string): string => {
    const labels: Record<string, string> = {
      Text: "متن",
      TextArea: "چندخطی",
      Number: "عدد",
      Email: "ایمیل",
      Phone: "تلفن",
      Date: "تاریخ",
      Select: "لیست انتخابی (تک‌انتخاب)",
      MultiSelect: "لیست انتخابی (چندانتخاب)",
      Boolean: "تیک‌زدنی",
    };
    return labels[type] || type;
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-6xl flex flex-col font-iran" dir="rtl">
        <DialogHeader>
          <DialogTitle className="text-right">
            {scopeType === "category" ? "طراحی فیلدهای دسته‌بندی" : "طراحی فیلدهای زیر دسته"}
            {loading && <Loader2 className="inline-block w-4 h-4 mr-2 animate-spin" />}
          </DialogTitle>
        </DialogHeader>

        {/* Error State */}
        {error && !loading && (
          <div className="border border-red-300 rounded-lg p-4 bg-red-50 mb-4">
            <p className="text-sm text-red-800 mb-2">{error}</p>
            <div className="flex gap-2 items-center">
              <Button
                variant="outline"
                size="sm"
                onClick={loadFields}
                className="text-red-700 border-red-300"
              >
                تلاش مجدد
              </Button>
              {process.env.NODE_ENV === "development" && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowDevDiagnostics(!showDevDiagnostics)}
                  className="text-xs"
                >
                  {showDevDiagnostics ? "پنهان کردن" : "نمایش"} جزئیات فنی
                </Button>
              )}
            </div>
            {showDevDiagnostics && process.env.NODE_ENV === "development" && (
              <details className="mt-2 text-xs">
                <summary className="cursor-pointer text-red-700">جزئیات خطا (فقط برای توسعه)</summary>
                <pre className="mt-2 p-2 bg-red-100 rounded text-xs overflow-auto max-h-40">
                  {JSON.stringify({ error, scopeId, scopeType }, null, 2)}
                </pre>
              </details>
            )}
          </div>
        )}

        {/* Loading State */}
        {loading && fields.length === 0 && !error && (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="w-6 h-6 animate-spin text-muted-foreground" />
            <span className="mr-2 text-sm text-muted-foreground">
              در حال بارگذاری...
            </span>
          </div>
        )}

        {/* Backend Down State */}
        {isBackendDown && !loading && (
          <div className="border border-amber-300 rounded-lg p-4 bg-amber-50 mb-4">
            <p className="text-sm text-amber-900 mb-2">
              Backend not reachable. Start backend with .\tools\run-backend.ps1
            </p>
            <Button variant="outline" size="sm" onClick={loadFields}>
              تلاش مجدد
            </Button>
            {process.env.NODE_ENV === "development" && lastTriedUrls.length > 0 && (
              <p className="text-xs text-amber-700 mt-2">
                Tried: {lastTriedUrls.join(", ")}
              </p>
            )}
          </div>
        )}

        {/* Main Content: Two Column Layout */}
        {!loading && !error && !isBackendDown && (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 flex-1 overflow-hidden">
            {/* Left Side: Existing Fields List */}
            <div className="border rounded-lg p-4 overflow-y-auto">
              <h3 className="font-medium text-right mb-3">فیلدهای موجود</h3>
              
              {fields.length === 0 ? (
                <div className="border rounded-lg p-6 text-center">
                  <p className="text-sm text-muted-foreground">
                    هیچ فیلدی تعریف نشده است.
                  </p>
                </div>
              ) : (
                <div className="space-y-2">
                  {fields.map((field, index) => (
                <div
                  key={field.id}
                  className="border rounded-lg p-4 space-y-3"
                >
                  {editingIndex === index ? (
                    // Edit Mode
                    <div className="space-y-3">
                      <div className="grid grid-cols-12 gap-3">
                        <div className="col-span-12 md:col-span-4">
                          <Label className="text-right">شناسه (غیرقابل تغییر)</Label>
                          <Input
                            value={field.key}
                            disabled
                            className="text-right bg-muted"
                            dir="rtl"
                          />
                        </div>
                        <div className="col-span-12 md:col-span-4">
                          <Label className="text-right">عنوان *</Label>
                          <Input
                            value={field.label}
                            onChange={(e) => {
                              const updated = [...fields];
                              updated[index] = { ...updated[index], label: e.target.value };
                              setFields(updated);
                            }}
                            className="text-right"
                            dir="rtl"
                          />
                        </div>
                        <div className="col-span-12 md:col-span-4">
                          <Label className="text-right">نوع</Label>
                          <Input
                            value={getTypeLabel(field.type)}
                            disabled
                            className="text-right bg-muted"
                            dir="rtl"
                          />
                        </div>
                        <div className="col-span-12 md:col-span-4">
                          <Label className="text-right">مقدار پیش‌فرض</Label>
                          <Input
                            value={field.defaultValue || ""}
                            onChange={(e) => {
                              const updated = [...fields];
                              updated[index] = {
                                ...updated[index],
                                defaultValue: e.target.value,
                              };
                              setFields(updated);
                            }}
                            className="text-right"
                            dir="rtl"
                          />
                        </div>
                        <div className="col-span-12 md:col-span-4">
                          <Label className="text-right">ترتیب نمایش</Label>
                          <Input
                            type="number"
                            value={field.displayOrder ?? 0}
                            onChange={(e) => {
                              const updated = [...fields];
                              updated[index] = {
                                ...updated[index],
                                displayOrder: Number(e.target.value) || 0,
                              };
                              setFields(updated);
                            }}
                            className="text-right"
                            dir="rtl"
                          />
                        </div>
                        <div className="col-span-12 md:col-span-4 flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={field.isRequired}
                            onChange={(e) => {
                              const updated = [...fields];
                              updated[index] = {
                                ...updated[index],
                                isRequired: e.target.checked,
                              };
                              setFields(updated);
                            }}
                          />
                          <Label>فیلد اجباری</Label>
                        </div>
                        {field.type === "Select" && (
                          <div className="col-span-12">
                            <Label className="text-right">گزینه‌ها</Label>
                            <Input
                              value={
                                field.options
                                  ?.map((o) => `${o.value}:${o.label}`)
                                  .join(", ") || ""
                              }
                              onChange={(e) => {
                                const options = parseOptions(e.target.value);
                                const updated = [...fields];
                                updated[index] = {
                                  ...updated[index],
                                  options,
                                };
                                setFields(updated);
                              }}
                              placeholder="value:label, value2:label2"
                              className="text-right"
                              dir="rtl"
                            />
                          </div>
                        )}
                      </div>
                      <div className="flex justify-end gap-2">
                        <Button
                          variant="outline"
                          onClick={() => setEditingIndex(null)}
                          disabled={saving}
                        >
                          <X className="w-4 h-4 ml-1" />
                          انصراف
                        </Button>
                        <Button
                          onClick={() => handleUpdateField(index)}
                          disabled={saving}
                        >
                          {saving ? (
                            <Loader2 className="w-4 h-4 ml-1 animate-spin" />
                          ) : (
                            <Save className="w-4 h-4 ml-1" />
                          )}
                          ذخیره
                        </Button>
                      </div>
                    </div>
                  ) : (
                    // View Mode
                    <div className="flex items-start justify-between">
                      <div className="flex-1 space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="font-medium">{field.label}</span>
                          {field.isRequired && (
                            <span className="text-xs text-red-600">(اجباری)</span>
                          )}
                        </div>
                        <div className="text-sm text-muted-foreground">
                          شناسه: <code className="text-xs">{field.key}</code> • نوع:{" "}
                          {getTypeLabel(field.type)}
                          {field.defaultValue && ` • پیش‌فرض: ${field.defaultValue}`}
                          {typeof field.displayOrder === "number" && ` • ترتیب: ${field.displayOrder}`}
                        </div>
                        {field.options && field.options.length > 0 && (
                          <div className="text-xs text-muted-foreground">
                            گزینه‌ها:{" "}
                            {field.options.map((o) => o.label).join(", ")}
                          </div>
                        )}
                      </div>
                      <div className="flex gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setEditingIndex(index)}
                          disabled={saving || deletingIndex !== null}
                        >
                          <Edit2 className="w-4 h-4" />
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleDeleteField(index)}
                          disabled={saving || deletingIndex === index}
                          className="text-red-600 hover:text-red-700"
                        >
                          {deletingIndex === index ? (
                            <Loader2 className="w-4 h-4 animate-spin" />
                          ) : (
                            <Trash2 className="w-4 h-4" />
                          )}
                        </Button>
                      </div>
                    </div>
                  )}
                </div>
                  ))}
                </div>
              )}
            </div>

            {/* Right Side: Add Field Form */}
            <div className="border rounded-lg p-4 overflow-y-auto">
              <h3 className="font-medium text-right mb-3">افزودن فیلد جدید</h3>
              <div className="space-y-3">
                <div className="grid grid-cols-12 gap-3">
              <div className="col-span-12 md:col-span-4">
                <Label className="text-right">
                  شناسه <span className="text-red-600">*</span>
                </Label>
                <Input
                  value={newField.key}
                  onChange={(e) => {
                    setNewField({ ...newField, key: e.target.value });
                    if (errors.key) setErrors({ ...errors, key: "" });
                  }}
                  className={`text-right ${errors.key ? "border-red-500" : ""}`}
                  dir="rtl"
                  placeholder="مثال: deviceBrand"
                />
                {errors.key && (
                  <p className="text-xs text-red-600 mt-1">{errors.key}</p>
                )}
              </div>
              <div className="col-span-12 md:col-span-4">
                <Label className="text-right">
                  عنوان <span className="text-red-600">*</span>
                </Label>
                <Input
                  value={newField.label}
                  onChange={(e) => {
                    setNewField({ ...newField, label: e.target.value });
                    if (errors.label) setErrors({ ...errors, label: "" });
                  }}
                  className={`text-right ${errors.label ? "border-red-500" : ""}`}
                  dir="rtl"
                  placeholder="برچسب فیلد"
                />
                {errors.label && (
                  <p className="text-xs text-red-600 mt-1">{errors.label}</p>
                )}
              </div>
              <div className="col-span-12 md:col-span-4">
                <Label className="text-right">ترتیب نمایش</Label>
                <Input
                  type="number"
                  value={newField.displayOrder ?? 0}
                  onChange={(e) =>
                    setNewField({ ...newField, displayOrder: Number(e.target.value) || 0 })
                  }
                  className="text-right"
                  dir="rtl"
                />
              </div>
              <div className="col-span-12 md:col-span-4">
                <Label className="text-right">نوع</Label>
                <Select
                  value={newField.type}
                  onValueChange={(v) =>
                    setNewField({ ...newField, type: v as FieldType })
                  }
                  dir="rtl"
                >
                  <SelectTrigger className="text-right">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="text">متن</SelectItem>
                    <SelectItem value="textarea">چندخطی</SelectItem>
                    <SelectItem value="number">عدد</SelectItem>
                    <SelectItem value="email">ایمیل</SelectItem>
                    <SelectItem value="tel">تلفن</SelectItem>
                    <SelectItem value="date">تاریخ</SelectItem>
                    <SelectItem value="select">لیست انتخابی (تک‌انتخاب)</SelectItem>
                    <SelectItem value="multiselect">لیست انتخابی (چندانتخاب)</SelectItem>
                    <SelectItem value="checkbox">تیک‌زدنی</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="col-span-12 md:col-span-4">
                <Label className="text-right">مقدار پیش‌فرض</Label>
                <Input
                  value={newField.defaultValue || ""}
                  onChange={(e) =>
                    setNewField({ ...newField, defaultValue: e.target.value })
                  }
                  className="text-right"
                  dir="rtl"
                  placeholder="مقدار پیش‌فرض (اختیاری)"
                />
              </div>
              <div className="col-span-12 md:col-span-4 flex items-center gap-2">
                <input
                  type="checkbox"
                  checked={newField.isRequired}
                  onChange={(e) =>
                    setNewField({ ...newField, isRequired: e.target.checked })
                  }
                />
                <Label>فیلد اجباری</Label>
              </div>
              {(newField.type === "select" || newField.type === "radio") && (
                <div className="col-span-12">
                  <Label className="text-right">
                    گزینه‌ها <span className="text-red-600">*</span> (value:label,
                    جداشده با کاما)
                  </Label>
                  <Input
                    value={newField.optionsText || ""}
                    onChange={(e) => {
                      setNewField({ ...newField, optionsText: e.target.value });
                      if (errors.optionsText)
                        setErrors({ ...errors, optionsText: "" });
                    }}
                    className={`text-right ${
                      errors.optionsText ? "border-red-500" : ""
                    }`}
                    dir="rtl"
                    placeholder="Dell:دل, HP:اچ پی, Lenovo:لنوو"
                  />
                  {errors.optionsText && (
                    <p className="text-xs text-red-600 mt-1">
                      {errors.optionsText}
                    </p>
                  )}
                </div>
              )}
                </div>
                <div className="flex justify-end mt-4">
                <Button
                  onClick={handleAddField}
                  disabled={saving || loading}
                  className="w-full"
                >
                  {saving ? (
                    <>
                      <Loader2 className="w-4 h-4 ml-1 animate-spin" />
                      در حال ذخیره...
                    </>
                  ) : (
                    "افزودن فیلد"
                  )}
                </Button>
                </div>
              </div>
            </div>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}



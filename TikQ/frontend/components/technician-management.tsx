"use client"

import { useEffect, useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog"
import { Badge } from "@/components/ui/badge"
import { Switch } from "@/components/ui/switch"
import { toast } from "@/hooks/use-toast"
import { useAuth } from "@/lib/auth-context"
import { useCategories } from "@/services/useCategories"
import { SpecialtiesCell } from "@/components/specialties-cell"
import { toFaDate } from "@/lib/datetime"
import {
  getAllTechnicians,
  createTechnician,
  updateTechnician,
  updateTechnicianStatus,
  deleteTechnician,
} from "@/lib/technicians-api"
import type { ApiTechnicianResponse } from "@/lib/api-types"
import { apiRequest } from "@/lib/api-client"
import type { ApiCategoryResponse, ApiSubcategoryResponse } from "@/lib/api-types"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { Search, Plus, Edit, Trash2, UserCheck, UserX, X } from "lucide-react"

export function TechnicianManagement() {
  const { token, user } = useAuth()
  const { categories: categoryContext } = useCategories()
  const [technicians, setTechnicians] = useState<ApiTechnicianResponse[]>([])
  const [loading, setLoading] = useState(false)
  const [updatingStatus, setUpdatingStatus] = useState<string | null>(null) // Track which technician is being updated
  const [saving, setSaving] = useState(false) // Track if save operation is in progress
  const [searchQuery, setSearchQuery] = useState("")
  const [page, setPage] = useState(1)
  const [pageSize] = useState(10)
  const [totalCount, setTotalCount] = useState(0)
  const [createDialogOpen, setCreateDialogOpen] = useState(false)
  const [editDialogOpen, setEditDialogOpen] = useState(false)
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false)
  const [selectedTechnician, setSelectedTechnician] = useState<ApiTechnicianResponse | null>(null)
  const [technicianToDelete, setTechnicianToDelete] = useState<ApiTechnicianResponse | null>(null)
  const [deleting, setDeleting] = useState(false)
  const [creating, setCreating] = useState(false)

  // Form state
  const [formData, setFormData] = useState({
    fullName: "",
    email: "",
    password: "",
    confirmPassword: "",
    phone: "",
    department: "",
    isActive: true,
    isSupervisor: false,
    subcategoryIds: [] as number[],
  })
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  // Expertise selection state
  const [categories, setCategories] = useState<ApiCategoryResponse[]>([])
  const [subcategories, setSubcategories] = useState<ApiSubcategoryResponse[]>([])
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null)
  const [selectedSubcategoryId, setSelectedSubcategoryId] = useState<number | null>(null)
  const [loadingCategories, setLoadingCategories] = useState(false)

  useEffect(() => {
    loadCategories()
    if (token) loadTechnicians()
  }, [token, categoryContext])

  useEffect(() => {
    if (!user) return
    const timeoutId = setTimeout(() => {
      setPage(1)
      loadTechnicians(1)
    }, 300)
    return () => clearTimeout(timeoutId)
  }, [searchQuery, token])

  useEffect(() => {
    if (!user) return
    loadTechnicians()
  }, [page, token])

  useEffect(() => {
    if (selectedCategoryId != null) {
      loadSubcategories(selectedCategoryId)
    } else {
      setSubcategories([])
      setSelectedSubcategoryId(null)
    }
  }, [token, selectedCategoryId])

  const loadCategories = async () => {
    setLoadingCategories(true)
    try {
      const raw = await apiRequest<unknown>("/api/categories", {
        method: "GET",
        token: token ?? undefined,
      })
      const rawList = Array.isArray(raw)
        ? raw
        : (raw as { items?: unknown[]; data?: unknown[] })?.items ??
          (raw as { items?: unknown[]; data?: unknown[] })?.data ??
          []
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
      })
      const normalized = rawList
        .filter((x): x is Record<string, unknown> => x != null && typeof x === "object")
        .map(normalize)
      setCategories(normalized)
    } catch (error: any) {
      console.error("Failed to load categories:", error)
      toast({
        title: "خطا در بارگذاری دسته‌بندی‌ها",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    } finally {
      setLoadingCategories(false)
    }
  }

  const loadSubcategories = async (categoryId: number) => {
    try {
      const raw = await apiRequest<unknown>(
        `/api/categories/${categoryId}/subcategories`,
        { method: "GET", token: token ?? undefined }
      )
      const rawList = Array.isArray(raw)
        ? raw
        : (raw as { items?: unknown[]; data?: unknown[] })?.items ??
          (raw as { items?: unknown[]; data?: unknown[] })?.data ??
          []
      const normalizeSub = (s: Record<string, unknown>): ApiSubcategoryResponse => ({
        id: (s.id as number) ?? (s.Id as number),
        categoryId: (s.categoryId as number) ?? (s.CategoryId as number) ?? categoryId,
        name: (s.name as string) ?? (s.Name as string) ?? "",
        isActive: (s.isActive as boolean) ?? (s.IsActive as boolean) ?? true,
        description: (s.description as string | null) ?? (s.Description as string | null) ?? null,
      })
      const normalized = rawList
        .filter((x): x is Record<string, unknown> => x != null && typeof x === "object")
        .map(normalizeSub)
      setSubcategories(normalized)
    } catch (error: any) {
      console.error("Failed to load subcategories:", error)
      toast({
        title: "خطا در بارگذاری زیر دسته‌ها",
        description: error?.message || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
    }
  }

  const addExpertise = () => {
    if (!selectedSubcategoryId) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا دسته و زیر دسته را انتخاب کنید",
        variant: "destructive",
      })
      return
    }

    if (formData.subcategoryIds.includes(selectedSubcategoryId)) {
      toast({
        title: "خطا",
        description: "این زیر دسته قبلاً اضافه شده است",
        variant: "destructive",
      })
      return
    }

    setFormData({
      ...formData,
      subcategoryIds: [...formData.subcategoryIds, selectedSubcategoryId],
    })
    setSelectedCategoryId(null)
    setSelectedSubcategoryId(null)
  }

  const removeExpertise = (subcategoryId: number) => {
    setFormData({
      ...formData,
      subcategoryIds: formData.subcategoryIds.filter((id) => id !== subcategoryId),
    })
  }

  const normalizeSpecialties = (value: unknown): string[] => {
    if (Array.isArray(value)) {
      return value
        .flatMap((item) => {
          if (typeof item === "string") return [item]
          if (item && typeof item === "object") {
            const candidate =
              (item as { name?: unknown; title?: unknown; label?: unknown }).name ??
              (item as { name?: unknown; title?: unknown; label?: unknown }).title ??
              (item as { name?: unknown; title?: unknown; label?: unknown }).label
            return typeof candidate === "string" ? [candidate] : []
          }
          return []
        })
        .map((item) => item.trim())
        .filter(Boolean)
    }

    if (typeof value === "string") {
      const trimmed = value.trim()
      if (!trimmed) return []
      if (trimmed.includes("\n") || trimmed.includes(",")) {
        return trimmed
          .split(/[\n,]/g)
          .map((item) => item.trim())
          .filter(Boolean)
      }
      return [trimmed]
    }

    if (value && typeof value === "object") {
      const candidate =
        (value as { name?: unknown; title?: unknown; label?: unknown }).name ??
        (value as { name?: unknown; title?: unknown; label?: unknown }).title ??
        (value as { name?: unknown; title?: unknown; label?: unknown }).label
      if (typeof candidate === "string") {
        const trimmed = candidate.trim()
        return trimmed ? [trimmed] : []
      }
    }

    return []
  }

  const getSubcategoryName = (subcategoryId: number): string => {
    for (const cat of categories) {
      const sub = cat.subcategories.find((s) => s.id === subcategoryId)
      if (sub) {
        return `${cat.name} > ${sub.name}`
      }
    }
    return `زیر دسته ${subcategoryId}`
  }

  const getNormalizedSpecialties = (technician: ApiTechnicianResponse): string[] => {
    const source = technician as ApiTechnicianResponse & {
      specialties?: unknown
      specialty?: unknown
      specialtiesList?: unknown
      specialtiesCsv?: unknown
      specialtiesText?: unknown
    }
    const direct = normalizeSpecialties(
      source.specialties ??
        source.specialty ??
        source.specialtiesList ??
        source.specialtiesCsv ??
        source.specialtiesText
    )
    if (direct.length > 0) return direct

    const fromSubcategories = normalizeSpecialties(
      (technician.subcategoryIds ?? []).map((subId) => getSubcategoryName(subId))
    )
    return fromSubcategories
  }

  const buildCoverage = (subcategoryIds: number[]) => {
    const coverage: { categoryId: number; subcategoryId: number }[] = []
    for (const cat of categories) {
      for (const sub of cat.subcategories) {
        if (subcategoryIds.includes(sub.id)) {
          coverage.push({ categoryId: cat.id, subcategoryId: sub.id })
        }
      }
    }
    return coverage
  }

  const loadTechnicians = async (pageOverride?: number) => {
    if (!user) return
    setLoading(true)
    try {
      const currentPage = pageOverride ?? page
      const data = await getAllTechnicians(token, {
        page: currentPage,
        pageSize,
        search: searchQuery || undefined,
      })
      setTechnicians(data.items)
      setTotalCount(data.totalCount)
    } catch (error: any) {
      console.error("Failed to load technicians:", error)
      // Extract more detailed error message
      const errorMessage = error?.body?.message || error?.message || "لطفاً دوباره تلاش کنید"
      const errorTitle = error?.status === 500 
        ? "خطا در سرور - بارگذاری تکنسین‌ها ناموفق بود"
        : "خطا در بارگذاری تکنسین‌ها"
      
      toast({
        title: errorTitle,
        description: errorMessage,
        variant: "destructive",
        duration: 5000,
      })
    } finally {
      setLoading(false)
    }
  }

  const handleCreate = async () => {
    if (!user) return
    if (!formData.fullName?.trim()) {
      toast({
        title: "خطا در ایجاد تکنسین",
        description: "نام کامل الزامی است",
        variant: "destructive",
      })
      return
    }
    if (!formData.email?.trim()) {
      toast({
        title: "خطا در ایجاد تکنسین",
        description: "ایمیل الزامی است",
        variant: "destructive",
      })
      return
    }
    if (!formData.password || !formData.confirmPassword) {
      toast({
        title: "خطا در ایجاد تکنسین",
        description: "رمز عبور و تکرار آن الزامی هستند",
        variant: "destructive",
      })
      return
    }
    if (formData.password !== formData.confirmPassword) {
      toast({
        title: "خطا در ایجاد تکنسین",
        description: "رمز عبور و تکرار آن مطابقت ندارند",
        variant: "destructive",
      })
      return
    }
    if (formData.password.length < 8 || !/^(?=.*[a-zA-Z])(?=.*\d).+$/.test(formData.password)) {
      toast({
        title: "خطا در ایجاد تکنسین",
        description: "رمز عبور باید حداقل ۸ کاراکتر و شامل حداقل یک حرف و یک عدد باشد",
        variant: "destructive",
      })
      return
    }
    if (creating) return
    setCreating(true)
    try {
      const coverage = buildCoverage(formData.subcategoryIds)
      await createTechnician(token, {
        fullName: formData.fullName.trim(),
        email: formData.email.trim(),
        password: formData.password,
        confirmPassword: formData.confirmPassword,
        phone: formData.phone?.trim() || null,
        department: formData.department?.trim() || null,
        isActive: formData.isActive,
        isSupervisor: formData.isSupervisor,
        role: formData.isSupervisor ? "SupervisorTechnician" : "Technician",
        subcategoryIds: formData.subcategoryIds.length > 0 ? formData.subcategoryIds : null,
        coverage: coverage.length > 0 ? coverage : null,
      })
      toast({
        title: "تکنسین ایجاد شد",
        description: "حساب کاربری تکنسین فعال است و در لیست نمایش داده می‌شود",
      })
      setCreateDialogOpen(false)
      resetForm()
      setPage(1)
      await loadTechnicians(1)
    } catch (error: any) {
      console.error("Failed to create technician:", error)
      const body = error?.body
      const message =
        (typeof body?.message === "string" ? body.message : null) ||
        error?.message ||
        "لطفاً دوباره تلاش کنید"
      const code = typeof body?.error === "string" ? body.error : null
      const description = code === "EMAIL_EXISTS"
        ? "این ایمیل قبلاً ثبت شده است. ایمیل دیگری وارد کنید."
        : code === "PHONE_EXISTS"
          ? "این شماره تلفن قبلاً ثبت شده است."
          : message
      toast({
        title: "خطا در ایجاد تکنسین",
        description,
        variant: "destructive",
        duration: 6000,
      })
    } finally {
      setCreating(false)
    }
  }

  const handleUpdate = async () => {
    if (!user || !selectedTechnician) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد سیستم شوید",
        variant: "destructive",
      })
      return
    }

    // Prevent double-clicks
    if (saving) {
      return
    }
    
    // Validate required fields
    if (!formData.fullName || !formData.email) {
      toast({
        title: "خطا",
        description: "نام کامل و ایمیل الزامی هستند",
        variant: "destructive",
      })
      return
    }

    setSaving(true)
    try {
      console.log("[TechnicianManagement] Updating technician:", {
        id: selectedTechnician.id,
        formData,
      })

      await updateTechnician(token, selectedTechnician.id, {
        fullName: formData.fullName,
        email: formData.email,
        phone: formData.phone || null,
        department: formData.department || null,
        isActive: formData.isActive, // Include isActive status
        isSupervisor: formData.isSupervisor, // Include isSupervisor status
        subcategoryIds: formData.subcategoryIds.length > 0 ? formData.subcategoryIds : null,
      })

      toast({
        title: "تکنسین به‌روزرسانی شد",
        description: `اطلاعات ${formData.fullName} با موفقیت به‌روزرسانی شد`,
      })
      setEditDialogOpen(false)
      setSelectedTechnician(null)
      resetForm()
      await loadTechnicians()
    } catch (error: any) {
      console.error("[TechnicianManagement] Failed to update technician:", error)
      
      const errorMessage = error?.message || error?.body?.message || "لطفاً دوباره تلاش کنید"
      toast({
        title: "خطا در به‌روزرسانی تکنسین",
        description: errorMessage,
        variant: "destructive",
      })
    } finally {
      setSaving(false)
    }
  }

  const handleToggleStatus = async (technician: ApiTechnicianResponse) => {
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد سیستم شوید",
        variant: "destructive",
      })
      return
    }

    // Prevent double-clicks
    if (updatingStatus === technician.id) {
      return
    }

    setUpdatingStatus(technician.id)
    const newStatus = !technician.isActive

    try {
      console.log("[TechnicianManagement] Updating status:", {
        technicianId: technician.id,
        currentStatus: technician.isActive,
        newStatus: newStatus,
      })

      await updateTechnicianStatus(token, technician.id, newStatus)

      toast({
        title: newStatus ? "تکنسین فعال شد" : "تکنسین غیرفعال شد",
        description: `${technician.fullName} ${newStatus ? "فعال" : "غیرفعال"} شد`,
      })

      // Reload technicians to get updated status
      await loadTechnicians()
    } catch (error: any) {
      console.error("[TechnicianManagement] Failed to update technician status:", error)
      
      const errorMessage = error?.message || error?.body?.message || "لطفاً دوباره تلاش کنید"
      toast({
        title: "خطا در تغییر وضعیت",
        description: errorMessage,
        variant: "destructive",
      })
    } finally {
      setUpdatingStatus(null)
    }
  }

  const handleDeleteClick = (technician: ApiTechnicianResponse) => {
    setTechnicianToDelete(technician)
    setDeleteDialogOpen(true)
  }

  const handleConfirmDelete = async () => {
    if (!user || !technicianToDelete) {
      return
    }

    setDeleting(true)
    try {
      console.log("[TechnicianManagement] Deleting technician:", {
        technicianId: technicianToDelete.id,
        technicianName: technicianToDelete.fullName,
      })

      await deleteTechnician(token, technicianToDelete.id)

      toast({
        title: "تکنسین حذف شد",
        description: `${technicianToDelete.fullName} با موفقیت از سیستم حذف شد. داده‌های قدیمی برای گزارش‌گیری باقی می‌ماند.`,
      })

      setDeleteDialogOpen(false)
      setTechnicianToDelete(null)
      await loadTechnicians()
    } catch (error: any) {
      console.error("[TechnicianManagement] Failed to delete technician:", error)
      
      const errorMessage = error?.message || error?.body?.message || "لطفاً دوباره تلاش کنید"
      toast({
        title: "خطا در حذف تکنسین",
        description: errorMessage,
        variant: "destructive",
      })
    } finally {
      setDeleting(false)
    }
  }

  const resetForm = () => {
    setFormData({
      fullName: "",
      email: "",
      password: "",
      confirmPassword: "",
      phone: "",
      department: "",
      isActive: true,
      isSupervisor: false,
      subcategoryIds: [],
    })
    setShowPassword(false)
    setShowConfirmPassword(false)
    setSelectedCategoryId(null)
    setSelectedSubcategoryId(null)
  }

  const openEditDialog = (technician: ApiTechnicianResponse) => {
    setSelectedTechnician(technician)
    setFormData({
      fullName: technician.fullName,
      email: technician.email,
      phone: technician.phone || "",
      department: technician.department || "",
      isActive: technician.isActive,
      isSupervisor: technician.isSupervisor || false,
      subcategoryIds: technician.subcategoryIds || [],
    })
    setEditDialogOpen(true)
  }

  const filteredTechnicians = technicians
  const totalPages = Math.max(Math.ceil(totalCount / pageSize), 1)
  const canCreate =
    !!formData.fullName &&
    !!formData.email &&
    !!formData.password &&
    formData.password === formData.confirmPassword

  return (
    <div className="space-y-6" dir="rtl">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <CardTitle className="text-right">مدیریت تکنسین‌ها</CardTitle>
            <Button onClick={() => {
              resetForm()
              setCreateDialogOpen(true)
            }} className="gap-2">
              <Plus className="w-4 h-4" />
              افزودن تکنسین
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div className="mb-4">
            <div className="relative">
              <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 text-gray-400 w-4 h-4" />
              <Input
                placeholder="جستجو بر اساس نام، ایمیل یا بخش..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pr-10 text-right"
                dir="rtl"
              />
            </div>
          </div>

          <div className="border rounded-lg overflow-x-auto min-h-[280px] pb-1">
            <Table className="w-full min-w-[700px]">
              <TableHeader>
                <TableRow>
                  <TableHead className="text-right">نام</TableHead>
                  <TableHead className="text-right">ایمیل</TableHead>
                  <TableHead className="text-right">تلفن</TableHead>
                  <TableHead className="text-right">بخش</TableHead>
                  <TableHead className="text-right">تخصص‌ها</TableHead>
                  <TableHead className="text-right">نقش</TableHead>
                  <TableHead className="text-right whitespace-nowrap">وضعیت</TableHead>
                  <TableHead className="text-right whitespace-nowrap">حساب کاربری</TableHead>
                  <TableHead className="text-right whitespace-nowrap">تاریخ ایجاد</TableHead>
                  <TableHead className="text-right whitespace-nowrap">عملیات</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading && filteredTechnicians.length === 0 ? (
                  Array.from({ length: 4 }).map((_, index) => (
                    <TableRow key={`skeleton-${index}`}>
                      {Array.from({ length: 10 }).map((__, cellIndex) => (
                        <TableCell key={`skeleton-cell-${index}-${cellIndex}`}>
                          <div className="h-4 w-3/4 rounded bg-muted/60" />
                        </TableCell>
                      ))}
                    </TableRow>
                  ))
                ) : filteredTechnicians.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={10} className="text-center py-8 text-muted-foreground">
                      {searchQuery ? "نتیجه‌ای یافت نشد" : "هیچ تکنسینی ثبت نشده است"}
                    </TableCell>
                  </TableRow>
                ) : (
                  filteredTechnicians.map((technician) => (
                    <TableRow key={technician.id}>
                      <TableCell className="font-medium">{technician.fullName}</TableCell>
                      <TableCell>{technician.email}</TableCell>
                      <TableCell>{technician.phone || "--"}</TableCell>
                      <TableCell>{technician.department || "--"}</TableCell>
                      <TableCell>
                        <SpecialtiesCell specialties={getNormalizedSpecialties(technician)} />
                      </TableCell>
                      <TableCell>
                        {technician.isSupervisor ? "سرپرست" : "تکنسین"}
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        <div className="flex flex-col gap-1">
                          <Badge
                            variant={technician.isActive ? "default" : "secondary"}
                            className={technician.isActive ? "" : "bg-gray-500"}
                          >
                            {technician.isActive ? "فعال" : "غیرفعال"}
                          </Badge>
                          {technician.isSupervisor && (
                            <Badge variant="outline" className="text-xs border-blue-500 text-blue-700">
                              سرپرست
                            </Badge>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        {technician.userId ? (
                          <Badge variant="outline" className="text-green-600 border-green-600">
                            <UserCheck className="w-3 h-3 mr-1" />
                            متصل
                          </Badge>
                        ) : (
                          <Badge variant="outline" className="text-amber-600 border-amber-600">
                            <UserX className="w-3 h-3 mr-1" />
                            متصل نیست
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        {toFaDate(technician.createdAt)}
                      </TableCell>
                      <TableCell className="whitespace-nowrap">
                        <div className="flex items-center gap-2 justify-end">
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => openEditDialog(technician)}
                            className="gap-1"
                          >
                            <Edit className="w-4 h-4" />
                            ویرایش
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleToggleStatus(technician)}
                            disabled={updatingStatus === technician.id}
                            className="gap-1"
                          >
                            {updatingStatus === technician.id ? (
                              <>
                                <div className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin" />
                                در حال تغییر...
                              </>
                            ) : technician.isActive ? (
                              <>
                                <UserX className="w-4 h-4" />
                                غیرفعال
                              </>
                            ) : (
                              <>
                                <UserCheck className="w-4 h-4" />
                                فعال
                              </>
                            )}
                          </Button>
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleDeleteClick(technician)}
                            className="gap-1 text-destructive hover:text-destructive hover:bg-destructive/10"
                          >
                            <Trash2 className="w-4 h-4" />
                            حذف
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>
          {!loading && filteredTechnicians.length > 0 && (
            <div className="flex items-center justify-between mt-4 text-sm text-muted-foreground">
              <span>
                صفحه {page} از {totalPages}
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((prev) => Math.max(prev - 1, 1))}
                  disabled={page <= 1}
                >
                  قبلی
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setPage((prev) => Math.min(prev + 1, totalPages))}
                  disabled={page >= totalPages}
                >
                  بعدی
                </Button>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Create Dialog */}
      <Dialog open={createDialogOpen} onOpenChange={setCreateDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle className="text-right">افزودن تکنسین جدید</DialogTitle>
            <DialogDescription className="text-right">
              اطلاعات تکنسین جدید را وارد کنید
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="fullName" className="text-right">نام کامل *</Label>
              <Input
                id="fullName"
                value={formData.fullName}
                onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="نام کامل تکنسین"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email" className="text-right">ایمیل *</Label>
              <Input
                id="email"
                type="email"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="email@example.com"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password" className="text-right">رمز عبور *</Label>
              <div className="flex gap-2">
                <Input
                  id="password"
                  type={showPassword ? "text" : "password"}
                  value={formData.password}
                  onChange={(e) => setFormData({ ...formData, password: e.target.value })}
                  className="text-right"
                  dir="rtl"
                  placeholder="حداقل ۸ کاراکتر"
                />
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowPassword((prev) => !prev)}
                >
                  {showPassword ? "مخفی" : "نمایش"}
                </Button>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="confirmPassword" className="text-right">تکرار رمز عبور *</Label>
              <div className="flex gap-2">
                <Input
                  id="confirmPassword"
                  type={showConfirmPassword ? "text" : "password"}
                  value={formData.confirmPassword}
                  onChange={(e) => setFormData({ ...formData, confirmPassword: e.target.value })}
                  className="text-right"
                  dir="rtl"
                  placeholder="تکرار رمز عبور"
                />
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowConfirmPassword((prev) => !prev)}
                >
                  {showConfirmPassword ? "مخفی" : "نمایش"}
                </Button>
              </div>
              <div className="flex justify-end">
                <Button
                  type="button"
                  variant="ghost"
                  onClick={() => {
                    const generated = generatePassword()
                    setFormData({ ...formData, password: generated, confirmPassword: generated })
                  }}
                >
                  ایجاد رمز عبور قوی
                </Button>
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="phone" className="text-right">تلفن</Label>
              <Input
                id="phone"
                value={formData.phone}
                onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="09123456789"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="department" className="text-right">بخش</Label>
              <Input
                id="department"
                value={formData.department}
                onChange={(e) => setFormData({ ...formData, department: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="بخش تکنسین"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="role" className="text-right">نقش تکنسین</Label>
              <Select
                value={formData.isSupervisor ? "supervisor" : "normal"}
                onValueChange={(value) => setFormData({ ...formData, isSupervisor: value === "supervisor" })}
                dir="rtl"
              >
                <SelectTrigger className="text-right">
                  <SelectValue placeholder="انتخاب نقش" />
                </SelectTrigger>
                <SelectContent className="font-iran">
                  <SelectItem value="normal">عادی</SelectItem>
                  <SelectItem value="supervisor">سرپرست</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="flex items-center gap-2">
              <Switch
                id="isActive"
                checked={formData.isActive}
                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
              />
              <Label htmlFor="isActive" className="text-right">فعال</Label>
            </div>

            {/* Expertise Selection */}
            <div className="space-y-4 border-t pt-4">
              <Label className="text-right font-semibold">تخصص‌ها (اختیاری)</Label>
              <div className="space-y-3">
                <div className="flex gap-2">
                  <Select
                    value={selectedCategoryId?.toString() || ""}
                    onValueChange={(value) => setSelectedCategoryId(value ? parseInt(value) : null)}
                    dir="rtl"
                  >
                    <SelectTrigger className="flex-1 text-right">
                      <SelectValue placeholder="انتخاب دسته" />
                    </SelectTrigger>
                    <SelectContent className="font-iran">
                      {categories.map((cat) => (
                        <SelectItem key={cat.id} value={cat.id.toString()}>
                          {cat.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Select
                    value={selectedSubcategoryId?.toString() || ""}
                    onValueChange={(value) => setSelectedSubcategoryId(value ? parseInt(value) : null)}
                    disabled={!selectedCategoryId || subcategories.length === 0}
                    dir="rtl"
                  >
                    <SelectTrigger className="flex-1 text-right">
                      <SelectValue placeholder="انتخاب زیر دسته" />
                    </SelectTrigger>
                    <SelectContent className="font-iran">
                      {subcategories.map((sub) => (
                        <SelectItem key={sub.id} value={sub.id.toString()}>
                          {sub.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Button type="button" onClick={addExpertise} disabled={!selectedSubcategoryId}>
                    <Plus className="w-4 h-4 ml-2" />
                    افزودن
                  </Button>
                </div>
                {formData.subcategoryIds.length > 0 && (
                  <div className="flex flex-wrap gap-2 p-3 border rounded-lg bg-muted/50">
                    {formData.subcategoryIds.map((subId) => (
                      <Badge key={subId} variant="secondary" className="gap-1">
                        {getSubcategoryName(subId)}
                        <button
                          type="button"
                          onClick={() => removeExpertise(subId)}
                          className="ml-1 hover:text-destructive"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </Badge>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
          </div>
          <div className="flex justify-end gap-2 pt-4 shrink-0 border-t pt-4">
            <Button variant="outline" onClick={() => {
              setCreateDialogOpen(false)
              resetForm()
            }}>
              انصراف
            </Button>
            <Button
              onClick={handleCreate}
              disabled={!canCreate || creating}
            >
              {creating ? "در حال ایجاد..." : "ایجاد تکنسین"}
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Edit Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={setEditDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl flex flex-col" dir="rtl">
          <DialogHeader className="shrink-0">
            <DialogTitle className="text-right">ویرایش تکنسین</DialogTitle>
            <DialogDescription className="text-right">
              اطلاعات تکنسین را ویرایش کنید
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto min-h-0 pr-1">
          <div className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="edit-fullName" className="text-right">نام کامل *</Label>
              <Input
                id="edit-fullName"
                value={formData.fullName}
                onChange={(e) => setFormData({ ...formData, fullName: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="نام کامل تکنسین"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-email" className="text-right">ایمیل *</Label>
              <Input
                id="edit-email"
                type="email"
                value={formData.email}
                onChange={(e) => setFormData({ ...formData, email: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="email@example.com"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-phone" className="text-right">تلفن</Label>
              <Input
                id="edit-phone"
                value={formData.phone}
                onChange={(e) => setFormData({ ...formData, phone: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="09123456789"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-department" className="text-right">بخش</Label>
              <Input
                id="edit-department"
                value={formData.department}
                onChange={(e) => setFormData({ ...formData, department: e.target.value })}
                className="text-right"
                dir="rtl"
                placeholder="بخش تکنسین"
              />
            </div>
            <div className="flex items-center gap-2">
              <Switch
                id="edit-isActive"
                checked={formData.isActive}
                onCheckedChange={(checked) => setFormData({ ...formData, isActive: checked })}
              />
              <Label htmlFor="edit-isActive" className="text-right">فعال</Label>
            </div>

            {/* Expertise Selection */}
            <div className="space-y-4 border-t pt-4">
              <Label className="text-right font-semibold">تخصص‌ها (اختیاری)</Label>
              <div className="space-y-3">
                <div className="flex gap-2">
                  <Select
                    value={selectedCategoryId?.toString() || ""}
                    onValueChange={(value) => setSelectedCategoryId(value ? parseInt(value) : null)}
                    dir="rtl"
                  >
                    <SelectTrigger className="flex-1 text-right">
                      <SelectValue placeholder="انتخاب دسته" />
                    </SelectTrigger>
                    <SelectContent className="font-iran">
                      {categories.map((cat) => (
                        <SelectItem key={cat.id} value={cat.id.toString()}>
                          {cat.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Select
                    value={selectedSubcategoryId?.toString() || ""}
                    onValueChange={(value) => setSelectedSubcategoryId(value ? parseInt(value) : null)}
                    disabled={!selectedCategoryId || subcategories.length === 0}
                    dir="rtl"
                  >
                    <SelectTrigger className="flex-1 text-right">
                      <SelectValue placeholder="انتخاب زیر دسته" />
                    </SelectTrigger>
                    <SelectContent className="font-iran">
                      {subcategories.map((sub) => (
                        <SelectItem key={sub.id} value={sub.id.toString()}>
                          {sub.name}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <Button type="button" onClick={addExpertise} disabled={!selectedSubcategoryId}>
                    <Plus className="w-4 h-4 ml-2" />
                    افزودن
                  </Button>
                </div>
                {formData.subcategoryIds.length > 0 && (
                  <div className="flex flex-wrap gap-2 p-3 border rounded-lg bg-muted/50">
                    {formData.subcategoryIds.map((subId) => (
                      <Badge key={subId} variant="secondary" className="gap-1">
                        {getSubcategoryName(subId)}
                        <button
                          type="button"
                          onClick={() => removeExpertise(subId)}
                          className="ml-1 hover:text-destructive"
                        >
                          <X className="w-3 h-3" />
                        </button>
                      </Badge>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
          </div>
          <div className="flex justify-end gap-2 pt-4 shrink-0 border-t pt-4">
            <Button variant="outline" onClick={() => {
              setEditDialogOpen(false)
              setSelectedTechnician(null)
              resetForm()
            }}>
              انصراف
            </Button>
            <Button
              onClick={handleUpdate}
              disabled={saving || !formData.fullName || !formData.email}
            >
              {saving ? (
                <>
                  <div className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin ml-2" />
                  در حال ذخیره...
                </>
              ) : (
                "ذخیره تغییرات"
              )}
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <Dialog open={deleteDialogOpen} onOpenChange={setDeleteDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-md" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right text-destructive flex items-center gap-2">
              <Trash2 className="w-5 h-5" />
              تأیید حذف تکنسین
            </DialogTitle>
            <DialogDescription className="text-right">
              <div className="space-y-4 mt-4">
                <p>
                  آیا از حذف <strong>{technicianToDelete?.fullName}</strong> مطمئن هستید؟
                </p>
                <div className="bg-amber-50 dark:bg-amber-950 border border-amber-200 dark:border-amber-800 rounded-lg p-4 space-y-2 text-sm">
                  <p className="font-semibold text-amber-800 dark:text-amber-200">توجه:</p>
                  <ul className="list-disc list-inside space-y-1 text-amber-700 dark:text-amber-300">
                    <li>این تکنسین دیگر قادر به ورود به سیستم نخواهد بود</li>
                    <li>تکنسین از لیست انتساب تیکت‌ها حذف می‌شود</li>
                    <li>داده‌های قدیمی و تاریخچه تیکت‌ها حفظ می‌شود</li>
                    <li>این عملیات قابل بازگشت است (با تماس با پشتیبانی)</li>
                  </ul>
                </div>
              </div>
            </DialogDescription>
          </DialogHeader>
          <div className="flex justify-end gap-2 pt-4">
            <Button
              variant="outline"
              onClick={() => {
                setDeleteDialogOpen(false)
                setTechnicianToDelete(null)
              }}
              disabled={deleting}
            >
              انصراف
            </Button>
            <Button
              variant="destructive"
              onClick={handleConfirmDelete}
              disabled={deleting}
            >
              {deleting ? (
                <>
                  <div className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin ml-2" />
                  در حال حذف...
                </>
              ) : (
                <>
                  <Trash2 className="w-4 h-4 ml-2" />
                  حذف تکنسین
                </>
              )}
            </Button>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  )
}

function generatePassword(length = 12) {
  const letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
  const numbers = "0123456789"
  const symbols = "!@#$%^&*"
  const all = letters + numbers + symbols
  const pick = (chars: string) => chars[Math.floor(Math.random() * chars.length)]
  const required = [pick(letters), pick(numbers), pick(symbols)]
  const remaining = Array.from({ length: Math.max(length - required.length, 0) }, () => pick(all))
  const password = [...required, ...remaining].sort(() => Math.random() - 0.5).join("")
  return password
}
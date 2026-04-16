"use client"

import type React from "react"

import { useState, useEffect, useMemo } from "react"
import { useForm, Controller } from "react-hook-form"
import { yupResolver } from "@hookform/resolvers/yup"
import * as yup from "yup"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Separator } from "@/components/ui/separator"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Switch } from "@/components/ui/switch"
import { toast } from "@/hooks/use-toast"
import { useAuth } from "@/lib/auth-context"
import { usePreferences } from "@/lib/preferences-context"
import { getSystemSettings, updateSystemSettings } from "@/lib/settings-api"
import { getMyNotificationPreferences, updateMyNotificationPreferences } from "@/lib/notification-preferences-api"
import type { ApiSystemSettingsResponse, ApiNotificationPreferencesResponse, ApiUserPreferencesResponse } from "@/lib/api-types"
import {
  User,
  Upload,
  Camera,
  X,
  Settings,
  Bell,
  Shield,
  Palette,
  Globe,
  Monitor,
  Moon,
  Sun,
  Languages,
} from "lucide-react"

const systemSettingsSchema = yup.object({
  appName: yup.string().required("نام سامانه الزامی است"),
  supportEmail: yup.string().email("ایمیل معتبر وارد کنید").required("ایمیل پشتیبانی الزامی است"),
  supportPhone: yup.string().optional(),
  defaultLanguage: yup.string().oneOf(["fa"]).default("fa").required(),
  defaultTheme: yup.string().oneOf(["light", "dark", "system"]).required(),
  timezone: yup.string().required(),
  defaultPriority: yup.string().required(),
  defaultStatus: yup.string().required(),
  responseSlaHours: yup.number().min(1).max(168).required(),
  autoAssignEnabled: yup.boolean().required(),
  allowClientAttachments: yup.boolean().required(),
  maxAttachmentSizeMB: yup.number().min(1).max(100).required(),
  emailNotificationsEnabled: yup.boolean().required(),
  smsNotificationsEnabled: yup.boolean().required(),
  notifyOnTicketCreated: yup.boolean().required(),
  notifyOnTicketAssigned: yup.boolean().required(),
  notifyOnTicketReplied: yup.boolean().required(),
  notifyOnTicketClosed: yup.boolean().required(),
  passwordMinLength: yup.number().min(4).max(32).required(),
  require2FA: yup.boolean().required(),
  sessionTimeoutMinutes: yup.number().min(5).max(1440).required(),
  allowedEmailDomains: yup.array().of(yup.string()).default([]),
})

interface SettingsDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

interface SystemSettings {
  notifications: {
    email: boolean
    push: boolean
    sms: boolean
    desktop: boolean
  }
  appearance: {
    theme: "light" | "dark" | "system"
    language: "fa" | "en"
    fontSize: "small" | "medium" | "large"
  }
  privacy: {
    profileVisibility: boolean
    activityStatus: boolean
    readReceipts: boolean
  }
  system: {
    autoSave: boolean
    soundEffects: boolean
    animations: boolean
    compactMode: boolean
  }
}

export function SettingsDialog({ open, onOpenChange }: SettingsDialogProps) {
  const { user, updateProfile, isLoading, token } = useAuth()
  const { preferences, updatePreferences, isLoading: preferencesLoading } = usePreferences()
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null)
  const [isUploadingAvatar, setIsUploadingAvatar] = useState(false)
  const [systemSettingsLoading, setSystemSettingsLoading] = useState(false)
  const [systemSettingsData, setSystemSettingsData] = useState<ApiSystemSettingsResponse | null>(null)
  const [appearanceSaving, setAppearanceSaving] = useState(false)
  const [notificationPreferences, setNotificationPreferences] = useState<ApiNotificationPreferencesResponse | null>(null)
  const [notificationPreferencesLoading, setNotificationPreferencesLoading] = useState(false)
  const [notificationPreferencesSaving, setNotificationPreferencesSaving] = useState(false)
  const isAdmin = user?.role === "admin"

  const [systemSettings, setSystemSettings] = useState<SystemSettings>({
    notifications: {
      email: true,
      push: true,
      sms: false,
      desktop: true,
    },
    appearance: {
      theme: "system",
      language: "fa",
      fontSize: "medium",
    },
    privacy: {
      profileVisibility: true,
      activityStatus: true,
      readReceipts: true,
    },
    system: {
      autoSave: true,
      soundEffects: true,
      animations: true,
      compactMode: false,
    },
  })

  const systemSettingsForm = useForm<ApiSystemSettingsResponse>({
    resolver: yupResolver(systemSettingsSchema),
    defaultValues: {
      appName: "",
      supportEmail: "",
      supportPhone: "",
      defaultLanguage: "fa",
      defaultTheme: "system",
      timezone: "Asia/Tehran",
      defaultPriority: "Medium",
      defaultStatus: "New",
      responseSlaHours: 24,
      autoAssignEnabled: false,
      allowClientAttachments: true,
      maxAttachmentSizeMB: 10,
      emailNotificationsEnabled: true,
      smsNotificationsEnabled: false,
      notifyOnTicketCreated: true,
      notifyOnTicketAssigned: true,
      notifyOnTicketReplied: true,
      notifyOnTicketClosed: true,
      passwordMinLength: 6,
      require2FA: false,
      sessionTimeoutMinutes: 60,
      allowedEmailDomains: [],
    },
  })

  // Fetch system settings when dialog opens (admin only)
  useEffect(() => {
    if (open && isAdmin && token) {
      setSystemSettingsLoading(true)
      getSystemSettings(token)
        .then((data) => {
          const normalized = { ...data, defaultLanguage: "fa" as const }
          setSystemSettingsData(normalized)
          systemSettingsForm.reset(normalized)
        })
        .catch((error: any) => {
          console.error("Failed to load system settings:", error)
          const status = error?.status || 0
          const errorMessage = error?.message || "خطای نامشخص"
          
          if (status === 403 || status === 401 || errorMessage?.includes("403") || errorMessage?.includes("401")) {
            toast({
              title: "دسترسی محدود",
              description: "فقط مدیران می‌توانند تنظیمات سیستم را مشاهده کنند",
              variant: "destructive",
            })
          } else {
            toast({
              title: "خطا در بارگذاری تنظیمات",
              description: errorMessage || "لطفاً دوباره تلاش کنید",
              variant: "destructive",
            })
          }
        })
        .finally(() => {
          setSystemSettingsLoading(false)
        })
    } else if (open && !isAdmin) {
      // Clear form if not admin
      setSystemSettingsData(null)
      systemSettingsForm.reset()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, isAdmin, token])

  // Fetch notification preferences when dialog opens
  useEffect(() => {
    if (open && token) {
      console.log("[Notifications] Fetching notification preferences...")
      setNotificationPreferencesLoading(true)
      getMyNotificationPreferences(token)
        .then((data) => {
          console.log("[Notifications] Received preferences:", data)
          setNotificationPreferences(data)
          // Update local state for UI
          setSystemSettings((prev) => ({
            ...prev,
            notifications: {
              email: data.emailEnabled,
              push: data.pushEnabled,
              sms: data.smsEnabled,
              desktop: data.desktopEnabled,
            },
          }))
        })
        .catch((error: any) => {
          console.error("[Notifications] Failed to load preferences:", error)
          const status = error?.status || 0
          const errorMessage = error?.message || "خطای نامشخص"
          
          // Use defaults on error
          const defaults: ApiNotificationPreferencesResponse = {
            emailEnabled: true,
            pushEnabled: true,
            smsEnabled: false,
            desktopEnabled: true,
          }
          console.log("[Notifications] Using defaults due to error:", defaults)
          setNotificationPreferences(defaults)
          setSystemSettings((prev) => ({
            ...prev,
            notifications: {
              email: defaults.emailEnabled,
              push: defaults.pushEnabled,
              sms: defaults.smsEnabled,
              desktop: defaults.desktopEnabled,
            },
          }))
          
          if (status !== 401 && status !== 403) {
            // Don't show error for auth issues (user might not be logged in)
            toast({
              title: "خطا در بارگذاری تنظیمات اعلان‌ها",
              description: errorMessage || "از مقادیر پیش‌فرض استفاده می‌شود",
              variant: "destructive",
            })
          }
        })
        .finally(() => {
          setNotificationPreferencesLoading(false)
        })
    } else if (!open) {
      // Clear state when dialog closes
      setNotificationPreferences(null)
    }
  }, [open, token])

  const onSystemSettingsSubmit = async (data: ApiSystemSettingsResponse) => {
    const payload = { ...data, defaultLanguage: "fa" as const }
    console.log("Submitting system settings:", payload)

    if (!user || !isAdmin) {
      toast({
        title: "دسترسی محدود",
        description: "فقط مدیران می‌توانند تنظیمات سیستم را تغییر دهند",
        variant: "destructive",
      })
      return
    }

    setSystemSettingsLoading(true)
    try {
      console.log("Calling updateSystemSettings API...")
      const updated = await updateSystemSettings(token, payload)
      console.log("Settings updated successfully:", updated)
      setSystemSettingsData(updated)
      systemSettingsForm.reset(updated)
      toast({
        title: "تنظیمات سیستم ذخیره شد",
        description: "تغییرات با موفقیت اعمال شد",
      })
    } catch (error: any) {
      console.error("Failed to update system settings:", error)
      const status = error?.status || 0
      const errorBody = error?.body || {}
      let errorMessage = error?.message || "خطای نامشخص"
      
      // Try to extract detailed error message from validation errors
      if (errorBody.errors && typeof errorBody.errors === "object") {
        const errors = errorBody.errors as Record<string, unknown>
        const firstErrorKey = Object.keys(errors)[0]
        const firstError = errors[firstErrorKey]
        if (Array.isArray(firstError) && firstError.length > 0) {
          errorMessage = String(firstError[0])
        }
      }
      
      if (status === 403 || status === 401 || errorMessage?.includes("403") || errorMessage?.includes("401")) {
        toast({
          title: "دسترسی محدود",
          description: "فقط مدیران می‌توانند تنظیمات سیستم را تغییر دهند",
          variant: "destructive",
        })
      } else if (status === 400) {
        // Validation error
        toast({
          title: "خطا در اعتبارسنجی",
          description: errorMessage || "لطفاً مقادیر را بررسی کنید",
          variant: "destructive",
        })
      } else {
        toast({
          title: "خطا در ذخیره تنظیمات",
          description: errorMessage || "لطفاً دوباره تلاش کنید",
          variant: "destructive",
        })
      }
    } finally {
      setSystemSettingsLoading(false)
    }
  }

  const handleSystemSettingsReset = () => {
    if (systemSettingsData) {
      systemSettingsForm.reset(systemSettingsData)
      toast({
        title: "تنظیمات بازنشانی شد",
        description: "همه تغییرات لغو شد",
      })
    }
  }

  const handleNotificationToggle = (setting: "email" | "push" | "sms" | "desktop") => {
    // If preferences haven't loaded yet, initialize with defaults
    const currentPrefs = notificationPreferences || {
      emailEnabled: true,
      pushEnabled: true,
      smsEnabled: false,
      desktopEnabled: true,
    }

    const updated = {
      ...currentPrefs,
      emailEnabled: setting === "email" ? !currentPrefs.emailEnabled : currentPrefs.emailEnabled,
      pushEnabled: setting === "push" ? !currentPrefs.pushEnabled : currentPrefs.pushEnabled,
      smsEnabled: setting === "sms" ? !currentPrefs.smsEnabled : currentPrefs.smsEnabled,
      desktopEnabled: setting === "desktop" ? !currentPrefs.desktopEnabled : currentPrefs.desktopEnabled,
    }

    console.log("[Notifications] Toggle changed:", setting, "New state:", updated)
    setNotificationPreferences(updated)
    setSystemSettings((prev) => ({
      ...prev,
      notifications: {
        email: updated.emailEnabled,
        push: updated.pushEnabled,
        sms: updated.smsEnabled,
        desktop: updated.desktopEnabled,
      },
    }))
  }

  const handleNotificationSave = async () => {
    if (!user) {
      toast({
        title: "خطا",
        description: "لطفاً ابتدا وارد شوید",
        variant: "destructive",
      })
      return
    }

    // Use current preferences or defaults
    const prefsToSave = notificationPreferences || {
      emailEnabled: true,
      pushEnabled: true,
      smsEnabled: false,
      desktopEnabled: true,
    }

    console.log("[Notifications] Saving preferences:", prefsToSave)
    setNotificationPreferencesSaving(true)
    try {
      const updated = await updateMyNotificationPreferences(token, prefsToSave)
      console.log("[Notifications] Save successful, received:", updated)
      setNotificationPreferences(updated)
      setSystemSettings((prev) => ({
        ...prev,
        notifications: {
          email: updated.emailEnabled,
          push: updated.pushEnabled,
          sms: updated.smsEnabled,
          desktop: updated.desktopEnabled,
        },
      }))
      toast({
        title: "تنظیمات اعلان‌ها ذخیره شد",
        description: "تغییرات با موفقیت اعمال شد",
      })
    } catch (error: any) {
      console.error("[Notifications] Failed to save:", error)
      const status = error?.status || 0
      const errorBody = error?.body || {}
      let errorMessage = error?.message || "خطای نامشخص"
      
      // Try to extract detailed error message
      if (errorBody.errors && typeof errorBody.errors === "object") {
        const errors = errorBody.errors as Record<string, unknown>
        const firstErrorKey = Object.keys(errors)[0]
        const firstError = errors[firstErrorKey]
        if (Array.isArray(firstError) && firstError.length > 0) {
          errorMessage = String(firstError[0])
        }
      } else if (errorBody.message && typeof errorBody.message === "string") {
        errorMessage = errorBody.message
      }
      
      toast({
        title: "خطا در ذخیره تنظیمات",
        description: errorMessage || "لطفاً دوباره تلاش کنید",
        variant: "destructive",
      })
      
      // Revert to last saved state by refetching
      try {
        const current = await getMyNotificationPreferences(token)
        setNotificationPreferences(current)
        setSystemSettings((prev) => ({
          ...prev,
          notifications: {
            email: current.emailEnabled,
            push: current.pushEnabled,
            sms: current.smsEnabled,
            desktop: current.desktopEnabled,
          },
        }))
      } catch (reloadError) {
        console.error("[Notifications] Failed to reload after error:", reloadError)
        // Keep current state if reload fails
      }
    } finally {
      setNotificationPreferencesSaving(false)
    }
  }

  const toggleSetting = (category: keyof SystemSettings, setting: string, value?: any) => {
    // This is only used for the old systemSettings that aren't connected to backend
    // Keep it for backward compatibility but notifications should use handleNotificationToggle
    setSystemSettings((prev) => ({
      ...prev,
      [category]: {
        ...prev[category],
        [setting]: value !== undefined ? value : !prev[category][setting as keyof (typeof prev)[typeof category]],
      },
    }))
  }

  const ToggleButton = ({
    active,
    onToggle,
    label,
    description,
  }: {
    active: boolean
    onToggle: () => void
    label: string
    description?: string
  }) => (
    <div className="flex items-center justify-between py-3 px-1" dir="rtl">
      {/* Button on LEFT in RTL */}
      <Button
        type="button"
        variant={active ? "default" : "outline"}
        size="sm"
        onClick={onToggle}
        className="w-16 h-8 flex-shrink-0 ml-4"
      >
        {active ? "فعال" : "غیرفعال"}
      </Button>
      {/* Text content on RIGHT in RTL */}
      <div className="flex-1 text-right">
        <div className="text-sm font-medium">{label}</div>
        {description && <div className="text-xs text-muted-foreground mt-1">{description}</div>}
      </div>
    </div>
  )

  const defaultAppearancePreferences: ApiUserPreferencesResponse = useMemo(
    () => ({
      theme: "system",
      fontSize: "md",
      language: "fa",
      direction: "rtl",
      timezone: "Asia/Tehran",
      notifications: {
        emailEnabled: true,
        pushEnabled: true,
        smsEnabled: false,
        desktopEnabled: true,
      },
    }),
    []
  )

  const handleAppearanceChange = async (field: "theme" | "fontSize" | "language", value: string) => {
    const base = preferences ?? defaultAppearancePreferences
    const updated: ApiUserPreferencesResponse = {
      ...base,
      [field]: value,
    }

    setAppearanceSaving(true)
    try {
      const success = await updatePreferences(updated)
      if (success) {
        toast({
          title: "تنظیمات ظاهری به‌روزرسانی شد",
          description: "تغییرات شما ذخیره شد",
        })
      } else {
        toast({
          title: "خطا در ذخیره تنظیمات",
          description: "لطفاً دوباره تلاش کنید",
          variant: "destructive",
        })
      }
    } finally {
      setAppearanceSaving(false)
    }
  }

  const ThemeSelector = () => {
    const currentTheme = preferences?.theme ?? defaultAppearancePreferences.theme
    return (
      <div className="space-y-3" dir="rtl">
        <Label className="text-sm font-medium text-right block">تم ظاهری</Label>
        <div className="grid grid-cols-3 gap-2" dir="rtl">
          {[
            { value: "light", label: "روشن", icon: Sun },
            { value: "dark", label: "تیره", icon: Moon },
            { value: "system", label: "سیستم", icon: Monitor },
          ].map(({ value, label, icon: Icon }) => (
            <Button
              key={value}
              type="button"
              variant={currentTheme === value ? "default" : "outline"}
              size="sm"
              onClick={() => handleAppearanceChange("theme", value)}
              disabled={appearanceSaving}
              className="h-12 flex-col gap-1"
            >
              <Icon className="w-4 h-4" />
              <span className="text-xs">{label}</span>
            </Button>
          ))}
        </div>
      </div>
    )
  }

  const FontSizeSelector = () => {
    const currentFontSize = preferences?.fontSize ?? defaultAppearancePreferences.fontSize
    return (
      <div className="space-y-3" dir="rtl">
        <Label className="text-sm font-medium text-right block">اندازه فونت</Label>
        <div className="grid grid-cols-3 gap-2" dir="rtl">
          {[
            { value: "sm", label: "کوچک" },
            { value: "md", label: "متوسط" },
            { value: "lg", label: "بزرگ" },
          ].map(({ value, label }) => (
            <Button
              key={value}
              type="button"
              variant={currentFontSize === value ? "default" : "outline"}
              size="sm"
              onClick={() => handleAppearanceChange("fontSize", value)}
              disabled={appearanceSaving}
              className="h-10"
            >
              {label}
            </Button>
          ))}
        </div>
      </div>
    )
  }

  const handleAvatarUpload = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    if (!file) return
    const allowedTypes = ["image/jpeg", "image/jpg", "image/png", "image/gif"]
    if (!allowedTypes.includes(file.type)) {
      toast({ title: "فرمت فایل نامعتبر", description: "لطفاً فایل JPG، PNG یا GIF انتخاب کنید", variant: "destructive" })
      return
    }
    const maxSize = 5 * 1024 * 1024
    if (file.size > maxSize) {
      toast({ title: "حجم فایل زیاد است", description: "حداکثر حجم مجاز ۵ مگابایت است", variant: "destructive" })
      return
    }
    setIsUploadingAvatar(true)
    try {
      const reader = new FileReader()
      reader.onload = async (e) => {
        const result = e.target?.result as string
        setAvatarPreview(result)
        await new Promise((r) => setTimeout(r, 500))
        const success = await updateProfile({ avatar: result })
        if (success) toast({ title: "تصویر پروفایل به‌روزرسانی شد", description: "تصویر جدید شما با موفقیت ذخیره شد" })
        else throw new Error("Upload failed")
      }
      reader.readAsDataURL(file)
    } catch {
      toast({ title: "خطا در آپلود تصویر", description: "لطفاً دوباره تلاش کنید", variant: "destructive" })
      setAvatarPreview(null)
    } finally {
      setIsUploadingAvatar(false)
    }
    event.target.value = ""
  }

  const handleRemoveAvatar = async () => {
    try {
      setIsUploadingAvatar(true)
      const success = await updateProfile({ avatar: null })
      if (success) {
        setAvatarPreview(null)
        toast({ title: "تصویر پروفایل حذف شد", description: "تصویر پروفایل شما با موفقیت حذف شد" })
      }
    } catch {
      toast({ title: "خطا در حذف تصویر", description: "لطفاً دوباره تلاش کنید", variant: "destructive" })
    } finally {
      setIsUploadingAvatar(false)
    }
  }

  const triggerFileInput = () => document.getElementById("avatar-upload")?.click()

  if (!user) return null

  const currentAvatar = avatarPreview ?? user.avatar

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-4xl text-right" dir="rtl">
        <DialogHeader className="flex flex-col items-end space-y-1.5 text-right sm:text-right pr-10" dir="rtl">
          <DialogTitle className="text-right flex items-center gap-2 justify-end text-lg font-semibold" dir="rtl">
            <Settings className="w-5 h-5 shrink-0" />
            <span>تنظیمات سیستم</span>
          </DialogTitle>
        </DialogHeader>

        <div className="overflow-y-auto max-h-[calc(90vh-120px)]" dir="rtl">
          <Tabs defaultValue="general" className="w-full" dir="rtl">
            {/* Tabs navigation - RTL order: rightmost item is first in RTL */}
            <TabsList className="grid w-full grid-cols-1 mb-6" dir="rtl">
              <TabsTrigger value="general" className="gap-2 text-xs flex-row-reverse" dir="rtl">
                <User className="w-4 h-4" />
                تنظیمات عمومی
              </TabsTrigger>
            </TabsList>

            <TabsContent value="general" className="space-y-4 w-full">
              <Card dir="rtl" className="w-full">
                <CardHeader className="text-right">
                  <CardTitle className="text-right">اطلاعات شخصی</CardTitle>
                  <CardDescription className="text-right">مشاهده اطلاعات پروفایل (فقط نمایش، غیرقابل ویرایش)</CardDescription>
                </CardHeader>
                <CardContent className="space-y-6" dir="rtl">
                  <div className="flex items-center gap-4 justify-end" dir="rtl">
                    <div className="space-y-2 text-right">
                      <Label className="text-right">تصویر پروفایل</Label>
                      <p className="text-xs text-muted-foreground text-right">فقط تصویر پروفایل قابل تغییر است</p>
                      <div className="flex gap-2 justify-end">
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={triggerFileInput}
                          disabled={isUploadingAvatar}
                        >
                          {isUploadingAvatar ? (
                            <>
                              <div className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin ml-2" />
                              در حال آپلود...
                            </>
                          ) : (
                            <>
                              <Upload className="w-4 h-4 ml-2" />
                              تغییر تصویر
                            </>
                          )}
                        </Button>
                        {currentAvatar && (
                          <Button
                            type="button"
                            variant="outline"
                            size="sm"
                            onClick={handleRemoveAvatar}
                            disabled={isUploadingAvatar}
                            className="text-red-600 hover:text-red-700 bg-transparent"
                          >
                            <X className="w-4 h-4 ml-2" />
                            حذف تصویر
                          </Button>
                        )}
                        <input
                          id="avatar-upload"
                          type="file"
                          accept="image/jpeg,image/jpg,image/png,image/gif"
                          className="hidden"
                          onChange={handleAvatarUpload}
                        />
                      </div>
                      <p className="text-xs text-muted-foreground text-right">JPG، PNG، GIF (حداکثر ۵ مگابایت)</p>
                    </div>
                    <div className="relative">
                      <Avatar className="h-20 w-20">
                        <AvatarImage src={currentAvatar || "/placeholder.svg"} alt={user.name} />
                        <AvatarFallback className="text-lg">{user.name.charAt(0)}</AvatarFallback>
                      </Avatar>
                      {isUploadingAvatar && (
                        <div className="absolute inset-0 bg-black/50 rounded-full flex items-center justify-center">
                          <div className="w-6 h-6 border-2 border-white border-t-transparent rounded-full animate-spin" />
                        </div>
                      )}
                      <Button
                        type="button"
                        variant="secondary"
                        size="sm"
                        className="absolute -bottom-2 -right-2 h-8 w-8 rounded-full p-0"
                        onClick={triggerFileInput}
                        disabled={isUploadingAvatar}
                      >
                        <Camera className="w-4 h-4" />
                      </Button>
                    </div>
                  </div>

                  <div className="space-y-4" dir="rtl">
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2 text-right">
                        <Label className="text-right text-muted-foreground">نام و نام خانوادگی</Label>
                        <div className="flex h-10 w-full items-center rounded-md border border-input bg-muted/40 px-3 py-2 text-sm text-foreground" dir="rtl">
                          {user?.name ?? "—"}
                        </div>
                      </div>
                      <div className="space-y-2 text-right">
                        <Label className="text-right text-muted-foreground">ایمیل</Label>
                        <div className="flex h-10 w-full items-center rounded-md border border-input bg-muted/40 px-3 py-2 text-sm text-foreground" dir="rtl">
                          {user?.email ?? "—"}
                        </div>
                      </div>
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-2 text-right">
                        <Label className="text-right text-muted-foreground">شماره تماس</Label>
                        <div className="flex h-10 w-full items-center rounded-md border border-input bg-muted/40 px-3 py-2 text-sm text-foreground" dir="rtl">
                          {user?.phone ?? "—"}
                        </div>
                      </div>
                      <div className="space-y-2 text-right">
                        <Label className="text-right text-muted-foreground">بخش</Label>
                        <div className="flex h-10 w-full items-center rounded-md border border-input bg-muted/40 px-3 py-2 text-sm text-foreground" dir="rtl">
                          {user?.department ?? "—"}
                        </div>
                      </div>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* تنظیمات ظاهری: directly below اطلاعات شخصی, RTL */}
              <Card dir="rtl" className="w-full">
                <CardHeader className="w-full text-right space-y-2" dir="rtl">
                  <CardTitle className="text-right flex items-center gap-2 justify-end" dir="rtl">
                    <Palette className="w-5 h-5" />
                    تنظیمات ظاهری
                  </CardTitle>
                  <CardDescription className="text-right" dir="rtl">
                    ظاهر و نمایش سیستم را شخصی‌سازی کنید
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-6 text-right" dir="rtl">
                  {preferencesLoading && !preferences ? (
                    <div className="py-6 text-center">
                      <div className="w-8 h-8 border-2 border-current border-t-transparent rounded-full animate-spin mx-auto mb-3" />
                      <p className="text-sm text-muted-foreground">در حال بارگذاری تنظیمات...</p>
                    </div>
                  ) : (
                    <>
                      <ThemeSelector />
                      <Separator />
                      <FontSizeSelector />
                      <Separator />
                      <div className="space-y-3" dir="rtl">
                        <Label className="text-sm font-medium text-right block">زبان سیستم</Label>
                        <div className="flex items-center gap-2">
                          <div className="inline-flex h-10 items-center gap-2 rounded-md border border-input bg-primary/10 px-4 text-sm font-medium text-primary">
                            <Languages className="w-4 h-4" />
                            فارسی
                          </div>
                        </div>
                      </div>
                      {appearanceSaving && (
                        <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
                          <div className="w-4 h-4 border-2 border-current border-t-transparent rounded-full animate-spin" />
                          در حال ذخیره...
                        </div>
                      )}
                    </>
                  )}
                </CardContent>
              </Card>
            </TabsContent>
            {/* Removed: ticketing-defaults tab (تنظیمات پیش‌فرض تیکتینگ) */}
          </Tabs>
        </div>
      </DialogContent>
    </Dialog>
  )
}

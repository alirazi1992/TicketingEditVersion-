"use client"

import { useState, useEffect } from "react"
import { Controller } from "react-hook-form"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { FileUpload } from "@/components/file-upload"
import { DynamicFieldRenderer } from "@/components/dynamic-field-renderer"
import type { FormFieldDef } from "@/lib/dynamic-forms"
import { getEffectiveFieldDefinitions, type FieldDefinitionResponse } from "@/lib/field-definitions-api"
import { useAuth } from "@/lib/auth-context"
import {
  FileText,
  Paperclip,
  AlertCircle,
  Settings,
  HardDrive,
  Monitor,
  Printer,
  Wifi,
  Shield,
  Key,
  GraduationCap,
  Wrench,
} from "lucide-react"
import type { UploadedFile } from "@/lib/file-upload"

interface TicketFormStep2Props {
  control: any
  errors: any
  selectedIssue: string
  selectedSubIssue: string
  categoriesData?: any
  attachedFiles: UploadedFile[]
  onFilesChange: (files: UploadedFile[]) => void
}

export function TicketFormStep2({
  control,
  errors,
  selectedIssue,
  selectedSubIssue,
  categoriesData,
  attachedFiles,
  onFilesChange,
}: TicketFormStep2Props) {
  const { token, user } = useAuth()
  const [backendFields, setBackendFields] = useState<FieldDefinitionResponse[]>([])
  const [loadingFields, setLoadingFields] = useState(false)

  // Fetch field definitions from backend when category/subcategory changes
  useEffect(() => {
    const fetchFields = async () => {
      if (!user || !selectedIssue || !selectedSubIssue || !categoriesData) {
        setBackendFields([])
        return
      }

      const cat = categoriesData[selectedIssue]
      const sub = cat?.subIssues?.[selectedSubIssue]
      const categoryBackendId = cat?.backendId
      const subcategoryBackendId = sub?.backendId

      if (!categoryBackendId || typeof categoryBackendId !== "number") {
        setBackendFields([])
        return
      }

      setLoadingFields(true)
      try {
        const fields = await getEffectiveFieldDefinitions(token, categoryBackendId, subcategoryBackendId)
        setBackendFields(fields)
      } catch (error) {
        console.error("[TicketFormStep2] Error fetching fields:", error)
        setBackendFields([])
      } finally {
        setLoadingFields(false)
      }
    }

    fetchFields()
  }, [token, selectedIssue, selectedSubIssue, categoriesData])

  // Map backend fields to FormFieldDef format
  const mapBackendFieldsToFormDefs = (): FormFieldDef[] => {
    return backendFields.map((field) => {
      // Map backend FieldType to frontend FieldType
      let frontendType: FormFieldDef["type"] = "text"
      const backendType = field.type.toLowerCase()
      if (backendType === "multiselect") {
        frontendType = "multiselect"
      } else if (backendType === "select") {
        frontendType = "select"
      } else if (backendType === "textarea") {
        frontendType = "textarea"
      } else if (backendType === "number") {
        frontendType = "number"
      } else if (backendType === "email") {
        frontendType = "email"
      } else if (backendType === "phone") {
        frontendType = "tel"
      } else if (backendType === "date") {
        frontendType = "date"
      } else if (backendType === "boolean") {
        frontendType = "checkbox"
      } else {
        frontendType = "text"
      }
      
      return {
        id: String(field.id), // Use backend id for dyn_{id} mapping
        label: field.label,
        type: frontendType,
        required: field.isRequired,
        placeholder: field.defaultValue || undefined,
        options: field.options || [],
        helpText: undefined,
      }
    })
  }

  const dynamicDefs = mapBackendFieldsToFormDefs()
  const renderError = (name: string) => {
    const message = errors?.[name]?.message
    return message ? <p className="text-sm text-red-500 text-right">{message}</p> : null
  }
  const renderDynamicFields = () => {
    if (selectedIssue === "hardware") {
      if (selectedSubIssue === "computer-not-working") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <HardDrive className="w-4 h-4" />
                اطلاعات تکمیلی رایانه (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="deviceBrand" className="text-right">
                    برند رایانه
                  </Label>
                  <Controller
                    name="deviceBrand"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب برند" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="dell">Dell</SelectItem>
                          <SelectItem value="hp">HP</SelectItem>
                          <SelectItem value="lenovo">Lenovo</SelectItem>
                          <SelectItem value="asus">ASUS</SelectItem>
                          <SelectItem value="acer">Acer</SelectItem>
                          <SelectItem value="apple">Apple</SelectItem>
                          <SelectItem value="other">سایر</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("deviceBrand")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="deviceModel" className="text-right">
                    مدل رایانه
                  </Label>
                  <Controller
                    name="deviceModel"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="مثال: OptiPlex 7090" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("deviceModel")}
                </div>
              </div>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="powerStatus" className="text-right">
                    وضعیت روشن شدن
                  </Label>
                  <Controller
                    name="powerStatus"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب وضعیت" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="no-power">هیچ چراغی روشن نمی‌شود</SelectItem>
                          <SelectItem value="power-but-no-display">چراغ روشن می‌شود اما صفحه سیاه است</SelectItem>
                          <SelectItem value="starts-then-shuts">روشن می‌شود اما خاموش می‌شود</SelectItem>
                          <SelectItem value="strange-sounds">صدای عجیب می‌دهد</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("powerStatus")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="lastWorking" className="text-right">
                    آخرین بار کی کار می‌کرد؟
                  </Label>
                  <Controller
                    name="lastWorking"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب زمان" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="today">امروز صبح</SelectItem>
                          <SelectItem value="yesterday">دیروز</SelectItem>
                          <SelectItem value="few-days">چند روز پیش</SelectItem>
                          <SelectItem value="week">یک هفته پیش</SelectItem>
                          <SelectItem value="longer">بیشتر از یک هفته</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("lastWorking")}
                </div>
              </div>
            </CardContent>
          </Card>
        )
      }

      if (selectedSubIssue === "printer-issues") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Printer className="w-4 h-4" />
                اطلاعات تکمیلی چاپگر (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="printerBrand" className="text-right">
                    برند چاپگر
                  </Label>
                  <Controller
                    name="printerBrand"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب برند" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="hp">HP</SelectItem>
                          <SelectItem value="canon">Canon</SelectItem>
                          <SelectItem value="epson">Epson</SelectItem>
                          <SelectItem value="brother">Brother</SelectItem>
                          <SelectItem value="samsung">Samsung</SelectItem>
                          <SelectItem value="other">سایر</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("printerBrand")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="printerType" className="text-right">
                    نوع چاپگر
                  </Label>
                  <Controller
                    name="printerType"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="نوع چاپگر" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="inkjet">جوهرافشان</SelectItem>
                          <SelectItem value="laser">لیزری</SelectItem>
                          <SelectItem value="multifunction">چندکاره</SelectItem>
                          <SelectItem value="dot-matrix">سوزنی</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("printerType")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="printerProblem" className="text-right">
                  مشکل دقیق چاپگر
                </Label>
                <Controller
                  name="printerProblem"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="no-print">اصلاً چاپ نمی‌کند</SelectItem>
                        <SelectItem value="poor-quality">کیفیت چاپ ضعیف</SelectItem>
                        <SelectItem value="paper-jam">کاغذ گیر می‌کند</SelectItem>
                        <SelectItem value="ink-problem">مشکل جوهر یا تونر</SelectItem>
                        <SelectItem value="connection-issue">مشکل اتصال</SelectItem>
                        <SelectItem value="error-message">پیام خطا می‌دهد</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("printerProblem")}
              </div>
            </CardContent>
          </Card>
        )
      }

      if (selectedSubIssue === "monitor-problems") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Monitor className="w-4 h-4" />
                اطلاعات تکمیلی مانیتور (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="monitorSize" className="text-right">
                    سایز مانیتور
                  </Label>
                  <Controller
                    name="monitorSize"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب سایز" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="19">19 اینچ</SelectItem>
                          <SelectItem value="21">21 اینچ</SelectItem>
                          <SelectItem value="24">24 اینچ</SelectItem>
                          <SelectItem value="27">27 اینچ</SelectItem>
                          <SelectItem value="32">32 اینچ</SelectItem>
                          <SelectItem value="other">سایر</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("monitorSize")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="connectionType" className="text-right">
                    نوع اتصال
                  </Label>
                  <Controller
                    name="connectionType"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="نوع کابل" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="hdmi">HDMI</SelectItem>
                          <SelectItem value="vga">VGA</SelectItem>
                          <SelectItem value="dvi">DVI</SelectItem>
                          <SelectItem value="displayport">DisplayPort</SelectItem>
                          <SelectItem value="usb-c">USB-C</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("connectionType")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="displayIssue" className="text-right">
                  مشکل نمایش
                </Label>
                <Controller
                  name="displayIssue"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="no-display">هیچ تصویری نمایش نمی‌دهد</SelectItem>
                        <SelectItem value="flickering">تصویر چشمک می‌زند</SelectItem>
                        <SelectItem value="color-problem">مشکل رنگ</SelectItem>
                        <SelectItem value="resolution-issue">مشکل وضوح تصویر</SelectItem>
                        <SelectItem value="lines-spots">خط یا لکه روی صفحه</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("displayIssue")}
              </div>
            </CardContent>
          </Card>
        )
      }

      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Settings className="w-4 h-4" />
              اطلاعات تکمیلی سخت‌افزار (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="deviceBrand" className="text-right">
                  برند دستگاه
                </Label>
                <Controller
                  name="deviceBrand"
                  control={control}
                  render={({ field }) => (
                    <Input {...field} placeholder="مثال: HP, Dell, ..." className="text-right" dir="rtl" />
                  )}
                />
                {renderError("deviceBrand")}
              </div>
              <div className="space-y-2">
                <Label htmlFor="deviceModel" className="text-right">
                  مدل دستگاه
                </Label>
                <Controller
                  name="deviceModel"
                  control={control}
                  render={({ field }) => (
                    <Input {...field} placeholder="مدل دقیق دستگاه" className="text-right" dir="rtl" />
                  )}
                />
                {renderError("deviceModel")}
              </div>
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "software") {
      if (selectedSubIssue === "os-issues") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Settings className="w-4 h-4" />
                اطلاعات تکمیلی سیستم عامل (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="operatingSystem" className="text-right">
                    سیستم عامل
                  </Label>
                  <Controller
                    name="operatingSystem"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب سیستم عامل" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="windows-11">Windows 11</SelectItem>
                          <SelectItem value="windows-10">Windows 10</SelectItem>
                          <SelectItem value="windows-8">Windows 8</SelectItem>
                          <SelectItem value="macos">macOS</SelectItem>
                          <SelectItem value="linux">Linux</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("operatingSystem")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="osVersion" className="text-right">
                    نسخه سیستم عامل
                  </Label>
                  <Controller
                    name="osVersion"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="مثال: 22H2, Big Sur" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("osVersion")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="osIssueType" className="text-right">
                  نوع مشکل سیستم عامل
                </Label>
                <Controller
                  name="osIssueType"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="boot-problem">مشکل راه‌اندازی</SelectItem>
                        <SelectItem value="slow-performance">عملکرد کند</SelectItem>
                        <SelectItem value="frequent-crashes">خرابی مکرر</SelectItem>
                        <SelectItem value="update-issues">مشکل به‌روزرسانی</SelectItem>
                        <SelectItem value="driver-problems">مشکل درایور</SelectItem>
                        <SelectItem value="blue-screen">صفحه آبی مرگ</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("osIssueType")}
              </div>
            </CardContent>
          </Card>
        )
      }

      if (selectedSubIssue === "application-problems") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Settings className="w-4 h-4" />
                اطلاعات تکمیلی نرم‌افزار (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="softwareName" className="text-right">
                    نام نرم‌افزار
                  </Label>
                  <Controller
                    name="softwareName"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="مثال: Microsoft Office" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("softwareName")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="softwareVersion" className="text-right">
                    نسخه نرم‌افزار
                  </Label>
                  <Controller
                    name="softwareVersion"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="مثال: 2021, v3.5" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("softwareVersion")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="applicationIssue" className="text-right">
                  مشکل نرم‌افزار
                </Label>
                <Controller
                  name="applicationIssue"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="wont-start">اجرا نمی‌شود</SelectItem>
                        <SelectItem value="crashes-frequently">مکرراً خراب می‌شود</SelectItem>
                        <SelectItem value="feature-not-working">قابلیت خاص کار نمی‌کند</SelectItem>
                        <SelectItem value="slow-performance">عملکرد کند</SelectItem>
                        <SelectItem value="error-messages">پیام خطا می‌دهد</SelectItem>
                        <SelectItem value="file-corruption">فایل‌ها خراب می‌شوند</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("applicationIssue")}
              </div>
            </CardContent>
          </Card>
        )
      }

      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Settings className="w-4 h-4" />
              اطلاعات تکمیلی نرم‌افزار (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="softwareName" className="text-right">
                نام نرم‌افزار
              </Label>
              <Controller
                name="softwareName"
                control={control}
                render={({ field }) => (
                  <Input {...field} placeholder="نام نرم‌افزار مورد نظر" className="text-right" dir="rtl" />
                )}
              />
              {renderError("softwareName")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "network") {
      if (selectedSubIssue === "internet-connection") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Wifi className="w-4 h-4" />
                اطلاعات تکمیلی اتصال اینترنت (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="connectionType" className="text-right">
                    نوع اتصال
                  </Label>
                  <Controller
                    name="connectionType"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="نوع اتصال" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="ethernet">اترنت (کابلی)</SelectItem>
                          <SelectItem value="wifi">Wi-Fi (بی‌سیم)</SelectItem>
                          <SelectItem value="mobile">اینترنت موبایل</SelectItem>
                          <SelectItem value="adsl">ADSL</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("connectionType")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="internetProvider" className="text-right">
                    ارائه‌دهنده اینترنت
                  </Label>
                  <Controller
                    name="internetProvider"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="مثال: ایرانسل، شاتل" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("internetProvider")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="connectionIssue" className="text-right">
                  مشکل اتصال
                </Label>
                <Controller
                  name="connectionIssue"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="no-connection">اصلاً اتصال ندارم</SelectItem>
                        <SelectItem value="intermittent">گاهی قطع می‌شود</SelectItem>
                        <SelectItem value="very-slow">خیلی کند است</SelectItem>
                        <SelectItem value="limited-access">دسترسی محدود</SelectItem>
                        <SelectItem value="specific-sites">سایت‌های خاص باز نمی‌شود</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("connectionIssue")}
              </div>
            </CardContent>
          </Card>
        )
      }

      if (selectedSubIssue === "wifi-problems") {
        return (
          <Card>
            <CardHeader>
              <CardTitle className="text-sm text-right flex items-center gap-2">
                <Wifi className="w-4 h-4" />
                اطلاعات تکمیلی Wi-Fi (الزامی)
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="wifiNetwork" className="text-right">
                    نام شبکه Wi-Fi
                  </Label>
                  <Controller
                    name="wifiNetwork"
                    control={control}
                    render={({ field }) => (
                      <Input {...field} placeholder="نام شبکه (SSID)" className="text-right" dir="rtl" />
                    )}
                  />
                  {renderError("wifiNetwork")}
                </div>
                <div className="space-y-2">
                  <Label htmlFor="deviceType" className="text-right">
                    نوع دستگاه
                  </Label>
                  <Controller
                    name="deviceType"
                    control={control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="نوع دستگاه" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="laptop">لپ‌تاپ</SelectItem>
                          <SelectItem value="desktop">رایانه رومیزی</SelectItem>
                          <SelectItem value="mobile">موبایل</SelectItem>
                          <SelectItem value="tablet">تبلت</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("deviceType")}
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="wifiIssue" className="text-right">
                  مشکل Wi-Fi
                </Label>
                <Controller
                  name="wifiIssue"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="انتخاب مشکل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="cant-see-network">شبکه را نمی‌بینم</SelectItem>
                        <SelectItem value="cant-connect">نمی‌توانم وصل شوم</SelectItem>
                        <SelectItem value="weak-signal">سیگنال ضعیف</SelectItem>
                        <SelectItem value="keeps-disconnecting">مدام قطع می‌شود</SelectItem>
                        <SelectItem value="wrong-password">رمز عبور اشتباه است</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("wifiIssue")}
              </div>
            </CardContent>
          </Card>
        )
      }

      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Wifi className="w-4 h-4" />
              اطلاعات تکمیلی شبکه (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="networkLocation" className="text-right">
                محل شبکه
              </Label>
              <Controller
                name="networkLocation"
                control={control}
                render={({ field }) => (
                  <Input {...field} placeholder="مثال: طبقه دوم، اتاق 205" className="text-right" dir="rtl" />
                )}
              />
              {renderError("networkLocation")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "email") {
      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Settings className="w-4 h-4" />
              اطلاعات تکمیلی ایمیل (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="emailProvider" className="text-right">
                  ارائه‌دهنده ایمیل
                </Label>
                <Controller
                  name="emailProvider"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="ارائه‌دهنده ایمیل" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="gmail">Gmail</SelectItem>
                        <SelectItem value="outlook">Outlook</SelectItem>
                        <SelectItem value="yahoo">Yahoo</SelectItem>
                        <SelectItem value="company">ایمیل شرکت</SelectItem>
                        <SelectItem value="other">سایر</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("emailProvider")}
              </div>
              <div className="space-y-2">
                <Label htmlFor="emailClient" className="text-right">
                  نرم‌افزار ایمیل
                </Label>
                <Controller
                  name="emailClient"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                        <SelectValue placeholder="نرم‌افزار" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="outlook-app">Outlook</SelectItem>
                        <SelectItem value="thunderbird">Thunderbird</SelectItem>
                        <SelectItem value="web-browser">مرورگر وب</SelectItem>
                          <SelectItem value="mobile-app">اپ موبایل</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {renderError("emailClient")}
              </div>
            </div>
            <div className="space-y-2">
              <Label htmlFor="errorMessage" className="text-right">
                پیام خطا (در صورت وجود)
              </Label>
              <Controller
                name="errorMessage"
                control={control}
                render={({ field }) => (
                  <Textarea
                    {...field}
                    placeholder="متن کامل پیام خطا را اینجا بنویسید"
                    rows={2}
                    className="text-right"
                    dir="rtl"
                  />
                )}
              />
              {renderError("errorMessage")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "security") {
      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Shield className="w-4 h-4 text-red-500" />
              اطلاعات تکمیلی امنیتی (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="incidentTime" className="text-right">
                زمان تقریبی حادثه
              </Label>
                <Controller
                  name="incidentTime"
                  control={control}
                  render={({ field }) => (
                    <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                      <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب زمان" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="just-now">همین الان</SelectItem>
                      <SelectItem value="today">امروز</SelectItem>
                      <SelectItem value="yesterday">دیروز</SelectItem>
                      <SelectItem value="this-week">این هفته</SelectItem>
                      <SelectItem value="not-sure">مطمئن نیستم</SelectItem>
                      </SelectContent>
                    </Select>
                  )}
                />
                {renderError("incidentTime")}
              </div>
              <div className="space-y-2">
                <Label htmlFor="securitySeverity" className="text-right">
                  شدت مشکل
                </Label>
              <Controller
                name="securitySeverity"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب شدت" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="low">کم - فقط مشکوک است</SelectItem>
                      <SelectItem value="medium">متوسط - احتمال مشکل</SelectItem>
                      <SelectItem value="high">بالا - مشکل جدی</SelectItem>
                      <SelectItem value="critical">بحرانی - فوری</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("securitySeverity")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="affectedData" className="text-right">
                اطلاعات تحت تأثیر
              </Label>
              <Controller
                name="affectedData"
                control={control}
                render={({ field }) => (
                  <Textarea
                    {...field}
                    placeholder="چه اطلاعاتی ممکن است تحت تأثیر قرار گرفته باشد؟"
                    rows={2}
                    className="text-right"
                    dir="rtl"
                  />
                )}
              />
              {renderError("affectedData")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "access") {
      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Key className="w-4 h-4" />
              اطلاعات تکمیلی دسترسی (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="requestedSystem" className="text-right">
                سیستم یا نرم‌افزار مورد نظر
              </Label>
              <Controller
                name="requestedSystem"
                control={control}
                render={({ field }) => (
                  <Input {...field} placeholder="نام سیستم یا نرم‌افزار" className="text-right" dir="rtl" />
                )}
              />
              {renderError("requestedSystem")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="accessLevel" className="text-right">
                سطح دسترسی مورد نیاز
              </Label>
              <Controller
                name="accessLevel"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب سطح" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="read-only">فقط خواندن</SelectItem>
                      <SelectItem value="read-write">خواندن و نوشتن</SelectItem>
                      <SelectItem value="admin">مدیریت</SelectItem>
                      <SelectItem value="full">دسترسی کامل</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("accessLevel")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="accessReason" className="text-right">
                دلیل درخواست
              </Label>
              <Controller
                name="accessReason"
                control={control}
                render={({ field }) => (
                  <Textarea
                    {...field}
                    placeholder="چرا به این دسترسی نیاز دارید؟"
                    rows={2}
                    className="text-right"
                    dir="rtl"
                  />
                )}
              />
              {renderError("accessReason")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "training") {
      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <GraduationCap className="w-4 h-4" />
              اطلاعات تکمیلی آموزش (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="trainingTopic" className="text-right">
                موضوع آموزش
              </Label>
              <Controller
                name="trainingTopic"
                control={control}
                render={({ field }) => (
                  <Input {...field} placeholder="چه چیزی می‌خواهید یاد بگیرید؟" className="text-right" dir="rtl" />
                )}
              />
              {renderError("trainingTopic")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="currentLevel" className="text-right">
                سطح فعلی شما
              </Label>
              <Controller
                name="currentLevel"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب سطح" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="beginner">مبتدی</SelectItem>
                      <SelectItem value="intermediate">متوسط</SelectItem>
                      <SelectItem value="advanced">پیشرفته</SelectItem>
                      <SelectItem value="expert">حرفه‌ای</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("currentLevel")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="preferredMethod" className="text-right">
                روش آموزش ترجیحی
              </Label>
              <Controller
                name="preferredMethod"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب روش" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="in-person">حضوری</SelectItem>
                      <SelectItem value="online">آنلاین</SelectItem>
                      <SelectItem value="documentation">مستندات</SelectItem>
                      <SelectItem value="video">ویدیو</SelectItem>
                      <SelectItem value="hands-on">عملی</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("preferredMethod")}
            </div>
          </CardContent>
        </Card>
      )
    }

    if (selectedIssue === "maintenance") {
      return (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Wrench className="w-4 h-4" />
              اطلاعات تکمیلی نگهداری (الزامی)
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="equipmentType" className="text-right">
                نوع تجهیزات
              </Label>
              <Controller
                name="equipmentType"
                control={control}
                render={({ field }) => (
                  <Input {...field} placeholder="مثال: رایانه، چاپگر، سرور" className="text-right" dir="rtl" />
                )}
              />
              {renderError("equipmentType")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="maintenanceType" className="text-right">
                نوع نگهداری
              </Label>
              <Controller
                name="maintenanceType"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب نوع" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="preventive">پیشگیرانه</SelectItem>
                      <SelectItem value="corrective">اصلاحی</SelectItem>
                      <SelectItem value="emergency">اضطراری</SelectItem>
                      <SelectItem value="scheduled">برنامه‌ریزی شده</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("maintenanceType")}
            </div>
            <div className="space-y-2">
              <Label htmlFor="preferredTime" className="text-right">
                زمان ترجیحی
              </Label>
              <Controller
                name="preferredTime"
                control={control}
                render={({ field }) => (
                  <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                    <SelectTrigger className="text-right">
                      <SelectValue placeholder="انتخاب زمان" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="morning">صبح</SelectItem>
                      <SelectItem value="afternoon">بعدازظهر</SelectItem>
                      <SelectItem value="evening">عصر</SelectItem>
                      <SelectItem value="weekend">آخر هفته</SelectItem>
                      <SelectItem value="after-hours">خارج از ساعت کاری</SelectItem>
                    </SelectContent>
                  </Select>
                )}
              />
              {renderError("preferredTime")}
            </div>
          </CardContent>
        </Card>
      )
    }

    return null
  }

  return (
    <div className="space-y-6" dir="rtl">
      {/* Title and Description */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-right">
            <FileText className="w-5 h-5" />
            عنوان و شرح مشکل
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="title" className="text-right">
              عنوان تیکت *
            </Label>
            <Controller
              name="title"
              control={control}
              render={({ field }) => (
                <Input {...field} placeholder="عنوان کوتاه و واضح از مشکل" className="text-right" dir="rtl" />
              )}
            />
            {errors.title && <p className="text-sm text-red-500 text-right">{errors.title.message}</p>}
          </div>

          <div className="space-y-2">
            <Label htmlFor="description" className="text-right">
              شرح کامل مشکل (اختیاری)
            </Label>
            <Controller
              name="description"
              control={control}
              render={({ field }) => (
                <Textarea
                  {...field}
                  placeholder="لطفاً مشکل خود را به تفصیل شرح دهید. چه اتفاقی افتاده؟ چه زمانی شروع شده؟ چه کاری انجام داده‌اید؟"
                  rows={6}
                  className="text-right"
                  dir="rtl"
                />
              )}
            />
            {errors.description && <p className="text-sm text-red-500 text-right">{errors.description.message}</p>}
          </div>

          <div className="bg-amber-50 border border-amber-200 rounded-lg p-4">
            <div className="flex items-start gap-2">
              <AlertCircle className="w-4 h-4 text-amber-600 mt-0.5 flex-shrink-0" />
              <div className="text-sm text-amber-800 text-right">
                <p className="font-medium mb-1">نکات مهم برای شرح مشکل:</p>
                <ul className="list-disc list-inside space-y-1 text-right">
                  <li>زمان دقیق بروز مشکل را ذکر کنید</li>
                  <li>پیام‌های خطا را دقیقاً بنویسید</li>
                  <li>اقداماتی که انجام داده‌اید را شرح دهید</li>
                  <li>تأثیر مشکل بر کار شما را بیان کنید</li>
                </ul>
              </div>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Dynamic Admin-Defined Fields */}
      {dynamicDefs.length > 0 ? (
        <Card>
          <CardHeader>
            <CardTitle className="text-sm text-right flex items-center gap-2">
              <Settings className="w-4 h-4" />
              اطلاعات تکمیلی برای این مورد
            </CardTitle>
          </CardHeader>
          <CardContent>
            {loadingFields ? (
              <div className="text-center py-4 text-muted-foreground">در حال بارگذاری فیلدها...</div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {dynamicDefs.map((f, index) => {
                  return (
                    <DynamicFieldRenderer 
                      key={`${f.id}-${index}`} 
                      field={{...f, id: f.id}} 
                      control={control} 
                      errors={errors} 
                    />
                  )
                })}
              </div>
            )}
          </CardContent>
        </Card>
      ) : null}

      {/* File Upload */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-right">
            <Paperclip className="w-5 h-5" />
            پیوست فایل (الزامی)
          </CardTitle>
        </CardHeader>
        <CardContent>
          <FileUpload onFilesChange={onFilesChange} maxFiles={5} />
          <div className="mt-4 bg-green-50 border border-green-200 rounded-lg p-4">
            <p className="text-sm text-green-800 text-right">
              <strong>توصیه:</strong> برای تسریع در حل مشکل، فایل‌های زیر را پیوست کنید:
            </p>
            <ul className="list-disc list-inside mt-2 text-sm text-green-700 text-right space-y-1">
              <li>تصاویر از صفحه خطا یا مشکل</li>
              <li>فایل‌های لاگ مربوطه</li>
              <li>اسکرین‌شات از تنظیمات</li>
              <li>مستندات مرتبط</li>
            </ul>
          </div>
        </CardContent>
      </Card>
    </div>
  )
}



"use client"

import { Controller } from "react-hook-form"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"

interface DynamicFormFieldsProps {
  category: string
  control: any
  errors: any
}

export function DynamicFormFields({ category, control, errors }: DynamicFormFieldsProps) {
  const renderHardwareFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="deviceType">نوع دستگاه *</Label>
        <Controller
          name="deviceType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="انتخاب نوع دستگاه" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="desktop">رایانه رومیزی</SelectItem>
                <SelectItem value="laptop">لپ‌تاپ</SelectItem>
                <SelectItem value="printer">چاپگر</SelectItem>
                <SelectItem value="monitor">مانیتور</SelectItem>
                <SelectItem value="keyboard">کیبورد</SelectItem>
                <SelectItem value="mouse">ماوس</SelectItem>
                <SelectItem value="other">سایر</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.deviceType && <p className="text-sm text-red-500">{errors.deviceType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="deviceModel">مدل دستگاه *</Label>
        <Controller
          name="deviceModel"
          control={control}
          render={({ field }) => <Input {...field} placeholder="مدل دستگاه" />}
        />
        {errors.deviceModel && <p className="text-sm text-red-500">{errors.deviceModel.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="serialNumber">شماره سریال</Label>
        <Controller
          name="serialNumber"
          control={control}
          render={({ field }) => <Input {...field} placeholder="شماره سریال (اختیاری)" />}
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor="warrantyStatus">وضعیت گارانتی *</Label>
        <Controller
          name="warrantyStatus"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="وضعیت گارانتی" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="under-warranty">تحت گارانتی</SelectItem>
                <SelectItem value="expired">گارانتی منقضی</SelectItem>
                <SelectItem value="unknown">نامشخص</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.warrantyStatus && <p className="text-sm text-red-500">{errors.warrantyStatus.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="problemType">نوع مشکل *</Label>
        <Controller
          name="problemType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع مشکل" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="not-turning-on">روشن نمی‌شود</SelectItem>
                <SelectItem value="slow-performance">عملکرد کند</SelectItem>
                <SelectItem value="hardware-failure">خرابی سخت‌افزار</SelectItem>
                <SelectItem value="connectivity">مشکل اتصال</SelectItem>
                <SelectItem value="other">سایر</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.problemType && <p className="text-sm text-red-500">{errors.problemType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="lastWorkingDate">آخرین زمان کارکرد صحیح</Label>
        <Controller name="lastWorkingDate" control={control} render={({ field }) => <Input {...field} type="date" />} />
      </div>
    </div>
  )

  const renderSoftwareFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="softwareName">نام نرم‌افزار *</Label>
        <Controller
          name="softwareName"
          control={control}
          render={({ field }) => <Input {...field} placeholder="نام نرم‌افزار" />}
        />
        {errors.softwareName && <p className="text-sm text-red-500">{errors.softwareName.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="softwareVersion">نسخه نرم‌افزار</Label>
        <Controller
          name="softwareVersion"
          control={control}
          render={({ field }) => <Input {...field} placeholder="نسخه (اختیاری)" />}
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor="operatingSystem">سیستم عامل *</Label>
        <Controller
          name="operatingSystem"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="انتخاب سیستم عامل" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="windows-11">Windows 11</SelectItem>
                <SelectItem value="windows-10">Windows 10</SelectItem>
                <SelectItem value="macos">macOS</SelectItem>
                <SelectItem value="linux">Linux</SelectItem>
                <SelectItem value="other">سایر</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.operatingSystem && <p className="text-sm text-red-500">{errors.operatingSystem.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="installationType">نوع نصب *</Label>
        <Controller
          name="installationType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع نصب" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="new-install">نصب جدید</SelectItem>
                <SelectItem value="update">به‌روزرسانی</SelectItem>
                <SelectItem value="reinstall">نصب مجدد</SelectItem>
                <SelectItem value="repair">تعمیر</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.installationType && <p className="text-sm text-red-500">{errors.installationType.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="errorMessage">پیام خطا</Label>
        <Controller
          name="errorMessage"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="متن کامل پیام خطا (اختیاری)" rows={3} />}
        />
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="licenseInfo">اطلاعات لایسنس</Label>
        <Controller
          name="licenseInfo"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="اطلاعات لایسنس یا کلید محصول (اختیاری)" rows={2} />}
        />
      </div>
    </div>
  )

  const renderNetworkFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="connectionType">نوع اتصال *</Label>
        <Controller
          name="connectionType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع اتصال" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="ethernet">اترنت</SelectItem>
                <SelectItem value="wifi">Wi-Fi</SelectItem>
                <SelectItem value="vpn">VPN</SelectItem>
                <SelectItem value="mobile">موبایل</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.connectionType && <p className="text-sm text-red-500">{errors.connectionType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="networkLocation">محل شبکه *</Label>
        <Controller
          name="networkLocation"
          control={control}
          render={({ field }) => <Input {...field} placeholder="محل دقیق (طبقه، اتاق)" />}
        />
        {errors.networkLocation && <p className="text-sm text-red-500">{errors.networkLocation.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="ipAddress">آدرس IP</Label>
        <Controller
          name="ipAddress"
          control={control}
          render={({ field }) => <Input {...field} placeholder="192.168.1.100 (اختیاری)" />}
        />
      </div>

      <div className="space-y-2">
        <Label htmlFor="internetAccess">دسترسی اینترنت *</Label>
        <Controller
          name="internetAccess"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="وضعیت اینترنت" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="no-access">بدون دسترسی</SelectItem>
                <SelectItem value="limited">دسترسی محدود</SelectItem>
                <SelectItem value="slow">اینترنت کند</SelectItem>
                <SelectItem value="intermittent">قطع و وصل</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.internetAccess && <p className="text-sm text-red-500">{errors.internetAccess.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="affectedServices">سرویس‌های تحت تأثیر *</Label>
        <Controller
          name="affectedServices"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="ایمیل، اینترنت، شبکه داخلی، ..." rows={2} />}
        />
        {errors.affectedServices && <p className="text-sm text-red-500">{errors.affectedServices.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="outageStartTime">زمان شروع قطعی</Label>
        <Controller
          name="outageStartTime"
          control={control}
          render={({ field }) => <Input {...field} type="datetime-local" />}
        />
      </div>
    </div>
  )

  const renderEmailFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="emailProvider">ارائه‌دهنده ایمیل *</Label>
        <Controller
          name="emailProvider"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="ارائه‌دهنده ایمیل" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="outlook">Outlook</SelectItem>
                <SelectItem value="gmail">Gmail</SelectItem>
                <SelectItem value="exchange">Exchange Server</SelectItem>
                <SelectItem value="other">سایر</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.emailProvider && <p className="text-sm text-red-500">{errors.emailProvider.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="emailClient">کلاینت ایمیل *</Label>
        <Controller
          name="emailClient"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="کلاینت ایمیل" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="outlook-app">Outlook App</SelectItem>
                <SelectItem value="web-browser">مرورگر وب</SelectItem>
                <SelectItem value="thunderbird">Thunderbird</SelectItem>
                <SelectItem value="mobile-app">اپلیکیشن موبایل</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.emailClient && <p className="text-sm text-red-500">{errors.emailClient.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="problemType">نوع مشکل *</Label>
        <Controller
          name="problemType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع مشکل" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="cannot-send">نمی‌توانم ایمیل ارسال کنم</SelectItem>
                <SelectItem value="cannot-receive">ایمیل دریافت نمی‌کنم</SelectItem>
                <SelectItem value="login-issue">مشکل ورود</SelectItem>
                <SelectItem value="sync-issue">مشکل همگام‌سازی</SelectItem>
                <SelectItem value="attachment-issue">مشکل پیوست</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.problemType && <p className="text-sm text-red-500">{errors.problemType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="lastAccessTime">آخرین دسترسی موفق</Label>
        <Controller
          name="lastAccessTime"
          control={control}
          render={({ field }) => <Input {...field} type="datetime-local" />}
        />
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="errorDetails">جزئیات خطا</Label>
        <Controller
          name="errorDetails"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="پیام خطا یا جزئیات بیشتر (اختیاری)" rows={3} />}
        />
      </div>
    </div>
  )

  const renderSecurityFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="incidentType">نوع حادثه امنیتی *</Label>
        <Controller
          name="incidentType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع حادثه" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="malware">بدافزار</SelectItem>
                <SelectItem value="phishing">فیشینگ</SelectItem>
                <SelectItem value="data-breach">نقض داده</SelectItem>
                <SelectItem value="unauthorized-access">دسترسی غیرمجاز</SelectItem>
                <SelectItem value="suspicious-activity">فعالیت مشکوک</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.incidentType && <p className="text-sm text-red-500">{errors.incidentType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="dataCompromised">وضعیت امنیت داده‌ها *</Label>
        <Controller
          name="dataCompromised"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="وضعیت داده‌ها" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="no-compromise">بدون نقض</SelectItem>
                <SelectItem value="possible-compromise">احتمال نقض</SelectItem>
                <SelectItem value="confirmed-compromise">نقض تأیید شده</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.dataCompromised && <p className="text-sm text-red-500">{errors.dataCompromised.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="affectedSystems">سیستم‌های تحت تأثیر *</Label>
        <Controller
          name="affectedSystems"
          control={control}
          render={({ field }) => (
            <Textarea {...field} placeholder="لیست سیستم‌ها، سرورها یا خدماتی که تحت تأثیر قرار گرفته‌اند" rows={2} />
          )}
        />
        {errors.affectedSystems && <p className="text-sm text-red-500">{errors.affectedSystems.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="immediateActions">اقدامات فوری انجام شده *</Label>
        <Controller
          name="immediateActions"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="اقداماتی که تا کنون انجام داده‌اید" rows={3} />}
        />
        {errors.immediateActions && <p className="text-sm text-red-500">{errors.immediateActions.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="suspiciousActivity">جزئیات فعالیت مشکوک</Label>
        <Controller
          name="suspiciousActivity"
          control={control}
          render={({ field }) => (
            <Textarea {...field} placeholder="توضیح کامل فعالیت مشکوک یا حادثه (اختیاری)" rows={3} />
          )}
        />
      </div>
    </div>
  )

  const renderAccessFields = () => (
    <div className="grid grid-cols-2 gap-4">
      <div className="space-y-2">
        <Label htmlFor="accessType">نوع دسترسی *</Label>
        <Controller
          name="accessType"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="نوع دسترسی" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="new-account">حساب جدید</SelectItem>
                <SelectItem value="permission-change">تغییر مجوز</SelectItem>
                <SelectItem value="system-access">دسترسی سیستم</SelectItem>
                <SelectItem value="network-access">دسترسی شبکه</SelectItem>
                <SelectItem value="application-access">دسترسی اپلیکیشن</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.accessType && <p className="text-sm text-red-500">{errors.accessType.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="targetSystem">سیستم مقصد *</Label>
        <Controller
          name="targetSystem"
          control={control}
          render={({ field }) => <Input {...field} placeholder="نام سیستم یا اپلیکیشن" />}
        />
        {errors.targetSystem && <p className="text-sm text-red-500">{errors.targetSystem.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="managerApproval">تأیید مدیر *</Label>
        <Controller
          name="managerApproval"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="وضعیت تأیید" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="approved">تأیید شده</SelectItem>
                <SelectItem value="pending">در انتظار تأیید</SelectItem>
                <SelectItem value="verbal-approval">تأیید شفاهی</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
        {errors.managerApproval && <p className="text-sm text-red-500">{errors.managerApproval.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="accessDuration">مدت زمان دسترسی</Label>
        <Controller
          name="accessDuration"
          control={control}
          render={({ field }) => (
            <Select onValueChange={field.onChange} value={field.value}>
              <SelectTrigger>
                <SelectValue placeholder="مدت زمان" />
              </SelectTrigger>
              <SelectContent side="bottom" align="start" sideOffset={8} avoidCollisions={false}>
                <SelectItem value="permanent">دائمی</SelectItem>
                <SelectItem value="temporary">موقت</SelectItem>
                <SelectItem value="project-based">بر اساس پروژه</SelectItem>
              </SelectContent>
            </Select>
          )}
        />
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="requestedPermissions">مجوزهای درخواستی *</Label>
        <Controller
          name="requestedPermissions"
          control={control}
          render={({ field }) => <Textarea {...field} placeholder="لیست دقیق مجوزها و دسترسی‌های مورد نیاز" rows={3} />}
        />
        {errors.requestedPermissions && <p className="text-sm text-red-500">{errors.requestedPermissions.message}</p>}
      </div>

      <div className="col-span-2 space-y-2">
        <Label htmlFor="businessJustification">توجیه کسب‌وکار *</Label>
        <Controller
          name="businessJustification"
          control={control}
          render={({ field }) => (
            <Textarea {...field} placeholder="دلیل نیاز به این دسترسی و تأثیر آن بر کار" rows={3} />
          )}
        />
        {errors.businessJustification && <p className="text-sm text-red-500">{errors.businessJustification.message}</p>}
      </div>
    </div>
  )

  switch (category) {
    case "hardware":
      return renderHardwareFields()
    case "software":
      return renderSoftwareFields()
    case "network":
      return renderNetworkFields()
    case "email":
      return renderEmailFields()
    case "security":
      return renderSecurityFields()
    case "access":
      return renderAccessFields()
    default:
      return null
  }
}

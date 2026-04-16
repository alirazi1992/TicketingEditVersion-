"use client"

import { useState } from "react"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Switch } from "@/components/ui/switch"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Slider } from "@/components/ui/slider"
import { Badge } from "@/components/ui/badge"
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { toast } from "@/hooks/use-toast"
import { Settings, Zap, Star, Users, Target, CheckCircle, Info } from "lucide-react"

interface AutoAssignmentRule {
  id: string
  name: string
  enabled: boolean
  priority: "urgent" | "high" | "medium" | "low" | "all"
  category: string
  maxWorkload: number
  minRating: number
  preferSpecialty: boolean
  requireExperience?: number
  maxResponseTime?: number
}

const defaultRules: AutoAssignmentRule[] = [
  {
    id: "urgent-rule",
    name: "تیکت‌های فوری",
    enabled: true,
    priority: "urgent",
    category: "all",
    maxWorkload: 3,
    minRating: 4.5,
    preferSpecialty: true,
    requireExperience: 30,
    maxResponseTime: 1.5,
  },
  {
    id: "high-rule",
    name: "تیکت‌های اولویت بالا",
    enabled: true,
    priority: "high",
    category: "all",
    maxWorkload: 4,
    minRating: 4.0,
    preferSpecialty: true,
    requireExperience: 20,
    maxResponseTime: 2.0,
  },
  {
    id: "specialty-rule",
    name: "تطبیق تخصصی",
    enabled: true,
    priority: "all",
    category: "all",
    maxWorkload: 5,
    minRating: 3.8,
    preferSpecialty: true,
    requireExperience: 15,
    maxResponseTime: 3.0,
  },
  {
    id: "general-rule",
    name: "تیکت‌های عمومی",
    enabled: false,
    priority: "all",
    category: "all",
    maxWorkload: 6,
    minRating: 3.5,
    preferSpecialty: false,
    requireExperience: 10,
    maxResponseTime: 4.0,
  },
]

const priorityLabels: Record<string, string> = {
  urgent: "فوری",
  high: "بالا",
  medium: "متوسط",
  low: "کم",
  all: "همه",
}

const categoryLabels: Record<string, string> = {
  all: "همه دسته‌ها",
  hardware: "سخت‌افزار",
  software: "نرم‌افزار",
  network: "شبکه",
  email: "ایمیل",
  security: "امنیت",
  access: "دسترسی",
}

interface AutoAssignmentSettingsProps {
  onSettingsChange?: (settings: any) => void
}

export function AutoAssignmentSettings({ onSettingsChange }: AutoAssignmentSettingsProps) {
  const [globalEnabled, setGlobalEnabled] = useState(false)
  const [rules, setRules] = useState<AutoAssignmentRule[]>(defaultRules)
  const [selectedRule, setSelectedRule] = useState<AutoAssignmentRule | null>(null)
  const [editDialogOpen, setEditDialogOpen] = useState(false)

  const handleGlobalToggle = (enabled: boolean) => {
    setGlobalEnabled(enabled)
    toast({
      title: enabled ? "تعیین خودکار فعال شد" : "تعیین خودکار غیرفعال شد",
      description: enabled
        ? "تیکت‌های جدید بر اساس قوانین تعریف شده به صورت خودکار واگذار می‌شوند"
        : "تعیین تکنسین فقط به صورت دستی انجام خواهد شد",
    })

    if (onSettingsChange) {
      onSettingsChange({ globalEnabled: enabled, rules })
    }
  }

  const handleRuleToggle = (ruleId: string, enabled: boolean) => {
    const updatedRules = rules.map((rule) => (rule.id === ruleId ? { ...rule, enabled } : rule))
    setRules(updatedRules)

    if (onSettingsChange) {
      onSettingsChange({ globalEnabled, rules: updatedRules })
    }
  }

  const handleRuleEdit = (rule: AutoAssignmentRule) => {
    setSelectedRule({ ...rule })
    setEditDialogOpen(true)
  }

  const handleRuleSave = () => {
    if (selectedRule) {
      const updatedRules = rules.map((rule) => (rule.id === selectedRule.id ? selectedRule : rule))
      setRules(updatedRules)
      setEditDialogOpen(false)
      setSelectedRule(null)

      toast({
        title: "قانون به‌روزرسانی شد",
        description: "تنظیمات تعیین خودکار ذخیره شد",
      })

      if (onSettingsChange) {
        onSettingsChange({ globalEnabled, rules: updatedRules })
      }
    }
  }

  const getActiveRulesCount = () => rules.filter((rule) => rule.enabled).length

  return (
    <div className="space-y-6" dir="rtl">
      <Card>
        <CardHeader>
          <div className="flex justify-between items-center">
            <CardTitle className="flex items-center gap-2">
              <Settings className="w-5 h-5" />
              تنظیمات تعیین خودکار تکنسین
            </CardTitle>
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2">
                <Switch checked={globalEnabled} onCheckedChange={handleGlobalToggle} id="global-auto-assign" />
                <Label htmlFor="global-auto-assign" className="text-sm font-medium">
                  فعال‌سازی کلی
                </Label>
              </div>
              {globalEnabled && (
                <Badge variant="secondary" className="gap-1">
                  <CheckCircle className="w-3 h-3" />
                  {getActiveRulesCount()} قانون فعال
                </Badge>
              )}
            </div>
          </div>
        </CardHeader>
        <CardContent>
          {!globalEnabled ? (
            <div className="text-center py-8">
              <Zap className="w-12 h-12 text-muted-foreground mx-auto mb-4" />
              <h3 className="text-lg font-medium text-muted-foreground mb-2">تعیین خودکار غیرفعال است</h3>
              <p className="text-sm text-muted-foreground mb-4">
                برای استفاده از قابلیت تعیین خودکار تکنسین، ابتدا آن را فعال کنید
              </p>
              <Button onClick={() => handleGlobalToggle(true)} className="gap-2">
                <Zap className="w-4 h-4" />
                فعال‌سازی تعیین خودکار
              </Button>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Info Banner */}
              <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
                <div className="flex items-start gap-3">
                  <Info className="w-5 h-5 text-blue-600 mt-0.5" />
                  <div>
                    <h4 className="font-medium text-blue-900 mb-1">نحوه عملکرد تعیین خودکار</h4>
                    <p className="text-sm text-blue-700">
                      سیستم بر اساس قوانین تعریف شده، تکنسین مناسب را انتخاب می‌کند. اولویت با تخصص، امتیاز، و بار کاری
                      تکنسین‌ها است.
                    </p>
                  </div>
                </div>
              </div>

              {/* Rules List */}
              <div className="space-y-3">
                <h4 className="font-medium">قوانین تعیین خودکار</h4>
                {rules.map((rule) => (
                  <div
                    key={rule.id}
                    className={`border rounded-lg p-4 transition-all ${
                      rule.enabled ? "bg-green-50 border-green-200" : "bg-gray-50 border-gray-200"
                    }`}
                  >
                    <div className="flex justify-between items-start">
                      <div className="flex-1">
                        <div className="flex items-center gap-3 mb-2">
                          <Switch
                            checked={rule.enabled}
                            onCheckedChange={(enabled) => handleRuleToggle(rule.id, enabled)}
                            size="sm"
                          />
                          <h5 className="font-medium">{rule.name}</h5>
                          {rule.enabled && (
                            <Badge variant="default" className="text-xs">
                              فعال
                            </Badge>
                          )}
                        </div>

                        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
                          <div className="flex items-center gap-2">
                            <Target className="w-3 h-3 text-muted-foreground" />
                            <span>اولویت: {priorityLabels[rule.priority]}</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <Users className="w-3 h-3 text-muted-foreground" />
                            <span>حداکثر بار کاری: {rule.maxWorkload}</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <Star className="w-3 h-3 text-muted-foreground" />
                            <span>حداقل امتیاز: {rule.minRating}</span>
                          </div>
                          <div className="flex items-center gap-2">
                            <CheckCircle className="w-3 h-3 text-muted-foreground" />
                            <span>ترجیح تخصص: {rule.preferSpecialty ? "بله" : "خیر"}</span>
                          </div>
                        </div>
                      </div>

                      <Button variant="ghost" size="sm" onClick={() => handleRuleEdit(rule)} className="gap-1">
                        <Settings className="w-3 h-3" />
                        ویرایش
                      </Button>
                    </div>
                  </div>
                ))}
              </div>

              {/* Statistics */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 pt-4 border-t">
                <div className="text-center">
                  <div className="text-2xl font-bold text-green-600">{getActiveRulesCount()}</div>
                  <div className="text-sm text-muted-foreground">قانون فعال</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-blue-600">85%</div>
                  <div className="text-sm text-muted-foreground">نرخ موفقیت</div>
                </div>
                <div className="text-center">
                  <div className="text-2xl font-bold text-purple-600">2.3s</div>
                  <div className="text-sm text-muted-foreground">میانگین زمان واگذاری</div>
                </div>
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Edit Rule Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={setEditDialogOpen}>
        <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] md:max-w-2xl" dir="rtl">
          <DialogHeader>
            <DialogTitle className="text-right">ویرایش قانون تعیین خودکار</DialogTitle>
          </DialogHeader>
          {selectedRule && (
            <div className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-medium mb-2 block">اولویت تیکت</Label>
                  <Select
                    value={selectedRule.priority}
                    onValueChange={(value: any) => setSelectedRule({ ...selectedRule, priority: value })}
                    dir="rtl"
                  >
                    <SelectTrigger className="text-right">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">همه اولویت‌ها</SelectItem>
                      <SelectItem value="urgent">فوری</SelectItem>
                      <SelectItem value="high">بالا</SelectItem>
                      <SelectItem value="medium">متوسط</SelectItem>
                      <SelectItem value="low">کم</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div>
                  <Label className="text-sm font-medium mb-2 block">دسته‌بندی</Label>
                  <Select
                    value={selectedRule.category}
                    onValueChange={(value) => setSelectedRule({ ...selectedRule, category: value })}
                    dir="rtl"
                  >
                    <SelectTrigger className="text-right">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">همه دسته‌ها</SelectItem>
                      <SelectItem value="hardware">سخت‌افزار</SelectItem>
                      <SelectItem value="software">نرم‌افزار</SelectItem>
                      <SelectItem value="network">شبکه</SelectItem>
                      <SelectItem value="email">ایمیل</SelectItem>
                      <SelectItem value="security">امنیت</SelectItem>
                      <SelectItem value="access">دسترسی</SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              <div>
                <Label className="text-sm font-medium mb-3 block">
                  حداکثر بار کاری تکنسین: {selectedRule.maxWorkload} تیکت
                </Label>
                <Slider
                  value={[selectedRule.maxWorkload]}
                  onValueChange={([value]) => setSelectedRule({ ...selectedRule, maxWorkload: value })}
                  max={10}
                  min={1}
                  step={1}
                  className="w-full"
                />
              </div>

              <div>
                <Label className="text-sm font-medium mb-3 block">حداقل امتیاز تکنسین: {selectedRule.minRating}</Label>
                <Slider
                  value={[selectedRule.minRating]}
                  onValueChange={([value]) => setSelectedRule({ ...selectedRule, minRating: value })}
                  max={5}
                  min={1}
                  step={0.1}
                  className="w-full"
                />
              </div>

              <div>
                <Label className="text-sm font-medium mb-3 block">
                  حداقل تجربه مورد نیاز: {selectedRule.requireExperience || 0} تیکت
                </Label>
                <Slider
                  value={[selectedRule.requireExperience || 0]}
                  onValueChange={([value]) => setSelectedRule({ ...selectedRule, requireExperience: value })}
                  max={100}
                  min={0}
                  step={5}
                  className="w-full"
                />
              </div>

              <div>
                <Label className="text-sm font-medium mb-3 block">
                  حداکثر زمان پاسخ: {selectedRule.maxResponseTime || 4} ساعت
                </Label>
                <Slider
                  value={[selectedRule.maxResponseTime || 4]}
                  onValueChange={([value]) => setSelectedRule({ ...selectedRule, maxResponseTime: value })}
                  max={8}
                  min={0.5}
                  step={0.5}
                  className="w-full"
                />
              </div>

              <div className="flex items-center gap-2">
                <Switch
                  checked={selectedRule.preferSpecialty}
                  onCheckedChange={(checked) => setSelectedRule({ ...selectedRule, preferSpecialty: checked })}
                  id="prefer-specialty"
                />
                <Label htmlFor="prefer-specialty" className="text-sm">
                  ترجیح تکنسین‌های متخصص در دسته‌بندی مربوطه
                </Label>
              </div>

              <div className="flex justify-end gap-2 pt-4 border-t">
                <Button variant="outline" onClick={() => setEditDialogOpen(false)}>
                  انصراف
                </Button>
                <Button onClick={handleRuleSave} className="gap-2">
                  <CheckCircle className="w-4 h-4" />
                  ذخیره تغییرات
                </Button>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  )
}

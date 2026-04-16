"use client";

import { useState, useEffect, useMemo } from "react";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Progress } from "@/components/ui/progress";
import { Skeleton } from "@/components/ui/skeleton";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { useAuth } from "@/lib/auth-context";
import {
  getCoverageSummary,
  getCoverageBreakdown,
  type CoverageSummary,
  type CoverageBreakdown,
  type CategorySubcategoryPair,
  type CoveredPair,
  type TechnicianCoverage,
} from "@/lib/automation-coverage-api";
import {
  FolderTree,
  Users,
  CheckCircle2,
  AlertTriangle,
  ArrowRight,
  RefreshCw,
  Ticket,
  TrendingUp,
  Inbox,
  ChevronRight,
  User,
  Layers,
  AlertCircle,
  ExternalLink,
  UserCheck,
  UserX,
  Info,
} from "lucide-react";

// Props for navigation callbacks to parent dashboard tabs
export interface AutomationCoverageViewProps {
  onNavigateToCategories?: () => void;
  onNavigateToTechnicians?: () => void;
  onNavigateToTickets?: () => void;
}

export function AutomationCoverageView({
  onNavigateToCategories,
  onNavigateToTechnicians,
  onNavigateToTickets,
}: AutomationCoverageViewProps = {}) {
  const { token, user } = useAuth();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [summary, setSummary] = useState<CoverageSummary | null>(null);
  const [breakdown, setBreakdown] = useState<CoverageBreakdown | null>(null);
  const [selectedCategoryId, setSelectedCategoryId] = useState<number | null>(null);
  const [selectedSubcategoryId, setSelectedSubcategoryId] = useState<number | null>(null);

  const fetchData = async () => {
    if (!user) return;
    setLoading(true);
    setError(null);
    try {
      const [summaryData, breakdownData] = await Promise.all([
        getCoverageSummary(token),
        getCoverageBreakdown(token),
      ]);
      setSummary(summaryData);
      setBreakdown(breakdownData);
    } catch (err: any) {
      const status = err?.status;
      if (status === 404) {
        setError("Not available (404)");
      } else if (err?.isNetworkError) {
        setError("Backend not reachable. Start backend with .\\tools\\run-backend.ps1");
      } else {
        setError(err?.message || "خطا در دریافت اطلاعات پوشش");
      }
      if (process.env.NODE_ENV === "development" && status !== 404) {
        console.error("Failed to fetch coverage data:", err);
      }
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchData();
  }, [token]);

  // Build category list from breakdown data
  const categories = useMemo(() => {
    if (!breakdown) return [];
    const allPairs = [...breakdown.coveredPairs, ...breakdown.uncoveredPairs];
    const catMap = new Map<number, { id: number; name: string; subcategoryCount: number; coveredCount: number }>();
    
    for (const pair of allPairs) {
      const existing = catMap.get(pair.categoryId);
      const isCovered = breakdown.coveredPairs.some(
        (cp) => cp.categoryId === pair.categoryId && cp.subcategoryId === pair.subcategoryId
      );
      if (existing) {
        existing.subcategoryCount++;
        if (isCovered) existing.coveredCount++;
      } else {
        catMap.set(pair.categoryId, {
          id: pair.categoryId,
          name: pair.categoryName,
          subcategoryCount: 1,
          coveredCount: isCovered ? 1 : 0,
        });
      }
    }
    return Array.from(catMap.values()).sort((a, b) => a.name.localeCompare(b.name, "fa"));
  }, [breakdown]);

  // Filter subcategories by selected category
  const subcategories = useMemo(() => {
    if (!breakdown || selectedCategoryId === null) return [];
    const allPairs = [...breakdown.coveredPairs, ...breakdown.uncoveredPairs];
    return allPairs
      .filter((p) => p.categoryId === selectedCategoryId)
      .map((p) => ({
        ...p,
        isCovered: breakdown.coveredPairs.some(
          (cp) => cp.subcategoryId === p.subcategoryId
        ),
        technicianCount: (breakdown.coveredPairs.find((cp) => cp.subcategoryId === p.subcategoryId) as CoveredPair)
          ?.technicianCount || 0,
      }))
      .sort((a, b) => a.subcategoryName.localeCompare(b.subcategoryName, "fa"));
  }, [breakdown, selectedCategoryId]);

  // Find technicians covering selected subcategory
  const coveringTechnicians = useMemo(() => {
    if (!breakdown || selectedSubcategoryId === null) return [];
    return breakdown.technicianCoverage.filter((tc) =>
      tc.pairs.some((p) => p.subcategoryId === selectedSubcategoryId)
    );
  }, [breakdown, selectedSubcategoryId]);

  if (loading) {
    return <CoverageLoadingSkeleton />;
  }

  if (error) {
    return (
      <Alert variant="destructive" className="mb-6">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle className="font-iran">خطا</AlertTitle>
        <AlertDescription className="font-iran">
          {error}
          <Button variant="link" onClick={fetchData} className="mr-2 font-iran">
            تلاش مجدد
          </Button>
        </AlertDescription>
      </Alert>
    );
  }

  if (!summary || !breakdown) {
    return (
      <Alert className="mb-6">
        <AlertCircle className="h-4 w-4" />
        <AlertTitle className="font-iran">داده‌ای یافت نشد</AlertTitle>
        <AlertDescription className="font-iran">
          هیچ اطلاعاتی برای نمایش وجود ندارد.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-6" dir="rtl">
      {/* KPI Cards - 2 rows for better organization */}
      <div className="space-y-4">
        {/* Row 1: Coverage Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <KpiCard
            icon={<FolderTree className="h-5 w-5" />}
            label="دسته‌بندی‌ها"
            value={summary.totalCategories}
            color="blue"
            tooltip="تعداد کل دسته‌بندی‌های فعال"
          />
          <KpiCard
            icon={<Layers className="h-5 w-5" />}
            label="زیردسته‌ها"
            value={summary.totalSubcategories}
            color="indigo"
            tooltip="تعداد کل زیردسته‌ها (هر زیردسته یک جفت دسته‌بندی/زیردسته است)"
          />
          <KpiCard
            icon={<CheckCircle2 className="h-5 w-5" />}
            label="پوشش داده شده"
            value={summary.coveredPairsCount}
            suffix={`(${summary.coveragePercent}%)`}
            color="green"
            tooltip="زیردسته‌هایی که حداقل یک تکنسین دارند"
          />
          <KpiCard
            icon={<AlertTriangle className="h-5 w-5" />}
            label="بدون پوشش"
            value={summary.uncoveredPairsCount}
            color={summary.uncoveredPairsCount > 0 ? "amber" : "gray"}
            tooltip="زیردسته‌هایی که هیچ تکنسینی ندارند - تیکت‌های این دسته‌ها اختصاص خودکار نمی‌شوند"
          />
        </div>

        {/* Row 2: Ticket & Technician Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <KpiCard
            icon={<Ticket className="h-5 w-5" />}
            label="کل تیکت‌های ۳۰ روز"
            value={summary.ticketsLast30Days}
            color="purple"
            tooltip="تعداد کل تیکت‌های ایجاد شده در ۳۰ روز گذشته"
          />
          <KpiCard
            icon={<UserCheck className="h-5 w-5" />}
            label="تخصیص یافته"
            value={summary.autoAssignedLast30Days}
            color="green"
            tooltip="تیکت‌هایی که به حداقل یک تکنسین اختصاص یافتند"
          />
          <KpiCard
            icon={<UserX className="h-5 w-5" />}
            label="بدون تکنسین"
            value={summary.unassignedLast30Days}
            color={summary.unassignedLast30Days > 0 ? "red" : "gray"}
            tooltip="تیکت‌هایی که به هیچ تکنسینی اختصاص نیافتند (احتمالاً به دلیل نبود پوشش)"
          />
          <KpiCard
            icon={<Users className="h-5 w-5" />}
            label="تکنسین‌های فعال"
            value={summary.techniciansCount}
            color="cyan"
            tooltip="تعداد تکنسین‌های فعال با حساب کاربری"
          />
        </div>
      </div>

      {/* Coverage Progress */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center justify-between text-base font-iran">
            <span className="flex items-center gap-2">
              <TrendingUp className="h-5 w-5 text-primary" />
              درصد پوشش کلی
            </span>
            <Button variant="ghost" size="sm" onClick={fetchData}>
              <RefreshCw className="h-4 w-4" />
            </Button>
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="space-y-2">
            <div className="flex items-center justify-between text-sm font-iran">
              <span>{summary.coveredPairsCount} از {summary.totalPairs} زیردسته</span>
              <span className="font-bold text-primary">{summary.coveragePercent}%</span>
            </div>
            <Progress value={summary.coveragePercent} className="h-3" />
          </div>
          {summary.unassignedLast30Days > 0 && (
            <div className="mt-3 flex items-center gap-2 text-amber-600 text-sm font-iran">
              <AlertTriangle className="h-4 w-4" />
              {summary.unassignedLast30Days} تیکت در ۳۰ روز اخیر بدون تکنسین ماندند (احتمالاً به دلیل نبود پوشش)
            </div>
          )}
        </CardContent>
      </Card>

      {/* How it works */}
      <Card className="border-blue-200 bg-blue-50/50">
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center gap-2 text-base font-iran text-blue-800">
            <Inbox className="h-5 w-5" />
            نحوه کار سیستم خودکار
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap items-center gap-2 text-sm font-iran text-blue-700">
            <Badge variant="outline" className="font-iran">۱. مشتری دسته‌بندی انتخاب می‌کند</Badge>
            <ArrowRight className="h-4 w-4 text-blue-400" />
            <Badge variant="outline" className="font-iran">۲. سیستم تکنسین‌های پوشش‌دهنده را پیدا می‌کند</Badge>
            <ArrowRight className="h-4 w-4 text-blue-400" />
            <Badge variant="outline" className="font-iran">۳. تیکت به تکنسین‌ها اختصاص می‌یابد</Badge>
            <ArrowRight className="h-4 w-4 text-blue-400" />
            <Badge variant="outline" className="font-iran">۴. تیکت در صندوق ورودی تکنسین ظاهر می‌شود</Badge>
          </div>
          <p className="mt-3 text-xs text-blue-600 font-iran">
            <strong>قانون تطبیق:</strong> تکنسین باید دقیقاً همان زیردسته را در لیست پوشش خود داشته باشد.
          </p>
        </CardContent>
      </Card>

      {/* 3-Column Coverage Map */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base font-iran">
            <FolderTree className="h-5 w-5 text-primary" />
            نقشه پوشش
          </CardTitle>
          <CardDescription className="font-iran">
            دسته‌بندی را انتخاب کنید → زیردسته را ببینید → تکنسین‌های پوشش‌دهنده را مشاهده کنید
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            {/* Column 1: Categories */}
            <div className="border rounded-lg p-2">
              <h4 className="text-sm font-medium font-iran mb-2 flex items-center gap-1">
                <FolderTree className="h-4 w-4" />
                دسته‌بندی‌ها
              </h4>
              <ScrollArea className="h-64">
                <div className="space-y-1">
                  {categories.map((cat) => (
                    <button
                      key={cat.id}
                      onClick={() => {
                        setSelectedCategoryId(cat.id);
                        setSelectedSubcategoryId(null);
                      }}
                      className={`w-full text-right p-2 rounded text-sm font-iran transition-colors ${
                        selectedCategoryId === cat.id
                          ? "bg-primary text-primary-foreground"
                          : "hover:bg-muted"
                      }`}
                    >
                      <div className="flex items-center justify-between">
                        <span>{cat.name}</span>
                        <Badge
                          variant={cat.coveredCount === cat.subcategoryCount ? "default" : "outline"}
                          className="text-xs"
                        >
                          {cat.coveredCount}/{cat.subcategoryCount}
                        </Badge>
                      </div>
                    </button>
                  ))}
                  {categories.length === 0 && (
                    <p className="text-sm text-muted-foreground font-iran p-2">
                      هیچ دسته‌بندی‌ای وجود ندارد
                    </p>
                  )}
                </div>
              </ScrollArea>
            </div>

            {/* Column 2: Subcategories */}
            <div className="border rounded-lg p-2">
              <h4 className="text-sm font-medium font-iran mb-2 flex items-center gap-1">
                <Layers className="h-4 w-4" />
                زیردسته‌ها
              </h4>
              <ScrollArea className="h-64">
                <div className="space-y-1">
                  {selectedCategoryId ? (
                    subcategories.length > 0 ? (
                      subcategories.map((sub) => (
                        <button
                          key={sub.subcategoryId}
                          onClick={() => setSelectedSubcategoryId(sub.subcategoryId)}
                          className={`w-full text-right p-2 rounded text-sm font-iran transition-colors ${
                            selectedSubcategoryId === sub.subcategoryId
                              ? "bg-primary text-primary-foreground"
                              : "hover:bg-muted"
                          }`}
                        >
                          <div className="flex items-center justify-between">
                            <div className="flex items-center gap-1">
                              {!sub.isCovered && (
                                <AlertTriangle className="h-3 w-3 text-amber-500" />
                              )}
                              <span>{sub.subcategoryName}</span>
                            </div>
                            {sub.isCovered ? (
                              <Badge variant="default" className="text-xs">
                                {sub.technicianCount} تکنسین
                              </Badge>
                            ) : (
                              <Badge variant="destructive" className="text-xs">
                                بدون پوشش
                              </Badge>
                            )}
                          </div>
                        </button>
                      ))
                    ) : (
                      <p className="text-sm text-muted-foreground font-iran p-2">
                        زیردسته‌ای وجود ندارد
                      </p>
                    )
                  ) : (
                    <p className="text-sm text-muted-foreground font-iran p-2">
                      ابتدا یک دسته‌بندی انتخاب کنید
                    </p>
                  )}
                </div>
              </ScrollArea>
            </div>

            {/* Column 3: Technicians */}
            <div className="border rounded-lg p-2">
              <h4 className="text-sm font-medium font-iran mb-2 flex items-center gap-1">
                <Users className="h-4 w-4" />
                تکنسین‌های پوشش‌دهنده
              </h4>
              <ScrollArea className="h-64">
                <div className="space-y-1">
                  {selectedSubcategoryId ? (
                    coveringTechnicians.length > 0 ? (
                      coveringTechnicians.map((tech) => (
                        <div
                          key={tech.technicianId}
                          className="p-2 rounded bg-muted/50 text-sm font-iran"
                        >
                          <div className="flex items-center gap-2">
                            <User className="h-4 w-4 text-primary" />
                            <span className="font-medium">{tech.technicianName}</span>
                          </div>
                          <div className="text-xs text-muted-foreground mt-1">
                            {tech.coveredPairsCount} زیردسته تحت پوشش
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="p-3 text-center">
                        <AlertTriangle className="h-8 w-8 text-amber-500 mx-auto mb-2" />
                        <p className="text-sm text-amber-600 font-iran">
                          هیچ تکنسینی این زیردسته را پوشش نمی‌دهد
                        </p>
                        <p className="text-xs text-muted-foreground font-iran mt-1">
                          تیکت‌های این زیردسته اختصاص خودکار نخواهند شد
                        </p>
                      </div>
                    )
                  ) : (
                    <p className="text-sm text-muted-foreground font-iran p-2">
                      زیردسته‌ای انتخاب کنید
                    </p>
                  )}
                </div>
              </ScrollArea>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Uncovered Pairs Warning */}
      {breakdown.uncoveredPairs.length > 0 && (
        <Card className="border-amber-200">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base font-iran text-amber-700">
              <AlertTriangle className="h-5 w-5" />
              زیردسته‌های بدون پوشش ({breakdown.uncoveredPairs.length})
            </CardTitle>
            <CardDescription className="font-iran text-amber-600">
              این زیردسته‌ها هیچ تکنسینی ندارند و تیکت‌های آنها اختصاص خودکار نخواهند شد
            </CardDescription>
          </CardHeader>
          <CardContent>
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
              {breakdown.uncoveredPairs.slice(0, 12).map((pair) => (
                <div
                  key={`${pair.categoryId}-${pair.subcategoryId}`}
                  className="flex items-center gap-2 p-2 rounded bg-amber-50 border border-amber-200 text-sm font-iran"
                >
                  <AlertTriangle className="h-4 w-4 text-amber-500 shrink-0" />
                  <span className="text-amber-800">
                    {pair.categoryName} <ChevronRight className="inline h-3 w-3" /> {pair.subcategoryName}
                  </span>
                </div>
              ))}
            </div>
            {breakdown.uncoveredPairs.length > 12 && (
              <p className="text-sm text-muted-foreground mt-2 font-iran">
                و {breakdown.uncoveredPairs.length - 12} مورد دیگر...
              </p>
            )}
          </CardContent>
        </Card>
      )}

      {/* Technician Coverage Summary */}
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base font-iran">
            <Users className="h-5 w-5 text-primary" />
            خلاصه پوشش تکنسین‌ها ({breakdown.technicianCoverage.length})
          </CardTitle>
        </CardHeader>
        <CardContent>
          {breakdown.technicianCoverage.length > 0 ? (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
              {breakdown.technicianCoverage.map((tech) => (
                <div
                  key={tech.technicianId}
                  className="flex items-center justify-between p-3 rounded-lg border bg-muted/30"
                >
                  <div className="flex items-center gap-2">
                    <User className="h-5 w-5 text-primary" />
                    <div>
                      <p className="font-medium font-iran text-sm">{tech.technicianName}</p>
                      <p className="text-xs text-muted-foreground font-iran">
                        {tech.coveredPairsCount} زیردسته
                      </p>
                    </div>
                  </div>
                  <Badge variant={tech.isActive ? "default" : "secondary"}>
                    {tech.isActive ? "فعال" : "غیرفعال"}
                  </Badge>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-muted-foreground font-iran">
              هیچ تکنسینی پوششی تعریف نکرده است
            </p>
          )}
        </CardContent>
      </Card>

      {/* Navigation Actions */}
      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="flex items-center gap-2 text-base font-iran">
            <ExternalLink className="h-5 w-5 text-primary" />
            دسترسی سریع
          </CardTitle>
        </CardHeader>
        <CardContent>
          <div className="flex flex-wrap gap-3">
            <Button 
              variant="outline" 
              className="font-iran gap-2"
              onClick={onNavigateToCategories}
              disabled={!onNavigateToCategories}
            >
              <FolderTree className="h-4 w-4" />
              مدیریت دسته‌بندی
            </Button>
            <Button 
              variant="outline" 
              className="font-iran gap-2"
              onClick={onNavigateToTechnicians}
              disabled={!onNavigateToTechnicians}
            >
              <Users className="h-4 w-4" />
              مدیریت تکنسین‌ها
            </Button>
            <Button 
              variant="outline" 
              className="font-iran gap-2"
              onClick={onNavigateToTickets}
              disabled={!onNavigateToTickets}
            >
              <Ticket className="h-4 w-4" />
              مشاهده تیکت‌ها
            </Button>
            {summary.unassignedLast30Days > 0 && (
              <Button 
                variant="destructive" 
                className="font-iran gap-2"
                onClick={onNavigateToTickets}
                disabled={!onNavigateToTickets}
              >
                <UserX className="h-4 w-4" />
                تیکت‌های بدون تکنسین ({summary.unassignedLast30Days})
              </Button>
            )}
          </div>
        </CardContent>
      </Card>
    </div>
  );
}

// ============ Sub-components ============

interface KpiCardProps {
  icon: React.ReactNode;
  label: string;
  value: number;
  suffix?: string;
  color: "blue" | "indigo" | "green" | "amber" | "gray" | "purple" | "cyan" | "red";
  tooltip?: string;
}

function KpiCard({ icon, label, value, suffix, color, tooltip }: KpiCardProps) {
  const colorClasses: Record<string, string> = {
    blue: "bg-blue-50 text-blue-600 border-blue-200",
    indigo: "bg-indigo-50 text-indigo-600 border-indigo-200",
    green: "bg-green-50 text-green-600 border-green-200",
    amber: "bg-amber-50 text-amber-600 border-amber-200",
    gray: "bg-gray-50 text-gray-600 border-gray-200",
    purple: "bg-purple-50 text-purple-600 border-purple-200",
    cyan: "bg-cyan-50 text-cyan-600 border-cyan-200",
    red: "bg-red-50 text-red-600 border-red-200",
  };

  const cardContent = (
    <Card className={`border ${colorClasses[color]}`}>
      <CardContent className="p-4 text-center">
        <div className="flex justify-center items-center gap-1 mb-2">
          {icon}
          {tooltip && (
            <TooltipProvider>
              <Tooltip>
                <TooltipTrigger asChild>
                  <Info className="h-3 w-3 opacity-50 hover:opacity-100 cursor-help" />
                </TooltipTrigger>
                <TooltipContent side="top" className="max-w-xs font-iran text-xs">
                  {tooltip}
                </TooltipContent>
              </Tooltip>
            </TooltipProvider>
          )}
        </div>
        <p className="text-2xl font-bold">
          {value.toLocaleString("fa-IR")}
          {suffix && <span className="text-sm font-normal mr-1">{suffix}</span>}
        </p>
        <p className="text-xs font-iran mt-1">{label}</p>
      </CardContent>
    </Card>
  );

  return cardContent;
}

function CoverageLoadingSkeleton() {
  return (
    <div className="space-y-6" dir="rtl">
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
        {[...Array(6)].map((_, i) => (
          <Skeleton key={i} className="h-24" />
        ))}
      </div>
      <Skeleton className="h-20" />
      <Skeleton className="h-32" />
      <Skeleton className="h-80" />
    </div>
  );
}















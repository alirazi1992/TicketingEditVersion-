"use client"

import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { ChevronDown } from "lucide-react"

type AssignedTechnicianItem = {
  id?: string
  userId?: string
  name?: string
  fullName?: string
  role?: string
  isSupervisor?: boolean
  isActive?: boolean
}

type AssignedTechniciansCellProps = {
  technicians?: AssignedTechnicianItem[]
  emptyLabel?: string
}

const getRoleLabel = (tech: AssignedTechnicianItem) => {
  if (tech.isSupervisor || tech.role === "SupervisorTechnician") return "سرپرست"
  if (tech.role === "Technician") return "تکنسین"
  return tech.role || "تکنسین"
}

export function AssignedTechniciansCell({
  technicians = [],
  emptyLabel = "تعیین نشده",
}: AssignedTechniciansCellProps) {
  const normalized = technicians.filter((tech) => tech.name || tech.fullName)

  if (normalized.length === 0) {
    return <span className="text-muted-foreground">{emptyLabel}</span>
  }

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="outline" size="sm" className="gap-1">
          مشاهده ({normalized.length})
          <ChevronDown className="h-3 w-3" />
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-[420px] text-right" align="start" sideOffset={8} dir="rtl">
        <div className="space-y-2">
          <div className="text-sm font-semibold">تکنسین‌های تخصیص داده‌شده</div>
          <div className="max-h-[260px] overflow-y-auto">
            <div className="space-y-3">
              {normalized.map((tech, index) => {
                const displayName = tech.fullName || tech.name || "نامشخص"
                const roleLabel = getRoleLabel(tech)
                const key = tech.id || tech.userId || `${displayName}-${index}`
                return (
                  <div key={key} className="flex items-center justify-between gap-3">
                    <div className="flex flex-col">
                      <div className="text-sm font-medium">{displayName}</div>
                      <div className="text-xs text-muted-foreground">{roleLabel}</div>
                    </div>
                    <div className="flex items-center gap-2">
                      {typeof tech.isActive === "boolean" && (
                        <span
                          className={`h-2 w-2 rounded-full ${
                            tech.isActive ? "bg-emerald-500" : "bg-muted-foreground"
                          }`}
                          aria-hidden="true"
                        />
                      )}
                      <Badge variant="outline" className="text-xs">
                        {roleLabel}
                      </Badge>
                    </div>
                  </div>
                )
              })}
            </div>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  )
}

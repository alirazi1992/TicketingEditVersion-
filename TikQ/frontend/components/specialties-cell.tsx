"use client"

import { Badge } from "@/components/ui/badge"
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover"
import { ChevronDown } from "lucide-react"

type SpecialtiesCellProps = {
  specialties?: string[]
}

export function SpecialtiesCell({
  specialties = [],
}: SpecialtiesCellProps) {
  const normalized = specialties.filter((specialty) => specialty?.trim())
  if (normalized.length === 0) {
    return <span className="text-muted-foreground">—</span>
  }

  const badgeClassName = "text-xs px-2 py-0.5 leading-4"

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="inline-flex items-center gap-1 text-xs px-2 py-1 rounded border hover:bg-muted"
          aria-label="مشاهده همه تخصص‌ها"
        >
          مشاهده ({normalized.length})
          <ChevronDown className="h-3 w-3" />
        </button>
      </PopoverTrigger>
      <PopoverContent
        className="w-[420px] text-right"
        align="start"
        sideOffset={8}
        dir="rtl"
      >
        <div className="space-y-2">
          <div className="text-sm font-semibold">تخصص‌ها</div>
          <div className="max-h-[240px] overflow-y-auto">
            <div className="flex flex-wrap gap-2 text-right" dir="rtl">
              {normalized.map((specialty, index) => (
                <Badge
                  key={`${specialty}-${index}-all`}
                  variant="outline"
                  className={badgeClassName}
                >
                  {specialty}
                </Badge>
              ))}
            </div>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  )
}

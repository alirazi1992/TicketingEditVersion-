"use client"

import { Controller } from "react-hook-form"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import type { FormFieldDef } from "@/lib/dynamic-forms"

interface DynamicFieldRendererProps {
  field: FormFieldDef
  control: any
  errors: any
}

function toControlledValue(value: unknown, type?: string) {
  if (type === "checkbox" || type === "switch") {
    return Boolean(value)
  }

  if (type === "number") {
    if (value === null || value === undefined || value === "") return ""
    const num = typeof value === "number" ? value : Number(value)
    return Number.isNaN(num) ? "" : num
  }

  if (type === "date" || type === "datetime") {
    if (!value) return ""
    return typeof value === "string" ? value : String(value)
  }

  if (type === "select" || type === "radio") {
    if (value === null || value === undefined) return ""
    return String(value)
  }

  if (value === null || value === undefined) return ""
  return typeof value === "string" ? value : String(value)
}

export function DynamicFieldRenderer({ field, control, errors }: DynamicFieldRendererProps) {
  const name = `dyn_${field.id}`
  const error = errors?.[name]?.message as string | undefined

  const requiredMsg = "این فیلد الزامی است"

  const commonLabel = (
    <Label htmlFor={name} className="text-right">
      {field.label} {field.required ? "*" : ""}
    </Label>
  )

  const help = field.helpText ? (
    <p className="text-xs text-muted-foreground text-right">{field.helpText}</p>
  ) : null

  if (field.type === "textarea") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => (
            <Textarea
              {...rhf}
              value={toControlledValue(rhf.value, field.type)}
              onChange={(e) => rhf.onChange(e.target.value)}
              placeholder={field.placeholder}
              rows={4}
              className="text-right"
              dir="rtl"
            />
          )}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  if (field.type === "select" || field.type === "radio") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => (
            <Select
              onValueChange={rhf.onChange}
              value={toControlledValue(rhf.value, "select")}
              dir="rtl"
            >
              <SelectTrigger className="text-right">
                <SelectValue placeholder={field.placeholder || "انتخاب کنید"} />
              </SelectTrigger>
              <SelectContent className="text-right">
                {(field.options || [])
                  .filter((opt) => opt.value != null && String(opt.value) !== "")
                  .map((opt) => (
                    <SelectItem key={opt.value} value={String(opt.value)}>
                      {opt.label}
                    </SelectItem>
                  ))}
              </SelectContent>
            </Select>
          )}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  if (field.type === "multiselect") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => {
            const selectedValues = Array.isArray(rhf.value) ? rhf.value : (rhf.value ? [rhf.value] : [])
            return (
              <div className="space-y-2">
                <div className="border rounded-md p-3 space-y-2 max-h-48 overflow-y-auto" dir="rtl">
                  {(field.options || []).map((opt) => (
                    <label key={opt.value} className="flex items-center gap-2 cursor-pointer text-right">
                      <input
                        type="checkbox"
                        checked={selectedValues.includes(opt.value)}
                        onChange={(e) => {
                          if (e.target.checked) {
                            rhf.onChange([...selectedValues, opt.value])
                          } else {
                            rhf.onChange(selectedValues.filter((v: string) => v !== opt.value))
                          }
                        }}
                        className="w-4 h-4"
                      />
                      <span>{opt.label}</span>
                    </label>
                  ))}
                </div>
                {selectedValues.length > 0 && (
                  <p className="text-xs text-muted-foreground text-right">
                    {selectedValues.length} مورد انتخاب شده
                  </p>
                )}
              </div>
            )
          }}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  if (field.type === "date" || field.type === "datetime") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => (
            <Input
              {...rhf}
              value={toControlledValue(rhf.value, field.type)}
              type={field.type === "datetime" ? "datetime-local" : "date"}
              placeholder={field.placeholder}
              className="text-right"
              dir="rtl"
            />
          )}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  if (field.type === "checkbox") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => (
            <input
              type="checkbox"
              checked={toControlledValue(rhf.value, "checkbox") as boolean}
              onChange={(e) => rhf.onChange(e.target.checked)}
            />
          )}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  if (field.type === "file") {
    return (
      <div className="space-y-2">
        {commonLabel}
        <Controller
          name={name}
          control={control}
          rules={field.required ? { required: requiredMsg } : undefined}
          render={({ field: rhf }) => (
            <Input
              type="file"
              onChange={(e) => rhf.onChange(e.target.files?.[0] || null)}
            />
          )}
        />
        {help}
        {error && <p className="text-sm text-red-500 text-right">{error}</p>}
      </div>
    )
  }

  const inputType = field.type === "email" || field.type === "tel" || field.type === "number" ? field.type : "text"

  return (
    <div className="space-y-2">
      {commonLabel}
      <Controller
        name={name}
        control={control}
        rules={field.required ? { required: requiredMsg } : undefined}
        render={({ field: rhf }) => (
          <Input
            {...rhf}
            value={toControlledValue(rhf.value, field.type)}
            type={inputType}
            placeholder={field.placeholder}
            className="text-right"
            dir="rtl"
            onChange={(e) => {
              if (field.type === "number") {
                const v = e.target.value
                rhf.onChange(v === "" ? "" : Number(v))
              } else {
                rhf.onChange(e.target.value)
              }
            }}
          />
        )}
      />
      {help}
      {error && <p className="text-sm text-red-500 text-right">{error}</p>}
    </div>
  )
}

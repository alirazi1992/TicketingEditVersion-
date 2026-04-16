export type FieldOption = {
  value: string
  label: string
}

export type FieldType =
  | "text"
  | "textarea"
  | "number"
  | "email"
  | "tel"
  | "date"
  | "datetime"
  | "select"
  | "multiselect"
  | "radio"
  | "checkbox"
  | "file"

export interface FormFieldDef {
  id: string
  label: string
  type: FieldType
  required?: boolean
  placeholder?: string
  helpText?: string
  options?: FieldOption[]
}

export type FormDef = FormFieldDef[]

export function parseOptions(input: string): FieldOption[] {
  // Accept comma-separated list of either "value:label" or plain value
  return input
    .split(",")
    .map((s) => s.trim())
    .filter(Boolean)
    .map((pair) => {
      const [value, label] = pair.split(":").map((p) => p.trim())
      if (label) return { value, label }
      return { value, label: value }
    })
}

export function isChoice(type: FieldType): boolean {
  return type === "select" || type === "multiselect" || type === "radio"
}

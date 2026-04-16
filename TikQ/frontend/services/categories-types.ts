export type FieldType =
  | "text"
  | "textarea"
  | "number"
  | "email"
  | "tel"
  | "date"
  | "datetime"
  | "select"
  | "radio"
  | "checkbox"
  | "file"

export interface DynamicField {
  id: string
  label: string
  type: FieldType
  required?: boolean
  placeholder?: string
  options?: Array<{ value: string; label: string }>
}

export interface SubIssue {
  id?: string
  backendId?: number
  label: string
  description?: string
  fields?: DynamicField[]
}

export interface Category {
  id?: string
  backendId?: number
  label: string
  description?: string
  subIssues: Record<string, SubIssue>
}

export type CategoriesData = Record<string, Category>


import * as yup from "yup"
import type { CategoriesData } from "@/services/categories-types"

const requiredMsg = "پر کردن این فیلد الزامی است"

// Phone number validation for Iranian numbers
const validatePhoneNumber = (phone: string): boolean => {
  const iranianPhoneRegex = /^(\+98|0)?9\d{9}$/
  return iranianPhoneRegex.test(phone.replace(/\s/g, ""))
}

// Issue Selection Schema (Step 1)
export const issueSelectionSchema = (categories?: CategoriesData) =>
  yup.object({
    priority: yup.string().required("انتخاب اولویت الزامی است"),
    mainIssue: yup.string().required("انتخاب دسته‌بندی اصلی الزامی است"),
    subIssue: yup.string().when("mainIssue", (mainIssue, schema: yup.StringSchema) => {
      const hasSubIssues =
        typeof mainIssue === "string" &&
        !!mainIssue &&
        !!categories?.[mainIssue] &&
        Object.keys(categories[mainIssue].subIssues || {}).length > 0

      if (hasSubIssues) {
        return schema.required("انتخاب زیرشاخه الزامی است")
      }

      return schema.optional().nullable().transform((value) => value ?? "")
    }),
  })

// Ticket Details Schema (Step 2)
export const ticketDetailsSchema = yup.object({
  title: yup
    .string()
    .required("عنوان تیکت الزامی است")
    .min(5, "عنوان تیکت باید حداقل ۵ کاراکتر باشد")
    .max(100, "عنوان تیکت نباید بیشتر از ۱۰۰ کاراکتر باشد"),
  description: yup
    .string()
    .optional()
    .transform((value) => (value ?? "").trim())
    .test(
      "min-if-present",
      "شرح مشکل باید حداقل ۱۰ کاراکتر باشد",
      (value) => !value || value.length >= 10
    )
    .max(5000, "شرح مشکل نباید بیشتر از ۵۰۰۰ کاراکتر باشد"),
})

// Contact Information Schema - always required
export const contactInfoSchema = yup.object({
  clientName: yup
    .string()
    .required("نام الزامی است")
    .min(2, "نام باید حداقل ۲ کاراکتر باشد")
    .max(50, "نام نباید بیشتر از ۵۰ کاراکتر باشد"),

  clientEmail: yup.string().required("ایمیل الزامی است").email("فرمت ایمیل صحیح نیست"),

  clientPhone: yup
    .string()
    .required("شماره تماس الزامی است")
    .test("phone-validation", "شماره تماس معتبر نیست", validatePhoneNumber),
})

// Update the combined schema to include contact info
export const getCombinedSchema = (step: number, categories?: CategoriesData) => {
  const baseSchema = contactInfoSchema.concat(issueSelectionSchema(categories))

  if (step === 1) {
    return baseSchema
  } else {
    return baseSchema.concat(ticketDetailsSchema)
  }
}

// Legacy schemas for backward compatibility
export const generalInfoSchema = issueSelectionSchema()

// Ticket access schema
export const ticketAccessSchema = yup.object({
  ticketId: yup
    .string()
    .required("کد تیکت الزامی است")
    .matches(/^TK-\d{4}-\d{3}$/, "فرمت کد تیکت نامعتبر است (مثال: TK-2024-001)"),

  email: yup.string().required("ایمیل الزامی است").email("فرمت ایمیل صحیح نیست"),

  phone: yup
    .string()
    .required("شماره تماس الزامی است")
    .test("phone-validation", "شماره تماس معتبر نیست", validatePhoneNumber),
})

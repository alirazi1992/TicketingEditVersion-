export interface UploadedFile {
  id: string
  name: string
  size: number
  type: string
  url: string
  uploadedAt: string
  file?: File // Store the actual File object for upload
}

export const validateFile = (file: File): string | null => {
  const maxSize = 10 * 1024 * 1024 // 10MB
  const allowedTypes = [
    "image/jpeg",
    "image/png",
    "image/gif",
    "application/pdf",
    "text/plain",
    "application/msword",
    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    "application/vnd.ms-excel",
    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  ]

  if (file.size > maxSize) {
    return "حجم فایل نباید بیشتر از ۱۰ مگابایت باشد"
  }

  if (!allowedTypes.includes(file.type)) {
    return "نوع فایل مجاز نیست"
  }

  return null
}

export const uploadFile = async (file: File): Promise<UploadedFile> => {
  // Simulate file upload
  await new Promise((resolve) => setTimeout(resolve, 1000))

  return {
    id: Math.random().toString(36).substr(2, 9),
    name: file.name,
    size: file.size,
    type: file.type,
    url: URL.createObjectURL(file),
    uploadedAt: new Date().toISOString(),
  }
}

export const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return "0 بایت"

  const k = 1024
  const sizes = ["بایت", "کیلوبایت", "مگابایت", "گیگابایت"]
  const i = Math.floor(Math.log(bytes) / Math.log(k))

  return Number.parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + " " + sizes[i]
}
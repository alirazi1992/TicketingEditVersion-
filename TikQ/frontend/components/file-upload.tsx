"use client"

import { useState, useCallback } from "react"
import { useDropzone } from "react-dropzone"
import { Button } from "@/components/ui/button"
import { Progress } from "@/components/ui/progress"
import { toast } from "@/hooks/use-toast"
import { Upload, X, File, ImageIcon, FileText } from "lucide-react"
import { toFaTime } from "@/lib/datetime"


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

export interface UploadedFile {
  id: string
  name: string
  size: number
  type: string
  url: string
  uploadedAt: string
  file?: File // Store the actual File object for upload
}

interface FileUploadProps {
  onFilesChange: (files: UploadedFile[]) => void
  maxFiles?: number
  className?: string
}

export function FileUpload({ onFilesChange, maxFiles = 5, className }: FileUploadProps) {
  const [files, setFiles] = useState<UploadedFile[]>([])
  const [uploading, setUploading] = useState<string[]>([])
  const [uploadProgress, setUploadProgress] = useState<Record<string, number>>({})

  const onDrop = useCallback(
    async (acceptedFiles: File[]) => {
      if (files.length + acceptedFiles.length > maxFiles) {
        toast({
          title: "تعداد فایل‌ها زیاد است",
          description: `حداکثر ${maxFiles} فایل مجاز است`,
          variant: "destructive",
        })
        return
      }

      for (const file of acceptedFiles) {
        const error = validateFile(file)
        if (error) {
          toast({
            title: "خطا در فایل",
            description: `${file.name}: ${error}`,
            variant: "destructive",
          })
          continue
        }

        const tempId = Math.random().toString(36).substr(2, 9)
        setUploading((prev) => [...prev, tempId])
        setUploadProgress((prev) => ({ ...prev, [tempId]: 0 }))

        
        const progressInterval = setInterval(() => {
          setUploadProgress((prev) => {
            const currentProgress = prev[tempId] || 0
            if (currentProgress >= 90) {
              clearInterval(progressInterval)
              return prev
            }
            return { ...prev, [tempId]: currentProgress + 10 }
          })
        }, 100)

        try {
          const uploadedFile = await uploadFile(file)
          // Store the actual File object for later upload
          uploadedFile.file = file
          setFiles((prev) => {
            const newFiles = [...prev, uploadedFile]
            onFilesChange(newFiles)
            return newFiles
          })
          setUploadProgress((prev) => ({ ...prev, [tempId]: 100 }))

          setTimeout(() => {
            setUploading((prev) => prev.filter((id) => id !== tempId))
            setUploadProgress((prev) => {
              const { [tempId]: _, ...rest } = prev
              return rest
            })
          }, 500)

          toast({
            title: "فایل آپلود شد",
            description: `${file.name} با موفقیت آپلود شد`,
          })
        } catch (error) {
          setUploading((prev) => prev.filter((id) => id !== tempId))
          setUploadProgress((prev) => {
            const { [tempId]: _, ...rest } = prev
            return rest
          })
          toast({
            title: "خطا در آپلود",
            description: `خطا در آپلود ${file.name}`,
            variant: "destructive",
          })
        }
      }
    },
    [files.length, maxFiles, onFilesChange],
  )

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    multiple: true,
    disabled: files.length >= maxFiles,
  })

  const removeFile = (fileId: string) => {
    setFiles((prev) => {
      const newFiles = prev.filter((f) => f.id !== fileId)
      onFilesChange(newFiles)
      return newFiles
    })
    toast({
      title: "فایل حذف شد",
      description: "فایل از لیست پیوست‌ها حذف شد",
    })
  }

  const getFileIcon = (type: string) => {
    if (type.startsWith("image/")) return <ImageIcon className="w-4 h-4" />
    if (type.includes("pdf") || type.includes("document")) return <FileText className="w-4 h-4" />
    return <File className="w-4 h-4" />
  }

  return (
    <div className={`space-y-4 ${className}`} dir="rtl">
      <div
        {...getRootProps()}
        className={`border-2 border-dashed rounded-lg p-6 text-center cursor-pointer transition-colors ${
          isDragActive
            ? "border-primary bg-primary/5"
            : files.length >= maxFiles
              ? "border-muted-foreground/25 bg-muted/50 cursor-not-allowed"
              : "border-muted-foreground/25 hover:border-primary hover:bg-primary/5"
        }`}
      >
        <input {...getInputProps()} />
        <Upload className="w-8 h-8 mx-auto text-muted-foreground mb-2" />
        {files.length >= maxFiles ? (
          <p className="text-sm text-muted-foreground">حداکثر تعداد فایل ({maxFiles}) آپلود شده است</p>
        ) : isDragActive ? (
          <p className="text-sm text-muted-foreground">فایل‌ها را اینجا رها کنید...</p>
        ) : (
          <div>
            <p className="text-sm text-muted-foreground mb-2">فایل‌های مربوط به مشکل را اینجا بکشید یا کلیک کنید</p>
            <Button variant="outline" size="sm" type="button">
              انتخاب فایل
            </Button>
            <p className="text-xs text-muted-foreground mt-2">حداکثر {maxFiles} فایل، هر کدام تا ۱۰ مگابایت</p>
          </div>
        )}
      </div>

      {/* Upload Progress */}
      {uploading.length > 0 && (
        <div className="space-y-2">
          {uploading.map((tempId) => (
            <div key={tempId} className="space-y-1">
              <div className="flex justify-between text-sm">
                <span>در حال آپلود...</span>
                <span>{uploadProgress[tempId] || 0}%</span>
              </div>
              <Progress value={uploadProgress[tempId] || 0} className="h-2" />
            </div>
          ))}
        </div>
      )}

      {/* Uploaded Files */}
      {files.length > 0 && (
        <div className="space-y-2">
          <h4 className="text-sm font-medium text-right">فایل‌های پیوست شده:</h4>
          <div className="space-y-2">
            {files.map((file) => (
              <div key={file.id} className="flex items-center justify-between p-3 border rounded-lg bg-muted/50">
                <div className="flex items-center gap-2">
                  {getFileIcon(file.type)}
                  <div>
                    <p className="text-sm font-medium text-right">{file.name}</p>
                    <p className="text-xs text-muted-foreground text-right">
                      {formatFileSize(file.size)} • {toFaTime(file.uploadedAt)}
                    </p>
                  </div>
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => removeFile(file.id)}
                  className="text-red-500 hover:text-red-700 hover:bg-red-50"
                >
                  <X className="w-4 h-4" />
                </Button>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
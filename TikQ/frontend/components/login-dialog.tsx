"use client"

import { useState } from "react"
import { useRouter } from "next/navigation"
import { useForm, Controller } from "react-hook-form"
import { yupResolver } from "@hookform/resolvers/yup"
import * as yup from "yup"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog"
import { toast } from "@/hooks/use-toast"
import { Eye, EyeOff, LogIn, UserPlus, Shield, Wrench, User } from "lucide-react"
import { useAuth } from "@/lib/auth-context"


const loginSchema = yup.object({
  email: yup.string().required("ایمیل الزامی است").email("فرمت ایمیل صحیح نیست"),
  password: yup.string().required("رمز عبور الزامی است").min(6, "رمز عبور باید حداقل ۶ کاراکتر باشد"),
})

const signupSchema = yup.object({
  name: yup.string().required("نام و نام خانوادگی الزامی است").min(2, "نام باید حداقل ۲ کاراکتر باشد"),
  email: yup.string().required("ایمیل الزامی است").email("فرمت ایمیل صحیح نیست"),
  phone: yup
    .string()
    .required("شماره تماس الزامی است")
    .matches(/^(\+98|0)?9\d{9}$/, "شماره تماس معتبر نیست"),
  department: yup.string().required("انتخاب بخش الزامی است"),
  role: yup.string().required("انتخاب نقش الزامی است"),
  password: yup.string().required("رمز عبور الزامی است").min(6, "رمز عبور باید حداقل ۶ کاراکتر باشد"),
  confirmPassword: yup
    .string()
    .required("تکرار رمز عبور الزامی است")
    .oneOf([yup.ref("password")], "رمزهای عبور مطابقت ندارند"),
})

interface LoginDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

export function LoginDialog({ open, onOpenChange }: LoginDialogProps) {
  const router = useRouter()
  const { login, register } = useAuth()
  const [activeTab, setActiveTab] = useState("login")
  const [loginType, setLoginType] = useState("client")
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  
  const loginForm = useForm({
    resolver: yupResolver(loginSchema),
    defaultValues: {
      email: "",
      password: "",
    },
  })

  const signupForm = useForm({
    resolver: yupResolver(signupSchema),
    defaultValues: {
      name: "",
      email: "",
      phone: "",
      department: "",
      role: "client",
      password: "",
      confirmPassword: "",
    },
  })

  const handleLogin = async (data: any) => {
    try {
      const landingPath = await login(data.email, data.password)
      toast({
        title: "ورود موفق",
        description: "به سیستم خوش آمدید",
      })
      onOpenChange(false)
      loginForm.reset()
      router.replace(landingPath)
    } catch (error) {
      toast({
        title: "خطا در ورود",
        description: "ایمیل یا رمز عبور اشتباه است",
        variant: "destructive",
      })
    }
  }

  const handleSignup = async (data: any) => {
    try {
      const success = await register({
        name: data.name,
        email: data.email,
        phone: data.phone,
        department: data.department,
        role: data.role,
        password: data.password,
      })

      if (success) {
        toast({
          title: "ثبت‌نام موفق",
          description: "حساب شما با موفقیت ایجاد شد",
        })
        onOpenChange(false)
        signupForm.reset()
        setActiveTab("login")
      } else {
        toast({
          title: "خطا در ثبت‌نام",
          description: "این ایمیل قبلاً ثبت شده است",
          variant: "destructive",
        })
      }
    } catch (error) {
      toast({
        title: "خطا در ثبت‌نام",
        description: "مشکلی در سیستم رخ داده است",
        variant: "destructive",
      })
    }
  }

  const getRoleIcon = (role: string) => {
    switch (role) {
      case "admin":
        return <Shield className="w-4 h-4" />
      case "technician":
        return <Wrench className="w-4 h-4" />
      default:
        return <User className="w-4 h-4" />
    }
  }

  const getRoleLabel = (role: string) => {
    switch (role) {
      case "admin":
        return "مدیر سیستم"
      case "technician":
        return "تکنسین"
      default:
        return "کاربر"
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] overflow-y-auto w-[95vw] sm:w-[90vw] sm:max-w-lg" dir="rtl">
        <DialogHeader>
          <DialogTitle className="text-right">
            {activeTab === "login" ? "ورود به سیستم" : "ثبت‌نام در سیستم"}
          </DialogTitle>
          <DialogDescription className="text-right">
            {activeTab === "login"
              ? "برای دسترسی به سیستم، اطلاعات خود را وارد کنید"
              : "برای ایجاد حساب کاربری جدید، فرم زیر را تکمیل کنید"}
          </DialogDescription>
        </DialogHeader>

        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full" dir="rtl">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="login" className="gap-2">
              <LogIn className="w-4 h-4" />
              ورود
            </TabsTrigger>
            <TabsTrigger value="signup" className="gap-2">
              <UserPlus className="w-4 h-4" />
              ثبت‌نام
            </TabsTrigger>
          </TabsList>

          <TabsContent value="login" className="space-y-4">
            {/* Role Selection for Login */}
            <div className="space-y-2">
              <Label className="text-right">نوع کاربری</Label>
              <Tabs value={loginType} onValueChange={setLoginType} className="w-full" dir="rtl">
                <TabsList className="grid w-full grid-cols-3">
                  <TabsTrigger value="client" className="gap-1 text-xs">
                    <User className="w-3 h-3" />
                    کاربر
                  </TabsTrigger>
                  <TabsTrigger value="technician" className="gap-1 text-xs">
                    <Wrench className="w-3 h-3" />
                    تکنسین
                  </TabsTrigger>
                  <TabsTrigger value="admin" className="gap-1 text-xs">
                    <Shield className="w-3 h-3" />
                    مدیر
                  </TabsTrigger>
                </TabsList>
              </Tabs>
            </div>

            <form onSubmit={loginForm.handleSubmit(handleLogin)} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="email" className="text-right">
                  ایمیل
                </Label>
                <Controller
                  name="email"
                  control={loginForm.control}
                  render={({ field }) => (
                    <Input {...field} type="email" placeholder="example@domain.com" className="text-right" dir="rtl" />
                  )}
                />
                {loginForm.formState.errors.email && (
                  <p className="text-sm text-red-500 text-right">{loginForm.formState.errors.email.message}</p>
                )}
              </div>

              <div className="space-y-2">
                <Label htmlFor="password" className="text-right">
                  رمز عبور
                </Label>
                <div className="relative">
                  <Controller
                    name="password"
                    control={loginForm.control}
                    render={({ field }) => (
                      <Input
                        {...field}
                        type={showPassword ? "text" : "password"}
                        placeholder="رمز عبور خود را وارد کنید"
                        className="text-right pl-10"
                        dir="rtl"
                      />
                    )}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    className="absolute left-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                    onClick={() => setShowPassword(!showPassword)}
                  >
                    {showPassword ? (
                      <EyeOff className="h-4 w-4 text-gray-400" />
                    ) : (
                      <Eye className="h-4 w-4 text-gray-400" />
                    )}
                  </Button>
                </div>
                {loginForm.formState.errors.password && (
                  <p className="text-sm text-red-500 text-right">{loginForm.formState.errors.password.message}</p>
                )}
              </div>

              <Button type="submit" className="w-full" disabled={loginForm.formState.isSubmitting}>
                {loginForm.formState.isSubmitting ? "در حال ورود..." : "ورود"}
              </Button>
            </form>

            <div className="text-center text-sm text-muted-foreground">
              <p>حساب کاربری ندارید؟</p>
              <Button variant="link" className="p-0 h-auto text-primary" onClick={() => setActiveTab("signup")}>
                ثبت‌نام کنید
              </Button>
            </div>
          </TabsContent>

          <TabsContent value="signup" className="space-y-4">
            <form onSubmit={signupForm.handleSubmit(handleSignup)} className="space-y-4">
              <div className="grid grid-cols-1 gap-4">
                <div className="space-y-2">
                  <Label htmlFor="name" className="text-right">
                    نام و نام خانوادگی *
                  </Label>
                  <Controller
                    name="name"
                    control={signupForm.control}
                    render={({ field }) => (
                      <Input {...field} placeholder="نام کامل خود را وارد کنید" className="text-right" dir="rtl" />
                    )}
                  />
                  {signupForm.formState.errors.name && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.name.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="email" className="text-right">
                    ایمیل *
                  </Label>
                  <Controller
                    name="email"
                    control={signupForm.control}
                    render={({ field }) => (
                      <Input
                        {...field}
                        type="email"
                        placeholder="example@domain.com"
                        className="text-right"
                        dir="rtl"
                      />
                    )}
                  />
                  {signupForm.formState.errors.email && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.email.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="phone" className="text-right">
                    شماره تماس *
                  </Label>
                  <Controller
                    name="phone"
                    control={signupForm.control}
                    render={({ field }) => (
                      <Input {...field} placeholder="09xxxxxxxxx" className="text-right" dir="rtl" />
                    )}
                  />
                  {signupForm.formState.errors.phone && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.phone.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="department" className="text-right">
                    بخش *
                  </Label>
                  <Controller
                    name="department"
                    control={signupForm.control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب بخش" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="it">فناوری اطلاعات</SelectItem>
                          <SelectItem value="hr">منابع انسانی</SelectItem>
                          <SelectItem value="finance">مالی</SelectItem>
                          <SelectItem value="marketing">بازاریابی</SelectItem>
                          <SelectItem value="operations">عملیات</SelectItem>
                          <SelectItem value="accounting">حسابداری</SelectItem>
                          <SelectItem value="sales">فروش</SelectItem>
                          <SelectItem value="other">سایر</SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {signupForm.formState.errors.department && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.department.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="role" className="text-right">
                    نقش *
                  </Label>
                  <Controller
                    name="role"
                    control={signupForm.control}
                    render={({ field }) => (
                      <Select onValueChange={field.onChange} value={field.value} dir="rtl">
                        <SelectTrigger className="text-right">
                          <SelectValue placeholder="انتخاب نقش" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="client">
                            <div className="flex items-center gap-2">
                              <User className="w-4 h-4" />
                              کاربر
                            </div>
                          </SelectItem>
                          <SelectItem value="technician">
                            <div className="flex items-center gap-2">
                              <Wrench className="w-4 h-4" />
                              تکنسین
                            </div>
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    )}
                  />
                  {signupForm.formState.errors.role && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.role.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="password" className="text-right">
                    رمز عبور *
                  </Label>
                  <div className="relative">
                    <Controller
                      name="password"
                      control={signupForm.control}
                      render={({ field }) => (
                        <Input
                          {...field}
                          type={showPassword ? "text" : "password"}
                          placeholder="حداقل ۶ کاراکتر"
                          className="text-right pl-10"
                          dir="rtl"
                        />
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="absolute left-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                      onClick={() => setShowPassword(!showPassword)}
                    >
                      {showPassword ? (
                        <EyeOff className="h-4 w-4 text-gray-400" />
                      ) : (
                        <Eye className="h-4 w-4 text-gray-400" />
                      )}
                    </Button>
                  </div>
                  {signupForm.formState.errors.password && (
                    <p className="text-sm text-red-500 text-right">{signupForm.formState.errors.password.message}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="confirmPassword" className="text-right">
                    تکرار رمز عبور *
                  </Label>
                  <div className="relative">
                    <Controller
                      name="confirmPassword"
                      control={signupForm.control}
                      render={({ field }) => (
                        <Input
                          {...field}
                          type={showConfirmPassword ? "text" : "password"}
                          placeholder="رمز عبور را مجدداً وارد کنید"
                          className="text-right pl-10"
                          dir="rtl"
                        />
                      )}
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="absolute left-0 top-0 h-full px-3 py-2 hover:bg-transparent"
                      onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                    >
                      {showConfirmPassword ? (
                        <EyeOff className="h-4 w-4 text-gray-400" />
                      ) : (
                        <Eye className="h-4 w-4 text-gray-400" />
                      )}
                    </Button>
                  </div>
                  {signupForm.formState.errors.confirmPassword && (
                    <p className="text-sm text-red-500 text-right">
                      {signupForm.formState.errors.confirmPassword.message}
                    </p>
                  )}
                </div>
              </div>

              <Button type="submit" className="w-full" disabled={signupForm.formState.isSubmitting}>
                {signupForm.formState.isSubmitting ? "در حال ثبت‌نام..." : "ثبت‌نام"}
              </Button>
            </form>

            <div className="text-center text-sm text-muted-foreground">
              <p>قبلاً ثبت‌نام کرده‌اید؟</p>
              <Button variant="link" className="p-0 h-auto text-primary" onClick={() => setActiveTab("login")}>
                وارد شوید
              </Button>
            </div>
          </TabsContent>
        </Tabs>
      </DialogContent>
    </Dialog>
  )
}


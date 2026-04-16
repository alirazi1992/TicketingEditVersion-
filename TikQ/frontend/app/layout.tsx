import type React from "react"
import type { Metadata } from "next"
import { Inter } from "next/font/google"
import localFont from "next/font/local"
import "./globals.css"
import { ThemeProvider } from "@/components/theme-provider"
import { AuthProvider } from "@/lib/auth-context"
import { PreferencesProvider } from "@/lib/preferences-context"
import { CategoryProvider } from "@/services/useCategories"
import { initialCategoriesData } from "@/data/initial-categories"
import { BackendStatusIndicator } from "@/components/backend-status-indicator"
import { ApiStatusDebug } from "@/components/api-status-debug"
import { RealtimeProvider } from "@/lib/realtime-context"

const inter = Inter({ subsets: ["latin"] })
const iranYekan = localFont({
  src: [
    { path: "../fonts/IRANYekanXVFaNumVF.woff2", weight: "100 900", style: "normal" },
  ],
  display: "swap",
})

export const metadata: Metadata = {
  title: "سیستم مدیریت خدمات IT",
  description: "سیستم مدیریت درخواست‌های فنی و پشتیبانی",
    generator: 'Ali_Razi'
}

export default function RootLayout({
  children,
}: {
  children: React.ReactNode
}) {
  return (
    <html lang="fa" dir="rtl" suppressHydrationWarning>
      <body className={`${inter.className} ${iranYekan.className} font-iran bg-background text-foreground antialiased`}>
        <ThemeProvider>
          <AuthProvider>
            <RealtimeProvider>
              <PreferencesProvider>
                <CategoryProvider initial={initialCategoriesData}>
                  <BackendStatusIndicator />
                  <ApiStatusDebug />
                  {children}
                </CategoryProvider>
              </PreferencesProvider>
            </RealtimeProvider>
          </AuthProvider>
        </ThemeProvider>
      </body>
    </html>
  )
}
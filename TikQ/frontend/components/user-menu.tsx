"use client"

import { useState } from "react"
import { Button } from "@/components/ui/button"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar"
import { Badge } from "@/components/ui/badge"
import { useAuth } from "@/lib/auth-context"
import { SettingsDialog } from "./settings-dialog"
import { User, Settings, LogOut, Shield, Wrench } from "lucide-react"

export function UserMenu() {
  const { user, logout } = useAuth()
  const [settingsOpen, setSettingsOpen] = useState(false)

  if (!user) return null

  const getRoleIcon = (role: string) => {
    switch (role) {
      case "admin":
        return <Shield className="w-3 h-3" />
      case "technician":
        return <Wrench className="w-3 h-3" />
      default:
        return <User className="w-3 h-3" />
    }
  }

  const getRoleLabel = (role: string, isSupervisor?: boolean) => {
    switch (role) {
      case "admin":
        return "مدیر سیستم"
      case "technician":
        return isSupervisor ? "سرپرست فنی" : "تکنسین"
      default:
        return "کاربر"
    }
  }

  const getRoleBadgeColor = (role: string) => {
    switch (role) {
      case "admin":
        return "bg-purple-100 text-purple-800 border-purple-200"
      case "technician":
        return "bg-blue-100 text-blue-800 border-blue-200"
      default:
        return "bg-green-100 text-green-800 border-green-200"
    }
  }

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="ghost" className="relative h-10 w-10 rounded-full">
            <Avatar className="h-10 w-10">
              <AvatarImage src={user.avatar ?? "/placeholder.svg"} alt={user.name} />
              <AvatarFallback>{user.name.charAt(0)}</AvatarFallback>
            </Avatar>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent className="w-80" align="end" forceMount>
          <DropdownMenuLabel className="font-normal">
            <div className="flex flex-col space-y-2" dir="rtl">
              <div className="flex items-center gap-3">
                <Avatar className="h-10 w-10">
                  <AvatarImage src={user.avatar ?? "/placeholder.svg"} alt={user.name} />
                  <AvatarFallback className="text-sm">{user.name.charAt(0)}</AvatarFallback>
                </Avatar>
                <div className="flex flex-col space-y-1 flex-1 text-right">
                  <p className="text-sm font-medium leading-none text-right">{user.name}</p>
                  <p className="text-xs leading-none text-muted-foreground text-right">{user.email}</p>
                </div>
                <div className="flex items-center">
                  <div className="w-2 h-2 bg-green-500 rounded-full"></div>
                </div>
              </div>
              <div className="flex items-center gap-2 justify-end">
                <Badge className={getRoleBadgeColor(user.role)}>
                  <div className="flex items-center gap-1">
                    <span>{getRoleLabel(user.role, user.isSupervisor)}</span>
                    {getRoleIcon(user.role)}
                  </div>
                </Badge>
                {user.department && <span className="text-xs text-muted-foreground">{user.department}</span>}
              </div>
              {user.phone && <p className="text-xs text-muted-foreground text-right">📱 {user.phone}</p>}
            </div>
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={() => setSettingsOpen(true)} className="justify-end">
            <div className="flex items-center gap-2">
              <span>تنظیمات حساب</span>
              <Settings className="h-4 w-4" />
            </div>
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem onClick={logout} className="text-red-600 justify-end">
            <div className="flex items-center gap-2">
              <span>خروج</span>
              <LogOut className="h-4 w-4" />
            </div>
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>

      <SettingsDialog open={settingsOpen} onOpenChange={setSettingsOpen} />
    </>
  )
}

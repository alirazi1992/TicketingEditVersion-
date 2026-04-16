"use client"

import type React from "react"

import { useEffect, useState } from "react"
import type { TechnicianProfile } from "@/data/technician-profiles"
import type { Ticket } from "@/types"
import { AdminTicketManagement } from "./admin-ticket-management"
import { AdminTechnicianAssignment } from "./admin-technician-assignment"
import { CategoryManagement } from "./category-management"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { TicketIcon, UserPlus, FolderTree, Users, FileDown, Settings } from "lucide-react"
import { EnhancedAutoAssignment } from "./enhanced-auto-assignment"
import { TechnicianManagement } from "./technician-management"
import { AdminReports } from "./admin-reports"

interface AdminDashboardProps {
  tickets: Ticket[]
  onTicketUpdate: (ticketId: string, updates: Partial<Ticket>) => void
  onRefreshTickets?: () => void | Promise<void>
  technicians: TechnicianProfile[]
  categoriesData: any
  onCategoryUpdate: (categories: any) => void
  authToken?: string | null
  activeSection?: "tickets" | "assignment" | "categories" | "auto-settings" | "technicians" | "reports"
}

export const AdminDashboard: React.FC<AdminDashboardProps> = ({
  tickets,
  onTicketUpdate,
  onRefreshTickets,
  technicians,
  categoriesData,
  onCategoryUpdate,
  authToken,
  activeSection,
}) => {
  const [activeTab, setActiveTab] = useState("tickets")

  useEffect(() => {
    if (activeSection) {
      setActiveTab(activeSection)
    }
  }, [activeSection])

  return (
    <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full" dir="rtl">
      <TabsList className="grid w-full grid-cols-6">
        <TabsTrigger value="tickets" className="gap-2">
          <TicketIcon className="w-4 h-4" />
          مدیریت کامل تیکت‌ها
        </TabsTrigger>
        <TabsTrigger value="assignment" className="gap-2">
          <UserPlus className="w-4 h-4" />
          تعیین تکنسین
        </TabsTrigger>
        <TabsTrigger value="technicians" className="gap-2">
          <Users className="w-4 h-4" />
          مدیریت تکنسین‌ها
        </TabsTrigger>
        <TabsTrigger value="categories" className="gap-2">
          <FolderTree className="w-4 h-4" />
          مدیریت دسته‌بندی
        </TabsTrigger>
        <TabsTrigger value="reports" className="gap-2">
          <FileDown className="w-4 h-4" />
          گزارش‌گیری
        </TabsTrigger>
        <TabsTrigger value="auto-settings" className="gap-2">
          <Settings className="w-4 h-4" />
          تنظیمات خودکار
        </TabsTrigger>
      </TabsList>

      <TabsContent value="tickets">
        <AdminTicketManagement
          technicians={technicians}
          authToken={authToken}
          tickets={tickets}
          onRefreshTickets={onRefreshTickets}
        />
      </TabsContent>

      <TabsContent value="assignment">
        <AdminTechnicianAssignment
          tickets={tickets}
          technicians={technicians}
          onTicketUpdate={onTicketUpdate}
          authToken={authToken}
        />
      </TabsContent>

      <TabsContent value="technicians">
        <TechnicianManagement />
      </TabsContent>

      <TabsContent value="categories">
        <CategoryManagement categoriesData={categoriesData} onCategoryUpdate={onCategoryUpdate} />
      </TabsContent>

      <TabsContent value="reports">
        <AdminReports />
      </TabsContent>

      <TabsContent value="auto-settings">
        <EnhancedAutoAssignment 
          tickets={tickets} 
          technicians={technicians} 
          onTicketUpdate={onTicketUpdate} 
          onTabChange={setActiveTab}
        />
      </TabsContent>
    </Tabs>
  )
}

"use client";

import { RoleGuard } from "@/components/role-guard";
import { MainDashboard } from "@/components/main-dashboard";

export default function AdminPage() {
  return (
    <RoleGuard requiredPath="/admin">
      <MainDashboard />
    </RoleGuard>
  );
}

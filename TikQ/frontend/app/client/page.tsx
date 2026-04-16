"use client";

import { RoleGuard } from "@/components/role-guard";
import { MainDashboard } from "@/components/main-dashboard";

export default function ClientPage() {
  return (
    <RoleGuard requiredPath="/client">
      <MainDashboard />
    </RoleGuard>
  );
}

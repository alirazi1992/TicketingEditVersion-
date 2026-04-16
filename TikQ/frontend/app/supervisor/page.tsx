"use client";

import { RoleGuard } from "@/components/role-guard";
import { MainDashboard } from "@/components/main-dashboard";

export default function SupervisorPage() {
  return (
    <RoleGuard requiredPath="/supervisor">
      <MainDashboard />
    </RoleGuard>
  );
}

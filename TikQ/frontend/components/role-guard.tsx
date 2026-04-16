"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { getLandingPathFromSession } from "@/lib/auth-routing";

/**
 * Protects dashboard routes: only allows access when session.landingPath matches requiredPath.
 * Otherwise redirects to session.landingPath (or /login if unauthenticated).
 */
export function RoleGuard({
  requiredPath,
  children,
}: {
  requiredPath: "/admin" | "/supervisor" | "/technician" | "/client";
  children: ReactNode;
}) {
  const router = useRouter();
  const { user, isLoading } = useAuth();

  useEffect(() => {
    if (isLoading) return;
    if (!user) {
      router.replace("/login");
      return;
    }
    const landingPath = getLandingPathFromSession({ user });
    if (landingPath !== requiredPath) {
      router.replace(landingPath);
    }
  }, [user, isLoading, requiredPath, router]);

  if (isLoading || !user) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-950 text-slate-100">
        <div className="text-center space-y-2">
          <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-slate-300 mx-auto" />
          <p className="text-sm text-slate-300">Loading...</p>
        </div>
      </div>
    );
  }

  const landingPath = getLandingPathFromSession({ user });
  if (landingPath !== requiredPath) {
    return null;
  }

  return <>{children}</>;
}

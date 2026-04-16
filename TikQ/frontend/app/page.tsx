"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/lib/auth-context";
import { getLandingPathFromSession } from "@/lib/auth-routing";

/**
 * Root "/" redirects: authenticated → landingPath from backend; unauthenticated → /login.
 * Dashboard content lives on /admin, /supervisor, /technician, /client with RoleGuard.
 */
export default function Home() {
  const { user, isLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (isLoading) return;
    if (!user) {
      router.replace("/login");
      return;
    }
    const landingPath = getLandingPathFromSession({ user });
    router.replace(landingPath);
  }, [user, isLoading, router]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-950 text-slate-100">
      <div className="text-center space-y-2">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-slate-300 mx-auto" />
        <p className="text-sm text-slate-300">Loading...</p>
      </div>
    </div>
  );
}

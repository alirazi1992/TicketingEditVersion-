"use client";

import { createContext, useContext, useEffect, useState, type ReactNode } from "react";
import { apiRequest } from "@/lib/api-client";
import type { ApiAuthResponse, ApiUserDto, ApiUserRole } from "@/lib/api-types";
import { getLandingPath } from "@/lib/auth-routing";

export interface User {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  department?: string | null;
  role: "client" | "technician" | "admin";
  avatar?: string | null;
  isSupervisor?: boolean;
  /** From backend; use for routing. Prefer over deriving from role. */
  landingPath?: string;
}

interface AuthContextType {
  user: User | null;
  token: string | null;
  login: (email: string, password: string) => Promise<string>;
  /** Break-glass admin: separate route; requires email + password + emergencyKey. Use only when server/directory unavailable. */
  emergencyLogin: (email: string, password: string, emergencyKey: string) => Promise<string>;
  register: (userData: {
    name: string;
    email: string;
    phone: string;
    department: string;
    role: string;
    password: string;
  }) => Promise<boolean>;
  logout: () => void | Promise<void>;
  updateProfile: (
    updates: Partial<Omit<User, "id" | "role">>
  ) => Promise<boolean>;
  changePassword: (
    currentPassword: string,
    newPassword: string,
    confirmNewPassword: string
  ) => Promise<boolean>;
  isLoading: boolean;
}

const USER_STORAGE_KEY = "ticketing.auth.user";
/** Auth is cookie-based; we never store JWT in localStorage. Token in context is always null for browser. */
const AuthContext = createContext<AuthContextType | undefined>(undefined);

/**
 * Map backend role → frontend role (lowercase).
 * Supports numeric enums (0,1,2,3), PascalCase strings ("Admin", "Technician", "Client"),
 * and other casing; tolerates "engineer" for backward compatibility.
 */
const roleFromApi = (role: ApiUserRole | string | number | undefined | null): User["role"] => {
  if (role === undefined || role === null) return "client";

  if (typeof role === "number") {
    switch (role) {
      case 2:
        return "admin";
      case 1:
      case 3:
        return "technician";
      default:
        return "client";
    }
  }

  const key = String(role).toLowerCase();
  switch (key) {
    case "admin":
      return "admin";
    case "technician":
    case "supervisor":
    case "engineer":
      return "technician";
    default:
      return "client";
  }
};

/**
 * Map frontend role → backend role
 * Here we return the **string** form so TypeScript is happy with ApiUserRole.
 * (Backend can still interpret this if configured for string enums, or ignore it.)
 */
const roleToApi = (role: string): ApiUserRole => {
  switch (role?.toLowerCase()) {
    case "admin":
      return "Admin";
    case "technician":
    case "engineer":
      return "Technician";
    default:
      return "Client";
  }
};

/** Build User from API DTO; role and isSupervisor can be overridden for login/register responses that put them at top level. */
const mapUser = (
  dto: ApiUserDto,
  landingPathOverride?: string,
  roleOverride?: ApiUserRole | string,
  isSupervisorOverride?: boolean
): User => ({
  id: dto.id,
  name: dto.fullName,
  email: dto.email,
  role: roleFromApi(roleOverride ?? dto.role),
  phone: dto.phoneNumber ?? null,
  department: dto.department ?? null,
  avatar: dto.avatarUrl ?? null,
  isSupervisor: isSupervisorOverride ?? dto.isSupervisor ?? false,
  landingPath: landingPathOverride ?? dto.landingPath ?? undefined,
});

function persistUser(user: User) {
  if (typeof window === "undefined") return;
  localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
  localStorage.setItem("userEmail", user.email);
  localStorage.setItem("userName", user.name);
}

function clearSession() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(USER_STORAGE_KEY);
  localStorage.removeItem("userEmail");
  localStorage.removeItem("userName");
}

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  /** Restore session via cookie (credentials: 'include'). No token passed. */
  const fetchCurrentUser = async () => {
    try {
      const me = await apiRequest<ApiUserDto>("/api/auth/me", {
        silent: true,
      });
      const mapped = mapUser(me);
      setUser(mapped);
      persistUser(mapped);
    } catch (error: any) {
      if (error?.status === 401) {
        console.warn("[AuthContext] Session invalid or expired, clearing");
      }
      clearSession();
      setUser(null);
      setToken(null);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (typeof window === "undefined") {
      setIsLoading(false);
      return;
    }

    // Do NOT restore user from localStorage here — verify session with backend first
    // so we never show a dashboard from stale cache when the cookie is expired.
    const timeoutId = setTimeout(() => setIsLoading(false), 5000);
    fetchCurrentUser()
      .finally(() => clearTimeout(timeoutId));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const login = async (email: string, password: string): Promise<string> => {
    setIsLoading(true);
    try {
      const response = await apiRequest<ApiAuthResponse>("/api/auth/login", {
        method: "POST",
        body: { email, password },
      });
      const roleRaw = response.role ?? response.user?.role;
      const isSupervisor = response.isSupervisor ?? response.user?.isSupervisor;
      const mapped = mapUser(
        response.user!,
        response.landingPath ?? response.user?.landingPath,
        roleRaw,
        isSupervisor
      );
      const landingPath = response.landingPath ?? response.user?.landingPath ?? getLandingPath(mapped);
      setUser(mapped);
      setToken(null); // Cookie holds JWT; frontend never stores it
      persistUser(mapped);
      return landingPath;
    } catch (error: any) {
      // Log the actual error for debugging
      console.error("Login error:", error);
      // Check if it's a network error (backend not running)
      if (error?.message?.includes("fetch") || error?.message?.includes("Failed to fetch")) {
        console.error("Backend may not be running. Check if the API server is running on http://localhost:8080");
      }
      // Return the error message so the UI can display it
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const emergencyLogin = async (email: string, password: string, emergencyKey: string): Promise<string> => {
    setIsLoading(true);
    try {
      const response = await apiRequest<ApiAuthResponse>("/api/auth/emergency-login", {
        method: "POST",
        body: { email, password, emergencyKey },
      });
      const roleRaw = response.role ?? response.user?.role;
      const isSupervisor = response.isSupervisor ?? response.user?.isSupervisor;
      const mapped = mapUser(
        response.user!,
        response.landingPath ?? response.user?.landingPath,
        roleRaw,
        isSupervisor
      );
      const landingPath = response.landingPath ?? response.user?.landingPath ?? getLandingPath(mapped);
      setUser(mapped);
      setToken(null);
      persistUser(mapped);
      return landingPath;
    } catch (error: any) {
      console.error("Emergency login error:", error);
      throw error;
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (userData: {
    name: string;
    email: string;
    phone: string;
    department: string;
    role: string;
    password: string;
  }) => {
    setIsLoading(true);
    try {
      const response = await apiRequest<ApiAuthResponse>("/api/auth/register", {
        method: "POST",
        body: {
          fullName: userData.name,
          email: userData.email,
          password: userData.password,
          role: roleToApi(userData.role),
          phoneNumber: userData.phone,
          department: userData.department,
        },
      });
      const roleRaw = response.role ?? response.user?.role;
      const isSupervisor = response.isSupervisor ?? response.user?.isSupervisor;
      const mapped = mapUser(
        response.user!,
        response.landingPath ?? response.user?.landingPath,
        roleRaw,
        isSupervisor
      );
      setUser(mapped);
      setToken(null);
      persistUser(mapped);
      return true;
    } catch {
      return false;
    } finally {
      setIsLoading(false);
    }
  };

  const logout = async () => {
    try {
      await apiRequest<{ ok: boolean }>("/api/auth/logout", { method: "POST" });
    } catch {
      // Ignore; clear local state anyway
    }
    setUser(null);
    setToken(null);
    clearSession();
  };

  const updateProfile = async (updates: Partial<Omit<User, "id" | "role">>) => {
    if (!user) return false;
    try {
      const payload: Record<string, unknown> = {};
      if (updates.name) payload.fullName = updates.name;
      if (updates.email) payload.email = updates.email;
      if (typeof updates.phone !== "undefined")
        payload.phoneNumber = updates.phone;
      if (typeof updates.department !== "undefined")
        payload.department = updates.department;
      if (typeof updates.avatar !== "undefined")
        payload.avatarUrl = updates.avatar;

      const updated = await apiRequest<ApiUserDto>("/api/auth/me", {
        method: "PUT",
        body: payload,
      });
      const mapped = mapUser(updated);
      setUser(mapped);
      persistUser(mapped);
      return true;
    } catch {
      return false;
    }
  };

  const changePassword = async (
    currentPassword: string,
    newPassword: string,
    confirmNewPassword: string
  ) => {
    if (!user) return false;
    try {
      const response = await apiRequest<{ success: boolean; message: string }>("/api/auth/change-password", {
        method: "POST",
        body: { 
          currentPassword, 
          newPassword, 
          confirmNewPassword 
        },
      });
      return response?.success ?? true;
    } catch (error: any) {
      console.error("Change password error:", error);
      // Re-throw to allow form to handle specific error messages
      throw error;
    }
  };

  const value: AuthContextType = {
    user,
    token,
    login,
    emergencyLogin,
    register,
    logout,
    updateProfile,
    changePassword,
    isLoading,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error("useAuth must be used within an AuthProvider");
  }
  return context;
}
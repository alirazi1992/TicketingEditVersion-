export type LandingPath = "/admin" | "/client" | "/technician" | "/supervisor";

const VALID_LANDING_PATHS: LandingPath[] = ["/admin", "/client", "/technician", "/supervisor"];

function isValidLandingPath(path: string | undefined): path is LandingPath {
  return path != null && VALID_LANDING_PATHS.includes(path as LandingPath);
}

/**
 * User-like shape for computing landing path (role + isSupervisor + optional landingPath).
 */
export interface UserLike {
  role?: string;
  isSupervisor?: boolean;
  landingPath?: string;
}

/**
 * Returns the landing path for a user.
 * Uses user.landingPath from backend when present and valid; otherwise computes from role + isSupervisor.
 * - Admin → /admin
 * - Technician (isSupervisor=false) → /technician
 * - Technician (isSupervisor=true) → /supervisor
 * - Client → /client
 */
export function getLandingPath(user: UserLike | null | undefined): LandingPath {
  if (!user) return "/client";
  if (isValidLandingPath(user.landingPath)) return user.landingPath;
  const role = user.role;
  if (role === "admin") return "/admin";
  if (role === "technician" || role === "engineer") return user.isSupervisor ? "/supervisor" : "/technician";
  return "/client";
}

/**
 * Session shape for routing (user may be partial from storage).
 */
export interface SessionLike {
  user: UserLike | null;
}

/**
 * Returns the landing path for the current session.
 * Prefer session.user.landingPath from the backend as source of truth.
 * Falls back to getLandingPath(session.user) when landingPath is missing (e.g. old tokens).
 */
export function getLandingPathFromSession(session: SessionLike): LandingPath {
  if (!session.user) return "/client";
  return getLandingPath(session.user);
}

import type { ApiUserDto } from "@/lib/api-types"

export type TechnicianAvailability = "available" | "busy" | "offline"

export interface TechnicianMetadata {
  specialties: string[]
  rating: number
  activeTickets: number
  status: TechnicianAvailability
  completedTickets: number
  avgResponseTime: string
  expertise?: string[]
}

export interface TechnicianProfile extends TechnicianMetadata {
  id: string
  name: string
  email: string
  department?: string | null
}

const defaultMetadata: TechnicianMetadata = {
  specialties: ["hardware"],
  rating: 4.2,
  activeTickets: 0,
  status: "available",
  completedTickets: 10,
  avgResponseTime: "2.0",
  expertise: [],
}

export const technicianMetadataByEmail: Record<string, TechnicianMetadata> = {
  "tech1@test.com": {
    specialties: ["network", "hardware"],
    rating: 4.8,
    activeTickets: 3,
    status: "available",
    completedTickets: 45,
    avgResponseTime: "1.5",
    expertise: ["شبکه های بی سیم", "VPN", "دسترسی راه دور"],
  },
  "tech2@test.com": {
    specialties: ["software", "security"],
    rating: 4.7,
    activeTickets: 2,
    status: "available",
    completedTickets: 38,
    avgResponseTime: "1.8",
    expertise: ["Windows", "Office", "ضدویروس"],
  },
}

export function buildTechnicianProfile(user: ApiUserDto): TechnicianProfile {
  const overrides = technicianMetadataByEmail[user.email.toLowerCase()] ?? defaultMetadata
  return {
    id: user.id,
    name: user.fullName,
    email: user.email,
    department: user.department ?? null,
    specialties: overrides.specialties,
    rating: overrides.rating,
    activeTickets: overrides.activeTickets,
    status: overrides.status,
    completedTickets: overrides.completedTickets,
    avgResponseTime: overrides.avgResponseTime,
    expertise: overrides.expertise ?? [],
  }
}

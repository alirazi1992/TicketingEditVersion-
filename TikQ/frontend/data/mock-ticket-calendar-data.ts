export type MockTicket = {
  id: string
  title: string
  description?: string
  status: "open" | "in-progress" | "resolved" | "closed"
  priority?: "low" | "medium" | "high" | "urgent"
  category?: string
  clientName?: string
  assignedTechnicianName?: string | null
  createdAt: string
  updatedAt?: string | null
}

const technicians = [
  "Ali Rahimi",
  "Sara Hosseini",
  "Hossein Karimi",
  "Fatemeh Haddad",
  "Reza Mohammadi",
  "Neda Kazemi",
]

const addHours = (base: Date, hours: number) => {
  const copy = new Date(base)
  copy.setHours(copy.getHours() + hours)
  return copy.toISOString()
}

const makeTicket = (
  id: number,
  date: Date,
  overrides: Partial<MockTicket> = {},
): MockTicket => {
  const defaults: MockTicket = {
    id: `TK-2024-${id.toString().padStart(3, "0")}`,
    title: "Sample ticket title",
    description: "Sample ticket description showing the type of request.",
    status: "open",
    priority: "medium",
    category: "hardware",
    clientName: "Example Client",
    assignedTechnicianName: null,
    createdAt: date.toISOString(),
    updatedAt: addHours(date, 4),
  }

  return {
    ...defaults,
    ...overrides,
  }
}

export const mockTicketCalendarData: MockTicket[] = [
  makeTicket(1, new Date("2024-03-02T07:35:00Z"), {
    title: "Firewall latency after rules change",
    status: "in-progress",
    category: "network",
    priority: "urgent",
    assignedTechnicianName: technicians[0],
  }),
  makeTicket(2, new Date("2024-03-02T09:15:00Z"), {
    title: "Laptop keyboard replacement request",
    status: "open",
    category: "hardware",
    priority: "medium",
    assignedTechnicianName: technicians[3],
  }),
  makeTicket(3, new Date("2024-03-03T10:10:00Z"), {
    title: "VPN client fails to connect",
    status: "resolved",
    category: "network",
    priority: "high",
    assignedTechnicianName: technicians[1],
  }),
  makeTicket(4, new Date("2024-03-03T13:45:00Z"), {
    title: "New employee account provisioning",
    status: "closed",
    category: "access",
    priority: "low",
    assignedTechnicianName: technicians[4],
  }),
  makeTicket(5, new Date("2024-03-04T08:00:00Z"), {
    title: "Shared printer offline in floor 3",
    status: "in-progress",
    category: "hardware",
    priority: "high",
    assignedTechnicianName: technicians[2],
  }),
  makeTicket(6, new Date("2024-03-04T11:30:00Z"), {
    title: "Password reset escalation",
    status: "resolved",
    category: "security",
    priority: "medium",
    assignedTechnicianName: technicians[5],
  }),
  makeTicket(7, new Date("2024-03-05T09:20:00Z"), {
    title: "CRM integration timeout",
    status: "open",
    category: "software",
    priority: "urgent",
    assignedTechnicianName: technicians[0],
  }),
  makeTicket(8, new Date("2024-03-05T12:10:00Z"), {
    title: "Conference room Wi-Fi dropouts",
    status: "in-progress",
    category: "network",
    priority: "high",
    assignedTechnicianName: technicians[1],
  }),
  makeTicket(9, new Date("2024-03-06T07:50:00Z"), {
    title: "Email distribution list update",
    status: "resolved",
    category: "email",
    priority: "medium",
    assignedTechnicianName: technicians[3],
  }),
  makeTicket(10, new Date("2024-03-06T15:25:00Z"), {
    title: "Security incident follow-up",
    status: "closed",
    category: "security",
    priority: "high",
    assignedTechnicianName: technicians[2],
  }),
  makeTicket(11, new Date("2024-03-08T08:40:00Z"), {
    title: "Software license renewal tracking",
    status: "open",
    category: "software",
    priority: "low",
    assignedTechnicianName: technicians[4],
  }),
  makeTicket(12, new Date("2024-03-09T09:10:00Z"), {
    title: "Video conference audio echo",
    status: "in-progress",
    category: "hardware",
    priority: "medium",
    assignedTechnicianName: technicians[5],
  }),
  makeTicket(13, new Date("2024-03-09T10:30:00Z"), {
    title: "Overdue backup verification",
    status: "open",
    category: "security",
    priority: "high",
    assignedTechnicianName: technicians[0],
  }),
  makeTicket(14, new Date("2024-03-10T08:05:00Z"), {
    title: "Warehouse handheld scanner setup",
    status: "resolved",
    category: "hardware",
    priority: "medium",
    assignedTechnicianName: technicians[3],
  }),
  makeTicket(15, new Date("2024-03-10T12:50:00Z"), {
    title: "Customer portal access denied",
    status: "in-progress",
    category: "access",
    priority: "high",
    assignedTechnicianName: technicians[4],
  }),
]


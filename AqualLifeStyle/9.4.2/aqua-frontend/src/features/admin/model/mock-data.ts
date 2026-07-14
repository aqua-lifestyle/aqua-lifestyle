import type { AdminDashboardData } from "./dashboard";

const daysAgo = (days: number) =>
  new Date(Date.now() - days * 86_400_000).toISOString();

export const mockAdminDashboard: AdminDashboardData = {
  activity: [
    {
      description: "Thandi Mokoena joined the Jasper tier.",
      id: "member-demo-1",
      kind: "member",
      meta: "Jasper",
      timestamp: daysAgo(0),
      title: "New member",
    },
    {
      description: "Lerato Nkosi placed an order for R 2,450.",
      id: "order-demo-1",
      kind: "order",
      meta: "Reserved",
      timestamp: daysAgo(1),
      title: "Order #1243",
    },
    {
      description: "Kabelo Dlamini submitted an enquiry.",
      id: "enquiry-demo-1",
      kind: "enquiry",
      meta: "Pending",
      timestamp: daysAgo(2),
      title: "Enquiry #89",
    },
  ],
  leaders: { pendingApplications: 7, total: 18 },
  members: {
    active: 210,
    byTier: [
      { name: "Jasper", value: 120 },
      { name: "Onyx", value: 85 },
      { name: "AQG", value: 40 },
    ],
    inactive: 35,
    recent: [
      { id: 1, joinedAt: daysAgo(0), name: "Thandi Mokoena", tier: "Jasper" },
      { id: 2, joinedAt: daysAgo(1), name: "Lerato Nkosi", tier: "Onyx" },
      { id: 3, joinedAt: daysAgo(2), name: "Kabelo Dlamini", tier: "AQG" },
      { id: 4, joinedAt: daysAgo(3), name: "Naledi Khumalo", tier: "Jasper" },
      { id: 5, joinedAt: daysAgo(4), name: "Sibusiso Ndlovu", tier: "Onyx" },
    ],
  },
  orders: {
    monthRevenue: 31_780,
    monthVolume: 46,
    recent: [
      { amount: 2450, createdAt: daysAgo(0), id: 1243, memberName: "Lerato Nkosi", status: "Reserved" },
      { amount: 1890, createdAt: daysAgo(1), id: 1242, memberName: "Naledi Khumalo", status: "Completed" },
      { amount: 3200, createdAt: daysAgo(2), id: 1241, memberName: "Thandi Mokoena", status: "Reserved" },
    ],
  },
  people: { recentReferrals: 24, totalFacilitators: 63 },
  savings: {
    interestAccrued: 6_750,
    source: "fallback",
    total: 45_000,
  },
  source: "fallback",
  stats: {
    totalEnquiries: 89,
    totalMembers: 245,
    totalOrders: 1243,
    totalRevenue: 85_000,
    totalSavings: 45_000,
  },
};

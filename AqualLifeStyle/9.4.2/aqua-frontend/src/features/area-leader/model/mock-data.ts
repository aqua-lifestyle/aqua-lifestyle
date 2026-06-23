import type { AreaLeaderDashboardData } from "./dashboard";

const daysAgo = (days: number) => new Date(Date.now() - days * 86_400_000).toISOString();

export const mockAreaLeaderDashboard: AreaLeaderDashboardData = {
  activities: [
    { description: "Lerato Nkosi placed an order for R 2,450.", id: "demo-order", kind: "order", meta: "Processing", timestamp: daysAgo(0), title: "Order #1243" },
    { description: "Thandi Mokoena joined the Area Space.", id: "demo-member", kind: "member", meta: "New member", timestamp: daysAgo(1), title: "Member registered" },
    { description: "Kabelo Dlamini submitted a product enquiry.", id: "demo-enquiry", kind: "enquiry", meta: "Pending", timestamp: daysAgo(2), title: "Enquiry #89" },
  ],
  areaSpace: { address: "42 Waterfall Avenue, Midrand", capacity: "120 members", id: 7, interestedMembers: 18, name: "Midrand Aqua Hub", presentationsCompleted: 6, statusText: "Approved" },
  pendingFacilitators: [
    { customerName: "Naledi Khumalo", directReferrals: 4, id: 31 },
    { customerName: "Sibusiso Ndlovu", directReferrals: 2, id: 32 },
  ],
  recentMembers: [
    { id: 1, name: "Thandi Mokoena" }, { id: 2, name: "Lerato Nkosi" },
    { id: 3, name: "Kabelo Dlamini" }, { id: 4, name: "Naledi Khumalo" },
  ],
  recentOrders: [
    { amount: 2450, createdAt: daysAgo(0), customerName: "Lerato Nkosi", id: 1243, status: 0, statusText: "Pending" },
    { amount: 1890, createdAt: daysAgo(1), customerName: "Thandi Mokoena", id: 1242, status: 2, statusText: "Completed" },
  ],
  stats: { pendingApprovals: 2, totalFacilitators: 6, totalMembers: 45, totalOrders: 28, totalRevenue: 12000 },
};

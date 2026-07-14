import type { AreaLeader, AreaSpace, Customer, Enquiry, Facilitator, OrderIntent } from "@/src/providers";
import type { AreaSpaceSummary } from "../ui/area-space-management";
import type { PendingFacilitator } from "../ui/facilitator-approval";
import type { AreaLeaderStats } from "../ui/kpi-cards";
import type { AreaLeaderOrder } from "../ui/order-management";
import type { AreaLeaderActivity } from "../ui/recent-activity";

export type AreaLeaderDashboardData = {
  activities: AreaLeaderActivity[];
  areaSpace: AreaSpaceSummary | null;
  pendingFacilitators: PendingFacilitator[];
  recentMembers: Array<{ id: number; name: string }>;
  recentOrders: AreaLeaderOrder[];
  stats: AreaLeaderStats;
};

type BuildDashboardInput = {
  areaLeaders: AreaLeader[];
  areaSpaces: AreaSpace[];
  customers: Customer[];
  enquiries: Enquiry[];
  facilitators: Facilitator[];
  myCustomerId: number | null;
  now?: Date;
  orders: OrderIntent[];
};

export const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    maximumFractionDigits: 0,
    style: "currency",
  }).format(amount);

const isSameMonth = (value: string, now: Date) => {
  const date = new Date(value);
  return date.getFullYear() === now.getFullYear() && date.getMonth() === now.getMonth();
};

const newestFirst = (left: string, right: string) =>
  new Date(right).getTime() - new Date(left).getTime();

export const buildAreaLeaderDashboard = ({
  areaLeaders,
  areaSpaces,
  customers,
  enquiries,
  facilitators,
  myCustomerId,
  now = new Date(),
  orders,
}: BuildDashboardInput): AreaLeaderDashboardData => {
  const leader = areaLeaders.find((item) => item.customerId === myCustomerId) ?? areaLeaders[0];
  const scopedFacilitators = leader
    ? facilitators.filter((item) => item.areaLeaderId === leader.id)
    : facilitators;
  const customerById = new Map(customers.map((customer) => [customer.id, customer]));
  const monthOrders = orders.filter((order) => isSameMonth(order.createdAt, now));
  const recentOrders = [...orders]
    .sort((a, b) => newestFirst(a.createdAt, b.createdAt))
    .slice(0, 5)
    .map((order) => ({
      amount: order.reservedPrice,
      createdAt: order.createdAt,
      customerName: customerById.get(order.customerId)?.name ?? `Member #${order.customerId}`,
      id: order.id,
      status: order.status,
      statusText: order.statusText,
    }));
  const activities: AreaLeaderActivity[] = [
    ...recentOrders.map((order) => ({
      description: `${order.customerName} placed an order for ${formatCurrency(order.amount)}.`,
      id: `order-${order.id}`,
      kind: "order" as const,
      meta: order.statusText,
      timestamp: order.createdAt,
      title: `Order #${order.id}`,
    })),
    ...enquiries.map((enquiry) => ({
      description: `${customerById.get(enquiry.customerId)?.name ?? `Member #${enquiry.customerId}`} submitted an enquiry.`,
      id: `enquiry-${enquiry.id}`,
      kind: "enquiry" as const,
      meta: enquiry.isPending ? "Pending" : "Updated",
      timestamp: enquiry.createdAt,
      title: `Enquiry #${enquiry.id}`,
    })),
  ].sort((a, b) => newestFirst(a.timestamp, b.timestamp)).slice(0, 8);
  const space = leader
    ? areaSpaces.find((item) => item.areaLeaderId === leader.id) ?? null
    : areaSpaces[0] ?? null;

  return {
    activities,
    areaSpace: space ? {
      address: space.addressLine,
      capacity: space.capacity,
      id: space.id,
      interestedMembers: space.interestedMembers,
      name: `Area Space ${space.id}`,
      presentationsCompleted: space.presentationsCompleted,
      statusText: ["Applied", "Under review", "Approved", "Suspended"][space.status] ?? "Unknown",
    } : null,
    pendingFacilitators: scopedFacilitators
      .filter((item) => item.isApproved === false)
      .map((item) => ({
        customerName: customerById.get(item.customerId)?.name ?? `Applicant #${item.customerId}`,
        directReferrals: item.directReferrals,
        id: item.id,
      })),
    recentMembers: [...customers].sort((a, b) => b.id - a.id).slice(0, 5).map(({ id, name }) => ({ id, name })),
    recentOrders,
    stats: {
      pendingApprovals: scopedFacilitators.filter((item) => item.isApproved === false).length,
      totalFacilitators: scopedFacilitators.filter((item) => item.isApproved !== false).length,
      totalMembers: customers.filter((customer) => customer.isActive).length,
      totalOrders: monthOrders.length,
      totalRevenue: monthOrders.reduce((total, order) => total + order.reservedPrice, 0),
    },
  };
};

export type DashboardMember = {
  id: number;
  name: string;
  tier: string;
  joinedAt: string | null;
};

export type DashboardOrder = {
  id: number;
  memberName: string;
  amount: number;
  status: string;
  createdAt: string;
};

export type DashboardActivity = {
  description: string;
  id: string;
  kind: "enquiry" | "member" | "order";
  meta: string;
  timestamp: string;
  title: string;
};

export type AdminDashboardData = {
  activity: DashboardActivity[];
  leaders: {
    pendingApplications: number | null;
    total: number;
  };
  members: {
    active: number;
    byTier: Array<{ name: string; value: number }>;
    inactive: number;
    recent: DashboardMember[];
  };
  orders: {
    monthRevenue: number;
    monthVolume: number;
    recent: DashboardOrder[];
  };
  people: {
    recentReferrals: number;
    totalFacilitators: number;
  };
  savings: {
    interestAccrued: number | null;
    total: number | null;
  };
  stats: {
    totalEnquiries: number;
    totalCustomerAccounts: number;
    totalOrders: number;
    totalRevenue: number;
    totalSavings: number | null;
  };
};

export type DashboardCustomerInput = {
  createdAt?: string;
  id: number;
  isActive: boolean;
  membershipId: number | null;
  name: string;
};

export type DashboardMembershipInput = {
  id: number;
  name: string;
};

export type DashboardOrderInput = {
  createdAt: string;
  customerId: number;
  id: number;
  reservedPrice: number;
  statusText: string;
};

export type DashboardEnquiryInput = {
  createdAt: string;
  customerId: number;
  id: number;
  isPending: boolean;
};

export type DashboardReferralInput = {
  convertedAt: string | null;
};

export type DashboardInputs = {
  areaLeaderCount: number;
  customers: DashboardCustomerInput[];
  enquiries: DashboardEnquiryInput[];
  facilitatorCount: number;
  memberships: DashboardMembershipInput[];
  now?: Date;
  orders: DashboardOrderInput[];
  referrals: DashboardReferralInput[];
};

const newestFirst = (left: string | null, right: string | null) =>
  new Date(right ?? 0).getTime() - new Date(left ?? 0).getTime();

const isSameMonth = (value: string, now: Date) => {
  const date = new Date(value);
  return (
    date.getFullYear() === now.getFullYear() &&
    date.getMonth() === now.getMonth()
  );
};

export const buildAdminDashboard = ({
  areaLeaderCount,
  customers,
  enquiries,
  facilitatorCount,
  memberships,
  now = new Date(),
  orders,
  referrals,
}: DashboardInputs): AdminDashboardData => {
  const customerById = new Map(customers.map((customer) => [customer.id, customer]));
  const membershipById = new Map(
    memberships.map((membership) => [membership.id, membership.name]),
  );
  const tierCounts = new Map<string, number>();

  customers.forEach((customer) => {
    const tier = customer.membershipId
      ? (membershipById.get(customer.membershipId) ?? "Unassigned")
      : "Unassigned";
    tierCounts.set(tier, (tierCounts.get(tier) ?? 0) + 1);
  });

  const recentMembers = [...customers]
    .sort((a, b) => newestFirst(a.createdAt ?? null, b.createdAt ?? null))
    .slice(0, 5)
    .map((customer) => ({
      id: customer.id,
      joinedAt: customer.createdAt ?? null,
      name: customer.name,
      tier: customer.membershipId
        ? (membershipById.get(customer.membershipId) ?? "Unassigned")
        : "Unassigned",
    }));

  const recentOrders = [...orders]
    .sort((a, b) => newestFirst(a.createdAt, b.createdAt))
    .slice(0, 5)
    .map((order) => ({
      amount: order.reservedPrice,
      createdAt: order.createdAt,
      id: order.id,
      memberName: customerById.get(order.customerId)?.name ?? `Member ${order.customerId}`,
      status: order.statusText,
    }));

  const memberActivity: DashboardActivity[] = recentMembers
    .filter((member): member is DashboardMember & { joinedAt: string } => Boolean(member.joinedAt))
    .map((member) => ({
      description: `${member.name} joined the ${member.tier} tier.`,
      id: `member-${member.id}`,
      kind: "member",
      meta: member.tier,
      timestamp: member.joinedAt,
      title: "New member",
    }));
  const orderActivity: DashboardActivity[] = recentOrders.map((order) => ({
    description: `${order.memberName} placed an order for ${formatCurrency(order.amount)}.`,
    id: `order-${order.id}`,
    kind: "order",
    meta: order.status,
    timestamp: order.createdAt,
    title: `Order #${order.id}`,
  }));
  const enquiryActivity: DashboardActivity[] = enquiries.map((enquiry) => ({
    description: `${customerById.get(enquiry.customerId)?.name ?? `Member ${enquiry.customerId}`} submitted an enquiry.`,
    id: `enquiry-${enquiry.id}`,
    kind: "enquiry",
    meta: enquiry.isPending ? "Pending" : "Updated",
    timestamp: enquiry.createdAt,
    title: `Enquiry #${enquiry.id}`,
  }));
  const monthOrders = orders.filter((order) => isSameMonth(order.createdAt, now));
  const totalRevenue = orders.reduce((total, order) => total + order.reservedPrice, 0);

  return {
    activity: [...memberActivity, ...orderActivity, ...enquiryActivity]
      .sort((a, b) => newestFirst(a.timestamp, b.timestamp))
      .slice(0, 8),
    leaders: {
      pendingApplications: null,
      total: areaLeaderCount,
    },
    members: {
      active: customers.filter((customer) => customer.isActive).length,
      byTier: [...tierCounts].map(([name, value]) => ({ name, value })),
      inactive: customers.filter((customer) => !customer.isActive).length,
      recent: recentMembers,
    },
    orders: {
      monthRevenue: monthOrders.reduce(
        (total, order) => total + order.reservedPrice,
        0,
      ),
      monthVolume: monthOrders.length,
      recent: recentOrders,
    },
    people: {
      recentReferrals: referrals.filter(
        (referral) => referral.convertedAt && isSameMonth(referral.convertedAt, now),
      ).length,
      totalFacilitators: facilitatorCount,
    },
    savings: {
      interestAccrued: null,
      total: null,
    },
    stats: {
      totalEnquiries: enquiries.length,
      totalCustomerAccounts: customers.length,
      totalOrders: orders.length,
      totalRevenue,
      totalSavings: null,
    },
  };
};

export const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    maximumFractionDigits: 0,
    style: "currency",
  }).format(amount);

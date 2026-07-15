import { describe, expect, it } from "vitest";

import { buildAreaLeaderDashboard } from "./dashboard";

describe("buildAreaLeaderDashboard", () => {
  it("builds current-month tenant KPIs and scopes facilitators to the signed-in leader", () => {
    const dashboard = buildAreaLeaderDashboard({
      areaLeaders: [{ id: 8, tenantId: 1, customerId: 1, licenseType: 0, licenseFee: 0, rank: 0, areaSpaceId: 4, monthlySubscription: 0, directReferrals: 0, indirectReferrals: 0, orderTarget: 0 }],
      areaSpaces: [{ id: 4, tenantId: 1, areaLeaderId: 8, addressLine: "Main Road", capacity: "50", interestedMembers: 12, status: 2, reviewStartedAt: null, presentationsCompleted: 3, startupOrdersCompleted: 1, approvedAt: null }],
      customers: [{ id: 1, name: "Leader", email: "leader@example.com", membershipId: 1, isActive: true, tenantId: 1, userId: 10 }, { id: 2, name: "Member", email: "member@example.com", membershipId: 1, isActive: true, tenantId: 1, userId: 11 }],
      enquiries: [],
      facilitators: [{ id: 5, tenantId: 1, customerId: 2, areaLeaderId: 8, rank: 0, directReferrals: 2, indirectReferrals: 0, awardBalance: 0, isApproved: false }],
      myCustomerId: 1,
      now: new Date("2026-07-14T10:00:00Z"),
      orders: [{ id: 20, customerId: 2, productId: 1, enquiryId: null, unitPrice: 250, reservedPrice: 200, status: 1, statusText: "Processing", createdAt: "2026-07-10T10:00:00Z", reservedAt: null, cancelledAt: null, completedAt: null }],
    });

    expect(dashboard.stats).toEqual({ pendingApprovals: 1, totalFacilitators: 0, totalMembers: 2, totalOrders: 1, totalRevenue: 200 });
    expect(dashboard.areaSpace?.address).toBe("Main Road");
    expect(dashboard.recentOrders[0]?.customerName).toBe("Member");
  });
});

import { describe, expect, it } from "vitest";

import { buildAdminDashboard } from "./dashboard";
import { mockAdminDashboard } from "./mock-data";

describe("buildAdminDashboard", () => {
  it("aggregates live platform records", () => {
    const dashboard = buildAdminDashboard({
      areaLeaderCount: 2,
      customers: [
        { createdAt: "2026-07-10T10:00:00Z", id: 1, isActive: true, membershipId: 10, name: "Ava" },
        { createdAt: "2026-06-10T10:00:00Z", id: 2, isActive: false, membershipId: 20, name: "Neo" },
      ],
      enquiries: [{ createdAt: "2026-07-12T10:00:00Z", customerId: 1, id: 9, isPending: true }],
      facilitatorCount: 3,
      failed: false,
      fallback: mockAdminDashboard,
      memberships: [{ id: 10, name: "Jasper" }, { id: 20, name: "Onyx" }],
      now: new Date("2026-07-14T12:00:00Z"),
      orders: [
        { createdAt: "2026-07-11T10:00:00Z", customerId: 1, id: 4, reservedPrice: 500, statusText: "Reserved" },
        { createdAt: "2026-06-11T10:00:00Z", customerId: 2, id: 3, reservedPrice: 250, statusText: "Completed" },
      ],
      referrals: [{ convertedAt: "2026-07-01T10:00:00Z" }],
    });

    expect(dashboard.source).toBe("live");
    expect(dashboard.stats).toMatchObject({ totalEnquiries: 1, totalMembers: 2, totalOrders: 2, totalRevenue: 750 });
    expect(dashboard.members.byTier).toEqual([{ name: "Jasper", value: 1 }, { name: "Onyx", value: 1 }]);
    expect(dashboard.orders).toMatchObject({ monthRevenue: 500, monthVolume: 1 });
    expect(dashboard.people.recentReferrals).toBe(1);
    expect(dashboard.activity[0].title).toBe("Enquiry #9");
  });

  it("returns demo data when a required API dataset fails", () => {
    expect(buildAdminDashboard({
      areaLeaderCount: 0,
      customers: [],
      enquiries: [],
      facilitatorCount: 0,
      failed: true,
      fallback: mockAdminDashboard,
      memberships: [],
      orders: [],
      referrals: [],
    })).toBe(mockAdminDashboard);
  });
});

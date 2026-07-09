import { describe, expect, it } from "vitest";

import type { Membership } from "@/src/providers";
import { getMembershipNameById, getMembershipTypeLabel } from "./memberships";

const buildMembership = (overrides: Partial<Membership> = {}): Membership => ({
  id: 1,
  name: "Jasper Plan",
  description: null,
  isActive: true,
  membershipType: 0,
  activationDate: null,
  monthlyObligationAmount: 100,
  lastObligationMetDate: null,
  ...overrides,
});

describe("getMembershipTypeLabel", () => {
  it("maps each membership type to its label", () => {
    expect(getMembershipTypeLabel(0)).toBe("Jasper");
    expect(getMembershipTypeLabel(1)).toBe("Onyx");
    expect(getMembershipTypeLabel(2)).toBe("AQGreen");
    expect(getMembershipTypeLabel(3)).toBe("Business Premier");
  });
});

describe("getMembershipNameById", () => {
  const memberships = [
    buildMembership({ id: 1, name: "Jasper Plan" }),
    buildMembership({ id: 2, name: "Onyx Plan" }),
  ];

  it("returns the empty label when the id is null", () => {
    expect(getMembershipNameById(memberships, null, "No membership")).toBe(
      "No membership",
    );
  });

  it("returns the matching membership name", () => {
    expect(getMembershipNameById(memberships, 2, "No membership")).toBe(
      "Onyx Plan",
    );
  });

  it("falls back to a generated label when no membership matches", () => {
    expect(getMembershipNameById(memberships, 99, "No membership")).toBe(
      "Membership 99",
    );
  });
});

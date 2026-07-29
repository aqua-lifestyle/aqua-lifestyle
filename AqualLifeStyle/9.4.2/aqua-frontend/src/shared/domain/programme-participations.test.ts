import { describe, expect, it } from "vitest";

import type {
  MyProgrammeParticipations,
  ProgrammeParticipation,
} from "./programme-participations";
import {
  getProgrammeStatusDescription,
  getProgrammeStatusLabel,
} from "./programme-participations";

const participation = (
  programmeName: ProgrammeParticipation["programmeName"],
  isActive: boolean,
): ProgrammeParticipation => ({
  activatedAt: isActive ? "2026-07-29T00:00:00Z" : null,
  canRecruitForThisProgramme: isActive,
  currency: "ZAR",
  isActive,
  joinedIndependently: true,
  nextPaymentAmount: null,
  nextPaymentDescription: null,
  programmeName,
  recruiterClubMemberNumber: null,
  startedAt: "2026-07-29T00:00:00Z",
  status: isActive ? "Active" : "Awaiting payment",
});

const participations = (
  entry: ProgrammeParticipation | null,
  onyx: ProgrammeParticipation | null,
): MyProgrammeParticipations => ({
  canJoinEntry: false,
  canJoinOnyxDirectly: false,
  clubMemberNumber: "CLB-000000000001",
  entry,
  onyx,
  pendingAQGreenCheckout: null,
  pendingDirectOnyxCheckout: null,
  travelBenefit: null,
});

describe("programme status presentation", () => {
  it("gives active programmes precedence over membership and pending participation", () => {
    const data = participations(
      participation("AQGreen", false),
      participation("Onyx", true),
    );

    expect(getProgrammeStatusLabel(data, "Jasper", "Customer")).toBe("Onyx");
    expect(getProgrammeStatusDescription(data, "Jasper")).toBe(
      "Active programme participation",
    );
  });

  it("gives membership precedence over pending participation", () => {
    const data = participations(participation("AQGreen", false), null);

    expect(getProgrammeStatusLabel(data, "Jasper", "Customer")).toBe("Jasper");
    expect(getProgrammeStatusDescription(data, "Jasper")).toBe(
      "Active membership plan",
    );
  });

  it("uses the pending and fallback copy when no active option exists", () => {
    const pending = participations(participation("AQGreen", false), null);

    expect(getProgrammeStatusLabel(pending, null, "Customer")).toBe(
      "Activation pending",
    );
    expect(getProgrammeStatusDescription(pending, null)).toBe(
      "AQGreen payment confirmation pending",
    );
    expect(getProgrammeStatusLabel(undefined, null, "Customer")).toBe("Customer");
    expect(getProgrammeStatusDescription(undefined, null)).toBe(
      "No active membership or programme",
    );
  });
});

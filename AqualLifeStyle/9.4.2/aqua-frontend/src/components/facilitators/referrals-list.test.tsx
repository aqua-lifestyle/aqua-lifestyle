import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Referral } from "@/src/providers/Referrals/context";
import { useReferralsActions, useReferralsState } from "@/src/providers";

import { ReferralsList } from "./referrals-list";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useReferralsActions: vi.fn(),
    useReferralsState: vi.fn(),
  };
});

const referrals: Referral[] = [
  {
    id: 1,
    tenantId: 1,
    referrerFacilitatorId: 1,
    referrerAreaLeaderId: null,
    referredCustomerId: 100,
    sourceEnquiryId: 50,
    type: 0,
    awardAmount: 100,
    awardIssued: true,
    confirmedAt: "2024-01-01T00:00:00Z",
    convertedAt: "2024-01-02T00:00:00Z",
  },
  {
    id: 2,
    tenantId: 1,
    referrerFacilitatorId: 1,
    referrerAreaLeaderId: null,
    referredCustomerId: 101,
    sourceEnquiryId: 51,
    type: 1,
    awardAmount: 50,
    awardIssued: false,
    confirmedAt: null,
    convertedAt: null,
  },
];

const baseState = {
  isConfirmError: false,
  isConfirmPending: false,
  isConfirmSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  confirmErrorMessage: null,
  loadErrorMessage: null,
  referrals,
  selectedReferral: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useReferralsActions as unknown as { mockReturnValue: { getReferrals: () => Promise<void> } }).mockReturnValue({
    getReferrals: vi.fn(),
  });
});

describe("ReferralsList", () => {
  it("renders the referrals list", () => {
    render(<ReferralsList />);

    expect(screen.getByRole("heading", { name: /Referrals/i })).toBeDefined();
    expect(screen.getByText("Customer #100")).toBeDefined();
    expect(screen.getByText("Customer #101")).toBeDefined();
  });

  it("shows loading state", () => {
    (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<ReferralsList />);

    expect(screen.queryByText("Customer #100")).toBeNull();
  });

  it("shows error state", () => {
    (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load referrals",
    });

    render(<ReferralsList />);

    expect(screen.getByText("Failed to load referrals")).toBeDefined();
  });

  it("shows empty state when there are no referrals", () => {
    (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      referrals: [],
    });

    render(<ReferralsList />);

    expect(screen.getByText("No referrals")).toBeDefined();
    expect(screen.getByText("No referrals found.")).toBeDefined();
  });

  it("renders the type filter", () => {
    render(<ReferralsList />);

    expect(screen.getByLabelText("Type")).toBeDefined();
  });
});

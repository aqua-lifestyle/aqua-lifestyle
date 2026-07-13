import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Referral } from "@/src/providers/Referrals/context";
import { useReferralsActions, useReferralsState } from "@/src/providers";

import { ReferralDetails } from "./referral-details";

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

const referral: Referral = {
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
};

const baseState = {
  isConfirmError: false,
  isConfirmPending: false,
  isConfirmSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  confirmErrorMessage: null,
  loadErrorMessage: null,
  referrals: [],
  selectedReferral: referral,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useReferralsActions as unknown as { mockReturnValue: { getReferral: () => Promise<void>; confirmAward: () => Promise<boolean> } }).mockReturnValue({
    getReferral: vi.fn(),
    confirmAward: vi.fn(),
  });
});

describe("ReferralDetails", () => {
  it("renders referral details", async () => {
    render(<ReferralDetails referralId={1} />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Referral details/i, level: 1 })).toBeDefined();
    });

    expect(screen.getByText("Customer #100")).toBeDefined();
    expect(screen.getByText("Enquiry #50")).toBeDefined();
  });

  it("shows loading state", () => {
    (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSelectedPending: true,
      selectedReferral: null,
    });

    render(<ReferralDetails referralId={1} />);

    expect(screen.queryByText("Customer #100")).toBeNull();
  });

  it("shows error state", () => {
    (useReferralsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSelectedError: true,
      selectedErrorMessage: "Referral not found",
    });

    render(<ReferralDetails referralId={1} />);

    expect(screen.getByText("Referral not found")).toBeDefined();
  });

  it("shows invalid id error", () => {
    render(<ReferralDetails referralId={-1} />);

    expect(screen.getByText("This referral id is invalid.")).toBeDefined();
  });
});

import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Referral } from "@/src/providers/Referrals/context";
import { useReferralsActions, useReferralsState, useAuthState } from "@/src/providers";

import { ReferralDetails } from "./referral-details";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useReferralsActions: vi.fn(),
    useReferralsState: vi.fn(),
    useAuthState: vi.fn(),
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
  referrals: [referral],
  selectedReferral: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session: {
      accessToken: "token",
      expiresAt: null,
      user: {
        id: 99,
        email: "test@example.com",
        name: "Test User",
        permissions: ["Pages.Referrals"],
        role: "admin",
      },
    },
  });

  vi.mocked(useReferralsState).mockReturnValue(baseState);
  vi.mocked(useReferralsActions).mockReturnValue({
    confirmAward: vi.fn(),
    getReferrals: vi.fn(),
    getReferralsByEnquiry: vi.fn(),
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
    vi.mocked(useReferralsState).mockReturnValue({
      ...baseState,
      isLoadPending: true,
      referrals: [],
    });

    render(<ReferralDetails referralId={1} />);

    expect(screen.queryByText("Customer #100")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useReferralsState).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Referral not found",
    });

    render(<ReferralDetails referralId={1} />);

    expect(screen.getByText("Referral not found")).toBeDefined();
  });

  it("shows invalid id error", () => {
    render(<ReferralDetails referralId={-1} />);

    expect(screen.getByText("This referral id is invalid.")).toBeDefined();
  });
});

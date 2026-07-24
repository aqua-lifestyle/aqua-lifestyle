import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Membership, SavingsWindowStatus } from "@/src/providers/Memberships/context";
import type { OrderIntent } from "@/src/providers/OrderIntents/context";
import { useAuthState, useMembershipsActions, useMembershipsState, useOrderIntentsActions, useOrderIntentsState } from "@/src/providers";

import { MemberDashboard } from "./member-dashboard";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthState: vi.fn(),
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
    useOrderIntentsActions: vi.fn(),
    useOrderIntentsState: vi.fn(),
  };
});

const memberships: Membership[] = [
  {
    id: 1,
    name: "Bronze",
    description: "Bronze membership",
    isActive: true,
    membershipType: 0,
    activationDate: "2024-01-01",
    monthlyObligationAmount: 100,
    lastObligationMetDate: "2024-01-31",
  },
];

const savingsWindowStatuses: SavingsWindowStatus[] = [
  {
    tier: 0,
    tierName: "Bronze",
    savingsWindowOpenDay: 1,
    savingsWindowCloseDay: 15,
    currentDay: 10,
    asOfDate: "2024-01-10",
    isSavingsWindowOpen: true,
    statusLabel: "Open",
  },
];

const orderIntents: OrderIntent[] = [
  {
    id: 1,
    customerId: 1,
    productId: 10,
    enquiryId: 50,
    unitPrice: 100,
    reservedPrice: 80,
    status: 2,
    statusText: "Completed",
    createdAt: "2024-01-01T00:00:00Z",
    reservedAt: "2024-01-01T00:00:00Z",
    cancelledAt: null,
    completedAt: "2024-01-02T00:00:00Z",
  },
];

const baseAuthState = {
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "demo-token",
    expiresAt: "2099-01-01",
    user: {
      email: "member@example.com",
      id: 1,
      name: "Member User",
      permissions: ["Aqua.Savings.ViewSelf"],
      role: "Member",
    },
  },
};

const baseMembershipsState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuccess: true,
  memberships,
  selectedErrorMessage: null,
  selectedMembership: null,
  tierBenefits: null,
  tierBenefitsErrorMessage: null,
  isTierBenefitsError: false,
  isTierBenefitsPending: false,
  isTierBenefitsSuccess: false,
  savingsWindowStatuses,
  savingsWindowStatusesErrorMessage: null,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: true,
};

const baseOrderIntentsState = {
  actionErrorMessage: null,
  isActionError: false,
  isActionPending: false,
  isActionSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  loadErrorMessage: null,
  orderIntents,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue(baseAuthState);
  vi.mocked(useMembershipsState).mockReturnValue(baseMembershipsState);
  vi.mocked(useOrderIntentsState).mockReturnValue(baseOrderIntentsState);
  vi.mocked(useMembershipsActions).mockReturnValue({
    getActiveTiers: vi.fn(),
    getMembership: vi.fn(),
    getMemberships: vi.fn(),
    getSavingsWindowStatuses: vi.fn(),
    getTierBenefits: vi.fn(),
  });
  vi.mocked(useOrderIntentsActions).mockReturnValue({
    cancelOrderIntent: vi.fn(),
    completeOrderIntent: vi.fn(),
    createForCurrentCustomer: vi.fn(),
    createFromEnquiry: vi.fn(),
    getOrderIntents: vi.fn(),
  });
});

describe("MemberDashboard", () => {
  it("renders the member dashboard", async () => {
    render(<MemberDashboard />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Member dashboard/i })).toBeDefined();
    });

    expect(screen.getByText("My Orders")).toBeDefined();
    expect(screen.getByText("1")).toBeDefined();
    expect(
      screen.getByRole("link", { name: "View my savings account" }),
    ).toHaveAttribute("href", "/member/savings");
  });

  it("shows loading state", () => {
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseMembershipsState,
      isPending: true,
      isSuccess: false,
    });
    vi.mocked(useOrderIntentsState).mockReturnValue({
      ...baseOrderIntentsState,
      isLoadSuccess: false,
      isLoadPending: true,
    });

    render(<MemberDashboard />);

    expect(screen.queryByText("My Orders")).toBeNull();
  });

  it("hides the savings account link without savings access", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      ...baseAuthState,
      session: {
        ...baseAuthState.session,
        user: {
          ...baseAuthState.session.user,
          permissions: [],
        },
      },
    });

    render(<MemberDashboard />);

    await screen.findByRole("heading", { name: /Member dashboard/i });
    expect(
      screen.queryByRole("link", { name: "View my savings account" }),
    ).not.toBeInTheDocument();
  });

  it("shows error state", () => {
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseMembershipsState,
      isError: true,
      errorMessage: "Failed to load memberships",
    });

    render(<MemberDashboard />);

    expect(screen.getByText("Failed to load memberships")).toBeDefined();
  });
});

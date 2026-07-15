import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Facilitator } from "@/src/providers/Facilitators/context";
import type { Referral } from "@/src/providers/Referrals/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useFacilitatorsActions,
  useFacilitatorsState,
  useCustomersActions,
  useCustomersState,
  useReferralsActions,
  useReferralsState,
  useAuthState,
  useToast,
} from "@/src/providers";

import { FacilitatorDashboard } from "./facilitator-dashboard";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useFacilitatorsActions: vi.fn(),
    useFacilitatorsState: vi.fn(),
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useReferralsActions: vi.fn(),
    useReferralsState: vi.fn(),
    useAuthState: vi.fn(),
    useToast: vi.fn(),
  };
});

const facilitators: Facilitator[] = [
  {
    id: 1,
    tenantId: 1,
    customerId: 99,
    areaLeaderId: 1,
    rank: 1,
    directReferrals: 0,
    indirectReferrals: 0,
    awardBalance: 0,
  },
];

const referrals: Referral[] = [
  {
    id: 1,
    tenantId: 1,
    referrerFacilitatorId: 1,
    referrerAreaLeaderId: null,
    referredCustomerId: 50,
    sourceEnquiryId: 10,
    type: 0,
    awardAmount: 100,
    awardIssued: true,
    confirmedAt: "2025-01-01T00:00:00Z",
    convertedAt: "2025-01-01T00:00:00Z",
  },
];

const session: AuthSession = {
  accessToken: "token",
  expiresAt: null,
  user: {
    id: 25,
    email: "test@example.com",
    name: "Test User",
    permissions: ["Pages.Facilitators"],
    role: "facilitator",
  },
};

const baseFacilitatorsState = {
  facilitators,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isRegisterError: false,
  isRegisterPending: false,
  isRegisterSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  loadErrorMessage: null,
  registerErrorMessage: null,
  selectedFacilitator: null,
  selectedErrorMessage: null,
};

const baseReferralsState = {
  isConfirmError: false,
  isConfirmPending: false,
  isConfirmSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  confirmErrorMessage: null,
  loadErrorMessage: null,
  referrals,
  selectedReferral: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session,
  });
  vi.mocked(useToast).mockReturnValue({ toast: vi.fn() });

  vi.mocked(useFacilitatorsState).mockReturnValue(baseFacilitatorsState);
  vi.mocked(useFacilitatorsActions).mockReturnValue({
    getFacilitator: vi.fn(),
    getFacilitators: vi.fn(),
    getFacilitatorsByCustomer: vi.fn(),
    registerFacilitator: vi.fn(),
  });

  vi.mocked(useCustomersState).mockReturnValue({
    changeMembershipErrorMessage: null,
    createErrorMessage: null,
    customers: [],
    isChangeMembershipError: false,
    isChangeMembershipPending: false,
    isChangeMembershipSuccess: false,
    isCreateError: false,
    isCreatePending: false,
    isCreateSuccess: false,
    isLoadError: false,
    isLoadPending: false,
    isLoadSuccess: false,
    isMyCustomerError: false,
    isMyCustomerPending: false,
    isMyCustomerSuccess: true,
    isSelectedError: false,
    isSelectedPending: false,
    isSelectedSuccess: false,
    isUpdateError: false,
    isUpdatePending: false,
    isUpdateSuccess: false,
    loadErrorMessage: null,
    myCustomer: { id: 99, email: "test@example.com", isActive: true, membershipId: null, name: "Test User", tenantId: 1, userId: 25 },
    myCustomerErrorMessage: null,
    selectedCustomer: null,
    selectedErrorMessage: null,
    updateErrorMessage: null,
  });
  vi.mocked(useCustomersActions).mockReturnValue({
    changeMembership: vi.fn(),
    createCustomer: vi.fn(),
    getCustomer: vi.fn(),
    getCustomers: vi.fn(),
    getMyCustomer: vi.fn(),
    updateCustomer: vi.fn(),
  });

  vi.mocked(useReferralsState).mockReturnValue(baseReferralsState);
  vi.mocked(useReferralsActions).mockReturnValue({
    confirmAward: vi.fn(),
    getReferrals: vi.fn(),
    getReferralsByEnquiry: vi.fn(),
  });
});

describe("FacilitatorDashboard", () => {
  it("renders the facilitator dashboard", () => {
    render(<FacilitatorDashboard />);

    expect(screen.getByRole("heading", { name: /Facilitator dashboard/i })).toBeDefined();
    expect(screen.getByText("My Referrals")).toBeDefined();
    expect(screen.getByText("Total Awards")).toBeDefined();
    expect(screen.getByText("Confirmed")).toBeDefined();
    expect(screen.getByText("Rank")).toBeDefined();
    expect(screen.getByText("Quick actions")).toBeDefined();
    expect(screen.getByText("Share your referral link")).toBeDefined();
    expect(screen.getByText(/FAC-1/)).toBeDefined();
    expect(screen.getByRole("link", { name: "View my referrals" })).toHaveAttribute(
      "href",
      "/facilitator/my-referrals",
    );
    expect(screen.getByRole("link", { name: "View facilitator details" })).toHaveAttribute(
      "href",
      "/facilitator/1",
    );
    expect(screen.getByText("Recent referral activity")).toBeDefined();
  });
});

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Facilitator } from "@/src/providers/Facilitators/context";
import type { Referral } from "@/src/providers/Referrals/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useFacilitatorsActions,
  useFacilitatorsState,
  useReferralsActions,
  useReferralsState,
} from "@/src/providers";

import { FacilitatorReferrals } from "./facilitator-referrals";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthState: vi.fn(),
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useFacilitatorsActions: vi.fn(),
    useFacilitatorsState: vi.fn(),
    useReferralsActions: vi.fn(),
    useReferralsState: vi.fn(),
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
  {
    id: 2,
    tenantId: 1,
    referrerFacilitatorId: 1,
    referrerAreaLeaderId: null,
    referredCustomerId: 51,
    sourceEnquiryId: 11,
    type: 1,
    awardAmount: 50,
    awardIssued: false,
    confirmedAt: null,
    convertedAt: null,
  },
];

const session: AuthSession = {
  accessToken: "token",
  expiresAt: null,
  user: {
    id: 25,
    email: "test@example.com",
    name: "Test User",
    permissions: ["Pages.Referrals"],
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

  vi.mocked(useFacilitatorsState).mockReturnValue(baseFacilitatorsState);
  vi.mocked(useFacilitatorsActions).mockReturnValue({
    getFacilitator: vi.fn(),
    getFacilitators: vi.fn(),
    getFacilitatorsByCustomer: vi.fn(),
    registerFacilitator: vi.fn(),
  });

  vi.mocked(useReferralsState).mockReturnValue(baseReferralsState);
  vi.mocked(useReferralsActions).mockReturnValue({
    confirmAward: vi.fn(),
    getReferrals: vi.fn(),
    getReferralsByEnquiry: vi.fn(),
  });
});

describe("FacilitatorReferrals", () => {
  it("renders the facilitator referrals page", () => {
    render(<FacilitatorReferrals />);

    expect(screen.getByRole("heading", { name: /My referrals/i })).toBeDefined();
    expect(screen.getAllByText("Type").length).toBeGreaterThan(0);
    expect(screen.getByRole("combobox", { name: /Type/i })).toBeDefined();
  });
});

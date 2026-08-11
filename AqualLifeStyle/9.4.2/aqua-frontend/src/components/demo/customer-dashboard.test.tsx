import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import type { CustomersState } from "@/src/providers/Customers/context";
import type { MembershipsState } from "@/src/providers/Memberships/context";
import type { ProductsState } from "@/src/providers/Products/context";
import { useMyProgrammeParticipations } from "@/src/shared/hooks/use-my-programme-participations";

import { CustomerDashboard } from "./customer-dashboard";

const { replace } = vi.hoisted(() => ({ replace: vi.fn() }));

vi.mock("next/navigation", () => ({
  usePathname: () => "/dashboard",
  useRouter: () => ({ push: vi.fn(), replace }),
}));

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );

  return {
    ...actual,
    useAuthState: vi.fn(),
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
    useOrderIntentsActions: vi.fn(),
    useOrderIntentsState: vi.fn(),
    useProductsActions: vi.fn(),
    useProductsState: vi.fn(),
  };
});

vi.mock("@/src/shared/hooks/use-my-programme-participations", () => ({
  useMyProgrammeParticipations: vi.fn(),
}));

const customersState: CustomersState = {
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
  myCustomer: {
    email: "jane@example.com",
    id: 7,
    isActive: true,
    membershipId: 1,
    name: "Jane Customer",
    tenantId: 1,
    userId: 42,
  },
  myCustomerErrorMessage: null,
  selectedCustomer: null,
  selectedErrorMessage: null,
  updateErrorMessage: null,
};

const membershipsState: MembershipsState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: true,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuccess: true,
  isTierBenefitsError: false,
  isTierBenefitsPending: false,
  isTierBenefitsSuccess: false,
  memberships: [
    {
      activationDate: "2026-01-01",
      description: "Entry membership",
      id: 1,
      isActive: true,
      lastObligationMetDate: null,
      membershipType: 0,
      monthlyObligationAmount: 0,
      name: "Jasper",
    },
    {
      activationDate: "2026-01-01",
      description: "Premium membership",
      id: 2,
      isActive: true,
      lastObligationMetDate: null,
      membershipType: 1,
      monthlyObligationAmount: 250,
      name: "Onyx",
    },
  ],
  savingsWindowStatuses: [
    {
      asOfDate: "2026-07-14",
      currentDay: 14,
      isSavingsWindowOpen: true,
      savingsWindowCloseDay: 15,
      savingsWindowOpenDay: 1,
      statusLabel: "Open",
      tier: 0,
      tierName: "Jasper",
    },
  ],
  savingsWindowStatusesErrorMessage: null,
  selectedErrorMessage: null,
  selectedMembership: null,
  tierBenefits: null,
  tierBenefitsErrorMessage: null,
};

const productsState: ProductsState = {
  eligibleErrorMessage: null,
  eligibleProducts: [
    {
      id: 5,
      isActive: true,
      membershipId: 1,
      name: "Water filter",
      price: 199,
    },
  ],
  errorMessage: null,
  isEligibleError: false,
  isEligiblePending: false,
  isEligibleSuccess: true,
  isError: false,
  isPending: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuccess: false,
  products: [],
  selectedErrorMessage: null,
  selectedProduct: null,
};

describe("CustomerDashboard", () => {
  const changeMembership = vi.fn();
  const getActiveTiers = vi.fn();
  const getEligibleProductsForCustomer = vi.fn();
  const getMyCustomer = vi.fn();
  const getSavingsWindowStatuses = vi.fn();
  const createForCurrentCustomer = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    changeMembership.mockResolvedValue(customersState.myCustomer);
    createForCurrentCustomer.mockResolvedValue(true);

    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: "2099-01-01",
        user: {
          email: "jane@example.com",
          id: 42,
          name: "Jane Customer",
          permissions: [],
          role: "Guest",
        },
      },
    });
    vi.mocked(useCustomersActions).mockReturnValue({
      changeMembership,
      createCustomer: vi.fn(),
      getCustomer: vi.fn(),
      getCustomers: vi.fn(),
      getMyCustomer,
      updateCustomer: vi.fn(),
    });
    vi.mocked(useCustomersState).mockReturnValue(customersState);
    vi.mocked(useMembershipsActions).mockReturnValue({
      getActiveTiers,
      getMembership: vi.fn(),
      getMemberships: vi.fn(),
      getSavingsWindowStatuses,
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue(membershipsState);
    vi.mocked(useOrderIntentsActions).mockReturnValue({
      cancelOrderIntent: vi.fn(),
      completeOrderIntent: vi.fn(),
      createForCurrentCustomer,
      createFromEnquiry: vi.fn(),
      getOrderIntents: vi.fn(),
      getMyOrderIntents: vi.fn(),
    });
    vi.mocked(useOrderIntentsState).mockReturnValue({
      actionErrorMessage: null,
      isActionError: false,
      isActionPending: false,
      isActionSuccess: false,
      isLoadError: false,
      isLoadPending: false,
      isLoadSuccess: false,
      loadErrorMessage: null,
      orderIntents: [],
    });
    vi.mocked(useProductsActions).mockReturnValue({
      getEligibleProductsForCustomer,
      getProduct: vi.fn(),
      getProducts: vi.fn(),
    });
    vi.mocked(useProductsState).mockReturnValue(productsState);
    vi.mocked(useMyProgrammeParticipations).mockReturnValue({
      data: undefined,
      errorMessage: undefined,
      isLoading: false,
      reload: vi.fn(),
    });
  });

  it("redirects signed-out visitors without loading customer data", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: false,
      isReady: true,
      session: null,
    });

    render(<CustomerDashboard />);

    expect(screen.getByText("Verifying customer access…")).toBeInTheDocument();
    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/login?redirect=%2Fdashboard");
    });
    expect(getMyCustomer).not.toHaveBeenCalled();
    expect(getEligibleProductsForCustomer).not.toHaveBeenCalled();
  });

  it("loads and renders the signed-in customer's dashboard", async () => {
    render(<CustomerDashboard />);

    await waitFor(() => {
      expect(getMyCustomer).toHaveBeenCalledOnce();
      expect(getActiveTiers).toHaveBeenCalledOnce();
      expect(getSavingsWindowStatuses).toHaveBeenCalledOnce();
      expect(getEligibleProductsForCustomer).toHaveBeenCalledWith(7);
    });

    expect(
      screen.getByRole("heading", { name: "Jane Customer" }),
    ).toBeInTheDocument();
    expect(screen.getAllByText("Jasper").length).toBeGreaterThan(0);
    expect(screen.getByText("Water filter")).toBeInTheDocument();
    expect(screen.getByText("Monthly obligation").parentElement).toHaveTextContent(
      /R\s*0[,.]00/,
    );
  });

  it("recognises active programme participation without a legacy membership", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: "2099-01-01",
        user: {
          email: "jane@example.com",
          id: 42,
          name: "Jane Customer",
          permissions: ["Aqua.ProgrammeParticipations.ViewSelf"],
          role: "Member",
        },
      },
    });
    vi.mocked(useCustomersState).mockReturnValue({
      ...customersState,
      myCustomer: {
        ...customersState.myCustomer!,
        membershipId: null,
      },
    });
    vi.mocked(useMyProgrammeParticipations).mockReturnValue({
      data: {
        areaId: "a0000000-0000-0000-0000-000000000001",
        areaName: "Johannesburg",
        canJoinEntry: true,
        canJoinOnyxDirectly: false,
        clubMemberNumber: "CLB-000000000007",
        entry: null,
        onyx: {
          activatedAt: "2026-07-29T00:00:00Z",
          canRecruitForThisProgramme: true,
          currency: "ZAR",
          isActive: true,
          joinedIndependently: true,
          nextPaymentAmount: null,
          nextPaymentDescription: null,
          programmeName: "Onyx",
          recruiterClubMemberNumber: null,
          startedAt: "2026-07-29T00:00:00Z",
          status: "Active",
        },
        pendingAQGreenCheckout: null,
        pendingDirectOnyxCheckout: null,
        funeralCover: null,
        travelBenefit: null,
      },
      errorMessage: undefined,
      isLoading: false,
      reload: vi.fn(),
    });

    render(<CustomerDashboard />);

    expect(await screen.findByText("Club Member")).toBeInTheDocument();
    expect(screen.getByText("Active programme participation")).toBeInTheDocument();
    expect(screen.getByText("Onyx")).toBeInTheDocument();
    expect(screen.getByText("Johannesburg")).toBeInTheDocument();
    expect(screen.getByText(/separate from programme level progression/i))
      .toBeInTheDocument();
  });

  it("routes Onyx joining through the programme participation workflow", async () => {
    render(<CustomerDashboard />);

    expect(
      screen.getByRole("link", { name: "View programmes" }),
    ).toHaveAttribute("href", "/member/programmes");
    expect(screen.queryByText("Premium membership")).not.toBeInTheDocument();
    expect(changeMembership).not.toHaveBeenCalled();
  });

  it("does not describe an unavailable customer account as inactive", async () => {
    vi.mocked(useCustomersState).mockReturnValue({
      ...customersState,
      myCustomer: null,
    });

    render(<CustomerDashboard />);

    expect(await screen.findByText("Unavailable")).toBeInTheDocument();
    expect(screen.queryByText("Inactive")).not.toBeInTheDocument();
  });

  it("gives customers with a legacy Onyx selection a completion path", async () => {
    vi.mocked(useCustomersState).mockReturnValue({
      ...customersState,
      myCustomer: {
        ...customersState.myCustomer!,
        membershipId: 2,
      },
    });

    render(<CustomerDashboard />);

    expect(
      screen.getByText(/previous Onyx selection still needs to be completed/i),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Complete Onyx joining" }),
    ).toHaveAttribute("href", "/member/programmes");
    expect(screen.queryByText("Current plan")).not.toBeInTheDocument();
  });

  it("reserves an eligible product", async () => {
    render(<CustomerDashboard />);

    fireEvent.click(screen.getByRole("button", { name: /Reserve/ }));

    await waitFor(() => {
      expect(createForCurrentCustomer).toHaveBeenCalledWith(5);
    });
  });
});

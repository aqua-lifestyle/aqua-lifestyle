import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Membership } from "@/src/providers/Memberships/context";
import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useAuthState,
} from "@/src/providers";

import { CustomersList } from "./customers-list";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
    useAuthState: vi.fn(),
  };
});

const memberships: Membership[] = [
  {
    id: 1,
    name: "Bronze",
    description: null,
    isActive: true,
    membershipType: 0,
    activationDate: null,
    monthlyObligationAmount: 0,
    lastObligationMetDate: null,
  },
  {
    id: 2,
    name: "Silver",
    description: null,
    isActive: true,
    membershipType: 1,
    activationDate: null,
    monthlyObligationAmount: 0,
    lastObligationMetDate: null,
  },
];

const customers: Customer[] = [
  {
    id: 1,
    name: "John Doe",
    email: "john@example.com",
    membershipId: 1,
    isActive: true,
    userId: 1,
    tenantId: 1,
  },
  {
    id: 2,
    name: "Jane Smith",
    email: "jane@example.com",
    membershipId: null,
    isActive: false,
    userId: 2,
    tenantId: 1,
  },
];

const baseState = {
  changeMembershipErrorMessage: null,
  createErrorMessage: null,
  customers,
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isChangeMembershipError: false,
  isChangeMembershipPending: false,
  isChangeMembershipSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isMyCustomerError: false,
  isMyCustomerPending: false,
  isMyCustomerSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isUpdateError: false,
  isUpdatePending: false,
  isUpdateSuccess: false,
  loadErrorMessage: null,
  myCustomer: null,
  myCustomerErrorMessage: null,
  selectedCustomer: null,
  selectedErrorMessage: null,
  updateErrorMessage: null,
};

const baseMembershipsState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuccess: true,
  memberships: [...memberships],
  selectedErrorMessage: null,
  selectedMembership: null,
  tierBenefits: null,
  tierBenefitsErrorMessage: null,
  isTierBenefitsError: false,
  isTierBenefitsPending: false,
  isTierBenefitsSuccess: false,
  savingsWindowStatuses: [],
  savingsWindowStatusesErrorMessage: null,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: false,
};

describe("CustomersList", () => {
  const getCustomers = vi.fn();
  const getMemberships = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useCustomersActions).mockReturnValue({
      changeMembership: vi.fn(),
      createCustomer: vi.fn(),
      getCustomer: vi.fn(),
      getCustomers,
      getMyCustomer: vi.fn(),
      updateCustomer: vi.fn(),
    });
    vi.mocked(useCustomersState).mockReturnValue({ ...baseState });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getActiveTiers: vi.fn(),
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({ ...baseMembershipsState });
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "access-token",
        expiresAt: "2026-01-01T00:00:00Z",
        user: {
          id: 1,
          email: "user@example.com",
          name: "Demo User",
          role: "SystemAdmin",
          permissions: [
            "Aqua.Members.View",
            "Aqua.Members.Create",
            "Pages.Customers",
            "Pages.Products",
            "Pages.Enquiries",
            "Pages.Memberships",
            "Pages.Orders",
          ],
        },
      },
    });
  });

  it("fetches customers and memberships on mount", () => {
    render(<CustomersList />);
    expect(getCustomers).toHaveBeenCalled();
    expect(getMemberships).toHaveBeenCalled();
  });

  it("shows a skeleton while loading", () => {
    vi.mocked(useCustomersState).mockReturnValue({
      ...baseState,
      customers: [],
      isLoadPending: true,
      isLoadSuccess: false,
    });
    render(<CustomersList />);
    expect(document.querySelector(".skeleton-shimmer")).toBeInTheDocument();
  });

  it("renders customer rows and summary cards", () => {
    render(<CustomersList />);
    const totalCard = screen.getByText("Total customers").closest("article");
    expect(totalCard).toBeInTheDocument();
    expect(within(totalCard!).getByText("2")).toBeInTheDocument();

    const johnRow = screen.getByText("John Doe").closest("tr");
    expect(johnRow).toBeInTheDocument();
    expect(within(johnRow!).getByText("Bronze")).toBeInTheDocument();

    expect(screen.getByText("Jane Smith")).toBeInTheDocument();
  });

  it("filters customers by status", () => {
    render(<CustomersList />);

    fireEvent.change(screen.getByLabelText("Status"), {
      target: { value: "active" },
    });

    expect(screen.getByText("John Doe")).toBeInTheDocument();
    expect(screen.queryByText("Jane Smith")).not.toBeInTheDocument();
  });

  it("filters customers by membership", () => {
    render(<CustomersList />);

    fireEvent.change(screen.getByLabelText("Membership"), {
      target: { value: "none" },
    });

    expect(screen.queryByText("John Doe")).not.toBeInTheDocument();
    expect(screen.getByText("Jane Smith")).toBeInTheDocument();
  });

  it("filters customers by search query", async () => {
    render(<CustomersList />);

    fireEvent.change(screen.getByPlaceholderText("Search..."), {
      target: { value: "jane" },
    });

    await waitFor(() => {
      expect(screen.queryByText("John Doe")).not.toBeInTheDocument();
      expect(screen.getByText("Jane Smith")).toBeInTheDocument();
    });
  });

  it("switches to card view", () => {
    render(<CustomersList />);

    expect(screen.getByRole("table")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Card view" }));

    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(screen.getByText("John Doe")).toBeInTheDocument();
  });
});

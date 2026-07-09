import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Membership } from "@/src/providers/Memberships/context";
import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";

import { CustomerDetails } from "./customer-details";

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
];

const selectedCustomer: Customer = {
  id: 1,
  name: "John Doe",
  email: "john@example.com",
  membershipId: 1,
  isActive: true,
};

const baseCustomersState = {
  createErrorMessage: null,
  customers: [],
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  isUpdateError: false,
  isUpdatePending: false,
  isUpdateSuccess: false,
  loadErrorMessage: null,
  selectedCustomer,
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

describe("CustomerDetails", () => {
  const getCustomer = vi.fn();
  const updateCustomer = vi.fn().mockResolvedValue(true);
  const getMemberships = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    updateCustomer.mockResolvedValue(true);

    vi.mocked(useCustomersActions).mockReturnValue({
      createCustomer: vi.fn(),
      getCustomer,
      getCustomers: vi.fn(),
      updateCustomer,
    });
    vi.mocked(useCustomersState).mockReturnValue({ ...baseCustomersState });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseMembershipsState,
    });
  });

  it("loads the customer on mount", () => {
    render(<CustomerDetails customerId={1} />);
    expect(getCustomer).toHaveBeenCalledWith(1);
    expect(getMemberships).toHaveBeenCalled();
  });

  it("shows an invalid id message", () => {
    vi.mocked(useCustomersState).mockReturnValue({
      ...baseCustomersState,
      selectedCustomer: null,
      isSelectedSuccess: false,
    });
    render(<CustomerDetails customerId={-1} />);
    expect(
      screen.getByText("This customer id is invalid."),
    ).toBeInTheDocument();
    expect(getCustomer).not.toHaveBeenCalled();
    expect(getMemberships).not.toHaveBeenCalled();
  });

  it("updates the customer through the edit form", async () => {
    render(<CustomerDetails customerId={1} />);

    expect(screen.getByText("John Doe")).toBeInTheDocument();
    expect(screen.getByText("john@example.com")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Name"), {
      target: { value: "John Updated" },
    });

    const form = screen.getByRole("button", { name: "Save customer" }).closest("form");
    expect(form).toBeTruthy();
    fireEvent.submit(form!);

    await waitFor(() => expect(updateCustomer).toHaveBeenCalledOnce());

    expect(updateCustomer).toHaveBeenCalledWith(
      expect.objectContaining({
        id: 1,
        name: "John Updated",
        email: "john@example.com",
        membershipId: 1,
        isActive: true,
      }),
    );
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Membership } from "@/src/providers/Memberships/context";
import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useToast,
} from "@/src/providers";

import { CustomerRegistrationForm } from "./customer-registration-form";

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
    useToast: vi.fn(),
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

const submitForm = () => {
  const button = screen.getByRole("button", { name: "Register customer" });
  const form = button.closest("form");
  expect(form).toBeTruthy();
  fireEvent.submit(form!);
};

describe("CustomerRegistrationForm", () => {
  const createCustomer = vi.fn().mockResolvedValue(true);
  const getMemberships = vi.fn();
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    createCustomer.mockResolvedValue(true);

    vi.mocked(useCustomersActions).mockReturnValue({
      createCustomer,
      getCustomer: vi.fn(),
      getCustomers: vi.fn(),
      updateCustomer: vi.fn(),
    });
    vi.mocked(useCustomersState).mockReturnValue({
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
      isSelectedSuccess: false,
      isUpdateError: false,
      isUpdatePending: false,
      isUpdateSuccess: false,
      loadErrorMessage: null,
      selectedCustomer: null,
      selectedErrorMessage: null,
      updateErrorMessage: null,
    });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({
      errorMessage: null,
      isError: false,
      isPending: false,
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      isSuccess: false,
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
    });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("fetches memberships on mount", () => {
    render(<CustomerRegistrationForm />);
    expect(getMemberships).toHaveBeenCalled();
  });

  it("shows validation errors for empty fields", async () => {
    render(<CustomerRegistrationForm />);

    submitForm();

    expect(
      await screen.findByText("Customer name must be at least 2 characters."),
    ).toBeInTheDocument();
    expect(screen.getByText("Enter a valid email address.")).toBeInTheDocument();
  });

  it("calls createCustomer and shows a toast on success", async () => {
    render(<CustomerRegistrationForm initialMembershipId={1} />);

    fireEvent.change(screen.getByLabelText("Full name"), {
      target: { value: "Thandaza Mkhize" },
    });
    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "thandaza@example.com" },
    });

    submitForm();

    await waitFor(() => expect(createCustomer).toHaveBeenCalledOnce());

    expect(createCustomer).toHaveBeenCalledWith({
      email: "thandaza@example.com",
      membershipId: 1,
      name: "Thandaza Mkhize",
    });
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Success",
        type: "success",
      }),
    );
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAuthState,
  useMembershipsActions,
  useMembershipsState,
  useToast,
} from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { CustomerDialog, getCustomerOnboardingConfirmation } from "./CustomerDialog";

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
  useMembershipsActions: vi.fn(),
  useMembershipsState: vi.fn(),
  useToast: vi.fn(),
}));
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn(), post: vi.fn() } }));

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin", tenantId: 1 },
  },
});

describe("CustomerDialog", () => {
  const getMemberships = vi.fn();
  const toast = vi.fn();

  it("describes restoration as reconnecting existing history", () => {
    expect(getCustomerOnboardingConfirmation(true)).toEqual({
      message: "The customer was reconnected to their existing account and history.",
      title: "Customer access restored",
    });
  });

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(httpClient.get).mockResolvedValue([]);
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) { this.removeAttribute("open"); });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getActiveTiers: vi.fn(),
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({
      errorMessage: null,
      isError: false,
      isPending: false,
      isSavingsWindowStatusesError: false,
      isSavingsWindowStatusesPending: false,
      isSavingsWindowStatusesSuccess: false,
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      isSuccess: true,
      isTierBenefitsError: false,
      isTierBenefitsPending: false,
      isTierBenefitsSuccess: false,
      memberships: [],
      savingsWindowStatuses: [],
      savingsWindowStatusesErrorMessage: null,
      selectedErrorMessage: null,
      selectedMembership: null,
      tierBenefits: null,
      tierBenefitsErrorMessage: null,
    });
    vi.mocked(useToast).mockReturnValue({ toast } as ReturnType<typeof useToast>);
  });

  it("requires the dedicated admin customer create permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));
    render(<CustomerDialog />);
    expect(screen.queryByRole("button", { name: /add customer/i })).not.toBeInTheDocument();
  });

  it("validates and creates a tenant-linked customer with justification", async () => {
    const onCreated = vi.fn();
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Customers.Create"]));
    vi.mocked(httpClient.post).mockResolvedValue({ id: 10 });
    render(<CustomerDialog onCreated={onCreated} />);

    fireEvent.click(screen.getByRole("button", { name: /add customer/i }));
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Ada" } });
    fireEvent.change(screen.getByLabelText("Last name"), { target: { value: "Lovelace" } });
    fireEvent.change(screen.getByLabelText("Email address"), { target: { value: "ada@example.com" } });
    fireEvent.change(screen.getByLabelText("Temporary password"), { target: { value: "Temporary123!" } });
    fireEvent.change(screen.getByLabelText("Reason for creating this account"), { target: { value: "Approved onboarding" } });
    fireEvent.click(screen.getByRole("button", { name: /create customer/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/AdminCustomer/Create",
      expect.objectContaining({
        email: "ada@example.com",
        firstName: "Ada",
        justification: "Approved onboarding",
        lastName: "Lovelace",
        password: "Temporary123!",
        tenantId: 1,
      }),
    ));
    expect(onCreated).toHaveBeenCalled();
    expect(toast).toHaveBeenCalled();
  });
});

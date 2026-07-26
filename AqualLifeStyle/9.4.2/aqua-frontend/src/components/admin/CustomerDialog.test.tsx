import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAuthState,
  useMembershipsActions,
  useMembershipsState,
  useToast,
} from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { CustomerDialog } from "./CustomerDialog";

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
    vi.mocked(httpClient.post).mockResolvedValue({
      customer: { id: 10, name: "Ada Lovelace" },
      passwordSetupUrl: null,
      removedCustomer: null,
      requiresRestoreConfirmation: false,
    });
    render(<CustomerDialog onCreated={onCreated} />);

    fireEvent.click(screen.getByRole("button", { name: /add customer/i }));
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Ada" } });
    fireEvent.change(screen.getByLabelText("Surname"), { target: { value: "Lovelace" } });
    fireEvent.change(screen.getByLabelText("Email address"), { target: { value: "ada@example.com" } });
    fireEvent.change(screen.getByLabelText("Contact number"), { target: { value: "+27 82 123 4567" } });
    fireEvent.change(screen.getByLabelText("Home address"), { target: { value: "10 Customer Road, Johannesburg" } });
    fireEvent.change(screen.getByLabelText("Temporary password for a new customer"), { target: { value: "Temporary123!" } });
    fireEvent.change(screen.getByLabelText("Reason for creating this account"), { target: { value: "Approved onboarding" } });
    fireEvent.click(screen.getByRole("button", { name: /create customer/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/AdminCustomer/Create",
      expect.objectContaining({
        email: "ada@example.com",
        contactNumber: "+27 82 123 4567",
        firstName: "Ada",
        homeAddress: "10 Customer Road, Johannesburg",
        justification: "Approved onboarding",
        lastName: "Lovelace",
        password: "Temporary123!",
        tenantId: 1,
      }),
    ));
    expect(onCreated).toHaveBeenCalled();
    expect(toast).toHaveBeenCalled();
  });

  it("requires explicit confirmation before restoring a removed customer", async () => {
    const onCreated = vi.fn();
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Customers.Create"]));
    vi.mocked(httpClient.post)
      .mockResolvedValueOnce({
        customer: null,
        passwordSetupUrl: null,
        removedCustomer: { customerId: 17, email: "dora@example.com", name: "Dora Shongwe", removalTime: "2026-07-16" },
        requiresRestoreConfirmation: true,
      })
      .mockResolvedValueOnce({
        customer: { id: 17, name: "Dora Shongwe" },
        passwordSetupUrl: "https://customers.example/reset-password?token=one-time",
        removedCustomer: null,
        requiresRestoreConfirmation: false,
      });
    render(<CustomerDialog onCreated={onCreated} />);

    fireEvent.click(screen.getByRole("button", { name: /add customer/i }));
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Dora" } });
    fireEvent.change(screen.getByLabelText("Surname"), { target: { value: "Shongwe" } });
    fireEvent.change(screen.getByLabelText("Email address"), { target: { value: "dora@example.com" } });
    fireEvent.change(screen.getByLabelText("Contact number"), { target: { value: "+27 83 234 5678" } });
    fireEvent.change(screen.getByLabelText("Home address"), { target: { value: "20 Restore Avenue, Johannesburg" } });
    fireEvent.change(screen.getByLabelText("Reason for creating this account"), { target: { value: "Returning customer approved" } });
    fireEvent.click(screen.getByRole("button", { name: /create customer/i }));

    expect(await screen.findByText("An existing removed customer was found. No account has been changed yet.")).toBeInTheDocument();
    expect(onCreated).not.toHaveBeenCalled();
    fireEvent.click(screen.getByRole("button", { name: "Restore customer" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenLastCalledWith(
      "/api/services/app/AdminCustomer/Restore",
      expect.objectContaining({ customerId: 17, email: "dora@example.com" }),
    ));
    const restoreInput = vi.mocked(httpClient.post).mock.calls.at(-1)?.[1] as Record<string, unknown>;
    expect(restoreInput).not.toHaveProperty("password");
    expect(restoreInput).not.toHaveProperty("tenantId");
    expect(await screen.findByLabelText("Password setup link")).toHaveValue(
      "https://customers.example/reset-password?token=one-time",
    );
    expect(onCreated).toHaveBeenCalledOnce();
  });
});

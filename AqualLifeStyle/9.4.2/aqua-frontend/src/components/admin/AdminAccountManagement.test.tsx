import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminCustomers } from "./AdminCustomers";
import { AdminTenants } from "./AdminTenants";
import { AdminUsers } from "./AdminUsers";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn(), useMembershipsActions: vi.fn(), useMembershipsState: vi.fn(), useToast: vi.fn() }));
vi.mock("@/src/shared/api", () => ({ httpClient: { delete: vi.fn(), get: vi.fn(), post: vi.fn(), put: vi.fn() } }));
const state = (permissions: string[]) => ({ isAuthenticated: true, isReady: true, session: { accessToken: "token", expiresAt: null, user: { email: "admin@example.com", id: 1, name: "Admin", permissions, role: "SystemAdmin" } } });

describe("administrator account management", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) { this.removeAttribute("open"); });
    vi.mocked(useToast).mockReturnValue({ toast: vi.fn() } as ReturnType<typeof useToast>);
  });

  it("loads customer accounts from the administrator service", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Customers.View"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "customer@example.com", firstName: "Aqua", id: 2, isActive: true, lastName: "Customer", membershipId: null, membershipName: null, name: "Aqua Customer", tenantId: 1, userId: 3 }], totalCount: 1 });
    render(<AdminCustomers />);
    await waitFor(() => expect(screen.getByText("Aqua Customer")).toBeInTheDocument());
    expect(httpClient.get).toHaveBeenCalledWith("/api/services/app/AdminCustomer/GetAll?MaxResultCount=100");
  });

  it("updates a customer with PUT and membership-plan selection", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Customers.View", "Aqua.Admin.Customers.Edit"]));
    vi.mocked(httpClient.get).mockImplementation(async (url: string) => url.includes("GetMembershipOptions")
      ? [{ id: 8, name: "AQGreen" }]
      : { items: [{ contactNumber: "+27 82 123 4567", creationTime: "2026-01-01", email: "customer@example.com", firstName: "Aqua", homeAddress: "10 Customer Road, Johannesburg", id: 2, isActive: true, lastName: "Customer", membershipId: null, membershipName: null, name: "Aqua Customer", tenantId: 1, userId: 3 }], totalCount: 1 });
    vi.mocked(httpClient.put).mockResolvedValue({ id: 2 });
    render(<AdminCustomers />);

    fireEvent.click(await screen.findByRole("button", { name: "Edit account" }));
    await screen.findByRole("option", { name: "AQGreen" });
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Updated" } });
    fireEvent.change(screen.getByLabelText("Membership plan"), { target: { value: "8" } });
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Customer requested the correction" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith(
      "/api/services/app/AdminCustomer/Update",
      expect.objectContaining({ firstName: "Updated", id: 2, membershipId: 8 }),
    ));
  });

  it("removes a customer with DELETE and an audited reason", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Customers.View", "Aqua.Admin.Customers.Delete"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "customer@example.com", firstName: "Aqua", id: 2, isActive: true, lastName: "Customer", membershipId: null, membershipName: null, name: "Aqua Customer", tenantId: 1, userId: 3 }], totalCount: 1 });
    vi.mocked(httpClient.delete).mockResolvedValue(undefined);
    render(<AdminCustomers />);

    fireEvent.click(await screen.findByRole("button", { name: "Remove" }));
    fireEvent.change(screen.getByLabelText("Reason for action"), { target: { value: "Duplicate customer account" } });
    fireEvent.click(screen.getByRole("button", { name: "Remove account" }));

    await waitFor(() => expect(httpClient.delete).toHaveBeenCalledWith(
      "/api/services/app/AdminCustomer/Delete",
      { id: 2, justification: "Duplicate customer account" },
    ));
  });

  it("shows every granted user-account action", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View", "Aqua.Admin.Users.Edit", "Aqua.Admin.Users.AssignRole", "Aqua.Admin.Users.ResetPassword", "Aqua.Admin.Users.Delete"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "user@example.com", firstName: "Club", id: 4, invitationExpiresAt: "2026-02-01", invitationStatus: "Accepted", isActive: true, lastName: "User", requiresPasswordSetup: false, role: 1, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);
    await waitFor(() => expect(screen.getByText("Club User")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Edit details" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Change access" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Send password reset email" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove" })).toBeInTheDocument();
  });

  it("shows only valid pending-invitation actions under the invite permission", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View", "Aqua.Admin.Users.Edit", "Aqua.Admin.Users.AssignRole", "Aqua.Admin.Users.ResetPassword", "Aqua.Admin.Users.Invite"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "pending@example.com", firstName: "Pending", id: 5, invitationExpiresAt: "2026-08-10T10:00:00Z", invitationStatus: "Pending", isActive: false, lastName: "User", requiresPasswordSetup: true, role: 4, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);

    expect(await screen.findByText("Invitation pending")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Resend invitation" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Revoke invitation" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit details" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change access" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Send password reset email" })).not.toBeInTheDocument();
  });

  it("does not expose invitation actions without the invite permission", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "pending@example.com", firstName: "Pending", id: 5, invitationExpiresAt: "2026-08-10T10:00:00Z", invitationStatus: "Pending", isActive: false, lastName: "User", requiresPasswordSetup: true, role: 4, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);
    await screen.findByText("Pending User");
    expect(screen.queryByRole("button", { name: "Resend invitation" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Revoke invitation" })).not.toBeInTheDocument();
  });

  it("sends a password reset email without an administrator-selected password", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View", "Aqua.Admin.Users.ResetPassword"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "active@example.com", firstName: "Active", id: 6, invitationExpiresAt: null, invitationStatus: "Accepted", isActive: true, lastName: "User", requiresPasswordSetup: false, role: 1, tenantId: 1 }], totalCount: 1 });
    vi.mocked(httpClient.post).mockResolvedValue(undefined);
    render(<AdminUsers />);

    fireEvent.click(await screen.findByRole("button", { name: "Send password reset email" }));
    fireEvent.change(screen.getByLabelText("Reason for reset"), { target: { value: "User requested account recovery" } });
    fireEvent.click(screen.getByRole("button", { name: "Send reset email" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/AdminUser/ResetPassword",
      { id: 6, justification: "User requested account recovery" },
    ));
    expect(vi.mocked(httpClient.post).mock.calls[0][1]).not.toHaveProperty("newPassword");
  });

  it("shows current inactive state after invitation acceptance and withholds password reset", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View", "Aqua.Admin.Users.ResetPassword"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "inactive@example.com", firstName: "Inactive", id: 8, invitationExpiresAt: "2026-02-01", invitationStatus: "Accepted", isActive: false, lastName: "User", requiresPasswordSetup: false, role: 1, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);

    expect(await screen.findByText("Inactive")).toBeInTheDocument();
    expect(screen.queryByText("Invitation accepted")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Send password reset email" })).not.toBeInTheDocument();
  });

  it("offers an invitation for a setup-required legacy account without an invitation", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Users.View", "Aqua.Admin.Users.Invite", "Aqua.Admin.Users.ResetPassword"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "legacy@example.com", firstName: "Legacy", id: 7, invitationExpiresAt: null, invitationStatus: null, isActive: false, lastName: "User", requiresPasswordSetup: true, role: 4, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);

    expect(await screen.findByText("Setup required")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Resend invitation" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Send password reset email" })).not.toBeInTheDocument();
  });

  it("explains that an Area's initial administrator is invited", async () => {
    vi.mocked(useAuthState).mockReturnValue(state(["Aqua.Admin.Tenants.View", "Aqua.Admin.Tenants.Create"]));
    vi.mocked(httpClient.get).mockResolvedValue({ items: [], totalCount: 0 });
    render(<AdminTenants />);

    fireEvent.click(await screen.findByRole("button", { name: "Add area" }));
    expect(screen.getByText("The initial administrator will be invited by email to choose their password and activate their account.")).toBeInTheDocument();
  });
});

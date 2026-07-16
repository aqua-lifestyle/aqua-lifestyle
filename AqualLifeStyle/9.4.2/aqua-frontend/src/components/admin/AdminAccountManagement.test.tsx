import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminCustomers } from "./AdminCustomers";
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
      : { items: [{ creationTime: "2026-01-01", email: "customer@example.com", firstName: "Aqua", id: 2, isActive: true, lastName: "Customer", membershipId: null, membershipName: null, name: "Aqua Customer", tenantId: 1, userId: 3 }], totalCount: 1 });
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
    vi.mocked(httpClient.get).mockResolvedValue({ items: [{ creationTime: "2026-01-01", email: "user@example.com", firstName: "Club", id: 4, isActive: true, lastName: "User", role: 1, tenantId: 1 }], totalCount: 1 });
    render(<AdminUsers />);
    await waitFor(() => expect(screen.getByText("Club User")).toBeInTheDocument());
    expect(screen.getByRole("button", { name: "Edit details" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Change access" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Set temporary password" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Remove" })).toBeInTheDocument();
  });
});

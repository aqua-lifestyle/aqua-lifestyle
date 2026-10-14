import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import { EditCustomerAccountDialog } from "./EditCustomerAccountDialog";
import { ChangeUserAccessLevelDialog } from "./UserAccountManagementDialogs";

vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn(), put: vi.fn(), post: vi.fn() } }));

const customer = { contactNumber: "+27820000000", email: "member@example.test", firstName: "Test", homeAddress: "Synthetic test address", id: 1, isActive: true, lastName: "Member", membershipId: 3, tenantId: 7 };

describe("admin selection integrity", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) { this.removeAttribute("open"); });
    vi.mocked(httpClient.put).mockResolvedValue(undefined);
    vi.mocked(httpClient.post).mockResolvedValue(undefined);
  });

  it("retains the known membership ID while plan choices are still loading", async () => {
    let finish!: (options: { id: number; name: string }[]) => void;
    vi.mocked(httpClient.get).mockReturnValue(new Promise(resolve => { finish = resolve; }));
    render(<EditCustomerAccountDialog customer={customer} onUpdated={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit account" }));
    expect(screen.getByLabelText("Membership plan")).toHaveValue("3");
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Correct contact information" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith("/api/services/app/AdminCustomer/Update", expect.objectContaining({ id: 1, membershipId: 3 })));
    await act(async () => finish([]));
  });

  it("retains the known membership ID after choices fail to load", async () => {
    vi.mocked(httpClient.get).mockRejectedValue(new Error("Synthetic plan lookup failure"));
    render(<EditCustomerAccountDialog customer={customer} onUpdated={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit account" }));
    await screen.findByRole("alert");
    expect(screen.getByLabelText("Membership plan")).toHaveValue("3");
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Correct contact information" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith("/api/services/app/AdminCustomer/Update", expect.objectContaining({ id: 1, membershipId: 3 })));
  });

  it("preserves the existing ability to choose no membership explicitly", async () => {
    vi.mocked(httpClient.get).mockResolvedValue([{ id: 3, name: "Current plan" }]);
    render(<EditCustomerAccountDialog customer={customer} onUpdated={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit account" }));
    await screen.findByRole("option", { name: "Current plan" });
    fireEvent.change(screen.getByLabelText("Membership plan"), { target: { value: "" } });
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Explicit plan selection change" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith("/api/services/app/AdminCustomer/Update", expect.objectContaining({ id: 1, membershipId: null })));
  });

  it("does not convert an unsupported assignment identifier into no membership", async () => {
    vi.mocked(httpClient.get).mockResolvedValue([]);
    render(<EditCustomerAccountDialog customer={{ ...customer, membershipId: 0 }} onUpdated={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit account" }));
    const select = screen.getByLabelText("Membership plan");
    expect(select).toHaveValue("unavailable");
    expect(screen.getByRole("button", { name: "Save changes" })).toBeDisabled();
    fireEvent.submit(select.closest("form")!);
    expect(httpClient.put).not.toHaveBeenCalled();
    expect(await screen.findByRole("alert")).toHaveTextContent("The current membership assignment is unavailable.");
  });

  it("does not turn an unsupported current role into Customer without a selection", async () => {
    render(<ChangeUserAccessLevelDialog onUpdated={vi.fn()} user={{ email: "member@example.test", firstName: "Test", id: 1, isActive: true, lastName: "Member", role: 99 }} />);
    fireEvent.click(screen.getByRole("button", { name: "Change access" }));
    const select = screen.getByLabelText("Access level");
    expect(select).toHaveValue("");
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Explicit access selection" } });
    fireEvent.submit(select.closest("form")!);
    expect(httpClient.post).not.toHaveBeenCalled();
    fireEvent.change(select, { target: { value: "0" } });
    fireEvent.click(screen.getByRole("button", { name: "Update access" }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith("/api/services/app/AdminUser/AssignRole", { id: 1, justification: "Explicit access selection", role: 0 }));
  });

  it("reopens with the current membership assignment after an authoritative row refresh", async () => {
    vi.mocked(httpClient.get).mockResolvedValue([{ id: 3, name: "Previous plan" }, { id: 4, name: "Current plan" }]);
    const onUpdated = vi.fn();
    const { rerender } = render(<EditCustomerAccountDialog customer={customer} onUpdated={onUpdated} />);
    fireEvent.click(screen.getByRole("button", { name: "Edit account" }));
    await screen.findByRole("option", { name: "Current plan" });
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    rerender(<EditCustomerAccountDialog customer={{ ...customer, membershipId: 4 }} onUpdated={onUpdated} />);
    await act(async () => { fireEvent.click(screen.getByRole("button", { name: "Edit account" })); });
    expect(screen.getByLabelText("Membership plan")).toHaveValue("4");
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Correct contact information" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));
    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith("/api/services/app/AdminCustomer/Update", expect.objectContaining({ membershipId: 4 })));
  });

  it("reopens with the current access level after an authoritative row refresh", async () => {
    const user = { email: "member@example.test", firstName: "Test", id: 1, isActive: true, lastName: "Member", role: 1 };
    const onUpdated = vi.fn();
    const { rerender } = render(<ChangeUserAccessLevelDialog onUpdated={onUpdated} user={user} />);
    fireEvent.click(screen.getByRole("button", { name: "Change access" }));
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    rerender(<ChangeUserAccessLevelDialog onUpdated={onUpdated} user={{ ...user, role: 4 }} />);
    fireEvent.click(screen.getByRole("button", { name: "Change access" }));
    expect(screen.getByLabelText("Access level")).toHaveValue("4");
    fireEvent.change(screen.getByLabelText("Reason for change"), { target: { value: "Retain current account access" } });
    fireEvent.click(screen.getByRole("button", { name: "Update access" }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith("/api/services/app/AdminUser/AssignRole", { id: 1, justification: "Retain current account access", role: 4 }));
  });
});

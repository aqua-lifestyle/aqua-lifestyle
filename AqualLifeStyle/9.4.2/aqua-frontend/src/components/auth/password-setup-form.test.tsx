import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import { completePasswordReset } from "@/src/shared/api/account-email-service";
import { AbpHttpError } from "@/src/shared/api/abp-error";
import { acceptInternalAccountInvitation, validateInternalAccountInvitation } from "@/src/shared/api/internal-account-invitation-service";
import { PasswordSetupForm } from "./password-setup-form";

vi.mock("@/src/shared/api", () => ({ httpClient: { post: vi.fn() } }));
vi.mock("@/src/shared/api/account-email-service", () => ({ completePasswordReset: vi.fn() }));
vi.mock("@/src/shared/api/internal-account-invitation-service", () => ({ acceptInternalAccountInvitation: vi.fn(), validateInternalAccountInvitation: vi.fn() }));

describe("PasswordSetupForm", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState(null, "", "/reset-password");
  });
  it("lets the customer choose a private password using the one-time link", async () => {
    vi.mocked(httpClient.post).mockResolvedValue(true);
    render(<PasswordSetupForm areaName="Default" resetToken="one-time-token" userId={42} />);

    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Set password" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/Account/CompletePasswordSetup",
      {
        areaName: "Default",
        newPassword: "CustomerChosen123!",
        resetToken: "one-time-token",
        userId: 42,
      },
    ));
    expect(await screen.findByText("Your password is set and your sign-in access is ready.")).toBeInTheDocument();
  });

  it("rejects an incomplete setup link", () => {
    render(<PasswordSetupForm areaName="" resetToken="" userId={0} />);

    expect(screen.getByText("This password setup link is incomplete. Ask an administrator for a new link.")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Set password" })).not.toBeInTheDocument();
  });

  it("directs an invalid emailed reset link to self-service recovery", () => {
    render(<PasswordSetupForm areaName="Default" redirectPath="/profile" resetToken="" tenantId={1} userId={42} />);

    expect(screen.getByText(/Request a new link from the forgot-password page/)).toBeInTheDocument();
    expect(screen.queryByText(/Ask an administrator/)).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Request a new reset link" }))
      .toHaveAttribute("href", "/forgot-password?area=Default&redirect=%2Fprofile");
  });

  it("uses self-service recovery wording when an emailed reset request fails", async () => {
    vi.mocked(completePasswordReset).mockRejectedValue(new Error("The request failed."));
    render(<PasswordSetupForm areaName="Default" resetToken="token" tenantId={1} userId={42} />);

    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Set password" }));

    expect(await screen.findByText(/Request a new link from the forgot-password page/)).toBeInTheDocument();
    expect(screen.queryByText(/Ask an administrator/)).not.toBeInTheDocument();
  });

  it("resets an emailed account password and preserves the Area for sign in", async () => {
    vi.mocked(completePasswordReset).mockResolvedValue({ ok: true });
    render(
      <PasswordSetupForm
        areaName="Johannesburg"
        redirectPath="/profile"
        resetToken="email-reset-token"
        tenantId={7}
        userId={42}
      />,
    );

    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Set password" }));

    await waitFor(() => expect(completePasswordReset).toHaveBeenCalledWith(
      7,
      42,
      "email-reset-token",
      "CustomerChosen123!",
    ));
    expect(screen.getByRole("link", { name: "Continue to sign in" }))
      .toHaveAttribute("href", "/login?area=Johannesburg&redirect=%2Fprofile");
  });

  it("reads an emailed reset token from the fragment and removes it from browser history", async () => {
    window.history.replaceState(null, "", "/reset-password?tenantId=7&userId=42#token=fragment-token");
    vi.mocked(completePasswordReset).mockResolvedValue({ ok: true });
    render(<PasswordSetupForm areaName="Johannesburg" tenantId={7} userId={42} />);

    expect(window.location.hash).toBe("");
    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Set password" }));

    await waitFor(() => expect(completePasswordReset).toHaveBeenCalledWith(
      7,
      42,
      "fragment-token",
      "CustomerChosen123!",
    ));
  });

  it("validates and accepts an invitation token from the fragment", async () => {
    window.history.replaceState(null, "", "/reset-password?invitation=invite-code#token=setup-token");
    vi.mocked(validateInternalAccountInvitation).mockResolvedValue({ accessLevel: "Area Administrator", areaDisplayName: "Johannesburg Central", areaName: "Joburg", expiresAt: "2026-08-10T10:00:00Z", inviteeName: "New Admin", status: "Pending", username: "admin@example.com" });
    vi.mocked(acceptInternalAccountInvitation).mockResolvedValue({ areaName: "Joburg", wasAlreadyAccepted: false });
    render(<PasswordSetupForm areaName="" invitationCode="invite-code" userId={0} />);

    expect(window.location.hash).toBe("");
    expect(await screen.findByText("Johannesburg Central")).toBeInTheDocument();
    expect(screen.getByText("Area Administrator")).toBeInTheDocument();
    expect(screen.getByText("admin@example.com")).toBeInTheDocument();
    expect(validateInternalAccountInvitation).toHaveBeenCalledWith("invite-code", "setup-token");

    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "CustomerChosen123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Set password" }));

    await waitFor(() => expect(acceptInternalAccountInvitation).toHaveBeenCalledWith("invite-code", "setup-token", "CustomerChosen123!"));
    expect(screen.getByRole("link", { name: "Continue to sign in" })).toHaveAttribute("href", "/login?area=Joburg");
  });

  it("rejects an already accepted invitation without returning account details", async () => {
    vi.mocked(validateInternalAccountInvitation).mockRejectedValue(new AbpHttpError(400, { message: "Invitation already accepted.", details: "This one-time invitation has already been used. Continue to the normal sign-in page." }));
    render(<PasswordSetupForm areaName="" invitationCode="accepted-code" resetToken="accepted-token" userId={0} />);

    expect(await screen.findByText("This one-time invitation has already been used. Continue to the normal sign-in page.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Continue to sign in" })).toHaveAttribute("href", "/login");
    expect(screen.queryByLabelText("New password")).not.toBeInTheDocument();
    expect(acceptInternalAccountInvitation).not.toHaveBeenCalled();
    expect(screen.queryByText("existing@example.com")).not.toBeInTheDocument();
  });

  it.each([
    ["Invitation expired.", "Ask a Platform Administrator to send a new invitation."],
    ["Invitation revoked.", "Ask a Platform Administrator if you still require access."],
  ])("shows a safe terminal invitation error for %s", async (message, details) => {
    vi.mocked(validateInternalAccountInvitation).mockRejectedValue(Object.assign(new Error(message), { details }));
    render(<PasswordSetupForm areaName="" invitationCode="unavailable-code" resetToken="unavailable-token" userId={0} />);

    expect(await screen.findByText(message)).toBeInTheDocument();
    expect(screen.queryByLabelText("New password")).not.toBeInTheDocument();
    expect(acceptInternalAccountInvitation).not.toHaveBeenCalled();
  });
});

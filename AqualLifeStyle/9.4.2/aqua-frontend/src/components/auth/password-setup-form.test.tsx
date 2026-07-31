import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import { completePasswordReset } from "@/src/shared/api/account-email-service";
import { PasswordSetupForm } from "./password-setup-form";

vi.mock("@/src/shared/api", () => ({ httpClient: { post: vi.fn() } }));
vi.mock("@/src/shared/api/account-email-service", () => ({ completePasswordReset: vi.fn() }));

describe("PasswordSetupForm", () => {
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
});

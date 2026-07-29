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

  it("resets an emailed account password and preserves the Area for sign in", async () => {
    vi.mocked(completePasswordReset).mockResolvedValue({ ok: true });
    render(
      <PasswordSetupForm
        areaName="Johannesburg"
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
      .toHaveAttribute("href", "/login?area=Johannesburg");
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { httpClient } from "@/src/shared/api";
import { PasswordSetupForm } from "./password-setup-form";

vi.mock("@/src/shared/api", () => ({ httpClient: { post: vi.fn() } }));

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
});

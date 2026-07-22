import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { ChangePasswordForm } from "./change-password-form";

const replace = vi.fn();
const clearSession = vi.fn();
const toast = vi.fn();

vi.mock("next/navigation", () => ({ useRouter: () => ({ replace }) }));
vi.mock("@/src/providers", () => ({ useAuthActions: vi.fn(), useToast: vi.fn() }));
vi.mock("@/src/shared/api", () => ({ httpClient: { post: vi.fn() } }));

describe("ChangePasswordForm", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.mocked(useAuthActions).mockReturnValue({ clearSession, setReady: vi.fn(), setSession: vi.fn() });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("validates the password before sending it", async () => {
    render(<ChangePasswordForm />);
    fireEvent.change(screen.getByLabelText("Current password"), { target: { value: "123qwe" } });
    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "short" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "different" } });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));
    expect(await screen.findByText("Use at least 8 characters.")).toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("changes the password and requires a fresh sign-in", async () => {
    vi.mocked(httpClient.post).mockResolvedValue({
      message: "Your password was changed. Sign in again with your new password.",
      succeeded: true,
    });
    render(<ChangePasswordForm />);
    fireEvent.change(screen.getByLabelText("Current password"), { target: { value: "123qwe" } });
    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith("/api/services/app/MyAccount/ChangePassword", {
      currentPassword: "123qwe", newPassword: "PrivateAdminPassword123!",
    }));
    await waitFor(() => expect(clearSession).toHaveBeenCalled());
    expect(replace).toHaveBeenCalledWith("/login");
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ title: "Password updated" }));
  });

  it("keeps the session when the server rejects the change", async () => {
    vi.mocked(httpClient.post).mockRejectedValue(new Error("Incorrect password"));
    render(<ChangePasswordForm />);
    fireEvent.change(screen.getByLabelText("Current password"), { target: { value: "wrong-password" } });
    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));
    expect(await screen.findByText("Incorrect password")).toBeInTheDocument();
    expect(clearSession).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
  });

  it("shows an incorrect-password result without ending the session", async () => {
    vi.mocked(httpClient.post).mockResolvedValue({
      message: "Your current password is incorrect. No changes were made.",
      succeeded: false,
    });
    render(<ChangePasswordForm />);
    fireEvent.change(screen.getByLabelText("Current password"), { target: { value: "wrong-password" } });
    fireEvent.change(screen.getByLabelText("New password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.change(screen.getByLabelText("Confirm new password"), { target: { value: "PrivateAdminPassword123!" } });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    expect(await screen.findByText("Your current password is incorrect. No changes were made.")).toBeInTheDocument();
    expect(clearSession).not.toHaveBeenCalled();
    expect(replace).not.toHaveBeenCalled();
  });
});

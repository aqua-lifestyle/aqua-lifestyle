import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { UserDialog } from "./UserDialog";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn(), useToast: vi.fn() }));
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn(), post: vi.fn() } }));

const authState = (permissions: string[]) => ({
  isAuthenticated: true, isReady: true,
  session: { accessToken: "token", expiresAt: null, user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin", tenantId: 1 } },
});

describe("UserDialog", () => {
  const toast = vi.fn();
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) { this.removeAttribute("open"); });
    vi.mocked(useToast).mockReturnValue({ toast } as ReturnType<typeof useToast>);
  });

  it("requires the dedicated user create permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));
    render(<UserDialog />);
    expect(screen.queryByRole("button", { name: /add user/i })).not.toBeInTheDocument();
  });

  it("creates a tenant-scoped user with role and audit justification", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Users.Create"]));
    vi.mocked(httpClient.post).mockResolvedValue({ id: 10 });
    const onCreated = vi.fn();
    render(<UserDialog onCreated={onCreated} />);
    fireEvent.click(screen.getByRole("button", { name: /add user/i }));
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Grace" } });
    fireEvent.change(screen.getByLabelText("Last name"), { target: { value: "Hopper" } });
    fireEvent.change(screen.getByLabelText("Email address"), { target: { value: "grace@example.com" } });
    fireEvent.change(screen.getByLabelText("Temporary password"), { target: { value: "SafePassword123!" } });
    fireEvent.change(screen.getByLabelText("Access level"), { target: { value: "1" } });
    fireEvent.change(screen.getByLabelText("Reason for creating this account"), { target: { value: "Approved account" } });
    fireEvent.click(screen.getByRole("button", { name: /create user/i }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/AdminUser/Create",
      expect.objectContaining({ email: "grace@example.com", role: 1, tenantId: 1, justification: "Approved account" }),
    ));
    expect(onCreated).toHaveBeenCalled();
  });
});

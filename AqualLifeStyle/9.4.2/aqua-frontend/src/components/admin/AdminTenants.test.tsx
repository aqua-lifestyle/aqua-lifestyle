import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminTenants } from "./AdminTenants";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn(), useToast: vi.fn() }));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn(), post: vi.fn() } };
});

describe("AdminTenants", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (this: HTMLDialogElement) {
      this.setAttribute("open", "");
    });
    HTMLDialogElement.prototype.close = vi.fn(function (this: HTMLDialogElement) {
      this.removeAttribute("open");
    });
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: {
          email: "admin@example.com",
          id: 1,
          name: "Administrator",
          permissions: [
            "Aqua.Admin.Tenants.View",
            "Aqua.Admin.Tenants.Activate",
          ],
          role: "SystemAdmin",
        },
      },
    });
    vi.mocked(useToast).mockReturnValue({ toast: vi.fn() });
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [{
        activationHistoryBeginsAt: null,
        areaLeaderId: null,
        areaLeaderName: null,
        hasActivationHistory: false,
        id: 1,
        isActive: true,
        name: "Johannesburg Central",
        tenancyName: "JohannesburgCentral",
      }],
      totalCount: 1,
    });
    vi.mocked(httpClient.post).mockResolvedValue({});
  });

  it("records a prospective cutoff baseline through the audited action", async () => {
    render(<AdminTenants />);

    expect(await screen.findByText("Cutoff history not recorded")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Record cutoff baseline" }));
    const dialog = screen.getByRole("dialog", { name: "Record cutoff baseline" });
    fireEvent.change(within(dialog).getByLabelText("Reason for action"), {
      target: { value: "Observed current Area state before commission rollout" },
    });
    fireEvent.click(within(dialog).getByRole("button", { name: "Record current state" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      "/api/services/app/AdminTenant/ObserveActivationState",
      {
        id: 1,
        justification: "Observed current Area state before commission rollout",
      },
    ));
  });

  it("hides the baseline action without activation permission", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: {
          email: "admin@example.com",
          id: 1,
          name: "Administrator",
          permissions: ["Aqua.Admin.Tenants.View"],
          role: "SystemAdmin",
        },
      },
    });

    render(<AdminTenants />);

    expect(await screen.findByText("Cutoff history not recorded")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Record cutoff baseline" })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });
});

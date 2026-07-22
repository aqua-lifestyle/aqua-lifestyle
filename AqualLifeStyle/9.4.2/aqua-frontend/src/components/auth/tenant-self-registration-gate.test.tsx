import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useTenantState } from "@/src/providers";
import { getTenantSelfRegistrationAvailability } from "@/src/shared/api/auth-service";

import { TenantSelfRegistrationGate } from "./tenant-self-registration-gate";

vi.mock("@/src/shared/api/auth-service", () => ({
  getTenantSelfRegistrationAvailability: vi.fn(),
}));

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>("@/src/providers");
  return {
    ...actual,
    useTenantState: vi.fn(),
  };
});

describe("TenantSelfRegistrationGate", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: "CapeTown", isHost: false });
  });

  it("shows the registration form when the current Area enables registration", async () => {
    vi.mocked(getTenantSelfRegistrationAvailability).mockResolvedValue({
      isSelfRegistrationEnabled: true,
      ok: true,
    });

    render(
      <TenantSelfRegistrationGate>
        {(tenancyName) => <p>Create your {tenancyName} account</p>}
      </TenantSelfRegistrationGate>,
    );

    expect(await screen.findByText("Create your CapeTown account")).toBeInTheDocument();
    expect(getTenantSelfRegistrationAvailability).toHaveBeenCalledWith("CapeTown");
  });

  it("fails closed and returns users to sign in when registration is disabled", async () => {
    vi.mocked(getTenantSelfRegistrationAvailability).mockResolvedValue({
      isSelfRegistrationEnabled: false,
      ok: true,
    });

    render(
      <TenantSelfRegistrationGate>
        {() => <p>Create your account</p>}
      </TenantSelfRegistrationGate>,
    );

    expect(await screen.findByText(/created by an authorised/i)).toBeInTheDocument();
    expect(screen.queryByText("Create your account")).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Return to sign in" })).toHaveAttribute(
      "href",
      "/login",
    );
  });

  it("does not expose registration when availability cannot be confirmed", async () => {
    vi.mocked(getTenantSelfRegistrationAvailability).mockResolvedValue({ ok: false });

    render(
      <TenantSelfRegistrationGate>
        {() => <p>Create your account</p>}
      </TenantSelfRegistrationGate>,
    );

    expect(await screen.findByText(/could not be confirmed/i)).toBeInTheDocument();
    expect(screen.queryByText("Create your account")).not.toBeInTheDocument();
  });
});

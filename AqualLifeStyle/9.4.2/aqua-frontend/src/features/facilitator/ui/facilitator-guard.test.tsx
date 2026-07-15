import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { isFacilitator } from "@/src/shared/auth/roles";
import { FacilitatorGuard } from "./facilitator-guard";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => "/facilitator/dashboard",
  useRouter: () => ({ replace }),
}));

vi.mock("@/src/providers", async () => ({
  ...(await vi.importActual<typeof import("@/src/providers")>("@/src/providers")),
  useAuthState: vi.fn(),
}));

const authState = (role: string) => ({
  isAuthenticated: true,
  isReady: true,
  session: { accessToken: "token", expiresAt: null, user: { email: null, id: 1, name: "Facilitator", permissions: [], role } },
});

describe("FacilitatorGuard", () => {
  beforeEach(() => vi.resetAllMocks());

  it("normalizes the Facilitator role", () => {
    expect(isFacilitator("Facilitator")).toBe(true);
    expect(isFacilitator("facilitator")).toBe(true);
    expect(isFacilitator("Member")).toBe(false);
  });

  it("redirects signed-out visitors to login", async () => {
    vi.mocked(useAuthState).mockReturnValue({ isAuthenticated: false, isReady: true, session: null });
    render(<FacilitatorGuard><p>Protected</p></FacilitatorGuard>);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login?redirect=%2Ffacilitator%2Fdashboard"));
  });

  it("redirects authenticated non-facilitators", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState("Member"));
    render(<FacilitatorGuard><p>Protected</p></FacilitatorGuard>);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/dashboard"));
  });

  it("renders for a Facilitator without client permission claims", () => {
    vi.mocked(useAuthState).mockReturnValue(authState("Facilitator"));
    render(<FacilitatorGuard><p>Protected</p></FacilitatorGuard>);
    expect(screen.getByText("Protected")).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
});

import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { isAreaLeader } from "@/src/shared/auth/roles";
import { AreaLeaderGuard } from "./area-leader-guard";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => "/area-leader/dashboard",
  useRouter: () => ({ replace }),
}));

vi.mock("@/src/providers", async () => ({
  ...(await vi.importActual<typeof import("@/src/providers")>("@/src/providers")),
  useAuthState: vi.fn(),
}));

const authState = (role: string, permissions: string[] = []) => ({
  isAuthenticated: true,
  isReady: true,
  session: { accessToken: "token", expiresAt: null, user: { email: null, id: 1, name: "Leader", permissions, role } },
});

describe("AreaLeaderGuard", () => {
  beforeEach(() => vi.resetAllMocks());

  it("normalizes the ABP AreaLeader role", () => {
    expect(isAreaLeader("AreaLeader")).toBe(true);
    expect(isAreaLeader("area_leader")).toBe(true);
    expect(isAreaLeader("Member")).toBe(false);
  });

  it("redirects signed-out visitors back to login", async () => {
    vi.mocked(useAuthState).mockReturnValue({ isAuthenticated: false, isReady: true, session: null });
    render(<AreaLeaderGuard><p>Protected</p></AreaLeaderGuard>);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/login?redirect=%2Farea-leader%2Fdashboard"));
  });

  it("redirects authenticated non-leaders", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState("Member"));
    render(<AreaLeaderGuard><p>Protected</p></AreaLeaderGuard>);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("/dashboard"));
  });

  it("renders for an Area Leader when the token has no client permission claims", () => {
    vi.mocked(useAuthState).mockReturnValue(authState("AreaLeader"));
    render(<AreaLeaderGuard><p>Protected</p></AreaLeaderGuard>);
    expect(screen.getByText("Protected")).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
});

import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { AuthenticatedPage } from "./authenticated-page";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  usePathname: () => "/profile",
  useRouter: () => ({ replace }),
}));

vi.mock("@/src/providers", async () => ({
  ...(await vi.importActual<typeof import("@/src/providers")>("@/src/providers")),
  useAuthState: vi.fn(),
}));

describe("AuthenticatedPage", () => {
  beforeEach(() => vi.resetAllMocks());

  it("waits for the saved session to be restored before deciding access", () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: false,
      isReady: false,
      session: null,
    });

    render(<AuthenticatedPage><p>Profile</p></AuthenticatedPage>);

    expect(screen.getByText("Checking your account…")).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });

  it("replaces a protected route with sign-in when signed out", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: false,
      isReady: true,
      session: null,
    });

    render(<AuthenticatedPage><p>Profile</p></AuthenticatedPage>);

    expect(screen.queryByText("Profile")).not.toBeInTheDocument();
    await waitFor(() => {
      expect(replace).toHaveBeenCalledWith("/login?redirect=%2Fprofile");
    });
  });

  it("renders the protected page for an authenticated account", () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: null,
      },
    });

    render(<AuthenticatedPage><p>Profile</p></AuthenticatedPage>);

    expect(screen.getByText("Profile")).toBeInTheDocument();
    expect(replace).not.toHaveBeenCalled();
  });
});

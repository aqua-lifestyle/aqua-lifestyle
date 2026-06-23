import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminAccessLevels } from "./AdminAccessLevels";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn() } }));

describe("AdminAccessLevels", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: { accessToken: "token", expiresAt: null, user: { email: "admin@example.com", id: 1, name: "Admin", permissions: ["Pages.Roles"], role: "SystemAdmin", tenantId: 1 } },
    });
  });

  it("presents technical roles as business access levels", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [{ description: null, displayName: "Admin", grantedPermissions: ["Aqua.Admin"], id: 1, name: "Admin" }],
      totalCount: 1,
    });

    render(<AdminAccessLevels />);

    await waitFor(() => expect(screen.getByText("Area administrator")).toBeInTheDocument());
    expect(screen.getByText("1 assigned")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith("/api/services/app/Role/GetAll?MaxResultCount=100");
  });
});

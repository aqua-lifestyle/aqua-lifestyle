import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { AdminMembers } from "./AdminMembers";

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
  useMembershipsActions: vi.fn(),
  useMembershipsState: vi.fn(),
  useToast: vi.fn(),
}));
vi.mock("@/src/shared/api", () => ({ httpClient: { get: vi.fn(), post: vi.fn() } }));

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin", tenantId: 1 },
  },
});

describe("AdminMembers", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useToast).mockReturnValue({ toast: vi.fn() } as ReturnType<typeof useToast>);
  });

  it("does not request member data without the member view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));
    render(<AdminMembers />);

    expect(screen.getByText("You do not have permission to view members.")).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  it("loads members and hides mutation controls without their granular permissions", async () => {
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.Admin.Members.View"]));
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [{
        creationTime: "2026-07-15T12:00:00Z",
        email: "ada@example.com",
        firstName: "Ada",
        id: 12,
        isActive: true,
        lastName: "Lovelace",
        membershipId: 2,
        membershipName: "Onyx",
        membershipType: 1,
        tenantId: 1,
        userId: 21,
      }],
      totalCount: 1,
    });
    render(<AdminMembers />);

    await waitFor(() => expect(screen.getByText("Ada Lovelace")).toBeInTheDocument());
    expect(httpClient.get).toHaveBeenCalledWith("/api/services/app/AdminMember/GetAll?MaxResultCount=100");
    expect(screen.getByText("Onyx")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit profile" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Change plan" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Suspend" })).not.toBeInTheDocument();
  });
});

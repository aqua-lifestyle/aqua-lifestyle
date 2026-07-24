import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { AdminSidebar } from "./AdminSidebar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/admin/customers",
}));

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
}));

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: "2026-07-16T00:00:00Z",
    user: { email: "admin@example.com", id: 7, name: "Admin", permissions, role: "SystemAdmin" },
  },
});

describe("AdminSidebar", () => {
  beforeEach(() => {
    vi.mocked(useAuthState).mockReturnValue(authState([
      "Aqua.Admin.Customers.View",
      "Aqua.Admin.ProgrammeParticipations.View",
      "Aqua.Admin.Users.View",
      "Pages.Roles",
    ]));
  });

  it("only renders management links granted to the administrator", () => {
    render(<AdminSidebar />);

    expect(screen.getByRole("link", { name: "Dashboard" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Customer accounts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "User accounts & access" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Access levels" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Programme participation" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Areas" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Club members" })).not.toBeInTheDocument();
  });

  it("marks the current section as active", () => {
    render(<AdminSidebar />);

    expect(screen.getByRole("link", { name: "Customer accounts" })).toHaveAttribute("aria-current", "page");
  });
});

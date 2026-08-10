import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { usePendingProgrammeApprovals } from "@/src/shared/hooks/use-pending-programme-approvals";
import { AdminSidebar } from "./AdminSidebar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/admin/customers",
}));

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
}));

vi.mock("@/src/shared/hooks/use-pending-programme-approvals", () => ({
  usePendingProgrammeApprovals: vi.fn(),
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
    vi.mocked(usePendingProgrammeApprovals).mockReturnValue({
      reload: vi.fn(),
      summary: undefined,
    });
    vi.mocked(useAuthState).mockReturnValue(authState([
      "Aqua.Admin.Customers.View",
      "Aqua.Admin.ProgrammeParticipations.View",
      "Aqua.Admin.Commissions.View",
      "Aqua.Admin.Savings.View",
      "Aqua.Admin.Loans.View",
      "Aqua.Admin.EntryMonthlyObligations.View",
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
    expect(screen.getByRole("link", { name: "Weekly earnings" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Savings accounts" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Loan agreements" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "AQGreen commitments" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Areas" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Club members" })).not.toBeInTheDocument();
  });

  it("marks the current section as active", () => {
    render(<AdminSidebar />);

    expect(screen.getByRole("link", { name: "Customer accounts" })).toHaveAttribute("aria-current", "page");
  });

  it("shows the global pending approval count beside the navigation link", () => {
    vi.mocked(usePendingProgrammeApprovals).mockReturnValue({
      reload: vi.fn(),
      summary: { aqGreenCount: 2, onyxCount: 1, totalCount: 3 },
    });

    render(<AdminSidebar />);

    expect(screen.getByLabelText("3 approvals awaiting review"))
      .toBeInTheDocument();
    expect(screen.getByRole("link", {
      name: /programme participation.*3 approvals awaiting review/i,
    })).toHaveAttribute("href", "/admin/programme-participations");
  });
});

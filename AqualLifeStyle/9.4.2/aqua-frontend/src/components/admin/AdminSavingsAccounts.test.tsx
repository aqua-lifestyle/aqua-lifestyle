import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { AdminSavingsAccounts } from "./AdminSavingsAccounts";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn() } };
});

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: {
      email: "admin@example.com",
      id: 1,
      name: "Administrator",
      permissions,
      role: "SystemAdmin",
    },
  },
});

describe("AdminSavingsAccounts", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.Admin.Savings.View"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [
        {
          contributionWindowEndDay: 15,
          contributionWindowStartDay: 1,
          contributions: [{ paymentId: "payment-1" }],
          currency: "ZAR",
          customerId: 10,
          customerName: "Lethabo Mokoena",
          email: "lethabo@example.com",
          id: "account-1",
          maturedAt: null,
          maturityInterestAmount: null,
          maturityInterestRatePercent: 20,
          maturityPayoutAmount: null,
          maturityPrincipalAmount: null,
          maturesAt: "2027-07-05T10:00:00Z",
          minimumContributionAmount: 100,
          openedAt: "2026-07-05T10:00:00Z",
          principalBalance: 500,
          projectedInterestAmount: 100,
          projectedMaturityAmount: 600,
          requiresMaturityProcessing: false,
          status: "Active",
          tenantId: 1,
          termsVersion: "2026-07",
        },
      ],
      totalCount: 1,
    });
  });

  it("reconciles persisted savings without exposing payment actions", async () => {
    render(<AdminSavingsAccounts />);

    expect(await screen.findByText("Lethabo Mokoena")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      `${apiEndpoints.savings.getAdminAccounts}?MaxResultCount=100`,
    );
    expect(screen.getByText("Area 1")).toBeInTheDocument();
    expect(screen.getByText(/payouts cannot be recorded/i)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /record|pay|contribute/i }),
    ).not.toBeInTheDocument();
  });

  it("does not request accounts without administrator savings access", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminSavingsAccounts />);

    expect(
      screen.getByText(
        "You do not have permission to view savings accounts.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

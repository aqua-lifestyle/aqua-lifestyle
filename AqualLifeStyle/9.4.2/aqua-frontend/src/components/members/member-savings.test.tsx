import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import type { SavingsAccount } from "@/src/shared/domain/savings";
import { MemberSavings } from "./member-savings";

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
      email: "member@example.com",
      id: 7,
      name: "Club Member",
      permissions,
      role: "Member",
    },
  },
});

const account: SavingsAccount = {
  contributionWindowEndDay: 15,
  contributionWindowStartDay: 1,
  contributions: [
    {
      amount: 500,
      contributedAt: "2026-07-10T10:00:00Z",
      interestAmount: 100,
      interestRatePercent: 20,
      paymentId: "payment-1",
    },
  ],
  currency: "ZAR",
  customerId: 10,
  customerName: "Club Member",
  email: "member@example.com",
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
};

describe("MemberSavings", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.Savings.ViewSelf"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({ account });
  });

  it("shows the persisted savings ledger and projected maturity values", async () => {
    render(<MemberSavings />);

    expect(
      await screen.findByRole("heading", { name: "My savings" }),
    ).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.savings.getMyAccount,
    );
    expect(
      await screen.findByText(
        "20% of this contribution",
        {},
        { timeout: 5_000 },
      ),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/R\s*500[,.]00/)).toHaveLength(2);
    expect(screen.getByText(/R\s*600[,.]00/)).toBeInTheDocument();
    expect(screen.queryByText("Bronze")).not.toBeInTheDocument();
  });

  it("shows an honest empty state when no account is persisted", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({ account: null });

    render(<MemberSavings />);

    expect(await screen.findByText("No savings account")).toBeInTheDocument();
    expect(
      screen.getByText(/contact the club team if you expected/i),
    ).toBeInTheDocument();
  });

  it("does not request savings without self-view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberSavings />);

    expect(
      screen.getByText(
        "Your account does not have access to Club Member savings.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  it("shows a descriptive request error", async () => {
    vi.mocked(httpClient.get).mockRejectedValue(
      new Error("Savings service unavailable"),
    );

    render(<MemberSavings />);

    expect(
      await screen.findByText("Savings service unavailable"),
    ).toBeInTheDocument();
  });
});

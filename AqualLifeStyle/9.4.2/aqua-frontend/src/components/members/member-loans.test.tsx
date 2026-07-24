import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { activeLoanAgreement } from "../loans/loan-test-data";
import { MemberLoans } from "./member-loans";

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

describe("MemberLoans", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.Loans.ViewSelf"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [activeLoanAgreement],
    });
  });

  it("shows confirmed loan balances and repayments", async () => {
    render(<MemberLoans />);

    expect(await screen.findByText("Onyx loan")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.loans.getMyAgreements,
    );
    expect(screen.getByText("Includes 30% interest")).toBeInTheDocument();
    expect(screen.getAllByText("Week 1")).toHaveLength(2);
    expect(screen.getByText(/only confirmed payments/i)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /accept|pay/i }),
    ).not.toBeInTheDocument();
  });

  it("shows an honest empty state when no loan is recorded", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({ items: [] });

    render(<MemberLoans />);

    expect(await screen.findByText("No loan agreements")).toBeInTheDocument();
  });

  it("does not request loans without self-view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberLoans />);

    expect(
      screen.getByText(
        "Your account does not have access to loan information.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

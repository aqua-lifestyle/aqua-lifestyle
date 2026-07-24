import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { activeLoanAgreement } from "../loans/loan-test-data";
import { AdminLoanAgreements } from "./AdminLoanAgreements";

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

describe("AdminLoanAgreements", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.Admin.Loans.View"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [activeLoanAgreement],
      totalCount: 1,
    });
  });

  it("reconciles persisted loans without exposing financial actions", async () => {
    render(<AdminLoanAgreements />);

    expect(await screen.findByText("Lethabo Mokoena")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      `${apiEndpoints.loans.getAdminAgreements}?MaxResultCount=100`,
    );
    expect(screen.getByText("Area 1")).toBeInTheDocument();
    expect(screen.getByText(/payments cannot be recorded/i)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /offer|approve|pay/i }),
    ).not.toBeInTheDocument();
  });

  it("does not request loans without administrator access", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminLoanAgreements />);

    expect(
      screen.getByText(
        "You do not have permission to view loan agreements.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
  return { ...actual, httpClient: { get: vi.fn(), post: vi.fn() } };
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
    HTMLDialogElement.prototype.showModal = vi.fn(function (
      this: HTMLDialogElement,
    ) { this.setAttribute("open", ""); });
    HTMLDialogElement.prototype.close = vi.fn(function (
      this: HTMLDialogElement,
    ) { this.removeAttribute("open"); });
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.Admin.Loans.View"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [activeLoanAgreement],
      totalCount: 1,
    });
  });

  it("requires an explicit justified administrator graduation decision", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.Admin.Loans.View",
        "Aqua.Admin.ProgrammeParticipations.GraduateToOnyx",
      ]),
    );

    render(<AdminLoanAgreements />);

    fireEvent.click(await screen.findByRole("button", { name: "Review graduation" }));
    fireEvent.change(screen.getByLabelText("Reason for action"), {
      target: { value: "Level 2 and approved funding evidence reviewed." },
    });
    fireEvent.click(screen.getByRole("button", { name: "Approve graduation" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.graduateAQGreenToOnyx,
      {
        justification: "Level 2 and approved funding evidence reviewed.",
        loanAgreementId: activeLoanAgreement.id,
      },
    ));
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

  it("warns when the reconciliation result is truncated", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [activeLoanAgreement],
      totalCount: 101,
    });

    render(<AdminLoanAgreements />);

    expect(
      await screen.findByText(/Showing 1 of 101 records/i),
    ).toBeInTheDocument();
  });
});

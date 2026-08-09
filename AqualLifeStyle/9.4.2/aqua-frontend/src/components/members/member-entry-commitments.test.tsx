import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { overdueEntryCommitment } from "../entry-commitments/entry-commitment-test-data";
import { MemberEntryCommitments } from "./member-entry-commitments";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/browser/navigation", () => ({ navigateToExternalUrl: vi.fn() }));
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
      email: "member@example.com",
      id: 7,
      name: "Club Member",
      permissions,
      role: "Member",
    },
  },
});

describe("MemberEntryCommitments", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.EntryMonthlyObligations.ViewSelf",
        "Aqua.EntryMonthlyObligations.Pay",
      ]),
    );
    vi.mocked(httpClient.get).mockResolvedValue([overdueEntryCommitment]);
    vi.mocked(httpClient.post).mockResolvedValue({
      amount: 600,
      checkoutId: "checkout-1",
      checkoutUrl: "https://payments.example.test/checkout/july",
      currency: "ZAR",
      obligationId: overdueEntryCommitment.id,
      periodMonth: 7,
      periodYear: 2026,
    });
  });

  it("shows persisted AQGreen commitment status", async () => {
    render(<MemberEntryCommitments />);

    expect(await screen.findByText("July 2026")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.entryMonthlyObligations.getMyObligations,
    );
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getAllByText(/R\s*600[,.]00/)).toHaveLength(2);
    expect(screen.getByRole("button", { name: "Pay July 2026" })).toBeInTheDocument();
  });

  it("starts checkout for the exact selected month", async () => {
    const june = {
      ...overdueEntryCommitment,
      id: "commitment-june",
      periodMonth: 6,
    };
    vi.mocked(httpClient.get).mockResolvedValue([june, overdueEntryCommitment]);
    render(<MemberEntryCommitments />);

    fireEvent.click(await screen.findByRole("button", { name: "Pay July 2026" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.entryMonthlyObligations.createCheckout,
      { obligationId: overdueEntryCommitment.id },
    ));
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/july",
    );
  });

  it("does not offer payment without the payment permission", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.EntryMonthlyObligations.ViewSelf"]),
    );
    render(<MemberEntryCommitments />);

    expect(await screen.findByText("July 2026")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pay July 2026" }))
      .not.toBeInTheDocument();
  });

  it("does not request commitments without self-view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberEntryCommitments />);

    expect(
      screen.getByText(
        "Your account does not have access to AQGreen commitments.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

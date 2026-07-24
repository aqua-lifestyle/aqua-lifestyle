import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { overdueEntryCommitment } from "../entry-commitments/entry-commitment-test-data";
import { AdminEntryCommitments } from "./AdminEntryCommitments";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn() } };
});

describe("AdminEntryCommitments", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: {
          email: "admin@example.com",
          id: 1,
          name: "Administrator",
          permissions: ["Aqua.Admin.EntryMonthlyObligations.View"],
          role: "SystemAdmin",
        },
      },
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [overdueEntryCommitment],
      totalCount: 1,
    });
  });

  it("reconciles commitments without exposing payment actions", async () => {
    render(<AdminEntryCommitments />);

    expect(await screen.findByText("Lethabo Mokoena")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      `${apiEndpoints.entryMonthlyObligations.getAdminObligations}?MaxResultCount=100`,
    );
    expect(screen.getByText("Area 1")).toBeInTheDocument();
    expect(screen.getByText(/payments cannot be recorded/i)).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /pay|record/i }),
    ).not.toBeInTheDocument();
  });

  it("warns when the reconciliation result is truncated", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [overdueEntryCommitment],
      totalCount: 101,
    });

    render(<AdminEntryCommitments />);

    expect(
      await screen.findByText(/Showing 1 of 101 records/i),
    ).toBeInTheDocument();
  });
});

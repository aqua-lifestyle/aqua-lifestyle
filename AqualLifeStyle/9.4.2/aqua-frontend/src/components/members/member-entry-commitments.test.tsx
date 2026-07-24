import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { overdueEntryCommitment } from "../entry-commitments/entry-commitment-test-data";
import { MemberEntryCommitments } from "./member-entry-commitments";

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

describe("MemberEntryCommitments", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.EntryMonthlyObligations.ViewSelf"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue([overdueEntryCommitment]);
  });

  it("shows persisted Entry commitment status", async () => {
    render(<MemberEntryCommitments />);

    expect(await screen.findByText("August 2026")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.entryMonthlyObligations.getMyObligations,
    );
    expect(screen.getByText("Overdue")).toBeInTheDocument();
    expect(screen.getAllByText(/R\s*600[,.]00/)).toHaveLength(2);
    expect(
      screen.queryByRole("button", { name: /pay|record/i }),
    ).not.toBeInTheDocument();
  });

  it("does not request commitments without self-view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberEntryCommitments />);

    expect(
      screen.getByText(
        "Your account does not have access to Entry commitments.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { MemberProgrammeProgress } from "./member-programme-progress";
import { heldProgress, levelOneProgress } from "./programme-progress-test-data";

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

describe("MemberProgrammeProgress", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.ProgrammeParticipations.ViewSelf"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue(levelOneProgress);
  });

  it("loads and shows AQGreen level, earnings, and funeral cover", async () => {
    render(<MemberProgrammeProgress />);

    expect(
      await screen.findByRole("heading", { name: "Level 1" }),
    ).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.getMyProgress,
    );
    expect(
      screen.getByText(
        /R\s*30[\s,.]000[,.]00\s+funeral cover included/i,
      ),
    ).toBeInTheDocument();
    expect(screen.getAllByText("Weekly earnings").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/R\s*150[,.]00/).length).toBeGreaterThan(0);
    expect(
      screen.getByText("Pay your AQGreen monthly subscription."),
    ).toBeInTheDocument();
  });

  it("reports held earnings with the obligation reason", async () => {
    vi.mocked(httpClient.get).mockResolvedValue(heldProgress);

    render(<MemberProgrammeProgress />);

    expect(await screen.findByText(/AQGreen monthly commitment is overdue/)).toBeInTheDocument();
    expect(screen.getByText("Overdue")).toBeInTheDocument();
  });

  it("keeps V2 placement occupancy separate from personal recruitment", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      ...levelOneProgress,
      directRecruits: 2,
      structuralProgress: {
        achievedCount: 18,
        completedLevel: 1,
        cutoff: "2026-08-11T10:00:00Z",
        measureLabel: "Qualifying placement occupants",
        progressPercent: 72,
        remainingCount: 7,
        requiredCount: 25,
        rulesVersion: "AQG-V2-1",
        targetLevel: 2,
      },
    });

    render(<MemberProgrammeProgress />);

    expect(await screen.findByText(
      "Structural progress: 18 of 25 qualifying placement occupants",
    )).toBeInTheDocument();
    expect(screen.getByText(/Personal recruits: 2\./)).toBeInTheDocument();
    expect(screen.getByRole("progressbar", {
      name: "72% of the current AQGreen structural target",
    })).toHaveAttribute("aria-valuenow", "72");
    expect(screen.queryByText("2 of 5 direct recruits")).not.toBeInTheDocument();
  });

  it("does not request progress without self-view permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberProgrammeProgress />);

    expect(
      screen.getByText("Your account does not have access to AQGreen progress."),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

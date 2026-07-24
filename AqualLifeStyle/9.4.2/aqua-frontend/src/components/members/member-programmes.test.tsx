import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { MemberProgrammes } from "./member-programmes";

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
      email: "member@example.com",
      id: 7,
      name: "Club Member",
      permissions,
      role: "Guest",
    },
  },
});

describe("MemberProgrammes", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    HTMLDialogElement.prototype.showModal = vi.fn(function (
      this: HTMLDialogElement,
    ) {
      this.setAttribute("open", "");
    });
    HTMLDialogElement.prototype.close = vi.fn(function (
      this: HTMLDialogElement,
    ) {
      this.removeAttribute("open");
    });
    vi.mocked(useAuthState).mockReturnValue(
      authState(["Aqua.ProgrammeParticipations.ViewSelf"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: null,
      onyx: null,
      travelBenefit: null,
    });
    vi.mocked(httpClient.post).mockResolvedValue({});
  });

  it("shows both joining choices without requiring a recruiter", async () => {
    render(<MemberProgrammes />);

    await screen.findByRole("button", { name: "Join Entry" });
    expect(screen.getByRole("button", { name: "Join Onyx" })).toBeInTheDocument();
    expect(screen.getAllByText(/recruiter is optional/i)).toHaveLength(2);
  });

  it("starts Entry independently and reloads the participation record", async () => {
    render(<MemberProgrammes />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Join Entry" }),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm joining choice" }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.startEntry,
        { recruiterCustomerId: null },
      ),
    );
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(2));
  });

  it("does not load participation without the dedicated permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<MemberProgrammes />);

    expect(
      screen.getByText(
        "Your account does not have access to programme participation.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  it("shows a qualified Club Member's travel benefit without promising a booking", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: null,
      onyx: null,
      travelBenefit: {
        activatedAt: null,
        eligibleAt: "2026-07-20T10:00:00Z",
        memberTripContributionPercent: 10,
        status: "Waiting period",
        waitingPeriodEndsAt: "2026-10-20T10:00:00Z",
      },
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Travel benefit")).toBeInTheDocument();
    expect(screen.getByText("Waiting period")).toBeInTheDocument();
    expect(screen.getByText(/contribute 10%/i)).toBeInTheDocument();
    expect(
      screen.getByText(/trip selection, pricing, and booking/i),
    ).toBeInTheDocument();
  });
});

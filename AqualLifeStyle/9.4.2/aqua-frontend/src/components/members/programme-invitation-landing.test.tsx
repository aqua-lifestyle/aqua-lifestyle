import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { ProgrammeInvitationLanding } from "./programme-invitation-landing";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn(), post: vi.fn() } };
});

const preview = {
  areaName: "Default",
  inviteCode: "AQ7G2X9KLMNP",
  programmeKey: "AQGREEN",
  programmeName: "AQGreen",
  recruiterClubMemberNumber: "CLB-ABCDEFGH2345",
  recruiterEligible: true,
  recruiterName: "Ada Recruiter",
};

describe("ProgrammeInvitationLanding", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(httpClient.get).mockResolvedValue(preview);
    vi.mocked(httpClient.post).mockResolvedValue({});
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: { email: "invitee@example.com", id: 2, name: "Invitee", permissions: [], role: "Guest" },
      },
    });
  });

  it("previews the recruiter and confirms joining without exposing internal IDs", async () => {
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    expect(await screen.findByText("Ada Recruiter")).toBeInTheDocument();
    expect(screen.getByText("CLB-ABCDEFGH2345")).toBeInTheDocument();
    expect(screen.getByText(/eligible to recruit into AQGreen/i)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /confirm and join/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    ));
    expect(await screen.findByText(/network place is recorded/i)).toBeInTheDocument();
  });

  it("offers registration and sign-in before confirmation", async () => {
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: false,
      isReady: true,
      session: null,
    });
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    expect(await screen.findByRole("link", { name: /create my account/i }))
      .toHaveAttribute("href", expect.stringContaining("/signup?area=Default"));
    expect(screen.getByRole("link", { name: /sign in to continue/i }))
      .toHaveAttribute("href", "/login?redirect=%2Fi%2FAQ7G2X9KLMNP");
  });
});

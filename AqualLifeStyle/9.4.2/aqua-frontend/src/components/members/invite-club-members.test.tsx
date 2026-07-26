import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { InviteClubMembers } from "./invite-club-members";

vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn() } };
});

describe("InviteClubMembers", () => {
  const writeText = vi.fn().mockResolvedValue(undefined);
  const share = vi.fn().mockResolvedValue(undefined);

  beforeEach(() => {
    vi.clearAllMocks();
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText },
    });
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: share,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      invitations: [{
        clubMemberNumber: "CLB-ABCDEFGH2345",
        code: "AQ7G2X9KLMNP",
        programmeKey: "AQGREEN",
        programmeName: "AQGreen",
      }],
    });
  });

  it("loads a stable invitation and supports copy and native sharing", async () => {
    render(<InviteClubMembers />);

    expect(await screen.findByText("AQ7G2X9KLMNP")).toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.getMyInvitations,
    );

    fireEvent.click(screen.getByRole("button", { name: /copy code/i }));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith("AQ7G2X9KLMNP"));

    fireEvent.click(screen.getByRole("button", { name: /^share/i }));
    await waitFor(() => expect(share).toHaveBeenCalledWith(expect.objectContaining({
      url: "http://localhost:3000/i/AQ7G2X9KLMNP",
    })));
  });

  it("uses the clipboard when native sharing is unavailable", async () => {
    Object.defineProperty(navigator, "share", {
      configurable: true,
      value: undefined,
    });
    render(<InviteClubMembers />);

    fireEvent.click(await screen.findByRole("button", { name: /^share/i }));
    await waitFor(() => expect(writeText).toHaveBeenCalledWith(
      "http://localhost:3000/i/AQ7G2X9KLMNP",
    ));
  });
});

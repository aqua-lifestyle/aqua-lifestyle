import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { ProgrammeInvitationLanding } from "./programme-invitation-landing";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
  useSystemHealthActions: vi.fn(),
  useSystemHealthState: vi.fn(),
}));
vi.mock("@/src/shared/browser/navigation", () => ({ navigateToExternalUrl: vi.fn() }));
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
    vi.mocked(httpClient.post).mockResolvedValue({
      amount: 6120,
      checkoutUrl: "https://payments.example.test/checkout/secure",
      currency: "ZAR",
    });
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: true,
      isReady: true,
      session: {
        accessToken: "token",
        expiresAt: null,
        user: { email: "invitee@example.com", id: 2, name: "Invitee", permissions: [], role: "Guest" },
      },
    });
    vi.mocked(useSystemHealthActions).mockReturnValue({
      checkHealth: vi.fn().mockResolvedValue(undefined),
    });
    vi.mocked(useSystemHealthState).mockReturnValue({
      errorMessage: null,
      health: {
        buildId: "test-build",
        checkedAtUtc: "2026-08-01T10:00:00Z",
        contractCapabilities: [
          "aqgreen-single-payment-v1",
          "direct-onyx-checkout-v1",
        ],
        databaseStatus: "Healthy",
        environment: "Test",
        imageId: "unavailable",
        isDatabaseReachable: true,
        paymentContractVersion: "aqua-payments-2026-08-01-single-payment",
        releaseDate: "2026-08-01T00:00:00Z",
        status: "Healthy",
        traceId: "test-trace",
        version: "1.0.0",
      },
      isError: false,
      isPending: false,
      isSuccess: true,
    });
  });

  it("blocks an authenticated invitation payment against an incompatible API", async () => {
    vi.mocked(useSystemHealthState).mockReturnValue({
      errorMessage: "Contract mismatch",
      health: null,
      isError: true,
      isPending: false,
      isSuccess: false,
    });

    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    expect(await screen.findByText(/cannot verify a compatible payment API/i))
      .toBeInTheDocument();
    expect(screen.getByRole("button", { name: /confirm and continue/i }))
      .toBeDisabled();
  });

  it("routes an AQGreen invitation only to the AQGreen joining endpoint", async () => {
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    expect(await screen.findByText("Ada Recruiter")).toBeInTheDocument();
    expect(screen.getByText("CLB-ABCDEFGH2345")).toBeInTheDocument();
    expect(
      screen.getByText(/eligible to invite Club Members to AQGreen/i),
    ).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /confirm and continue to payment/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    ));
    expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 0 },
    );
    expect(httpClient.post).not.toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
      expect.anything(),
    );
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it("routes an Onyx invitation only to the Onyx joining endpoint", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      ...preview,
      programmeKey: "ONYX",
      programmeName: "Onyx",
    });

    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(
      await screen.findByRole("button", { name: /confirm and continue to payment/i }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
        { inviteCode: "AQ7G2X9KLMNP" },
      ),
    );
    expect(httpClient.post).not.toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.startEntry,
      expect.anything(),
    );
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it.each([
    ["JASPER", "Jasper"],
    ["BUSINESSPREMIER", "BusinessPremier"],
    ["FUTURE-PROGRAMME", "Future programme"],
  ])(
    "fails closed for unsupported programme key %s",
    async (programmeKey, programmeName) => {
      vi.mocked(httpClient.get).mockResolvedValue({
        ...preview,
        programmeKey,
        programmeName,
      });

      render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

      expect(
        await screen.findByText(
          "Invitations are not currently supported for this programme.",
        ),
      ).toBeInTheDocument();
      expect(
        screen.queryByRole("button", { name: /confirm and join/i }),
      ).not.toBeInTheDocument();
      expect(screen.queryByText(/R1,200|R6,120/)).not.toBeInTheDocument();
      expect(httpClient.post).not.toHaveBeenCalled();
    },
  );

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

  it("uses the safe signup page when the invitation has no Area name", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      ...preview,
      areaName: null,
    });
    vi.mocked(useAuthState).mockReturnValue({
      isAuthenticated: false,
      isReady: true,
      session: null,
    });

    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    expect(await screen.findByRole("link", { name: /create my account/i }))
      .toHaveAttribute("href", "/signup");
  });
});

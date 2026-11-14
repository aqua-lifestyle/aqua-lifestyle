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
import { AbpHttpError, normalizeNetworkError } from "@/src/shared/api/abp-error";

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
  areaName: "Johannesburg",
  inviteCode: "AQ7G2X9KLMNP",
  programmeKey: "AQGREEN",
  programmeName: "AQGreen",
  recruiterClubMemberNumber: "CLB-ABCDEFGH2345",
  recruiterEligible: true,
  recruiterName: "Ada Recruiter",
  tenancyName: "Default",
};

describe("ProgrammeInvitationLanding", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
    vi.mocked(httpClient.get).mockResolvedValue(preview);
    vi.mocked(httpClient.post).mockImplementation(async (url) => url === apiEndpoints.programmeParticipations.startEntry
      ? { joiningPaidAmount: 0, joiningSchedule: null }
      : {
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
        user: { email: "invitee@example.com", id: 2, name: "Invitee", permissions: ["Aqua.ProgrammeParticipations.Join"], role: "Guest", tenantId: 1 },
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
          "aqgreen-flexible-joining-v1",
          "programme-approval-queue-v1",
          "direct-onyx-checkout-v1",
        ],
        databaseStatus: "Healthy",
        environment: "Test",
        imageId: "unavailable",
        isDatabaseReachable: true,
        paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
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

    expect(await screen.findByText("Payments are temporarily unavailable"))
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
    fireEvent.click(screen.getByRole("radio", {
      name: /Pay two R600 instalments/i,
    }));
    fireEvent.click(screen.getByRole("button", { name: /confirm and continue to payment/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    ));
    expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    );
    expect(httpClient.post).not.toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
      expect.anything(),
    );
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it("explains an operational hold and keeps payment disabled even if capabilities are present", async () => {
    const current = vi.mocked(useSystemHealthState).getMockImplementation()?.();
    vi.mocked(useSystemHealthState).mockReturnValue({
      ...current!, health: { ...current!.health!, paymentAvailability: "Held" },
    });
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);
    expect(await screen.findByText(/Payments are temporarily on hold/i)).toBeInTheDocument();
    expect(screen.queryByText(/cannot verify a compatible payment API/i)).not.toBeInTheDocument();
    const pay = screen.getByRole("button", { name: /confirm and continue/i });
    expect(pay).toBeDisabled(); fireEvent.click(pay);
    expect(httpClient.post).not.toHaveBeenCalled();
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
      .toHaveAttribute("href", expect.stringContaining("/signup?area=Default&invite=AQ7G2X9KLMNP"));
    expect(screen.getByRole("link", { name: /sign in to continue/i }))
      .toHaveAttribute("href", "/login?area=Default&invite=AQ7G2X9KLMNP&redirect=%2Fi%2FAQ7G2X9KLMNP");
  });

  it("shows an actionable invalid-invitation message without exposing validation diagnostics or opening checkout", async () => {
    vi.mocked(httpClient.get).mockRejectedValueOnce(new AbpHttpError(400, {
      validationErrors: [{ message: "The field InviteCode must be a string with a minimum length of 12." }],
    }));
    render(<ProgrammeInvitationLanding inviteCode="invalid" />);
    expect(await screen.findByText("Invitation unavailable")).toBeInTheDocument();
    expect(screen.getByText(/ask them for a valid invitation/i)).toBeInTheDocument();
    expect(screen.queryByText(/field InviteCode/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /confirm and continue/i })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("does not offer joining or registration under an ineligible recruiter", async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ ...preview, recruiterEligible: false });
    render(<ProgrammeInvitationLanding inviteCode={preview.inviteCode} />);
    expect(await screen.findByText("This invitation is not ready to use")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Contact the club team" })).toHaveAttribute("href", "/contact");
    expect(screen.queryByRole("button", { name: /confirm and continue/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /create my account/i })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("does not request checkout after admission is rejected", async () => {
    vi.mocked(httpClient.post).mockRejectedValueOnce(new AbpHttpError(500, {
      details: "Independent admission and recruiter-only joining are unavailable on this path.",
    }));
    render(<ProgrammeInvitationLanding inviteCode={preview.inviteCode} />);
    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));
    expect(await screen.findByText("Before you join AQGreen")).toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledExactlyOnceWith(apiEndpoints.programmeParticipations.startEntry, { inviteCode: preview.inviteCode });
    expect(navigateToExternalUrl).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: /confirm and continue/i })).toBeDisabled();
    expect(screen.queryByText(/Independent admission and recruiter-only/i)).not.toBeInTheDocument();
  });

  it("requires a status check after an uncertain checkout result and does not repeat admission or checkout", async () => {
    vi.mocked(httpClient.post).mockResolvedValueOnce({ joiningPaidAmount: 0, joiningSchedule: null }).mockRejectedValueOnce(normalizeNetworkError());
    render(<ProgrammeInvitationLanding inviteCode={preview.inviteCode} />);
    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));
    expect(await screen.findByText("Payment status not confirmed")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Check my payment status" })).toHaveAttribute("href", "/member/programmes");
    expect(screen.queryByRole("button", { name: /confirm and continue/i })).not.toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledTimes(2);
    expect(navigateToExternalUrl).not.toHaveBeenCalled();
    expect(screen.queryByText(/No payment has been taken/i)).not.toBeInTheDocument();
  });

  it("never uses a preview belonging to a different invitation", async () => {
    render(<ProgrammeInvitationLanding inviteCode="ZZZZZZZZZZZZ" />);
    expect(await screen.findByText("Invitation unavailable")).toBeInTheDocument();
    expect(screen.queryByText("Ada Recruiter")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /confirm and continue/i })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("uses the safe signup page when the invitation has no Area name", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      ...preview,
      tenancyName: null,
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

  const partPaidRecord = {
    statusCode: "AwaitingActivationPayment",
    joiningSchedule: 1,
    joiningPaidAmount: 600,
    nextPaymentAmount: 600,
    currency: "ZAR",
    programmeName: "AQGreen",
  };
  const mockSecondCheckout = (checkoutUrl: string) => {
    vi.mocked(httpClient.post).mockImplementation(async (requestUrl) =>
      requestUrl === apiEndpoints.programmeParticipations.startEntry
        ? partPaidRecord
        : { amount: 600, checkoutUrl, currency: "ZAR" });
  };

  it("continues a replayed invitation on its recorded two-instalment plan without defaulting to R1,200 once", async () => {
    mockSecondCheckout("https://payments.example.test/checkout/second");
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    ));
    expect(await screen.findByRole("button", { name: /Complete joining:.*600/ })).toBeEnabled();
    expect(httpClient.post).not.toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      expect.anything(),
    );
    expect(screen.queryByRole("radio", { name: /Pay R1,200 once/i })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: /Complete joining:.*600/ }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
    expect(navigateToExternalUrl).toHaveBeenCalledWith("https://payments.example.test/checkout/second");
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.2")).toBe("part-paid");
  });

  it("retires a settled first-stage reminder when the invitation returns the confirmed first instalment", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.2", "unpaid");
    mockSecondCheckout("https://payments.example.test/checkout/second");
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));

    expect(await screen.findByRole("button", { name: /Complete joining:.*600/ })).toBeEnabled();
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.2")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Complete joining:.*600/ }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
    expect(navigateToExternalUrl).toHaveBeenCalledWith("https://payments.example.test/checkout/second");
  });

  it("continues a started-but-unpaid recorded plan instead of restarting schedule selection", async () => {
    vi.mocked(httpClient.post).mockImplementation(async (requestUrl) =>
      requestUrl === apiEndpoints.programmeParticipations.startEntry
        ? { ...partPaidRecord, statusCode: "AwaitingJoiningPayment", joiningPaidAmount: 0 }
        : { amount: 600, checkoutUrl: "https://payments.example.test/checkout/first", currency: "ZAR" });
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));

    await screen.findByRole("button", { name: /Complete joining:.*600/ });
    expect(screen.queryByRole("radio", { name: /Pay R1,200 once/i })).not.toBeInTheDocument();
    fireEvent.click(await screen.findByRole("button", { name: /Complete joining:.*600/ }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.2")).toBe("unpaid");
  });

  it("does not open another checkout for a replayed invitation whose joining is already complete", async () => {
    vi.mocked(httpClient.post).mockResolvedValueOnce({
      ...partPaidRecord,
      statusCode: "PaymentConfirmedAwaitingApproval",
      joiningPaidAmount: 1200,
      nextPaymentAmount: null,
    });
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));

    expect(await screen.findByText("Check your joining status")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Complete joining|confirm and continue/i })).not.toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledExactlyOnceWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    );
  });

  it("keeps a same-stage reminder blocking a replayed invitation until the status is reviewed", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.2", "part-paid");
    vi.mocked(httpClient.post).mockResolvedValueOnce(partPaidRecord);
    render(<ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />);

    fireEvent.click(await screen.findByRole("button", { name: /confirm and continue/i }));

    expect(await screen.findByText("Check your payment status")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Complete joining/i })).not.toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledExactlyOnceWith(
      apiEndpoints.programmeParticipations.startEntry,
      { inviteCode: "AQ7G2X9KLMNP" },
    );
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.2")).toBe("part-paid");
  });
});

// These existing cases exercise ordinary deployments. Pilot-specific cases use
// the actual mode boundary separately rather than consuming their domain mock responses.
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({
  usePilotStatus: () => ({ isPilot: false, modeKnown: true }),
}));

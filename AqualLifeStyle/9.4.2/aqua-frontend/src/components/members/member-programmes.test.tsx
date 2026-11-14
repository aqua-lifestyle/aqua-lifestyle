import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { apiEndpoints, httpClient, refreshAccessToken } from "@/src/shared/api";
import { MemberProgrammes } from "./member-programmes";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { normalizeNetworkError } from "@/src/shared/api/abp-error";
import { useMyProgrammeJourney } from "@/src/shared/hooks/use-my-programme-journey";
import {
  createJourneyResponse,
  createProgrammeJourney,
} from "./programme-journey-test-data";

vi.mock("@/src/providers", () => ({
  useAuthState: vi.fn(),
  useSystemHealthActions: vi.fn(),
  useSystemHealthState: vi.fn(),
}));
vi.mock("@/src/shared/browser/navigation", () => ({ navigateToExternalUrl: vi.fn() }));
vi.mock("@/src/shared/hooks/use-my-programme-journey", () => ({
  useMyProgrammeJourney: vi.fn(),
}));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return {
    ...actual,
    httpClient: { get: vi.fn(), post: vi.fn() },
    refreshAccessToken: vi.fn(),
  };
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
      tenantId: 1,
    },
  },
});

const showAwaitingAQGreen = (pendingAQGreenCheckout: unknown = null, withPaymentFacts = true) => {
  vi.mocked(httpClient.get).mockResolvedValue({
    entry: { programmeName: "AQGreen", currency: "ZAR", statusCode: "AwaitingJoiningPayment", status: "Awaiting joining payment", joinedIndependently: true, joiningSchedule: null, joiningPaidAmount: withPaymentFacts ? 0 : undefined, nextPaymentAmount: 1200, canRecruitForThisProgramme: false, isActive: false },
    onyx: null, pendingAQGreenCheckout,
  });
  vi.mocked(useMyProgrammeJourney).mockReturnValue({
    data: createJourneyResponse(createProgrammeJourney("AQGREEN", { hasParticipation: true, nextActionCode: "CompleteJoiningPayment", nextActionTitle: "Complete AQGreen joining", participationStatus: "Awaiting joining payment" })),
    errorMessage: undefined, isLoading: false,
  });
};

describe("MemberProgrammes", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.localStorage.clear();
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
      authState(["Aqua.ProgrammeParticipations.ViewSelf", "Aqua.ProgrammeParticipations.Join"]),
    );
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
          "member-programme-journey-v1",
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
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: null,
      onyx: null,
      travelBenefit: null,
    });
    vi.mocked(httpClient.post).mockResolvedValue({
      amount: 6120,
      checkoutUrl: "https://payments.example.test/checkout/secure",
      currency: "ZAR",
    });
    vi.mocked(refreshAccessToken).mockResolvedValue("renewed-token");
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(),
      errorMessage: undefined,
      isLoading: false,
    });
  });

  it("blocks payment actions when the API payment contract is incompatible", async () => {
    vi.mocked(useSystemHealthState).mockReturnValue({
      errorMessage: "Backend health response did not match the expected contract.",
      health: null,
      isError: true,
      isPending: false,
      isSuccess: false,
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Payment availability not confirmed"))
      .toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Join AQGreen" })).not.toBeInTheDocument();
    expect(await screen.findByRole("button", { name: "Join Onyx" }))
      .toBeDisabled();
  });

  it("explains an operational hold without presenting it as an outdated deployment", async () => {
    const current = vi.mocked(useSystemHealthState).getMockImplementation()?.();
    vi.mocked(useSystemHealthState).mockReturnValue({
      ...current!, health: { ...current!.health!, paymentAvailability: "Held" },
    });
    render(<MemberProgrammes />);
    expect(await screen.findByText(/Payments are temporarily on hold/i)).toBeInTheDocument();
    expect(screen.queryByText(/cannot verify a compatible payment API/i)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Join AQGreen" })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("fails closed when the API does not advertise the member journey capability", async () => {
    const current = vi.mocked(useSystemHealthState).getMockImplementation()?.();
    vi.mocked(useSystemHealthState).mockReturnValue({
      ...current!,
      health: {
        ...current!.health!,
        contractCapabilities: [
          "aqgreen-flexible-joining-v1",
          "programme-approval-queue-v1",
          "direct-onyx-checkout-v1",
        ],
      },
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Programme information unavailable"))
      .toBeInTheDocument();
    expect(useMyProgrammeJourney).toHaveBeenCalledWith(false, false);
  });

  it("refreshes account access after programme activation changes the role", async () => {
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: "2026-07-30T00:00:00Z",
        canRecruitForThisProgramme: true,
        currency: "ZAR",
        isActive: true,
        joinedIndependently: true,
        nextPaymentAmount: null,
        nextPaymentDescription: null,
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-07-30T00:00:00Z",
        status: "Active",
      },
      onyx: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    await waitFor(() => expect(refreshAccessToken).toHaveBeenCalledOnce());
  });

  it("keeps a recruitable member signed in when no refresh token is available", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.ProgrammeParticipations.ViewSelf",
        "Aqua.ProgrammeParticipations.Join",
      ]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: "2026-07-30T00:00:00Z",
        canRecruitForThisProgramme: true,
        currency: "ZAR",
        isActive: true,
        joinedIndependently: true,
        nextPaymentAmount: null,
        nextPaymentDescription: null,
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-07-30T00:00:00Z",
        status: "Active",
      },
      onyx: null,
      travelBenefit: null,
    });
    vi.mocked(refreshAccessToken).mockResolvedValue(null);

    render(<MemberProgrammes />);

    await waitFor(() => expect(refreshAccessToken).toHaveBeenCalledOnce());
    expect(
      await screen.findByText(
        /sign out and sign in again to load your updated invitation access/i,
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(/session-ended/i)).not.toBeInTheDocument();
  });

  it("guides an unapproved prospective founder without offering independent AQGreen admission", async () => {
    render(<MemberProgrammes />);

    expect(await screen.findByText("Before you join AQGreen")).toBeInTheDocument();
    expect(screen.getByText(/authorised Host administrator must confirm your founder eligibility/i)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Contact the club team" })).toHaveAttribute("href", "/contact");
    expect(screen.queryByRole("button", { name: "Join AQGreen" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /request founder approval/i })).not.toBeInTheDocument();
    expect(within(screen.getByRole("article", { name: "AQGreen" })).queryByText("Start my own network")).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: "Join Onyx" })).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Invite Club Members" }),
    ).not.toBeInTheDocument();
  });

  it.each([[0, "Pay R1,200 once"], [1, "Pay first R600 instalment"]] as const)("uses an existing Host-authorised root participation for schedule %i without restarting admission", async (schedule, button) => {
    showAwaitingAQGreen();
    render(<MemberProgrammes />);

    fireEvent.click(await screen.findByRole("button", { name: button }));

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
        { schedule },
      ),
    );
    expect(httpClient.post).toHaveBeenCalledOnce();
    expect(httpClient.post).not.toHaveBeenCalledWith(apiEndpoints.programmeParticipations.startEntry, expect.anything());
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it("retains the checkout review across refresh even when the deployed member read returns no pending URL", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    showAwaitingAQGreen();
    render(<MemberProgrammes />);
    expect(await screen.findByRole("button", { name: "Refresh payment status" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Pay R1,200|Pay first R600/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Continue secure payment" })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("allows the second distinct R600 checkout only after the server confirms the first instalment", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    showAwaitingAQGreen();
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: { programmeName: "AQGreen", currency: "ZAR", statusCode: "AwaitingActivationPayment", joinedIndependently: true, joiningSchedule: 1, joiningPaidAmount: 600, nextPaymentAmount: 600 },
      onyx: null, pendingAQGreenCheckout: null,
    });
    render(<MemberProgrammes />);
    fireEvent.click(await screen.findByRole("button", { name: /Complete joining:.*600/ }));
    await waitFor(() => expect(httpClient.post).toHaveBeenCalledExactlyOnceWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout, { schedule: 1 },
    ));
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBe("part-paid");
  });

  it("retires a settled first-stage reminder when the confirmed first instalment leaves no open checkout", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "unpaid");
    showAwaitingAQGreen();
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: { programmeName: "AQGreen", currency: "ZAR", statusCode: "AwaitingActivationPayment", status: "Awaiting second joining instalment", joinedIndependently: true, joiningSchedule: 1, joiningPaidAmount: 600, nextPaymentAmount: 600, canRecruitForThisProgramme: false, isActive: false },
      onyx: null, pendingAQGreenCheckout: null,
    });
    render(<MemberProgrammes />);
    expect(await screen.findByRole("button", { name: /Complete joining:.*600/ })).toBeEnabled();
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBeNull();
  });

  it("keeps an unresolved second-instalment reminder even after the first instalment is confirmed", async () => {
    window.localStorage.setItem("aqua.aqgreenCheckoutReview.1.7", "part-paid");
    showAwaitingAQGreen();
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: { programmeName: "AQGreen", currency: "ZAR", statusCode: "AwaitingActivationPayment", status: "Awaiting second joining instalment", joinedIndependently: true, joiningSchedule: 1, joiningPaidAmount: 600, nextPaymentAmount: 600, canRecruitForThisProgramme: false, isActive: false },
      onyx: null, pendingAQGreenCheckout: null,
    });
    render(<MemberProgrammes />);
    expect(await screen.findByRole("button", { name: "Refresh payment status" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Complete joining/ })).not.toBeInTheDocument();
    expect(window.localStorage.getItem("aqua.aqgreenCheckoutReview.1.7")).toBe("part-paid");
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("does not infer zero paid or a default schedule when the joining facts are unavailable", async () => {
    showAwaitingAQGreen(null, false);
    render(<MemberProgrammes />);
    expect(await screen.findByText("Joining payment details unavailable")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Pay R1,200|Pay first R600|Complete joining:/ })).not.toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("does not offer an enabled payment operation when the account lacks joining permission", async () => {
    showAwaitingAQGreen();
    vi.mocked(useAuthState).mockReturnValue(authState(["Aqua.ProgrammeParticipations.ViewSelf"]));
    render(<MemberProgrammes />);
    expect(await screen.findByText("Joining access needs review")).toBeInTheDocument();
    const payment = await screen.findByRole("button", { name: "Pay R1,200 once" });
    expect(payment).toBeDisabled(); fireEvent.click(payment);
    expect(httpClient.post).not.toHaveBeenCalled();
    expect(window.localStorage.length).toBe(0);
  });

  it("keeps an uncertain checkout response actionable without claiming no charge or offering an ordinary retry", async () => {
    showAwaitingAQGreen();
    vi.mocked(httpClient.post).mockRejectedValueOnce(normalizeNetworkError());
    render(<MemberProgrammes />);
    fireEvent.click(await screen.findByRole("button", { name: "Pay R1,200 once" }));
    expect(await screen.findByText("Payment status not confirmed")).toBeInTheDocument();
    expect(screen.getByText(/Do not start a second payment/i)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Refresh payment status" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Pay R1,200 once" })).not.toBeInTheDocument();
    expect(screen.queryByText(/No payment has been taken/i)).not.toBeInTheDocument();
    expect(httpClient.post).toHaveBeenCalledOnce();
  });

  it("creates an Onyx checkout without claiming participation has started", async () => {
    render(<MemberProgrammes />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Join Onyx" }),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Continue to secure payment" }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
        { recruiterCustomerId: null },
      ),
    );
    expect(screen.queryByText(/Onyx participation started/i)).not.toBeInTheDocument();
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it("shows a resumable checkout without presenting pending payment as participation", async () => {
      vi.mocked(useAuthState).mockReturnValue(
        authState([
          "Aqua.ProgrammeParticipations.ViewSelf",
          "Aqua.ProgrammeParticipations.Join",
        ]),
      );
      vi.mocked(httpClient.get).mockResolvedValue({
        entry: null,
        onyx: null,
        pendingDirectOnyxCheckout: {
          amount: 6120,
          checkoutUrl: "https://payments.example.test/checkout/resume",
          currency: "ZAR",
          status: "Awaiting payment",
        },
        travelBenefit: null,
      });

      render(<MemberProgrammes />);

    expect(await screen.findByText("Awaiting payment")).toBeInTheDocument();
    expect(screen.getByText(/participation and network place do not exist yet/i))
      .toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Continue secure payment" }))
      .toHaveAttribute("href", "https://payments.example.test/checkout/resume");
    expect(screen.queryByRole("button", { name: "Join Onyx" }))
      .not.toBeInTheDocument();
  });

  it("lets an awaiting AQGreen participant resume the same secure checkout", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        hasParticipation: true,
        nextActionBody: "Continue the existing secure checkout.",
        nextActionCode: "CompleteJoiningPayment",
        nextActionTitle: "Complete AQGreen joining",
        participationStatus: "Awaiting joining payment",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        isActive: false,
        joinedIndependently: true,
        nextPaymentAmount: 1200,
        nextPaymentDescription: "Full AQGreen joining payment",
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-07-26T10:00:00Z",
        status: "Awaiting joining payment",
        statusCode: "AwaitingJoiningPayment",
      },
      onyx: null,
      pendingAQGreenCheckout: {
        amount: 1200,
        checkoutUrl: "https://payments.example.test/checkout/aqgreen-resume",
        currency: "ZAR",
        status: "Awaiting payment",
      },
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    expect((await screen.findAllByText("Awaiting joining payment")).length)
      .toBeGreaterThan(0);
    expect(screen.getByRole("link", { name: "Continue secure payment" }))
      .toHaveAttribute(
        "href",
        "https://payments.example.test/checkout/aqgreen-resume",
      );
    expect(screen.queryByRole("button", { name: "Pay R1,200 securely" }))
      .not.toBeInTheDocument();
  });

  it("shows the recorded Area decision reason for a declined participation", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        decisionReason: "Identity evidence requires correction before activation.",
        hasParticipation: true,
        nextActionBody: "Review the Area decision before taking another action.",
        nextActionCode: "ReviewDecision",
        nextActionTitle: "Review the Area decision",
        participationStatus: "Declined",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        decidedAt: "2026-08-09T11:42:22Z",
        decisionReason: "Identity evidence requires correction before activation.",
        isActive: false,
        joinedIndependently: true,
        nextPaymentAmount: null,
        nextPaymentDescription: null,
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-08-09T10:00:00Z",
        status: "Declined",
        statusCode: "Rejected",
      },
      onyx: null,
      pendingAQGreenCheckout: null,
      pendingDirectOnyxCheckout: null,
      funeralCover: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    expect((await screen.findAllByText("Declined")).length).toBeGreaterThan(0);
    expect(screen.getByText(/Identity evidence requires correction/))
      .toBeInTheDocument();
  });

  it("preserves completion of a verified historical AQGreen instalment", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        hasParticipation: true,
        nextActionCode: "CompleteJoiningPayment",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        isActive: false,
        joinedIndependently: true,
        joiningPaidAmount: 600,
        joiningSchedule: 1,
        nextPaymentAmount: 600,
        nextPaymentDescription: "Second AQGreen joining instalment",
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-07-26T10:00:00Z",
        status: "Awaiting activation payment",
        statusCode: "AwaitingActivationPayment",
      },
      onyx: null,
      pendingAQGreenCheckout: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);
    fireEvent.click(await screen.findByRole("button", { name: /Complete joining:.*600/i }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
  });

  it("fails closed when a stale participation snapshot still allows payment after confirmation", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        activationSteps: [
          { code: "Started", explanation: "Created.", label: "Joining started", state: "Complete" },
          { code: "Payment", explanation: "Confirmed.", label: "Joining payment", state: "Complete" },
          { code: "Approval", explanation: "Johannesburg Area Administrator review is pending.", label: "Area approval", state: "Current" },
          { code: "Active", explanation: "Network progression begins after activation.", label: "Programme active", state: "Upcoming" },
        ],
        hasParticipation: true,
        joining: {
          completedAt: "2026-08-09T10:00:00Z",
          isComplete: true,
          kind: "One-time AQGreen joining requirement",
          paidAmount: 1200,
          progressPercent: 100,
          remainingAmount: 0,
          requiredAmount: 1200,
          scheduleLabel: "One payment",
        },
        nextActionBody: "Your joining payment is complete. Do not pay it again; Area Administrator approval is next.",
        nextActionCode: "AwaitApproval",
        nextActionTitle: "Await Area approval",
        participationStatus: "Awaiting Area approval",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      areaId: "a0000000-0000-0000-0000-000000000001",
      areaName: "Johannesburg",
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        isActive: false,
        joinedIndependently: true,
        joiningPaidAmount: 0,
        joiningSchedule: null,
        joiningTotalAmount: 1200,
        nextPaymentAmount: 1200,
        nextPaymentDescription: "AQGreen joining payment",
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-08-09T09:00:00Z",
        status: "Awaiting joining payment",
        statusCode: "AwaitingJoiningPayment",
      },
      onyx: null,
      pendingAQGreenCheckout: null,
      pendingDirectOnyxCheckout: null,
      funeralCover: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Area: Johannesburg")).toBeInTheDocument();
    expect(screen.getByText("Joining payment confirmed")).toBeInTheDocument();
    expect(screen.getByText(/Do not pay it again/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Pay|Complete joining/i }))
      .not.toBeInTheDocument();
  });

  it("does not invent a continuation amount when the API omits it", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        hasParticipation: true,
        nextActionCode: "CompleteJoiningPayment",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        isActive: false,
        joinedIndependently: true,
        joiningPaidAmount: 600,
        joiningSchedule: 1,
        joiningTotalAmount: 1200,
        nextPaymentAmount: null,
        nextPaymentDescription: null,
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-08-09T09:00:00Z",
        status: "Awaiting activation payment",
        statusCode: "AwaitingActivationPayment",
      },
      onyx: null,
      pendingAQGreenCheckout: null,
      pendingDirectOnyxCheckout: null,
      funeralCover: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Joining payment details unavailable")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Complete joining/i }))
      .not.toBeInTheDocument();
  });

  it("offers both AQGreen schedules before the first checkout", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        hasParticipation: true,
        nextActionCode: "CompleteJoiningPayment",
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: {
        activatedAt: null,
        canRecruitForThisProgramme: false,
        currency: "ZAR",
        isActive: false,
        joinedIndependently: true,
        joiningPaidAmount: 0,
        joiningSchedule: null,
        nextPaymentAmount: 1200,
        nextPaymentDescription: "AQGreen joining payment",
        programmeName: "AQGreen",
        recruiterClubMemberNumber: null,
        startedAt: "2026-07-26T10:00:00Z",
        status: "Awaiting joining payment",
        statusCode: "AwaitingJoiningPayment",
      },
      onyx: null,
      pendingAQGreenCheckout: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);
    fireEvent.click(await screen.findByRole("button", {
      name: "Pay first R600 instalment",
    }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
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
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(
        createProgrammeJourney("AQGREEN"),
        createProgrammeJourney("ONYX", {
          benefits: [{
            amount: null,
            availableAt: "2026-10-20T10:00:00Z",
            code: "ONYX_TRAVEL",
            currency: null,
            description: "After the waiting period, you contribute 10%. Trip selection, pricing, and booking remain separate.",
            name: "Travel benefit",
            state: "Waiting period",
            unlockedAt: "2026-07-20T10:00:00Z",
          }],
        }),
      ),
      errorMessage: undefined,
      isLoading: false,
    });
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

  it("shows the included AQGreen funeral-cover benefit without claiming activation", async () => {
    vi.mocked(useMyProgrammeJourney).mockReturnValue({
      data: createJourneyResponse(createProgrammeJourney("AQGREEN", {
        benefits: [{
          amount: 30000,
          availableAt: null,
          code: "AQGREEN_FUNERAL_COVER",
          currency: "ZAR",
          description: "Included with completed joining. This does not represent insurer activation or enrolment.",
          name: "Funeral-cover inclusion",
          state: "Included",
          unlockedAt: "2026-08-09T10:00:00Z",
        }],
      })),
      errorMessage: undefined,
      isLoading: false,
    });
    vi.mocked(httpClient.get).mockResolvedValue({
      entry: null,
      funeralCover: {
        coverAmount: 30000,
        currency: "ZAR",
        includedAt: "2026-08-09T10:00:00Z",
        status: "Included",
      },
      onyx: null,
      travelBenefit: null,
    });

    render(<MemberProgrammes />);

    expect(await screen.findByText("Funeral-cover inclusion"))
      .toBeInTheDocument();
    expect(screen.getByText("Included")).toBeInTheDocument();
    expect(screen.getByText(/R\s*30[\s,\u00a0]*000/)).toBeInTheDocument();
    expect(screen.getByText(/does not represent insurer activation or enrolment/i))
      .toBeInTheDocument();
  });
});

// These existing cases exercise ordinary deployments. Pilot-specific cases use
// the actual mode boundary separately rather than consuming their domain mock responses.
vi.mock("@/src/shared/hooks/use-pilot-status", () => ({
  usePilotStatus: () => ({ isPilot: false, modeKnown: true }),
}));

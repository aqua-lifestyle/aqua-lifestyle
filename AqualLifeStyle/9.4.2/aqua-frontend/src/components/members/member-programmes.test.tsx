import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient, refreshAccessToken } from "@/src/shared/api";
import { MemberProgrammes } from "./member-programmes";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/browser/navigation", () => ({ navigateToExternalUrl: vi.fn() }));
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
    vi.mocked(httpClient.post).mockResolvedValue({
      amount: 6120,
      checkoutUrl: "https://payments.example.test/checkout/secure",
      currency: "ZAR",
    });
    vi.mocked(refreshAccessToken).mockResolvedValue("renewed-token");
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

  it("shows both network placements without requiring an invitation", async () => {
    render(<MemberProgrammes />);

    await screen.findByRole("button", { name: "Join AQGreen" });
    expect(screen.getByRole("button", { name: "Join Onyx" })).toBeInTheDocument();
    expect(screen.getByText(/an invitation is optional/i)).toBeInTheDocument();
    expect(
      screen.getByText(/AQGreen and an invitation are not required/i),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: "Invite Club Members" }),
    ).not.toBeInTheDocument();
  });

  it("records an independent AQGreen place and continues to one checkout", async () => {
    render(<MemberProgrammes />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Join AQGreen" }),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Continue to secure payment" }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.startEntry,
        { recruiterCustomerId: null },
      ),
    );
    expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 0 },
    );
    expect(navigateToExternalUrl).toHaveBeenCalledWith(
      "https://payments.example.test/checkout/secure",
    );
  });

  it("offers and submits the two-instalment AQGreen schedule", async () => {
    render(<MemberProgrammes />);

    fireEvent.click(await screen.findByRole("button", { name: "Join AQGreen" }));
    fireEvent.click(screen.getByRole("radio", { name: /two R600 instalments/i }));
    fireEvent.click(screen.getByRole("button", { name: "Continue to secure payment" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      { schedule: 1 },
    ));
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

    expect(await screen.findByText("Awaiting joining payment"))
      .toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Continue secure payment" }))
      .toHaveAttribute(
        "href",
        "https://payments.example.test/checkout/aqgreen-resume",
      );
    expect(screen.queryByRole("button", { name: "Pay R1,200 securely" }))
      .not.toBeInTheDocument();
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

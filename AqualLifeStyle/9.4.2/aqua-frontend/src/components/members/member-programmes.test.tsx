import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { MemberProgrammes } from "./member-programmes";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/browser/navigation", () => ({ navigateToExternalUrl: vi.fn() }));
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
    vi.mocked(httpClient.post).mockResolvedValue({
      amount: 6120,
      checkoutUrl: "https://payments.example.test/checkout/onyx",
      currency: "ZAR",
    });
  });

  it("shows both network placements without requiring a recruiter", async () => {
    render(<MemberProgrammes />);

    await screen.findByRole("button", { name: "Join AQGreen" });
    expect(screen.getByRole("button", { name: "Join Onyx" })).toBeInTheDocument();
    expect(screen.getAllByText(/recruiter is optional/i)).toHaveLength(2);
    expect(
      screen.queryByRole("link", { name: "Invite Club Members" }),
    ).not.toBeInTheDocument();
  });

  it("starts AQGreen independently and reloads the participation record", async () => {
    render(<MemberProgrammes />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Join AQGreen" }),
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Confirm programme joining" }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.startEntry,
        { recruiterCustomerId: null },
      ),
    );
    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(2));
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
      "https://payments.example.test/checkout/onyx",
    );
  });

  it("shows a resumable checkout without presenting pending payment as participation", async () => {
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

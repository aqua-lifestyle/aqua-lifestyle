import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { AdminProgrammeParticipations } from "./AdminProgrammeParticipations";

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
      email: "admin@example.com",
      id: 1,
      name: "Administrator",
      permissions,
      role: "SystemAdmin",
    },
  },
});

const participation = {
  activatedAt: null,
  areaName: "Johannesburg",
  confirmedPayments: [],
  currency: "ZAR",
  clubMemberNumber: "CLB-DORA23456789",
  customerName: "Dora Shongwe",
  email: "dora@example.com",
  isActive: false,
  joinedIndependently: true,
  nextPaymentAmount: 600,
  nextPaymentDescription: "AQGreen registration payment",
  programmeName: "AQGreen",
  recruiterClubMemberNumber: null,
  startedAt: "2026-07-24T10:00:00Z",
  status: "Awaiting first payment",
};

const lockedCheckout = {
  amount: 600,
  areaName: "Johannesburg",
  checkoutCreatedAt: "2026-08-01T09:00:00Z",
  checkoutId: "59a8bce4-e916-4f65-8102-2e4efc23cad1",
  clubMemberNumber: "CLB-DORA23456789",
  createdAt: "2026-08-01T08:59:00Z",
  currency: "ZAR",
  customerName: "Dora Shongwe",
  lockReason:
    "Awaiting authoritative provider confirmation or authorised termination.",
  paymentId: null,
  providerCheckoutId: "ch_safe_reference",
  schedule: 1,
  stage: 1,
  status: 1,
  tenantId: 1,
};

describe("AdminProgrammeParticipations", () => {
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
      authState(["Aqua.Admin.ProgrammeParticipations.View"]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [participation],
      totalCount: 1,
    });
  });

  it("loads every page reported by the service", async () => {
    const firstPage = Array.from({ length: 100 }, (_, index) => ({
      ...participation,
      clubMemberNumber: `CLB-${String(index + 1).padStart(12, "2")}`,
    }));
    const finalParticipation = {
      ...participation,
      clubMemberNumber: "CLB-FINAL2345678",
      customerName: "Final Club Member",
    };
    vi.mocked(httpClient.get)
      .mockResolvedValueOnce({ items: firstPage, totalCount: 101 })
      .mockResolvedValueOnce({
        items: [finalParticipation],
        totalCount: 101,
      });

    render(<AdminProgrammeParticipations />);

    await waitFor(() =>
      expect(httpClient.get).toHaveBeenNthCalledWith(
        2,
        `${apiEndpoints.programmeParticipations.getAdminParticipations}?Programme=0&SkipCount=100&MaxResultCount=100`,
      ),
    );
    expect(screen.getByText("101")).toBeInTheDocument();
  });

  it("loads AQGreen records and switches to Onyx reconciliation", async () => {
    render(<AdminProgrammeParticipations />);

    await screen.findByText("Dora Shongwe");
    expect(httpClient.get).toHaveBeenCalledWith(
      `${apiEndpoints.programmeParticipations.getAdminParticipations}?Programme=0&MaxResultCount=100`,
    );
    expect(screen.getByText("Independent network")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Onyx" }));

    await waitFor(() =>
      expect(httpClient.get).toHaveBeenLastCalledWith(
        `${apiEndpoints.programmeParticipations.getAdminParticipations}?Programme=1&MaxResultCount=100`,
      ),
    );
  });

  it("does not request records without the administrator permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminProgrammeParticipations />);

    expect(
      screen.getByText(/do not have permission to view programme participation/i),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });

  it("confirms an audited network placement correction using public Club Member numbers", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.Admin.ProgrammeParticipations.View",
        "Aqua.Admin.ProgrammeParticipations.CorrectRecruiter",
      ]),
    );
    vi.mocked(httpClient.post).mockResolvedValue(undefined);

    render(<AdminProgrammeParticipations />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Correct network placement" }),
    );
    fireEvent.change(screen.getByLabelText("New inviting Club Member number"), {
      target: { value: "clb-new23456789" },
    });
    fireEvent.change(screen.getByLabelText("Reason for correction"), {
      target: { value: "Verified against the signed joining form" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Confirm correction" }));

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.programmeParticipations.correctRecruiter,
        {
          clubMemberNumber: "CLB-DORA23456789",
          newRecruiterClubMemberNumber: "CLB-NEW23456789",
          programme: 0,
          reason: "Verified against the signed joining form",
        },
      ),
    );
    expect(
      await screen.findByText(/change was added to the audit history/i),
    ).toBeInTheDocument();
  });

  it("shows safe locked-checkout evidence with read-only permission", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.Admin.ProgrammeParticipations.ViewPaymentCheckouts",
      ]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [lockedCheckout],
      totalCount: 1,
    });

    render(<AdminProgrammeParticipations />);

    expect(await screen.findByText("AQGreen checkout recovery"))
      .toBeInTheDocument();
    expect(await screen.findByText(
      "ch_safe_reference",
      undefined,
      { timeout: 5_000 },
    )).toBeInTheDocument();
    expect(screen.getByText("Instalment 1 of 2", { exact: false }))
      .toBeInTheDocument();
    expect(screen.getByText(/read-only checkout access/i)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Review termination" }))
      .not.toBeInTheDocument();
  });

  it("requires justification before an authorised checkout termination", async () => {
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.Admin.ProgrammeParticipations.ViewPaymentCheckouts",
        "Aqua.Admin.ProgrammeParticipations.TerminatePaymentCheckouts",
      ]),
    );
    vi.mocked(httpClient.get).mockResolvedValue({
      items: [lockedCheckout],
      totalCount: 1,
    });
    vi.mocked(httpClient.post).mockResolvedValue(undefined);

    render(<AdminProgrammeParticipations />);

    fireEvent.click(await screen.findByRole("button", {
      name: "Review termination",
    }));
    expect(screen.getByRole("button", { name: "Terminate checkout" }))
      .toBeDisabled();
    fireEvent.change(
      screen.getByLabelText(
        "Authorised termination evidence and justification",
      ),
      { target: { value: "Yoco support confirmed checkout is no longer payable" } },
    );
    fireEvent.click(screen.getByRole("button", { name: "Terminate checkout" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.terminateAQGreenJoiningCheckout,
      {
        checkoutId: lockedCheckout.checkoutId,
        evidence: "Yoco support confirmed checkout is no longer payable",
      },
    ));
  });
});

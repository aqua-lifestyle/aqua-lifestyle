import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { AdminWeeklySalesReviews } from "./AdminWeeklySalesReviews";

vi.mock("@/src/providers", () => ({ useAuthState: vi.fn() }));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return {
    ...actual,
    httpClient: { get: vi.fn(), post: vi.fn() },
  };
});

const REVIEW_PERMISSION =
  "Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility";

const authState = (permissions: string[]) => ({
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "token",
    expiresAt: null,
    user: {
      email: "host@example.com",
      id: 1,
      name: "Host Administrator",
      permissions,
      role: "SystemAdmin",
    },
  },
});

const heldReview = {
  areaId: "area-1",
  areaName: "Soweto Central",
  clubMemberNumber: "CLB-ROOT",
  commissionWeekEndUtc: "2026-08-27T21:59:59.9999999Z",
  commissionWeekStartUtc: "2026-08-20T22:00:00Z",
  customerName: "Root Member",
  decisionId: "decision-1",
  email: "root@example.com",
  evidenceReferences: [],
  participantId: "participant-1",
  rejectionReason: null,
  reviewStatus: 1,
  reviewedAt: null,
  reviewedByUserId: null,
  reviewedFiveLitreQuantity: null,
  reviewedOneLitreQuantity: null,
  reviewedSprayQuantity: null,
  salesEligibilityRulesVersion: "AQGreenWeeklySalesEligibilityV1",
  tenantId: 1,
  thresholdResult: null,
  timeZoneId: "Africa/Johannesburg",
};

const finalizedNotMet = {
  ...heldReview,
  evidenceReferences: ["ticket:root-week"],
  reviewStatus: 2,
  reviewedAt: "2026-08-28T07:00:00Z",
  reviewedByUserId: 1,
  reviewedFiveLitreQuantity: 4,
  reviewedOneLitreQuantity: 5,
  reviewedSprayQuantity: 5,
  thresholdResult: 2,
};

const finalizedRejected = {
  ...heldReview,
  evidenceReferences: ["ticket:unreadable-receipt"],
  rejectionReason: "Receipt is unreadable",
  reviewStatus: 3,
  reviewedAt: "2026-08-28T07:00:00Z",
  reviewedByUserId: 1,
};

describe("AdminWeeklySalesReviews", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    window.history.replaceState({}, "", "/admin/weekly-sales-reviews");
    vi.mocked(useAuthState).mockReturnValue(authState([REVIEW_PERMISSION]));
    vi.mocked(httpClient.get).mockImplementation(async (url) =>
      url.includes("?Id=")
        ? finalizedNotMet
        : { items: [heldReview], totalCount: 1 },
    );
    vi.mocked(httpClient.post).mockResolvedValue({});
  });

  it("confirms verified quantities and renders the system-computed Not met result", async () => {
    render(<AdminWeeklySalesReviews />);

    await screen.findByText("Root Member");
    fireEvent.click(screen.getByRole("button", { name: "Review" }));
    fireEvent.change(screen.getByLabelText("Spray verified quantity"), {
      target: { value: "5" },
    });
    fireEvent.change(screen.getByLabelText("1L verified quantity"), {
      target: { value: "5" },
    });
    fireEvent.change(screen.getByLabelText("5L verified quantity"), {
      target: { value: "4" },
    });
    fireEvent.change(screen.getByLabelText("Evidence references (one per line)"), {
      target: { value: "ticket:root-week" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Confirm sales" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.weeklySalesReviews.confirm,
      {
        commissionWeekStartUtc: heldReview.commissionWeekStartUtc,
        evidenceReferences: ["ticket:root-week"],
        fiveLitreQuantity: 4,
        oneLitreQuantity: 5,
        participantId: heldReview.participantId,
        sprayQuantity: 5,
        tenantId: 1,
      },
    ));
    expect(await screen.findByText("Confirmed · Not met")).toBeInTheDocument();
    expect(screen.getByText("System result")).toBeInTheDocument();
    expect(screen.getByText("Not met")).toBeInTheDocument();
    expect(screen.queryByLabelText("Spray verified quantity")).not.toBeInTheDocument();
  });

  it("requires a reason and evidence reference before rejection", async () => {
    render(<AdminWeeklySalesReviews />);

    await screen.findByText("Root Member");
    fireEvent.click(screen.getByRole("button", { name: "Review" }));
    fireEvent.click(screen.getByRole("button", { name: "Reject evidence" }));

    expect(screen.getByText(/Add at least one evidence reference/i)).toBeInTheDocument();
    expect(httpClient.post).not.toHaveBeenCalled();
  });

  it("rejects evidence with a reason and renders the immutable rejected result", async () => {
    vi.mocked(httpClient.get).mockImplementation(async (url) =>
      url.includes("?Id=")
        ? finalizedRejected
        : { items: [heldReview], totalCount: 1 },
    );
    render(<AdminWeeklySalesReviews />);

    await screen.findByText("Root Member");
    fireEvent.click(screen.getByRole("button", { name: "Review" }));
    fireEvent.change(screen.getByLabelText("Evidence references (one per line)"), {
      target: { value: "ticket:unreadable-receipt" },
    });
    fireEvent.change(screen.getByLabelText("Rejection reason"), {
      target: { value: "Receipt is unreadable" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Reject evidence" }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith(
      apiEndpoints.weeklySalesReviews.reject,
      {
        commissionWeekStartUtc: heldReview.commissionWeekStartUtc,
        evidenceReferences: ["ticket:unreadable-receipt"],
        participantId: heldReview.participantId,
        rejectionReason: "Receipt is unreadable",
        tenantId: 1,
      },
    ));
    expect(await screen.findByText("Rejected")).toBeInTheDocument();
    expect(screen.getByText("Receipt is unreadable")).toBeInTheDocument();
    expect(screen.queryByLabelText("Rejection reason")).not.toBeInTheDocument();
  });

  it("does not load review data without the host review permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminWeeklySalesReviews />);

    expect(screen.getByText(/do not have permission/i)).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

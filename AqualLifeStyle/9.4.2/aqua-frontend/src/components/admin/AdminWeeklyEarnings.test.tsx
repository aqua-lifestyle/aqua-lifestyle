import {
  fireEvent,
  render,
  screen,
  waitFor,
  within,
} from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { AdminWeeklyEarnings } from "./AdminWeeklyEarnings";

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

const weeklyEarnings = {
  items: [
    {
      calculatedAt: "2026-07-24T08:00:00Z",
      components: [{ amount: 150, level: 1 }],
      currency: "ZAR",
      customerId: 42,
      customerName: "Dora Shongwe",
      email: "dora@example.com",
      highestCommissionedLevel: 1,
      highestQualifiedLevel: 1,
      holdReason: null,
      id: "earning-1",
      periodEnd: "2026-07-23T21:59:59Z",
      periodStart: "2026-07-16T22:00:00Z",
      paidAt: null,
      paymentReference: null,
      programmeName: "AQGreen",
      releasedAt: null,
      releaseReason: null,
      status: "Earned — awaiting release",
      tenantId: 3,
      totalAmount: 150,
    },
    {
      calculatedAt: "2026-07-24T08:00:00Z",
      components: [{ amount: 150, level: 1 }],
      currency: "ZAR",
      customerId: 43,
      customerName: "Lethabo Mokoena",
      email: "lethabo@example.com",
      highestCommissionedLevel: 1,
      highestQualifiedLevel: 1,
      holdReason: null,
      id: "earning-2",
      paidAt: null,
      paymentReference: null,
      periodEnd: "2026-07-23T21:59:59Z",
      periodStart: "2026-07-16T22:00:00Z",
      programmeName: "AQGreen",
      releasedAt: "2026-07-24T09:00:00Z",
      releaseReason: "Eligible weekly commission released.",
      status: "Released — awaiting payment",
      tenantId: 3,
      totalAmount: 150,
    },
  ],
  totalCount: 2,
};

describe("AdminWeeklyEarnings", () => {
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
      authState([
        "Aqua.Admin.Commissions.View",
        "Aqua.Admin.Commissions.Calculate",
        "Aqua.Admin.Commissions.Release",
        "Aqua.Admin.Commissions.RecordPayment",
      ]),
    );
    vi.mocked(httpClient.get).mockImplementation(async (url) =>
      url.includes("/AdminTenant/")
        ? {
            items: [{ id: 3, name: "Soweto", tenancyName: "soweto" }],
            totalCount: 1,
          }
        : weeklyEarnings,
    );
    vi.mocked(httpClient.post).mockResolvedValue({
      currency: "ZAR",
      earnedCount: 1,
      heldCount: 0,
      periodEnd: "2026-07-23T21:59:59Z",
      periodStart: "2026-07-16T22:00:00Z",
      programmeName: "AQGreen",
      recordsCreated: 6,
      totalEarnedAmount: 150,
      wasAlreadyCalculated: false,
    });
  });

  it("reviews weekly earnings and switches programmes", async () => {
    render(<AdminWeeklyEarnings />);

    await screen.findByText("Dora Shongwe");
    expect(
      screen.getByText("Earned — awaiting release"),
    ).toBeInTheDocument();
    expect(screen.getAllByText(/Commissioned level:/).length).toBeGreaterThan(0);
    expect(screen.queryByText(/Paid level:/)).not.toBeInTheDocument();
    expect(httpClient.get).toHaveBeenCalledWith(
      `${apiEndpoints.weeklyEarnings.getAll}?Programme=0&MaxResultCount=100`,
    );

    fireEvent.click(screen.getByRole("tab", { name: "Onyx" }));

    await waitFor(() =>
      expect(httpClient.get).toHaveBeenCalledWith(
        `${apiEndpoints.weeklyEarnings.getAll}?Programme=1&MaxResultCount=100`,
      ),
    );
  });

  it("prepares the latest completed week for the selected Area", async () => {
    render(<AdminWeeklyEarnings />);

    expect(
      screen.getByText(/Friday-to-Thursday cycle in Johannesburg time/i),
    ).toBeInTheDocument();

    fireEvent.change(await screen.findByLabelText("Area"), {
      target: { value: "3" },
    });
    fireEvent.click(
      screen.getByRole("button", { name: "Prepare weekly earnings" }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.weeklyEarnings.calculateLatestClosedWeek,
        { programme: 0, tenantId: 3 },
      ),
    );
    expect(
      await screen.findByText(/were prepared for 6 Club Members/i),
    ).toBeInTheDocument();
  });

  it("separates release approval from recording an external payment", async () => {
    render(<AdminWeeklyEarnings />);
    await screen.findByText("Dora Shongwe");

    fireEvent.click(
      screen.getByRole("button", { name: "Release for payment" }),
    );
    const releaseDialog = screen.getByRole("dialog", {
      name: "Release weekly earnings",
    });
    fireEvent.change(
      within(releaseDialog).getByLabelText("Reason for action"),
      { target: { value: "Calculation reviewed and approved." } },
    );
    fireEvent.click(
      within(releaseDialog).getByRole("button", {
        name: "Release for payment",
      }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.weeklyEarnings.release,
        {
          id: "earning-1",
          justification: "Calculation reviewed and approved.",
          programme: 0,
        },
      ),
    );

    fireEvent.click(
      screen.getByRole("button", { name: "Record external payment" }),
    );
    const paymentDialog = screen.getByRole("dialog", {
      name: "Record external payment",
    });
    fireEvent.change(
      within(paymentDialog).getByLabelText("External payment reference"),
      { target: { value: "bank-payment-entry-2" } },
    );
    fireEvent.change(
      within(paymentDialog).getByLabelText(
        "Reason for recording this payment",
      ),
      { target: { value: "Bank transfer confirmed." } },
    );
    fireEvent.click(
      within(paymentDialog).getByRole("button", {
        name: "Confirm payment record",
      }),
    );

    await waitFor(() =>
      expect(httpClient.post).toHaveBeenCalledWith(
        apiEndpoints.weeklyEarnings.recordPayment,
        {
          id: "earning-2",
          justification: "Bank transfer confirmed.",
          paymentReference: "bank-payment-entry-2",
          programme: 0,
        },
      ),
    );
  });

  it("does not load records without the review permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminWeeklyEarnings />);

    expect(
      screen.getByText("You do not have permission to view weekly earnings."),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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
      calculatedAt: "2026-07-20T08:00:00Z",
      components: [{ amount: 150, level: 1 }],
      currency: "ZAR",
      customerId: 42,
      customerName: "Dora Shongwe",
      email: "dora@example.com",
      highestCommissionedLevel: 1,
      highestQualifiedLevel: 1,
      holdReason: null,
      id: "earning-1",
      periodEnd: "2026-07-19T21:59:59Z",
      periodStart: "2026-07-12T22:00:00Z",
      programmeName: "Entry",
      status: "Earned — awaiting release",
      tenantId: 3,
      totalAmount: 150,
    },
  ],
  totalCount: 1,
};

describe("AdminWeeklyEarnings", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue(
      authState([
        "Aqua.Admin.Commissions.View",
        "Aqua.Admin.Commissions.Calculate",
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
      periodEnd: "2026-07-19T21:59:59Z",
      periodStart: "2026-07-12T22:00:00Z",
      programmeName: "Entry",
      recordsCreated: 6,
      totalEarnedAmount: 150,
      wasAlreadyCalculated: false,
    });
  });

  it("reviews weekly earnings and switches programmes", async () => {
    render(<AdminWeeklyEarnings />);

    await screen.findByText("Dora Shongwe");
    expect(screen.getAllByText(/R\s*150\.00/).length).toBeGreaterThan(0);
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

  it("does not load records without the review permission", () => {
    vi.mocked(useAuthState).mockReturnValue(authState([]));

    render(<AdminWeeklyEarnings />);

    expect(
      screen.getByText("You do not have permission to view weekly earnings."),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

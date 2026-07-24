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
  return { ...actual, httpClient: { get: vi.fn() } };
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
  confirmedPayments: [],
  currency: "ZAR",
  customerId: 42,
  customerName: "Dora Shongwe",
  email: "dora@example.com",
  id: "participation-1",
  isActive: false,
  joinedIndependently: true,
  nextPaymentAmount: 600,
  nextPaymentDescription: "Entry registration payment",
  programmeName: "Entry",
  recruiterCustomerId: null,
  startedAt: "2026-07-24T10:00:00Z",
  status: "Awaiting first payment",
  tenantId: 3,
};

describe("AdminProgrammeParticipations", () => {
  beforeEach(() => {
    vi.clearAllMocks();
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
      customerId: index + 1,
      id: `participation-${index + 1}`,
    }));
    const finalParticipation = {
      ...participation,
      customerId: 101,
      customerName: "Final Club Member",
      id: "participation-101",
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

  it("loads Entry records and switches to Onyx reconciliation", async () => {
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
      screen.getByText(
        "You do not have permission to view programme participation.",
      ),
    ).toBeInTheDocument();
    expect(httpClient.get).not.toHaveBeenCalled();
  });
});

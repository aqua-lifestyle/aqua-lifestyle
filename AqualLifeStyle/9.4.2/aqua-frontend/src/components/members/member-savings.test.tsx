import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { SavingsWindowStatus } from "@/src/providers/Memberships/context";
import { useAuthState, useMembershipsActions, useMembershipsState } from "@/src/providers";

import { MemberSavings } from "./member-savings";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthState: vi.fn(),
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
  };
});

const savingsWindowStatuses: SavingsWindowStatus[] = [
  {
    tier: 0,
    tierName: "Bronze",
    savingsWindowOpenDay: 1,
    savingsWindowCloseDay: 15,
    currentDay: 10,
    asOfDate: "2024-01-10",
    isSavingsWindowOpen: true,
    statusLabel: "Open",
  },
];

const baseAuthState = {
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "demo-token",
    expiresAt: "2099-01-01",
    user: {
      email: "member@example.com",
      id: 0,
      name: "Member User",
      permissions: [],
      role: "Member",
    },
  },
};

const baseState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  isSuccess: true,
  memberships: [],
  selectedErrorMessage: null,
  selectedMembership: null,
  tierBenefits: null,
  tierBenefitsErrorMessage: null,
  isTierBenefitsError: false,
  isTierBenefitsPending: false,
  isTierBenefitsSuccess: false,
  savingsWindowStatuses,
  savingsWindowStatusesErrorMessage: null,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: true,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue(baseAuthState);
  vi.mocked(useMembershipsState).mockReturnValue(baseState);
  vi.mocked(useMembershipsActions).mockReturnValue({
    getMembership: vi.fn(),
    getMemberships: vi.fn(),
    getSavingsWindowStatuses: vi.fn(),
    getTierBenefits: vi.fn(),
  });
});

describe("MemberSavings", () => {
  it("renders the member savings page", async () => {
    render(<MemberSavings />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /My savings/i })).toBeDefined();
    });

    expect(screen.getByText("Bronze")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseState,
      isSavingsWindowStatusesPending: true,
      savingsWindowStatuses: [],
    });

    render(<MemberSavings />);

    expect(screen.queryByText("Bronze")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseState,
      isSavingsWindowStatusesError: true,
      savingsWindowStatusesErrorMessage: "Failed to load savings",
    });

    render(<MemberSavings />);

    expect(screen.getByText("Failed to load savings")).toBeDefined();
  });

  it("shows empty state when there are no savings records", () => {
    vi.mocked(useMembershipsState).mockReturnValue({
      ...baseState,
      savingsWindowStatuses: [],
    });

    render(<MemberSavings />);

    expect(screen.getByText("No savings")).toBeDefined();
    expect(screen.getByText("You have no savings records.")).toBeDefined();
  });
});

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
  isConfirmError: false,
  isConfirmPending: false,
  isConfirmSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  confirmErrorMessage: null,
  loadErrorMessage: null,
  referrals: [],
  selectedReferral: null,
  selectedErrorMessage: null,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: true,
  savingsWindowStatuses,
  savingsWindowStatusesErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useAuthState as unknown as { mockReturnValue: typeof baseAuthState }).mockReturnValue(baseAuthState);
  (useMembershipsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useMembershipsActions as unknown as { mockReturnValue: { getSavingsWindowStatuses: () => Promise<void> } }).mockReturnValue({
    getSavingsWindowStatuses: vi.fn(),
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
    (useMembershipsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSavingsWindowStatusesPending: true,
      savingsWindowStatuses: [],
    });

    render(<MemberSavings />);

    expect(screen.queryByText("Bronze")).toBeNull();
  });

  it("shows error state", () => {
    (useMembershipsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSavingsWindowStatusesError: true,
      savingsWindowStatusesErrorMessage: "Failed to load savings",
    });

    render(<MemberSavings />);

    expect(screen.getByText("Failed to load savings")).toBeDefined();
  });

  it("shows empty state when there are no savings records", () => {
    (useMembershipsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      savingsWindowStatuses: [],
    });

    render(<MemberSavings />);

    expect(screen.getByText("No savings")).toBeDefined();
    expect(screen.getByText("You have no savings records.")).toBeDefined();
  });
});

import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import { useAreaLeadersActions, useAreaLeadersState, useAuthState } from "@/src/providers";

import { AreaLeadersList } from "./area-leaders-list";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAreaLeadersActions: vi.fn(),
    useAreaLeadersState: vi.fn(),
    useAuthState: vi.fn(),
  };
});

const areaLeaders: AreaLeader[] = [
  {
    id: 1,
    tenantId: 1,
    customerId: 10,
    licenseType: 0,
    licenseFee: 750,
    rank: 1,
    areaSpaceId: null,
    monthlySubscription: 100,
    directReferrals: 5,
    indirectReferrals: 3,
    orderTarget: 12,
  },
  {
    id: 2,
    tenantId: 1,
    customerId: 11,
    licenseType: 1,
    licenseFee: 2500,
    rank: 3,
    areaSpaceId: 5,
    monthlySubscription: 500,
    directReferrals: 20,
    indirectReferrals: 10,
    orderTarget: 45,
  },
];

const baseState = {
  areaLeaders,
  isApplyError: false,
  isApplyPending: false,
  isApplySuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isPromoteError: false,
  isPromotePending: false,
  isPromoteSuccess: false,
  isRecordStartupOrderError: false,
  isRecordStartupOrderPending: false,
  isRecordStartupOrderSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  applyErrorMessage: null,
  loadErrorMessage: null,
  promoteErrorMessage: null,
  recordStartupOrderErrorMessage: null,
  selectedAreaLeader: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session: {
      accessToken: "token",
      expiresAt: null,
      user: {
        id: 99,
        email: "test@example.com",
        name: "Test User",
        permissions: ["Pages.AreaLeaders"],
        role: "admin",
      },
    },
  });

  vi.mocked(useAreaLeadersState).mockReturnValue(baseState);
  vi.mocked(useAreaLeadersActions).mockReturnValue({
    applyAreaLeader: vi.fn(),
    getAreaLeader: vi.fn(),
    getAreaLeaders: vi.fn(),
    promoteAreaLeader: vi.fn(),
    recordStartupOrder: vi.fn(),
  });
});

describe("AreaLeadersList", () => {
  it("renders the area leaders list", () => {
    render(<AreaLeadersList />);

    expect(screen.getByRole("heading", { name: /Area Leaders/i })).toBeDefined();
    expect(screen.getByText("Customer #10")).toBeDefined();
    expect(screen.getByText("Customer #11")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useAreaLeadersState).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<AreaLeadersList />);

    expect(screen.queryByText("Customer #10")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useAreaLeadersState).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load area leaders",
    });

    render(<AreaLeadersList />);

    expect(screen.getByText("Failed to load area leaders")).toBeDefined();
  });

  it("shows empty state when there are no area leaders", () => {
    vi.mocked(useAreaLeadersState).mockReturnValue({
      ...baseState,
      areaLeaders: [],
    });

    render(<AreaLeadersList />);

    expect(screen.getByText("No area leaders")).toBeDefined();
    expect(screen.getByText("No area leaders found.")).toBeDefined();
  });

  it("renders the license type filter", () => {
    render(<AreaLeadersList />);

    expect(screen.getByLabelText("License Type")).toBeDefined();
  });
});

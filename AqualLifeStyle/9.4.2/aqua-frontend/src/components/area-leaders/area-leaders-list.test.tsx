import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import { useAreaLeadersActions, useAreaLeadersState } from "@/src/providers";

import { AreaLeadersList } from "./area-leaders-list";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAreaLeadersActions: vi.fn(),
    useAreaLeadersState: vi.fn(),
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
  applyErrorMessage: null,
  loadErrorMessage: null,
  promoteErrorMessage: null,
  recordStartupOrderErrorMessage: null,
  selectedAreaLeader: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useAreaLeadersActions as unknown as { mockReturnValue: { getAreaLeaders: () => Promise<void> } }).mockReturnValue({
    getAreaLeaders: vi.fn(),
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
    (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<AreaLeadersList />);

    expect(screen.queryByText("Customer #10")).toBeNull();
  });

  it("shows error state", () => {
    (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load area leaders",
    });

    render(<AreaLeadersList />);

    expect(screen.getByText("Failed to load area leaders")).toBeDefined();
  });

  it("shows empty state when there are no area leaders", () => {
    (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
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

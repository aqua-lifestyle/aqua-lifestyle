import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import { useAreaLeadersActions, useAreaLeadersState } from "@/src/providers";

import { AreaLeaderDetails } from "./area-leader-details";

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

const areaLeader: AreaLeader = {
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
};

const baseState = {
  areaLeaders: [],
  isApplyError: false,
  isApplyPending: false,
  isApplySuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isPromoteError: false,
  isPromotePending: false,
  isPromoteSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  applyErrorMessage: null,
  loadErrorMessage: null,
  promoteErrorMessage: null,
  selectedAreaLeader: areaLeader,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useAreaLeadersActions as unknown as { mockReturnValue: { getAreaLeader: () => Promise<void>; promoteAreaLeader: () => Promise<boolean> } }).mockReturnValue({
    getAreaLeader: vi.fn(),
    promoteAreaLeader: vi.fn(),
  });
});

describe("AreaLeaderDetails", () => {
  it("renders area leader details", async () => {
    render(<AreaLeaderDetails areaLeaderId={1} />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Area Leader details/i })).toBeDefined();
    });

    expect(screen.getByText("Customer #10")).toBeDefined();
    expect(screen.getAllByText("Entre Level").length).toBeGreaterThan(0);
  });

  it("shows loading state", () => {
    (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSelectedPending: true,
      selectedAreaLeader: null,
    });

    render(<AreaLeaderDetails areaLeaderId={1} />);

    expect(screen.queryByText("Customer #10")).toBeNull();
  });

  it("shows error state", () => {
    (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSelectedError: true,
      selectedErrorMessage: "Area leader not found",
    });

    render(<AreaLeaderDetails areaLeaderId={1} />);

    expect(screen.getByText("Area leader not found")).toBeDefined();
  });

  it("shows invalid id error", () => {
    render(<AreaLeaderDetails areaLeaderId={-1} />);

    expect(screen.getByText("This area leader id is invalid.")).toBeDefined();
  });
});

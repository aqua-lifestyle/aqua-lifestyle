import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useAuthState,
} from "@/src/providers";

import { FacilitatorApproval } from "./facilitator-approval";

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

const session: AuthSession = {
  accessToken: "token",
  expiresAt: null,
  user: {
    id: 99,
    email: "test@example.com",
    name: "Test User",
    permissions: ["Pages.AreaLeaders"],
    role: "admin",
  },
};

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
  applyErrorMessage: null,
  loadErrorMessage: null,
  promoteErrorMessage: null,
  selectedAreaLeader: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useAuthState as unknown as { mockReturnValue: typeof session }).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session,
  });

  (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useAreaLeadersActions as unknown as { mockReturnValue: { getAreaLeaders: () => Promise<void>, promoteAreaLeader: () => Promise<boolean> } }).mockReturnValue({
    getAreaLeaders: vi.fn(),
    promoteAreaLeader: vi.fn().mockResolvedValue(true),
  });
});

describe("FacilitatorApproval", () => {
  it("renders the facilitator approval page", () => {
    render(<FacilitatorApproval areaLeaderId={1} />);

    expect(screen.getByRole("heading", { name: /Facilitator approval/i })).toBeDefined();
    expect(screen.getByText("Select Area Leader")).toBeDefined();
    expect(screen.getByText("Promote Rank")).toBeDefined();
  });
});

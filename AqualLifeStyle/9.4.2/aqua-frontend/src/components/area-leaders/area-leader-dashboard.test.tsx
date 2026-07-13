import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import type { AreaSpace } from "@/src/providers/AreaSpaces/context";
import type { Facilitator } from "@/src/providers/Facilitators/context";
import type { OrderIntent } from "@/src/providers/OrderIntents/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useAreaSpacesActions,
  useAreaSpacesState,
  useAuthState,
  useFacilitatorsActions,
  useFacilitatorsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";

import { AreaLeaderDashboard } from "./area-leader-dashboard";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAreaLeadersActions: vi.fn(),
    useAreaLeadersState: vi.fn(),
    useAreaSpacesActions: vi.fn(),
    useAreaSpacesState: vi.fn(),
    useAuthState: vi.fn(),
    useFacilitatorsActions: vi.fn(),
    useFacilitatorsState: vi.fn(),
    useOrderIntentsActions: vi.fn(),
    useOrderIntentsState: vi.fn(),
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
];

const areaSpaces: AreaSpace[] = [
  {
    id: 1,
    tenantId: 1,
    areaLeaderId: 1,
    addressLine: "123 Main St",
    capacity: "50",
    interestedMembers: 10,
    status: 0,
    reviewStartedAt: null,
    presentationsCompleted: 0,
    startupOrdersCompleted: 0,
    approvedAt: null,
  },
];

const facilitators: Facilitator[] = [
  {
    id: 1,
    tenantId: 1,
    customerId: 20,
    areaLeaderId: 1,
    rank: 1,
    directReferrals: 0,
    indirectReferrals: 0,
    awardBalance: 0,
  },
];

const orderIntents: OrderIntent[] = [
  {
    id: 1,
    customerId: 10,
    productId: 1,
    enquiryId: null,
    unitPrice: 100,
    reservedPrice: 100,
    status: 0,
    statusText: "Pending",
    createdAt: "2025-01-01T00:00:00Z",
    reservedAt: null,
    cancelledAt: null,
    completedAt: null,
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

  (useAuthState as unknown as { mockReturnValue: typeof session }).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session,
  });

  (useAreaLeadersState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useAreaLeadersActions as unknown as { mockReturnValue: { getAreaLeaders: () => Promise<void> } }).mockReturnValue({
    getAreaLeaders: vi.fn(),
  });

  (useAreaSpacesState as unknown as { mockReturnValue: { areaSpaces, isLoadError: false, isLoadPending: false, isLoadSuccess: true, loadErrorMessage: null } }).mockReturnValue({
    areaSpaces,
    isLoadError: false,
    isLoadPending: false,
    isLoadSuccess: true,
    loadErrorMessage: null,
  });
  (useAreaSpacesActions as unknown as { mockReturnValue: { getAreaSpaces: () => Promise<void> } }).mockReturnValue({
    getAreaSpaces: vi.fn(),
  });

  (useFacilitatorsState as unknown as { mockReturnValue: { facilitators, isLoadError: false, isLoadPending: false, isLoadSuccess: true, loadErrorMessage: null } }).mockReturnValue({
    facilitators,
    isLoadError: false,
    isLoadPending: false,
    isLoadSuccess: true,
    loadErrorMessage: null,
  });
  (useFacilitatorsActions as unknown as { mockReturnValue: { getFacilitators: () => Promise<void> } }).mockReturnValue({
    getFacilitators: vi.fn(),
  });

  (useOrderIntentsState as unknown as { mockReturnValue: { orderIntents, isLoadError: false, isLoadPending: false, isLoadSuccess: true, loadErrorMessage: null } }).mockReturnValue({
    orderIntents,
    isLoadError: false,
    isLoadPending: false,
    isLoadSuccess: true,
    loadErrorMessage: null,
  });
  (useOrderIntentsActions as unknown as { mockReturnValue: { getOrderIntents: () => Promise<void> } }).mockReturnValue({
    getOrderIntents: vi.fn(),
  });
});

describe("AreaLeaderDashboard", () => {
  it("renders the area leader dashboard", () => {
    render(<AreaLeaderDashboard />);

    expect(screen.getByRole("heading", { name: /Area Leader dashboard/i })).toBeDefined();
    expect(screen.getByText("Area Leaders")).toBeDefined();
    expect(screen.getByText("Area Spaces")).toBeDefined();
    expect(screen.getByText("Facilitators")).toBeDefined();
    expect(screen.getByText("Orders")).toBeDefined();
    expect(screen.getByText("Recent orders")).toBeDefined();
    expect(screen.getByText("Quick actions")).toBeDefined();
  });
});

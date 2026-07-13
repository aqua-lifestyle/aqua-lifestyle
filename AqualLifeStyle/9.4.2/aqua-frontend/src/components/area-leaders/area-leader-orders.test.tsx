import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaLeader } from "@/src/providers/AreaLeaders/context";
import type { OrderIntent } from "@/src/providers/OrderIntents/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useAuthState,
} from "@/src/providers";

import { AreaLeaderOrders } from "./area-leader-orders";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAreaLeadersActions: vi.fn(),
    useAreaLeadersState: vi.fn(),
    useOrderIntentsActions: vi.fn(),
    useOrderIntentsState: vi.fn(),
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
  {
    id: 2,
    customerId: 10,
    productId: 2,
    enquiryId: null,
    unitPrice: 200,
    reservedPrice: 180,
    status: 2,
    statusText: "Completed",
    createdAt: "2025-01-02T00:00:00Z",
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
    permissions: ["Pages.Orders"],
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

describe("AreaLeaderOrders", () => {
  it("renders the area leader orders page", () => {
    render(<AreaLeaderOrders />);

    expect(screen.getByRole("heading", { name: /Orders/i })).toBeDefined();
    expect(screen.getAllByText("Status").length).toBeGreaterThan(0);
    expect(screen.getByRole("combobox", { name: /Status/i })).toBeDefined();
  });
});

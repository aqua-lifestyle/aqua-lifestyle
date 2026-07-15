import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { OrderIntent } from "@/src/providers/OrderIntents/context";
import {
  useCustomersActions,
  useCustomersState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";

import { MemberOrders } from "./member-orders";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useOrderIntentsActions: vi.fn(),
    useOrderIntentsState: vi.fn(),
  };
});

const orderIntents: OrderIntent[] = [
  {
    id: 1,
    customerId: 1,
    productId: 10,
    enquiryId: 50,
    unitPrice: 100,
    reservedPrice: 80,
    status: 2,
    statusText: "Completed",
    createdAt: "2024-01-01T00:00:00Z",
    reservedAt: "2024-01-01T00:00:00Z",
    cancelledAt: null,
    completedAt: "2024-01-02T00:00:00Z",
  },
];

const baseState = {
  actionErrorMessage: null,
  isActionError: false,
  isActionPending: false,
  isActionSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  loadErrorMessage: null,
  orderIntents,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useCustomersActions).mockReturnValue({
    changeMembership: vi.fn(),
    createCustomer: vi.fn(),
    getCustomer: vi.fn(),
    getCustomers: vi.fn(),
    getMyCustomer: vi.fn(),
    updateCustomer: vi.fn(),
  });
  vi.mocked(useCustomersState).mockReturnValue({
    changeMembershipErrorMessage: null,
    createErrorMessage: null,
    customers: [],
    isChangeMembershipError: false,
    isChangeMembershipPending: false,
    isChangeMembershipSuccess: false,
    isCreateError: false,
    isCreatePending: false,
    isCreateSuccess: false,
    isLoadError: false,
    isLoadPending: false,
    isLoadSuccess: false,
    isMyCustomerError: false,
    isMyCustomerPending: false,
    isMyCustomerSuccess: true,
    isSelectedError: false,
    isSelectedPending: false,
    isSelectedSuccess: false,
    isUpdateError: false,
    isUpdatePending: false,
    isUpdateSuccess: false,
    loadErrorMessage: null,
    myCustomer: {
      email: "member@example.com",
      id: 1,
      isActive: true,
      membershipId: 1,
      name: "Member User",
      tenantId: 1,
      userId: 42,
    },
    myCustomerErrorMessage: null,
    selectedCustomer: null,
    selectedErrorMessage: null,
    updateErrorMessage: null,
  });
  vi.mocked(useOrderIntentsState).mockReturnValue(baseState);
  vi.mocked(useOrderIntentsActions).mockReturnValue({
    cancelOrderIntent: vi.fn(),
    completeOrderIntent: vi.fn(),
    createForCurrentCustomer: vi.fn(),
    createFromEnquiry: vi.fn(),
    getOrderIntents: vi.fn(),
  });
});

describe("MemberOrders", () => {
  it("renders the member orders list", async () => {
    render(<MemberOrders />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /My orders/i })).toBeDefined();
    });

    expect(screen.getByText("Order #1")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useOrderIntentsState).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<MemberOrders />);

    expect(screen.queryByText("Order #1")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useOrderIntentsState).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load orders",
    });

    render(<MemberOrders />);

    expect(screen.getByText("Failed to load orders")).toBeDefined();
  });

  it("shows empty state when there are no orders", () => {
    vi.mocked(useOrderIntentsState).mockReturnValue({
      ...baseState,
      orderIntents: [],
    });

    render(<MemberOrders />);

    expect(screen.getByText("No orders")).toBeDefined();
    expect(screen.getByText("You have no orders yet.")).toBeDefined();
  });
});

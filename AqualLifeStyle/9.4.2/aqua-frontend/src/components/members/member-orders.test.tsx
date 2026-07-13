import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { OrderIntent } from "@/src/providers/OrderIntents/context";
import { useAuthState, useOrderIntentsActions, useOrderIntentsState } from "@/src/providers";

import { MemberOrders } from "./member-orders";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthState: vi.fn(),
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

const baseAuthState = {
  isAuthenticated: true,
  isReady: true,
  session: {
    accessToken: "demo-token",
    expiresAt: "2099-01-01",
    user: {
      email: "member@example.com",
      id: 1,
      name: "Member User",
      permissions: [],
      role: "Member",
    },
  },
};

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

  (useAuthState as unknown as { mockReturnValue: typeof baseAuthState }).mockReturnValue(baseAuthState);
  (useOrderIntentsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useOrderIntentsActions as unknown as { mockReturnValue: { getOrderIntents: () => Promise<void> } }).mockReturnValue({
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
    (useOrderIntentsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<MemberOrders />);

    expect(screen.queryByText("Order #1")).toBeNull();
  });

  it("shows error state", () => {
    (useOrderIntentsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load orders",
    });

    render(<MemberOrders />);

    expect(screen.getByText("Failed to load orders")).toBeDefined();
  });

  it("shows empty state when there are no orders", () => {
    (useOrderIntentsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      orderIntents: [],
    });

    render(<MemberOrders />);

    expect(screen.getByText("No orders")).toBeDefined();
    expect(screen.getByText("You have no orders yet.")).toBeDefined();
  });
});

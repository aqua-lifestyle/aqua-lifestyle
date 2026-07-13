import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Enquiry } from "@/src/providers/Enquiries/context";
import type { AuthSession } from "@/src/providers/Auth/context";
import {
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
} from "@/src/providers";

import { MemberEnquiries } from "./member-enquiries";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthState: vi.fn(),
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useEnquiriesActions: vi.fn(),
    useEnquiriesState: vi.fn(),
  };
});

const customers: Customer[] = [
  {
    id: 1,
    name: "John Doe",
    email: "john@example.com",
    membershipId: 1,
    isActive: true,
    tenantId: 1,
    userId: 99,
  },
];

const enquiries: Enquiry[] = [
  {
    id: 1,
    customerId: 99,
    productId: 1,
    message: "What are your hours?",
    response: null,
    status: 0,
    createdAt: "2025-01-01T00:00:00Z",
    isClosed: false,
    isPending: true,
    assignedToMemberId: null,
    isConverted: false,
    convertedAt: null,
    conversionProbability: 0,
    lastFollowUpDate: null,
    followUpCount: 0,
    isSalesReady: false,
    followUps: [],
  },
];

const session: AuthSession = {
  accessToken: "token",
  expiresAt: null,
  user: {
    id: 99,
    email: "test@example.com",
    name: "Test User",
    permissions: ["Pages.Enquiries"],
    role: "member",
  },
};

const baseCustomersState = {
  customers,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  loadErrorMessage: null,
};

const baseEnquiriesState = {
  enquiries,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  loadErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useAuthState as unknown as { mockReturnValue: typeof session }).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session,
  });

  (useCustomersState as unknown as { mockReturnValue: typeof baseCustomersState }).mockReturnValue(baseCustomersState);
  (useCustomersActions as unknown as { mockReturnValue: { getCustomers: () => Promise<void> } }).mockReturnValue({
    getCustomers: vi.fn(),
  });

  (useEnquiriesState as unknown as { mockReturnValue: typeof baseEnquiriesState }).mockReturnValue(baseEnquiriesState);
  (useEnquiriesActions as unknown as { mockReturnValue: { getEnquiries: () => Promise<void> } }).mockReturnValue({
    getEnquiries: vi.fn(),
  });
});

describe("MemberEnquiries", () => {
  it("renders the member enquiries page", () => {
    render(<MemberEnquiries />);

    expect(screen.getByRole("heading", { name: /My enquiries/i })).toBeDefined();
    expect(screen.getAllByText("Status").length).toBeGreaterThan(0);
    expect(screen.getByRole("combobox", { name: /Status/i })).toBeDefined();
    expect(screen.getByText("What are your hours?")).toBeDefined();
  });
});

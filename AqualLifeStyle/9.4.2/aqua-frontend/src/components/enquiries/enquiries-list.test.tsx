import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Enquiry } from "@/src/providers/Enquiries/context";
import type { Product } from "@/src/providers/Products/context";
import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";

import { EnquiriesList } from "./enquiries-list";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useCustomersActions: vi.fn(),
    useCustomersState: vi.fn(),
    useEnquiriesActions: vi.fn(),
    useEnquiriesState: vi.fn(),
    useProductsActions: vi.fn(),
    useProductsState: vi.fn(),
  };
});

const customers: Customer[] = [
  { id: 1, name: "John Doe", email: "john@example.com", membershipId: 1, isActive: true, tenantId: null, userId: 99 },
  { id: 2, name: "Jane Smith", email: "jane@example.com", membershipId: null, isActive: false, tenantId: null, userId: 100 },
];

const products: Product[] = [
  { id: 1, name: "Kayak", price: 1500, membershipId: 1, isActive: true },
  { id: 2, name: "Paddle", price: 300, membershipId: null, isActive: true },
];

const followUps: Enquiry["followUps"] = [];

const enquiries: Enquiry[] = [
  {
    id: 1,
    customerId: 1,
    productId: 1,
    message: "Is the kayak available?",
    response: null,
    status: 0,
    createdAt: "2024-06-15T10:00:00Z",
    isClosed: false,
    isPending: false,
    isConverted: false,
    convertedAt: null,
    assignedToMemberId: null,
    conversionProbability: 0,
    lastFollowUpDate: null,
    followUpCount: 0,
    isSalesReady: true,
    followUps,
  },
  {
    id: 2,
    customerId: 2,
    productId: 2,
    message: "Paddle stock question",
    response: "Yes, in stock",
    status: 2,
    createdAt: "2024-06-14T10:00:00Z",
    isClosed: true,
    isPending: false,
    isConverted: false,
    convertedAt: null,
    assignedToMemberId: null,
    conversionProbability: 0,
    lastFollowUpDate: null,
    followUpCount: 0,
    isSalesReady: false,
    followUps,
  },
];

const baseEnquiriesState = {
  actionErrorMessage: null,
  createErrorMessage: null,
  enquiries: [...enquiries],
  isActionError: false,
  isActionPending: false,
  isActionSuccess: false,
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isSalesReadyError: false,
  isSalesReadyPending: false,
  isSalesReadySuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  loadErrorMessage: null,
  salesReadyEnquiries: [],
  salesReadyErrorMessage: null,
  selectedEnquiry: null,
  selectedErrorMessage: null,
};

describe("EnquiriesList", () => {
  const getEnquiries = vi.fn();
  const getCustomers = vi.fn();
  const getProducts = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useEnquiriesActions).mockReturnValue({
      closeEnquiry: vi.fn(),
      convertEnquiryToCustomer: vi.fn(),
      createEnquiry: vi.fn(),
      getEnquiries,
      getEnquiry: vi.fn(),
      getSalesReadyEnquiries: vi.fn(),
      recordFollowUp: vi.fn(),
      reopenEnquiry: vi.fn(),
      respondToEnquiry: vi.fn(),
    });
    vi.mocked(useEnquiriesState).mockReturnValue({ ...baseEnquiriesState });
    vi.mocked(useCustomersActions).mockReturnValue({
      changeMembership: vi.fn(),
      createCustomer: vi.fn(),
      getCustomer: vi.fn(),
      getCustomers,
      getMyCustomer: vi.fn(),
      updateCustomer: vi.fn(),
    });
    vi.mocked(useCustomersState).mockReturnValue({
      changeMembershipErrorMessage: null,
      createErrorMessage: null,
      customers: [...customers],
      isCreateError: false,
      isCreatePending: false,
      isCreateSuccess: false,
      isChangeMembershipError: false,
      isChangeMembershipPending: false,
      isChangeMembershipSuccess: false,
      isLoadError: false,
      isLoadPending: false,
      isLoadSuccess: true,
      isMyCustomerError: false,
      isMyCustomerPending: false,
      isMyCustomerSuccess: false,
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      isUpdateError: false,
      isUpdatePending: false,
      isUpdateSuccess: false,
      loadErrorMessage: null,
      myCustomer: null,
      myCustomerErrorMessage: null,
      selectedCustomer: null,
      selectedErrorMessage: null,
      updateErrorMessage: null,
    });
    vi.mocked(useProductsActions).mockReturnValue({
      getEligibleProductsForCustomer: vi.fn(),
      getProduct: vi.fn(),
      getProducts,
    });
    vi.mocked(useProductsState).mockReturnValue({
      eligibleErrorMessage: null,
      eligibleProducts: [],
      errorMessage: null,
      isError: false,
      isPending: false,
      isSuccess: true,
      isEligibleError: false,
      isEligiblePending: false,
      isEligibleSuccess: false,
      products: [...products],
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      selectedErrorMessage: null,
      selectedProduct: null,
    });
  });

  it("fetches enquiries, customers, and products on mount", () => {
    render(<EnquiriesList />);
    expect(getEnquiries).toHaveBeenCalled();
    expect(getCustomers).toHaveBeenCalled();
    expect(getProducts).toHaveBeenCalled();
  });

  it("renders enquiry rows and summary cards", () => {
    render(<EnquiriesList />);
    const totalCard = screen.getByText("Total enquiries").closest("article");
    expect(totalCard).toBeInTheDocument();
    expect(within(totalCard!).getByText("2")).toBeInTheDocument();

    expect(screen.getByText("John Doe")).toBeInTheDocument();
    expect(screen.getByText("Jane Smith")).toBeInTheDocument();
    expect(screen.getByText(/Is the kayak available\?/)).toBeInTheDocument();
  });

  it("filters by status", () => {
    render(<EnquiriesList />);

    fireEvent.change(screen.getByLabelText("Status"), {
      target: { value: "resolved" },
    });

    expect(screen.queryByText(/Is the kayak available\?/)).not.toBeInTheDocument();
    expect(screen.getByText(/Paddle stock question/)).toBeInTheDocument();
  });

  it("filters by priority", () => {
    render(<EnquiriesList />);

    fireEvent.change(screen.getByLabelText("Priority"), {
      target: { value: "high" },
    });

    expect(screen.getByText(/Is the kayak available\?/)).toBeInTheDocument();
    expect(screen.queryByText(/Paddle stock question/)).not.toBeInTheDocument();
  });

  it("filters by search query", async () => {
    render(<EnquiriesList />);

    fireEvent.change(screen.getByPlaceholderText("Search customer or message..."), {
      target: { value: "kayak" },
    });

    await waitFor(() => {
      expect(screen.getByText(/Is the kayak available\?/)).toBeInTheDocument();
      expect(screen.queryByText(/Paddle stock question/)).not.toBeInTheDocument();
    });
  });
});

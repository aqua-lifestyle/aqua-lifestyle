import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Enquiry } from "@/src/providers/Enquiries/context";
import type { Membership } from "@/src/providers/Memberships/context";
import type { Product } from "@/src/providers/Products/context";
import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
  useToast,
} from "@/src/providers";

import { EnquiryDetails } from "./enquiry-details";

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
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
    useProductsActions: vi.fn(),
    useProductsState: vi.fn(),
    useToast: vi.fn(),
  };
});

const customers: Customer[] = [
  { id: 1, name: "John Doe", email: "john@example.com", membershipId: 1, isActive: true, tenantId: null, userId: 99 },
];

const products: Product[] = [
  { id: 1, name: "Kayak", price: 1500, membershipId: 1, isActive: true },
];

const memberships: Membership[] = [
  {
    id: 1,
    name: "Bronze",
    description: null,
    isActive: true,
    membershipType: 0,
    activationDate: null,
    monthlyObligationAmount: 0,
    lastObligationMetDate: null,
  },
];

const selectedEnquiry: Enquiry = {
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
  followUps: [],
};

const baseEnquiriesState = {
  actionErrorMessage: null,
  createErrorMessage: null,
  enquiries: [],
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
  isSelectedSuccess: true,
  loadErrorMessage: null,
  salesReadyEnquiries: [],
  salesReadyErrorMessage: null,
  selectedEnquiry,
  selectedErrorMessage: null,
};

describe("EnquiryDetails", () => {
  const getEnquiry = vi.fn();
  const getCustomers = vi.fn();
  const getProducts = vi.fn();
  const getMemberships = vi.fn();
  const respondToEnquiry = vi.fn().mockResolvedValue(true);
  const closeEnquiry = vi.fn().mockResolvedValue(true);
  const reopenEnquiry = vi.fn().mockResolvedValue(true);
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    respondToEnquiry.mockResolvedValue(true);
    closeEnquiry.mockResolvedValue(true);
    reopenEnquiry.mockResolvedValue(true);

    vi.mocked(useEnquiriesActions).mockReturnValue({
      closeEnquiry,
      convertEnquiryToCustomer: vi.fn(),
      createEnquiry: vi.fn(),
      getEnquiries: vi.fn(),
      getMyEnquiries: vi.fn(),
      getEnquiry,
      getSalesReadyEnquiries: vi.fn(),
      recordFollowUp: vi.fn(),
      reopenEnquiry,
      respondToEnquiry,
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
    vi.mocked(useMembershipsActions).mockReturnValue({
      getActiveTiers: vi.fn(),
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({
      errorMessage: null,
      isError: false,
      isPending: false,
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      isSuccess: true,
      memberships: [...memberships],
      selectedErrorMessage: null,
      selectedMembership: null,
      tierBenefits: null,
      tierBenefitsErrorMessage: null,
      isTierBenefitsError: false,
      isTierBenefitsPending: false,
      isTierBenefitsSuccess: false,
      savingsWindowStatuses: [],
      savingsWindowStatusesErrorMessage: null,
      isSavingsWindowStatusesError: false,
      isSavingsWindowStatusesPending: false,
      isSavingsWindowStatusesSuccess: false,
    });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("loads the enquiry and reference data on mount", () => {
    render(<EnquiryDetails enquiryId={1} />);
    expect(getEnquiry).toHaveBeenCalledWith(1);
    expect(getCustomers).toHaveBeenCalled();
    expect(getProducts).toHaveBeenCalled();
    expect(getMemberships).toHaveBeenCalled();
  });

  it("shows an invalid id message", () => {
    vi.mocked(useEnquiriesState).mockReturnValue({
      ...baseEnquiriesState,
      selectedEnquiry: null,
      isSelectedSuccess: false,
    });
    render(<EnquiryDetails enquiryId={-1} />);
    expect(screen.getByText("This enquiry id is invalid.")).toBeInTheDocument();
    expect(getEnquiry).not.toHaveBeenCalled();
  });

  it("renders the conversation and customer sidebar", () => {
    render(<EnquiryDetails enquiryId={1} />);
    expect(screen.getAllByText("John Doe").length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText(/Is the kayak available\?/)).toBeInTheDocument();
    expect(screen.getAllByText(/Kayak/).length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText("Bronze")).toBeInTheDocument();
  });

  it("submits a response and closes the enquiry when resolved", async () => {
    render(<EnquiryDetails enquiryId={1} />);

    fireEvent.change(screen.getByLabelText("Response message"), {
      target: { value: "Yes, the kayak is available." },
    });
    fireEvent.change(screen.getByLabelText("Status"), {
      target: { value: "2" },
    });

    const form = screen.getByRole("button", { name: "Save response" }).closest("form");
    expect(form).toBeTruthy();
    fireEvent.submit(form!);

    await waitFor(() => expect(respondToEnquiry).toHaveBeenCalledOnce());

    expect(respondToEnquiry).toHaveBeenCalledWith(1, {
      response: "Yes, the kayak is available.",
    });
    expect(closeEnquiry).toHaveBeenCalledWith(1);
    expect(reopenEnquiry).not.toHaveBeenCalled();
  });
});

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Customer } from "@/src/providers/Customers/context";
import type { Product } from "@/src/providers/Products/context";
import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
  useToast,
} from "@/src/providers";

import { EnquiryCreateForm } from "./enquiry-create-form";

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
    useToast: vi.fn(),
  };
});

const customers: Customer[] = [
  { id: 1, name: "John Doe", email: "john@example.com", membershipId: 1, isActive: true },
];

const products: Product[] = [
  { id: 1, name: "Kayak", price: 1500, membershipId: 1, isActive: true },
];

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
  isSelectedSuccess: false,
  loadErrorMessage: null,
  salesReadyEnquiries: [],
  salesReadyErrorMessage: null,
  selectedEnquiry: null,
  selectedErrorMessage: null,
};

const submitForm = () => {
  const button = screen.getByRole("button", { name: "Create enquiry" });
  const form = button.closest("form");
  expect(form).toBeTruthy();
  fireEvent.submit(form!);
};

describe("EnquiryCreateForm", () => {
  const getCustomers = vi.fn();
  const getProducts = vi.fn();
  const createEnquiry = vi.fn().mockResolvedValue(true);
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    createEnquiry.mockResolvedValue(true);

    vi.mocked(useCustomersActions).mockReturnValue({
      createCustomer: vi.fn(),
      getCustomer: vi.fn(),
      getCustomers,
      updateCustomer: vi.fn(),
    });
    vi.mocked(useCustomersState).mockReturnValue({
      createErrorMessage: null,
      customers: [...customers],
      isCreateError: false,
      isCreatePending: false,
      isCreateSuccess: false,
      isLoadError: false,
      isLoadPending: false,
      isLoadSuccess: true,
      isSelectedError: false,
      isSelectedPending: false,
      isSelectedSuccess: false,
      isUpdateError: false,
      isUpdatePending: false,
      isUpdateSuccess: false,
      loadErrorMessage: null,
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
    vi.mocked(useEnquiriesActions).mockReturnValue({
      closeEnquiry: vi.fn(),
      convertEnquiryToCustomer: vi.fn(),
      createEnquiry,
      getEnquiries: vi.fn(),
      getEnquiry: vi.fn(),
      getSalesReadyEnquiries: vi.fn(),
      recordFollowUp: vi.fn(),
      reopenEnquiry: vi.fn(),
      respondToEnquiry: vi.fn(),
    });
    vi.mocked(useEnquiriesState).mockReturnValue({ ...baseEnquiriesState });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("fetches customers and products on mount", () => {
    render(<EnquiryCreateForm />);
    expect(getCustomers).toHaveBeenCalled();
    expect(getProducts).toHaveBeenCalled();
  });

  it("shows validation errors for empty fields", async () => {
    render(<EnquiryCreateForm />);

    submitForm();

    expect(await screen.findByText("Select a customer.")).toBeInTheDocument();
    expect(screen.getByText("Select a product.")).toBeInTheDocument();
    expect(screen.getByText("Message must be at least 10 characters.")).toBeInTheDocument();
  });

  it("calls createEnquiry and shows a toast on success", async () => {
    render(<EnquiryCreateForm />);

    fireEvent.change(screen.getByLabelText("Customer"), {
      target: { value: "1" },
    });
    fireEvent.change(screen.getByLabelText("Product"), {
      target: { value: "1" },
    });
    fireEvent.change(screen.getByLabelText("Message"), {
      target: { value: "Can I get a discount on the kayak?" },
    });

    submitForm();

    await waitFor(() => expect(createEnquiry).toHaveBeenCalledOnce());

    expect(createEnquiry).toHaveBeenCalledWith({
      customerId: 1,
      message: "Can I get a discount on the kayak?",
      productId: 1,
    });
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Success",
        type: "success",
      }),
    );
  });
});

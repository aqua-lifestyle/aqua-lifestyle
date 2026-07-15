import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Membership } from "@/src/providers/Memberships/context";
import type { Product } from "@/src/providers/Products/context";
import {
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";

import { ProductDetails } from "./product-details";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useMembershipsActions: vi.fn(),
    useMembershipsState: vi.fn(),
    useProductsActions: vi.fn(),
    useProductsState: vi.fn(),
  };
});

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

const selectedProduct: Product = {
  id: 3,
  name: "Paddle",
  price: 300,
  membershipId: null,
  isActive: true,
};

const baseProductsState = {
  eligibleErrorMessage: null,
  eligibleProducts: [],
  errorMessage: null,
  isError: false,
  isPending: false,
  isSuccess: false,
  isEligibleError: false,
  isEligiblePending: false,
  isEligibleSuccess: false,
  products: [],
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  selectedErrorMessage: null,
  selectedProduct,
};

const baseMembershipsState = {
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
};

describe("ProductDetails", () => {
  const getProduct = vi.fn();
  const loadMemberships = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useProductsActions).mockReturnValue({
      getEligibleProductsForCustomer: vi.fn(),
      getProduct,
      getProducts: vi.fn(),
    });
    vi.mocked(useProductsState).mockReturnValue({ ...baseProductsState });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getActiveTiers: vi.fn(),
      getMembership: vi.fn(),
      getMemberships: loadMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({ ...baseMembershipsState });
  });

  it("loads the product on mount", () => {
    render(<ProductDetails productId={3} />);
    expect(getProduct).toHaveBeenCalledWith(3);
    expect(loadMemberships).toHaveBeenCalled();
  });

  it("shows an invalid id message", () => {
    vi.mocked(useProductsState).mockReturnValue({
      ...baseProductsState,
      selectedProduct: null,
      isSelectedSuccess: false,
    });
    render(<ProductDetails productId={-1} />);
    expect(
      screen.getByText("This product id is invalid."),
    ).toBeInTheDocument();
    expect(getProduct).not.toHaveBeenCalled();
    expect(loadMemberships).not.toHaveBeenCalled();
  });

  it("renders product overview and price", () => {
    render(<ProductDetails productId={3} />);
    expect(screen.getByText("Paddle")).toBeInTheDocument();
    expect(screen.getByText(/R\s*300\.00/)).toBeInTheDocument();
    expect(screen.getAllByText("Open to all").length).toBeGreaterThanOrEqual(2);
  });

  it("switches tabs", () => {
    render(<ProductDetails productId={3} />);

    expect(screen.getByText("Pricing & inventory")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Pricing & inventory" }));
    expect(screen.getByText("List price")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("tab", { name: "Eligibility" }));
    expect(screen.getByText("Membership eligibility")).toBeInTheDocument();
  });
});

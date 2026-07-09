import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Membership } from "@/src/providers/Memberships/context";
import type { Product } from "@/src/providers/Products/context";
import {
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";

import { ProductsCatalog } from "./products-catalog";

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

const products: Product[] = [
  { id: 1, name: "Kayak", price: 1500, membershipId: 1, isActive: true },
  { id: 3, name: "Paddle", price: 300, membershipId: null, isActive: true },
  { id: 2, name: "Wetsuit", price: 1200, membershipId: null, isActive: false },
];

const baseProductsState = {
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

describe("ProductsCatalog", () => {
  const getProducts = vi.fn();
  const getMemberships = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useProductsActions).mockReturnValue({
      getEligibleProductsForCustomer: vi.fn(),
      getProduct: vi.fn(),
      getProducts,
    });
    vi.mocked(useProductsState).mockReturnValue({ ...baseProductsState });
    vi.mocked(useMembershipsActions).mockReturnValue({
      getMembership: vi.fn(),
      getMemberships,
      getSavingsWindowStatuses: vi.fn(),
      getTierBenefits: vi.fn(),
    });
    vi.mocked(useMembershipsState).mockReturnValue({ ...baseMembershipsState });
  });

  it("fetches products and memberships on mount", () => {
    render(<ProductsCatalog />);
    expect(getProducts).toHaveBeenCalled();
    expect(getMemberships).toHaveBeenCalled();
  });

  it("renders product cards and stock summary", () => {
    render(<ProductsCatalog />);
    const totalCard = screen.getByText("Total products").closest("article");
    expect(totalCard).toBeInTheDocument();
    expect(within(totalCard!).getByText("3")).toBeInTheDocument();

    expect(screen.getByText("Kayak")).toBeInTheDocument();
    expect(screen.getByText("Paddle")).toBeInTheDocument();
    expect(screen.getByText("Wetsuit")).toBeInTheDocument();
  });

  it("filters products by stock status", () => {
    render(<ProductsCatalog />);

    fireEvent.change(screen.getByLabelText("Stock status"), {
      target: { value: "in-stock" },
    });

    expect(screen.getByText("Paddle")).toBeInTheDocument();
    expect(screen.queryByText("Kayak")).not.toBeInTheDocument();
    expect(screen.queryByText("Wetsuit")).not.toBeInTheDocument();
  });

  it("filters products by access tier", () => {
    render(<ProductsCatalog />);

    fireEvent.change(screen.getByLabelText("Access tier"), {
      target: { value: "none" },
    });

    expect(screen.getByText("Paddle")).toBeInTheDocument();
    expect(screen.getByText("Wetsuit")).toBeInTheDocument();
    expect(screen.queryByText("Kayak")).not.toBeInTheDocument();
  });

  it("filters products by search query", async () => {
    render(<ProductsCatalog />);

    fireEvent.click(screen.getByRole("button", { name: "Table view" }));

    fireEvent.change(screen.getByPlaceholderText("Search..."), {
      target: { value: "kayak" },
    });

    await waitFor(() => {
      expect(screen.getByText("Kayak")).toBeInTheDocument();
      expect(screen.queryByText("Paddle")).not.toBeInTheDocument();
    });
  });

  it("switches to table view", () => {
    render(<ProductsCatalog />);

    expect(screen.queryByRole("table")).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Table view" }));

    expect(screen.getByRole("table")).toBeInTheDocument();
    expect(screen.getByText("Kayak")).toBeInTheDocument();
  });
});

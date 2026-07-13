import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Product } from "@/src/providers/Products/context";
import { useProductsActions, useProductsState } from "@/src/providers";

import { PublicCatalog } from "./public-catalog";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useProductsActions: vi.fn(),
    useProductsState: vi.fn(),
  };
});

const products: Product[] = [
  {
    id: 1,
    name: "Product A",
    price: 100,
    membershipId: 1,
    isActive: true,
  },
  {
    id: 2,
    name: "Product B",
    price: 200,
    membershipId: null,
    isActive: false,
  },
];

const baseState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSuccess: true,
  eligibleErrorMessage: null,
  eligibleProducts: [],
  isEligibleError: false,
  isEligiblePending: false,
  isEligibleSuccess: false,
  products,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  selectedErrorMessage: null,
  selectedProduct: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useProductsState).mockReturnValue(baseState);
  vi.mocked(useProductsActions).mockReturnValue({
    getEligibleProductsForCustomer: vi.fn(),
    getProduct: vi.fn(),
    getProducts: vi.fn(),
  });
});

describe("PublicCatalog", () => {
  it("renders the product catalog", () => {
    render(<PublicCatalog />);

    expect(screen.getByRole("heading", { name: /Product Catalog/i })).toBeDefined();
    expect(screen.getByText("Product A")).toBeDefined();
    expect(screen.getByText("Product B")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useProductsState).mockReturnValue({
      ...baseState,
      isPending: true,
    });

    render(<PublicCatalog />);

    expect(screen.queryByText("Product A")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useProductsState).mockReturnValue({
      ...baseState,
      isError: true,
      errorMessage: "Failed to load products",
    });

    render(<PublicCatalog />);

    expect(screen.getByText("Failed to load products")).toBeDefined();
  });

  it("shows empty state when there are no products", () => {
    vi.mocked(useProductsState).mockReturnValue({
      ...baseState,
      products: [],
    });

    render(<PublicCatalog />);

    expect(screen.getByText("No products")).toBeDefined();
    expect(screen.getByText("No products available.")).toBeDefined();
  });

  it("renders the stock status filter", () => {
    render(<PublicCatalog />);

    expect(screen.getByLabelText("Stock Status")).toBeDefined();
  });
});

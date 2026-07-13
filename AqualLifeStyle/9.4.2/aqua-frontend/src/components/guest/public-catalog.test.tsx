import { fireEvent, render, screen, waitFor } from "@testing-library/react";
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

  (useProductsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useProductsActions as unknown as { mockReturnValue: { getProducts: () => Promise<void> } }).mockReturnValue({
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
    (useProductsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isPending: true,
    });

    render(<PublicCatalog />);

    expect(screen.queryByText("Product A")).toBeNull();
  });

  it("shows error state", () => {
    (useProductsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isError: true,
      errorMessage: "Failed to load products",
    });

    render(<PublicCatalog />);

    expect(screen.getByText("Failed to load products")).toBeDefined();
  });

  it("shows empty state when there are no products", () => {
    (useProductsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
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

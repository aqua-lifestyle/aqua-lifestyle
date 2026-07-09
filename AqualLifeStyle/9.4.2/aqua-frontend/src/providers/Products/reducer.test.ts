import { describe, expect, it } from "vitest";

import {
  getEligibleProductsError,
  getEligibleProductsPending,
  getEligibleProductsSuccess,
  getProductError,
  getProductPending,
  getProductSuccess,
  getProductsError,
  getProductsPending,
  getProductsSuccess,
} from "./actions";
import { initialProductsState, type Product } from "./context";
import { productsReducer } from "./reducer";

const product: Product = {
  id: 1,
  name: "Aqua Filter",
  price: 199,
  membershipId: 1,
  isActive: true,
};

describe("productsReducer", () => {
  it("returns the current state for unknown actions", () => {
    const state = productsReducer(initialProductsState, {
      type: "products/unknown",
    } as never);

    expect(state).toBe(initialProductsState);
  });

  it("tracks the products list lifecycle", () => {
    const pendingState = productsReducer(
      initialProductsState,
      getProductsPending(),
    );

    expect(pendingState.isPending).toBe(true);
    expect(pendingState.errorMessage).toBeNull();

    const successState = productsReducer(
      pendingState,
      getProductsSuccess([product]),
    );

    expect(successState.isPending).toBe(false);
    expect(successState.isSuccess).toBe(true);
    expect(successState.products).toEqual([product]);

    const errorState = productsReducer(
      successState,
      getProductsError("Unable to load products."),
    );

    expect(errorState.isError).toBe(true);
    expect(errorState.isPending).toBe(false);
    expect(errorState.isSuccess).toBe(false);
    expect(errorState.errorMessage).toBe("Unable to load products.");
  });

  it("tracks the eligible products lifecycle", () => {
    const pendingState = productsReducer(
      initialProductsState,
      getEligibleProductsPending(),
    );

    expect(pendingState.isEligiblePending).toBe(true);

    const successState = productsReducer(
      pendingState,
      getEligibleProductsSuccess([product]),
    );

    expect(successState.isEligibleSuccess).toBe(true);
    expect(successState.eligibleProducts).toEqual([product]);

    const errorState = productsReducer(
      successState,
      getEligibleProductsError("No eligible products."),
    );

    expect(errorState.isEligibleError).toBe(true);
    expect(errorState.eligibleProducts).toEqual([]);
    expect(errorState.eligibleErrorMessage).toBe("No eligible products.");
  });

  it("tracks the selected product lifecycle", () => {
    const pendingState = productsReducer(
      initialProductsState,
      getProductPending(),
    );

    expect(pendingState.isSelectedPending).toBe(true);

    const successState = productsReducer(
      pendingState,
      getProductSuccess(product),
    );

    expect(successState.isSelectedSuccess).toBe(true);
    expect(successState.selectedProduct).toEqual(product);

    const errorState = productsReducer(
      successState,
      getProductError("Product not found."),
    );

    expect(errorState.isSelectedError).toBe(true);
    expect(errorState.selectedProduct).toBeNull();
    expect(errorState.selectedErrorMessage).toBe("Product not found.");
  });
});

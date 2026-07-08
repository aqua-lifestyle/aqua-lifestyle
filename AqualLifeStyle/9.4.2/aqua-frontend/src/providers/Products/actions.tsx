import type { Product } from "./context";

export const ProductsActionTypes = {
  getProductPending: "products/getProductPending",
  getProductSuccess: "products/getProductSuccess",
  getProductError: "products/getProductError",
  getProductsPending: "products/getProductsPending",
  getProductsSuccess: "products/getProductsSuccess",
  getProductsError: "products/getProductsError",
} as const;

export type ProductsAction =
  | {
      type: typeof ProductsActionTypes.getProductPending;
    }
  | {
      type: typeof ProductsActionTypes.getProductSuccess;
      payload: Product;
    }
  | {
      type: typeof ProductsActionTypes.getProductError;
      payload: string;
    }
  | {
      type: typeof ProductsActionTypes.getProductsPending;
    }
  | {
      type: typeof ProductsActionTypes.getProductsSuccess;
      payload: Product[];
    }
  | {
      type: typeof ProductsActionTypes.getProductsError;
      payload: string;
    };

export const getProductPending = (): ProductsAction => ({
  type: ProductsActionTypes.getProductPending,
});

export const getProductSuccess = (product: Product): ProductsAction => ({
  type: ProductsActionTypes.getProductSuccess,
  payload: product,
});

export const getProductError = (message: string): ProductsAction => ({
  type: ProductsActionTypes.getProductError,
  payload: message,
});

export const getProductsPending = (): ProductsAction => ({
  type: ProductsActionTypes.getProductsPending,
});

export const getProductsSuccess = (products: Product[]): ProductsAction => ({
  type: ProductsActionTypes.getProductsSuccess,
  payload: products,
});

export const getProductsError = (message: string): ProductsAction => ({
  type: ProductsActionTypes.getProductsError,
  payload: message,
});

import type { Product } from "./context";

export const ProductsActionTypes = {
  getProductsPending: "products/getProductsPending",
  getProductsSuccess: "products/getProductsSuccess",
  getProductsError: "products/getProductsError",
} as const;

export type ProductsAction =
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

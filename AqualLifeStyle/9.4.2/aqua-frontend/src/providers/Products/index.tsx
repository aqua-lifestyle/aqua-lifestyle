"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { AbpHttpError, apiEndpoints, httpClient } from "@/src/shared/api";
import {
  getProductsError,
  getProductsPending,
  getProductsSuccess,
} from "./actions";
import {
  initialProductsState,
  type Product,
  ProductsActionsContext,
  ProductsStateContext,
} from "./context";
import { productsReducer } from "./reducer";

type ProductsProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to load products.";
};

export const ProductsProvider = ({ children }: ProductsProviderProps) => {
  const [state, dispatch] = useReducer(productsReducer, initialProductsState);

  const getProducts = useCallback(async () => {
    dispatch(getProductsPending());

    try {
      const products = await httpClient.get<Product[]>(apiEndpoints.products.getAll);
      dispatch(getProductsSuccess(products));
    } catch (error) {
      dispatch(getProductsError(getErrorMessage(error)));
    }
  }, []);

  const actions = useMemo(
    () => ({
      getProducts,
    }),
    [getProducts],
  );

  return (
    <ProductsStateContext.Provider value={state}>
      <ProductsActionsContext.Provider value={actions}>
        {children}
      </ProductsActionsContext.Provider>
    </ProductsStateContext.Provider>
  );
};

export const useProductsState = () => {
  return useContext(ProductsStateContext);
};

export const useProductsActions = () => {
  const context = useContext(ProductsActionsContext);

  if (!context) {
    throw new Error("useProductsActions must be used within a ProductsProvider.");
  }

  return context;
};

export type { Product };

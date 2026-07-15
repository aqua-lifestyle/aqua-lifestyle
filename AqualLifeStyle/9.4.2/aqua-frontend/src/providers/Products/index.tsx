"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { apiEndpoints, getRequestErrorMessage, httpClient } from "@/src/shared/api";
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
  return getRequestErrorMessage(error, "Unable to load products.");
};

export const ProductsProvider = ({ children }: ProductsProviderProps) => {
  const [state, dispatch] = useReducer(productsReducer, initialProductsState);

  const getEligibleProductsForCustomer = useCallback(
    async (customerId: number) => {
      dispatch(getEligibleProductsPending());

      try {
        const products = await httpClient.get<Product[]>(
          apiEndpoints.products.getAllForCustomer(customerId),
        );
        dispatch(getEligibleProductsSuccess(products));
      } catch (error) {
        dispatch(getEligibleProductsError(getErrorMessage(error)));
      }
    },
    [],
  );

  const getProducts = useCallback(async () => {
    dispatch(getProductsPending());

    try {
      const products = await httpClient.get<Product[]>(apiEndpoints.products.getAll);
      dispatch(getProductsSuccess(products));
    } catch (error) {
      dispatch(getProductsError(getErrorMessage(error)));
    }
  }, []);

  const getProduct = useCallback(async (id: number) => {
    dispatch(getProductPending());

    try {
      const product = await httpClient.get<Product>(
        apiEndpoints.products.getById(id),
      );
      dispatch(getProductSuccess(product));
    } catch (error) {
      dispatch(getProductError(getErrorMessage(error)));
    }
  }, []);

  const actions = useMemo(
    () => ({
      getEligibleProductsForCustomer,
      getProduct,
      getProducts,
    }),
    [getEligibleProductsForCustomer, getProduct, getProducts],
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

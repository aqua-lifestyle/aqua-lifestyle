import { createContext } from "react";

export type Product = {
  id: number;
  name: string;
  price: number;
  membershipId: number | null;
  isActive: boolean;
};

export type ProductsState = {
  eligibleErrorMessage: string | null;
  eligibleProducts: Product[];
  isPending: boolean;
  isSuccess: boolean;
  isError: boolean;
  isEligibleError: boolean;
  isEligiblePending: boolean;
  isEligibleSuccess: boolean;
  errorMessage: string | null;
  products: Product[];
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  selectedErrorMessage: string | null;
  selectedProduct: Product | null;
};

export type ProductsActions = {
  getEligibleProductsForCustomer: (customerId: number) => Promise<void>;
  getProduct: (id: number) => Promise<void>;
  getProducts: () => Promise<void>;
};

export const initialProductsState: ProductsState = {
  eligibleErrorMessage: null,
  eligibleProducts: [],
  isPending: false,
  isSuccess: false,
  isError: false,
  isEligibleError: false,
  isEligiblePending: false,
  isEligibleSuccess: false,
  errorMessage: null,
  products: [],
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  selectedErrorMessage: null,
  selectedProduct: null,
};

export const ProductsStateContext =
  createContext<ProductsState>(initialProductsState);

export const ProductsActionsContext =
  createContext<ProductsActions | null>(null);

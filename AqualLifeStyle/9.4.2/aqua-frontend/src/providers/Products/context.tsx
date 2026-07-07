import { createContext } from "react";

export type Product = {
  id: number;
  name: string;
  price: number;
  membershipId: number | null;
  isActive: boolean;
};

export type ProductsState = {
  isPending: boolean;
  isSuccess: boolean;
  isError: boolean;
  errorMessage: string | null;
  products: Product[];
};

export type ProductsActions = {
  getProducts: () => Promise<void>;
};

export const initialProductsState: ProductsState = {
  isPending: false,
  isSuccess: false,
  isError: false,
  errorMessage: null,
  products: [],
};

export const ProductsStateContext =
  createContext<ProductsState>(initialProductsState);

export const ProductsActionsContext =
  createContext<ProductsActions | null>(null);

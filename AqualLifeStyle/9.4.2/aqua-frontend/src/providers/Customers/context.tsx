import { createContext } from "react";

export type Customer = {
  id: number;
  name: string;
  email: string;
  membershipId: number | null;
  isActive: boolean;
};

export type CreateCustomerInput = {
  name: string;
  email: string;
  membershipId: number | null;
};

export type CustomersState = {
  createErrorMessage: string | null;
  customers: Customer[];
  isCreateError: boolean;
  isCreatePending: boolean;
  isCreateSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  loadErrorMessage: string | null;
};

export type CustomersActions = {
  createCustomer: (input: CreateCustomerInput) => Promise<boolean>;
  getCustomers: () => Promise<void>;
};

export const initialCustomersState: CustomersState = {
  createErrorMessage: null,
  customers: [],
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  loadErrorMessage: null,
};

export const CustomersStateContext =
  createContext<CustomersState>(initialCustomersState);

export const CustomersActionsContext =
  createContext<CustomersActions | null>(null);

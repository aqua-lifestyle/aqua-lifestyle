import { createContext } from "react";

export type Customer = {
  id: number;
  name: string;
  email: string;
  membershipId: number | null;
  isActive: boolean;
  tenantId: number | null;
  userId: number;
};

export type CreateCustomerInput = {
  name: string;
  email: string;
  membershipId: number | null;
};

export type UpdateCustomerInput = Customer;

export type CustomersState = {
  createErrorMessage: string | null;
  customers: Customer[];
  isCreateError: boolean;
  isCreatePending: boolean;
  isCreateSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  isMyCustomerError: boolean;
  isMyCustomerPending: boolean;
  isMyCustomerSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  isUpdateError: boolean;
  isUpdatePending: boolean;
  isUpdateSuccess: boolean;
  loadErrorMessage: string | null;
  myCustomer: Customer | null;
  myCustomerErrorMessage: string | null;
  selectedCustomer: Customer | null;
  selectedErrorMessage: string | null;
  updateErrorMessage: string | null;
};

export type CustomersActions = {
  createCustomer: (input: CreateCustomerInput) => Promise<boolean>;
  getCustomer: (id: number) => Promise<void>;
  getCustomers: () => Promise<void>;
  getMyCustomer: () => Promise<void>;
  updateCustomer: (input: UpdateCustomerInput) => Promise<boolean>;
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
  isMyCustomerError: false,
  isMyCustomerPending: false,
  isMyCustomerSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isUpdateError: false,
  isUpdatePending: false,
  isUpdateSuccess: false,
  loadErrorMessage: null,
  myCustomer: null,
  myCustomerErrorMessage: null,
  selectedCustomer: null,
  selectedErrorMessage: null,
  updateErrorMessage: null,
};

export const CustomersStateContext =
  createContext<CustomersState>(initialCustomersState);

export const CustomersActionsContext =
  createContext<CustomersActions | null>(null);

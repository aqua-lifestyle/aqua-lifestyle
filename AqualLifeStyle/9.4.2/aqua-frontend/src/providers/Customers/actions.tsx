import type { Customer } from "./context";

export const CustomersActionTypes = {
  createCustomerError: "customers/createCustomerError",
  createCustomerPending: "customers/createCustomerPending",
  createCustomerSuccess: "customers/createCustomerSuccess",
  getCustomersError: "customers/getCustomersError",
  getCustomersPending: "customers/getCustomersPending",
  getCustomersSuccess: "customers/getCustomersSuccess",
} as const;

export type CustomersAction =
  | {
      type: typeof CustomersActionTypes.createCustomerError;
      payload: string;
    }
  | {
      type: typeof CustomersActionTypes.createCustomerPending;
    }
  | {
      type: typeof CustomersActionTypes.createCustomerSuccess;
    }
  | {
      type: typeof CustomersActionTypes.getCustomersError;
      payload: string;
    }
  | {
      type: typeof CustomersActionTypes.getCustomersPending;
    }
  | {
      type: typeof CustomersActionTypes.getCustomersSuccess;
      payload: Customer[];
    };

export const createCustomerError = (message: string): CustomersAction => ({
  type: CustomersActionTypes.createCustomerError,
  payload: message,
});

export const createCustomerPending = (): CustomersAction => ({
  type: CustomersActionTypes.createCustomerPending,
});

export const createCustomerSuccess = (): CustomersAction => ({
  type: CustomersActionTypes.createCustomerSuccess,
});

export const getCustomersError = (message: string): CustomersAction => ({
  type: CustomersActionTypes.getCustomersError,
  payload: message,
});

export const getCustomersPending = (): CustomersAction => ({
  type: CustomersActionTypes.getCustomersPending,
});

export const getCustomersSuccess = (customers: Customer[]): CustomersAction => ({
  type: CustomersActionTypes.getCustomersSuccess,
  payload: customers,
});

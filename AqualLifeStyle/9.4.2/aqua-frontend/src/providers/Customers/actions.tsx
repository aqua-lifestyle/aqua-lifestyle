import type { Customer } from "./context";

export const CustomersActionTypes = {
  createCustomerError: "customers/createCustomerError",
  createCustomerPending: "customers/createCustomerPending",
  createCustomerSuccess: "customers/createCustomerSuccess",
  getCustomerError: "customers/getCustomerError",
  getCustomerPending: "customers/getCustomerPending",
  getCustomerSuccess: "customers/getCustomerSuccess",
  getCustomersError: "customers/getCustomersError",
  getCustomersPending: "customers/getCustomersPending",
  getCustomersSuccess: "customers/getCustomersSuccess",
  updateCustomerError: "customers/updateCustomerError",
  updateCustomerPending: "customers/updateCustomerPending",
  updateCustomerSuccess: "customers/updateCustomerSuccess",
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
      type: typeof CustomersActionTypes.getCustomerError;
      payload: string;
    }
  | {
      type: typeof CustomersActionTypes.getCustomerPending;
    }
  | {
      type: typeof CustomersActionTypes.getCustomerSuccess;
      payload: Customer;
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
    }
  | {
      type: typeof CustomersActionTypes.updateCustomerError;
      payload: string;
    }
  | {
      type: typeof CustomersActionTypes.updateCustomerPending;
    }
  | {
      type: typeof CustomersActionTypes.updateCustomerSuccess;
      payload: Customer;
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

export const getCustomerError = (message: string): CustomersAction => ({
  type: CustomersActionTypes.getCustomerError,
  payload: message,
});

export const getCustomerPending = (): CustomersAction => ({
  type: CustomersActionTypes.getCustomerPending,
});

export const getCustomerSuccess = (customer: Customer): CustomersAction => ({
  type: CustomersActionTypes.getCustomerSuccess,
  payload: customer,
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

export const updateCustomerError = (message: string): CustomersAction => ({
  type: CustomersActionTypes.updateCustomerError,
  payload: message,
});

export const updateCustomerPending = (): CustomersAction => ({
  type: CustomersActionTypes.updateCustomerPending,
});

export const updateCustomerSuccess = (customer: Customer): CustomersAction => ({
  type: CustomersActionTypes.updateCustomerSuccess,
  payload: customer,
});

"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { apiEndpoints, getErrorMessage, httpClient } from "@/src/shared/api";
import {
  createCustomerError,
  createCustomerPending,
  createCustomerSuccess,
  getCustomerError,
  getCustomerPending,
  getCustomerSuccess,
  getCustomersError,
  getCustomersPending,
  getCustomersSuccess,
  updateCustomerError,
  updateCustomerPending,
  updateCustomerSuccess,
} from "./actions";
import {
  type CreateCustomerInput,
  type Customer,
  CustomersActionsContext,
  CustomersStateContext,
  initialCustomersState,
  type UpdateCustomerInput,
} from "./context";
import { customersReducer } from "./reducer";

type CustomersProviderProps = {
  children: ReactNode;
};

export const CustomersProvider = ({ children }: CustomersProviderProps) => {
  const [state, dispatch] = useReducer(customersReducer, initialCustomersState);

  const getCustomers = useCallback(async () => {
    dispatch(getCustomersPending());

    try {
      const customers = await httpClient.get<Customer[]>(
        apiEndpoints.customers.getAll,
      );
      dispatch(getCustomersSuccess(customers));
    } catch (error) {
      dispatch(getCustomersError(getErrorMessage(error, "Unable to complete the customer request.")));
    }
  }, []);

  const createCustomer = useCallback(async (input: CreateCustomerInput) => {
    dispatch(createCustomerPending());

    try {
      await httpClient.post<void, CreateCustomerInput>(
        apiEndpoints.customers.create,
        input,
      );
      dispatch(createCustomerSuccess());
      return true;
    } catch (error) {
      dispatch(createCustomerError(getErrorMessage(error, "Unable to complete the customer request.")));
      return false;
    }
  }, []);

  const getCustomer = useCallback(async (id: number) => {
    dispatch(getCustomerPending());

    try {
      const customer = await httpClient.get<Customer>(
        apiEndpoints.customers.getById(id),
      );
      dispatch(getCustomerSuccess(customer));
    } catch (error) {
      dispatch(getCustomerError(getErrorMessage(error, "Unable to complete the customer request.")));
    }
  }, []);

  const updateCustomer = useCallback(async (input: UpdateCustomerInput) => {
    dispatch(updateCustomerPending());

    try {
      const customer = await httpClient.put<Customer, UpdateCustomerInput>(
        apiEndpoints.customers.update,
        input,
      );
      dispatch(updateCustomerSuccess(customer));
      return true;
    } catch (error) {
      dispatch(updateCustomerError(getErrorMessage(error, "Unable to complete the customer request.")));
      return false;
    }
  }, []);

  const actions = useMemo(
    () => ({
      createCustomer,
      getCustomer,
      getCustomers,
      updateCustomer,
    }),
    [createCustomer, getCustomer, getCustomers, updateCustomer],
  );

  return (
    <CustomersStateContext.Provider value={state}>
      <CustomersActionsContext.Provider value={actions}>
        {children}
      </CustomersActionsContext.Provider>
    </CustomersStateContext.Provider>
  );
};

export const useCustomersState = () => {
  return useContext(CustomersStateContext);
};

export const useCustomersActions = () => {
  const context = useContext(CustomersActionsContext);

  if (!context) {
    throw new Error("useCustomersActions must be used within a CustomersProvider.");
  }

  return context;
};

export type { CreateCustomerInput, Customer, UpdateCustomerInput };

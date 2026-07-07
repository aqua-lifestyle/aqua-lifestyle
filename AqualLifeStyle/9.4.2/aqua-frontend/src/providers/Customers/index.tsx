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
  createCustomerError,
  createCustomerPending,
  createCustomerSuccess,
  getCustomersError,
  getCustomersPending,
  getCustomersSuccess,
} from "./actions";
import {
  type CreateCustomerInput,
  type Customer,
  CustomersActionsContext,
  CustomersStateContext,
  initialCustomersState,
} from "./context";
import { customersReducer } from "./reducer";

type CustomersProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to complete the customer request.";
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
      dispatch(getCustomersError(getErrorMessage(error)));
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
      dispatch(createCustomerError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo(
    () => ({
      createCustomer,
      getCustomers,
    }),
    [createCustomer, getCustomers],
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

export type { CreateCustomerInput, Customer };

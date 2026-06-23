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
  changeMembershipError,
  changeMembershipPending,
  changeMembershipSuccess,
  createCustomerError,
  createCustomerPending,
  createCustomerSuccess,
  getCustomerError,
  getCustomerPending,
  getCustomerSuccess,
  getCustomersError,
  getCustomersPending,
  getCustomersSuccess,
  getMyCustomerError,
  getMyCustomerPending,
  getMyCustomerSuccess,
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

const getErrorMessage = (error: unknown): string => {
  return getRequestErrorMessage(error, "Unable to complete the customer request.");
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

  const getCustomer = useCallback(async (id: number) => {
    dispatch(getCustomerPending());

    try {
      const customer = await httpClient.get<Customer>(
        apiEndpoints.customers.getById(id),
      );
      dispatch(getCustomerSuccess(customer));
    } catch (error) {
      dispatch(getCustomerError(getErrorMessage(error)));
    }
  }, []);

  const getMyCustomer = useCallback(async () => {
    dispatch(getMyCustomerPending());

    try {
      const customer = await httpClient.get<Customer>(
        apiEndpoints.customers.getMyCustomer,
      );
      dispatch(getMyCustomerSuccess(customer));
    } catch (error) {
      dispatch(getMyCustomerError(getErrorMessage(error)));
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
      dispatch(updateCustomerError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const changeMembership = useCallback(
    async (input: { membershipId?: number | null }): Promise<Customer | null> => {
      dispatch(changeMembershipPending());

      try {
        const customer = await httpClient.post<Customer, { membershipId?: number | null }>(
          apiEndpoints.customers.changeMembership,
          input,
        );
        dispatch(changeMembershipSuccess(customer));
        return customer;
      } catch (error) {
        dispatch(changeMembershipError(getErrorMessage(error)));
        return null;
      }
    },
    [],
  );

  const actions = useMemo(
    () => ({
      changeMembership,
      createCustomer,
      getCustomer,
      getCustomers,
      getMyCustomer,
      updateCustomer,
    }),
    [changeMembership, createCustomer, getCustomer, getCustomers, getMyCustomer, updateCustomer],
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

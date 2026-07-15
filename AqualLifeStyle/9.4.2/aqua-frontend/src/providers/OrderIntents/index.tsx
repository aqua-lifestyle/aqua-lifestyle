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
  getOrderIntentsError,
  getOrderIntentsPending,
  getOrderIntentsSuccess,
  orderIntentActionError,
  orderIntentActionPending,
  orderIntentActionSuccess,
} from "./actions";
import {
  type OrderIntent,
  OrderIntentsActionsContext,
  OrderIntentsStateContext,
  initialOrderIntentsState,
} from "./context";
import { orderIntentsReducer } from "./reducer";

type OrderIntentsProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to complete the order intent request.";
};

export const OrderIntentsProvider = ({
  children,
}: OrderIntentsProviderProps) => {
  const [state, dispatch] = useReducer(
    orderIntentsReducer,
    initialOrderIntentsState,
  );

  const getOrderIntents = useCallback(async () => {
    dispatch(getOrderIntentsPending());

    try {
      const orderIntents = await httpClient.get<OrderIntent[]>(
        apiEndpoints.orderIntents.getAll,
      );
      dispatch(getOrderIntentsSuccess(orderIntents));
    } catch (error) {
      dispatch(getOrderIntentsError(getErrorMessage(error)));
    }
  }, []);

  const createFromEnquiry = useCallback(async (enquiryId: number) => {
    dispatch(orderIntentActionPending());

    try {
      const orderIntent = await httpClient.post<
        OrderIntent,
        Record<string, never>
      >(apiEndpoints.orderIntents.createFromEnquiry(enquiryId), {});
      dispatch(orderIntentActionSuccess(orderIntent));
      return true;
    } catch (error) {
      dispatch(orderIntentActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const createForCurrentCustomer = useCallback(async (productId: number) => {
    dispatch(orderIntentActionPending());

    try {
      const orderIntent = await httpClient.post<
        OrderIntent,
        Record<string, never>
      >(apiEndpoints.orderIntents.createForCurrentCustomer(productId), {});
      dispatch(orderIntentActionSuccess(orderIntent));
      return true;
    } catch (error) {
      dispatch(orderIntentActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const cancelOrderIntent = useCallback(async (id: number) => {
    dispatch(orderIntentActionPending());

    try {
      const orderIntent = await httpClient.post<
        OrderIntent,
        Record<string, never>
      >(apiEndpoints.orderIntents.cancel(id), {});
      dispatch(orderIntentActionSuccess(orderIntent));
      return true;
    } catch (error) {
      dispatch(orderIntentActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const completeOrderIntent = useCallback(async (id: number) => {
    dispatch(orderIntentActionPending());

    try {
      const orderIntent = await httpClient.post<
        OrderIntent,
        Record<string, never>
      >(apiEndpoints.orderIntents.complete(id), {});
      dispatch(orderIntentActionSuccess(orderIntent));
      return true;
    } catch (error) {
      dispatch(orderIntentActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo(
    () => ({
      cancelOrderIntent,
      completeOrderIntent,
      createFromEnquiry,
      createForCurrentCustomer,
      getOrderIntents,
    }),
    [cancelOrderIntent, completeOrderIntent, createForCurrentCustomer, createFromEnquiry, getOrderIntents],
  );

  return (
    <OrderIntentsStateContext.Provider value={state}>
      <OrderIntentsActionsContext.Provider value={actions}>
        {children}
      </OrderIntentsActionsContext.Provider>
    </OrderIntentsStateContext.Provider>
  );
};

export const useOrderIntentsState = () => {
  return useContext(OrderIntentsStateContext);
};

export const useOrderIntentsActions = () => {
  const context = useContext(OrderIntentsActionsContext);

  if (!context) {
    throw new Error(
      "useOrderIntentsActions must be used within an OrderIntentsProvider.",
    );
  }

  return context;
};

export type { OrderIntent, OrderIntentStatus } from "./context";

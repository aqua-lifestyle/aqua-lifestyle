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
      dispatch(getOrderIntentsError(getErrorMessage(error, "Unable to complete the order intent request.")));
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
      dispatch(orderIntentActionError(getErrorMessage(error, "Unable to complete the order intent request.")));
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
      dispatch(orderIntentActionError(getErrorMessage(error, "Unable to complete the order intent request.")));
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
      dispatch(orderIntentActionError(getErrorMessage(error, "Unable to complete the order intent request.")));
      return false;
    }
  }, []);

  const actions = useMemo(
    () => ({
      cancelOrderIntent,
      completeOrderIntent,
      createFromEnquiry,
      getOrderIntents,
    }),
    [cancelOrderIntent, completeOrderIntent, createFromEnquiry, getOrderIntents],
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

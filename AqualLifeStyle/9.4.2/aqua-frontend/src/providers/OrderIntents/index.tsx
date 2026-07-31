"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
  useRef,
} from "react";

import { apiEndpoints, getRequestErrorMessage, httpClient } from "@/src/shared/api";
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
  return getRequestErrorMessage(error, "Unable to complete the order intent request.");
};

export const OrderIntentsProvider = ({
  children,
}: OrderIntentsProviderProps) => {
  const [state, dispatch] = useReducer(
    orderIntentsReducer,
    initialOrderIntentsState,
  );
  const loadRequestIdentifier = useRef(0);

  const loadOrderIntents = useCallback(async (endpoint: string) => {
    const requestIdentifier = ++loadRequestIdentifier.current;
    dispatch(getOrderIntentsPending());

    try {
      const orderIntents = await httpClient.get<OrderIntent[]>(endpoint);
      if (requestIdentifier === loadRequestIdentifier.current) {
        dispatch(getOrderIntentsSuccess(orderIntents));
      }
    } catch (error) {
      if (requestIdentifier === loadRequestIdentifier.current) {
        dispatch(getOrderIntentsError(getErrorMessage(error)));
      }
    }
  }, []);

  const getOrderIntents = useCallback(
    () => loadOrderIntents(apiEndpoints.orderIntents.getAll),
    [loadOrderIntents],
  );

  const getMyOrderIntents = useCallback(
    () => loadOrderIntents(apiEndpoints.orderIntents.getMine),
    [loadOrderIntents],
  );

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
      getMyOrderIntents,
    }),
    [cancelOrderIntent, completeOrderIntent, createForCurrentCustomer, createFromEnquiry, getMyOrderIntents, getOrderIntents],
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

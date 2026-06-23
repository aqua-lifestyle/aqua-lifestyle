import type { OrderIntent } from "./context";

export const OrderIntentsActionTypes = {
  orderIntentActionError: "orderIntents/orderIntentActionError",
  orderIntentActionPending: "orderIntents/orderIntentActionPending",
  orderIntentActionSuccess: "orderIntents/orderIntentActionSuccess",
  getOrderIntentsError: "orderIntents/getOrderIntentsError",
  getOrderIntentsPending: "orderIntents/getOrderIntentsPending",
  getOrderIntentsSuccess: "orderIntents/getOrderIntentsSuccess",
} as const;

export type OrderIntentsAction =
  | {
      type: typeof OrderIntentsActionTypes.orderIntentActionError;
      payload: string;
    }
  | {
      type: typeof OrderIntentsActionTypes.orderIntentActionPending;
    }
  | {
      type: typeof OrderIntentsActionTypes.orderIntentActionSuccess;
      payload: OrderIntent;
    }
  | {
      type: typeof OrderIntentsActionTypes.getOrderIntentsError;
      payload: string;
    }
  | {
      type: typeof OrderIntentsActionTypes.getOrderIntentsPending;
    }
  | {
      type: typeof OrderIntentsActionTypes.getOrderIntentsSuccess;
      payload: OrderIntent[];
    };

export const orderIntentActionError = (
  message: string,
): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.orderIntentActionError,
  payload: message,
});

export const orderIntentActionPending = (): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.orderIntentActionPending,
});

export const orderIntentActionSuccess = (
  orderIntent: OrderIntent,
): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.orderIntentActionSuccess,
  payload: orderIntent,
});

export const getOrderIntentsError = (
  message: string,
): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.getOrderIntentsError,
  payload: message,
});

export const getOrderIntentsPending = (): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.getOrderIntentsPending,
});

export const getOrderIntentsSuccess = (
  orderIntents: OrderIntent[],
): OrderIntentsAction => ({
  type: OrderIntentsActionTypes.getOrderIntentsSuccess,
  payload: orderIntents,
});

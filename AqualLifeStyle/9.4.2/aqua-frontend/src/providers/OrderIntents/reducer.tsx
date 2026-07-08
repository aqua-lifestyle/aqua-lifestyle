import type { OrderIntentsAction } from "./actions";
import { OrderIntentsActionTypes } from "./actions";
import type { OrderIntentsState } from "./context";

export const orderIntentsReducer = (
  state: OrderIntentsState,
  action: OrderIntentsAction,
): OrderIntentsState => {
  switch (action.type) {
    case OrderIntentsActionTypes.orderIntentActionError:
      return {
        ...state,
        actionErrorMessage: action.payload,
        isActionError: true,
        isActionPending: false,
        isActionSuccess: false,
      };

    case OrderIntentsActionTypes.orderIntentActionPending:
      return {
        ...state,
        actionErrorMessage: null,
        isActionError: false,
        isActionPending: true,
        isActionSuccess: false,
      };

    case OrderIntentsActionTypes.orderIntentActionSuccess:
      return {
        ...state,
        actionErrorMessage: null,
        isActionError: false,
        isActionPending: false,
        isActionSuccess: true,
        orderIntents: upsertOrderIntent(state.orderIntents, action.payload),
      };

    case OrderIntentsActionTypes.getOrderIntentsError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case OrderIntentsActionTypes.getOrderIntentsPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case OrderIntentsActionTypes.getOrderIntentsSuccess:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
        orderIntents: action.payload,
      };

    default:
      return state;
  }
};

const upsertOrderIntent = (
  orderIntents: OrderIntentsState["orderIntents"],
  orderIntent: OrderIntentsState["orderIntents"][number],
) => {
  const exists = orderIntents.some((item) => item.id === orderIntent.id);

  if (!exists) {
    return [orderIntent, ...orderIntents];
  }

  return orderIntents.map((item) =>
    item.id === orderIntent.id ? orderIntent : item,
  );
};

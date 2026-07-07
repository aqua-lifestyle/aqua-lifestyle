import type { CustomersAction } from "./actions";
import { CustomersActionTypes } from "./actions";
import type { CustomersState } from "./context";

export const customersReducer = (
  state: CustomersState,
  action: CustomersAction,
): CustomersState => {
  switch (action.type) {
    case CustomersActionTypes.createCustomerError:
      return {
        ...state,
        createErrorMessage: action.payload,
        isCreateError: true,
        isCreatePending: false,
        isCreateSuccess: false,
      };

    case CustomersActionTypes.createCustomerPending:
      return {
        ...state,
        createErrorMessage: null,
        isCreateError: false,
        isCreatePending: true,
        isCreateSuccess: false,
      };

    case CustomersActionTypes.createCustomerSuccess:
      return {
        ...state,
        createErrorMessage: null,
        isCreateError: false,
        isCreatePending: false,
        isCreateSuccess: true,
      };

    case CustomersActionTypes.getCustomersError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case CustomersActionTypes.getCustomersPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case CustomersActionTypes.getCustomersSuccess:
      return {
        ...state,
        customers: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    default:
      return state;
  }
};

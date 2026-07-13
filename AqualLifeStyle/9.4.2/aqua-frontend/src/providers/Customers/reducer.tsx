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

    case CustomersActionTypes.getCustomerError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedCustomer: null,
        selectedErrorMessage: action.payload,
      };

    case CustomersActionTypes.getCustomerPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case CustomersActionTypes.getCustomerSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedCustomer: action.payload,
        selectedErrorMessage: null,
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

    case CustomersActionTypes.getMyCustomerError:
      return {
        ...state,
        isMyCustomerError: true,
        isMyCustomerPending: false,
        isMyCustomerSuccess: false,
        myCustomer: null,
        myCustomerErrorMessage: action.payload,
      };

    case CustomersActionTypes.getMyCustomerPending:
      return {
        ...state,
        isMyCustomerError: false,
        isMyCustomerPending: true,
        isMyCustomerSuccess: false,
        myCustomerErrorMessage: null,
      };

    case CustomersActionTypes.getMyCustomerSuccess:
      return {
        ...state,
        isMyCustomerError: false,
        isMyCustomerPending: false,
        isMyCustomerSuccess: true,
        myCustomer: action.payload,
        myCustomerErrorMessage: null,
      };

    case CustomersActionTypes.updateCustomerError:
      return {
        ...state,
        isUpdateError: true,
        isUpdatePending: false,
        isUpdateSuccess: false,
        updateErrorMessage: action.payload,
      };

    case CustomersActionTypes.updateCustomerPending:
      return {
        ...state,
        isUpdateError: false,
        isUpdatePending: true,
        isUpdateSuccess: false,
        updateErrorMessage: null,
      };

    case CustomersActionTypes.updateCustomerSuccess:
      return {
        ...state,
        customers: state.customers.map((customer) =>
          customer.id === action.payload.id ? action.payload : customer,
        ),
        isUpdateError: false,
        isUpdatePending: false,
        isUpdateSuccess: true,
        selectedCustomer: action.payload,
        updateErrorMessage: null,
      };

    default:
      return state;
  }
};

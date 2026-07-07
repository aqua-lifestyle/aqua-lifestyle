import type { EnquiriesAction } from "./actions";
import { EnquiriesActionTypes } from "./actions";
import type { EnquiriesState } from "./context";

export const enquiriesReducer = (
  state: EnquiriesState,
  action: EnquiriesAction,
): EnquiriesState => {
  switch (action.type) {
    case EnquiriesActionTypes.createEnquiryError:
      return {
        ...state,
        createErrorMessage: action.payload,
        isCreateError: true,
        isCreatePending: false,
        isCreateSuccess: false,
      };

    case EnquiriesActionTypes.createEnquiryPending:
      return {
        ...state,
        createErrorMessage: null,
        isCreateError: false,
        isCreatePending: true,
        isCreateSuccess: false,
      };

    case EnquiriesActionTypes.createEnquirySuccess:
      return {
        ...state,
        createErrorMessage: null,
        isCreateError: false,
        isCreatePending: false,
        isCreateSuccess: true,
      };

    case EnquiriesActionTypes.getEnquiriesError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case EnquiriesActionTypes.getEnquiriesPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case EnquiriesActionTypes.getEnquiriesSuccess:
      return {
        ...state,
        enquiries: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    default:
      return state;
  }
};

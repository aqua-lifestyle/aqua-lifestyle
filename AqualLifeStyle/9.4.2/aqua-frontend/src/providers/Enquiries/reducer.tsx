import type { EnquiriesAction } from "./actions";
import { EnquiriesActionTypes } from "./actions";
import type { EnquiriesState } from "./context";

export const enquiriesReducer = (
  state: EnquiriesState,
  action: EnquiriesAction,
): EnquiriesState => {
  switch (action.type) {
    case EnquiriesActionTypes.enquiryActionError:
      return {
        ...state,
        actionErrorMessage: action.payload,
        isActionError: true,
        isActionPending: false,
        isActionSuccess: false,
      };

    case EnquiriesActionTypes.enquiryActionPending:
      return {
        ...state,
        actionErrorMessage: null,
        isActionError: false,
        isActionPending: true,
        isActionSuccess: false,
      };

    case EnquiriesActionTypes.enquiryActionSuccess:
      return {
        ...state,
        actionErrorMessage: null,
        enquiries: state.enquiries.map((enquiry) =>
          enquiry.id === action.payload.id ? action.payload : enquiry,
        ),
        isActionError: false,
        isActionPending: false,
        isActionSuccess: true,
        selectedEnquiry: action.payload,
      };

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

    case EnquiriesActionTypes.getEnquiryError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedEnquiry: null,
        selectedErrorMessage: action.payload,
      };

    case EnquiriesActionTypes.getEnquiryPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case EnquiriesActionTypes.getEnquirySuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedEnquiry: action.payload,
        selectedErrorMessage: null,
      };

    default:
      return state;
  }
};

import type { FacilitatorsState } from "./context";
import { FacilitatorsActionTypes } from "./actions";
import type { FacilitatorsAction } from "./actions";

export const facilitatorsReducer = (
  state: FacilitatorsState,
  action: FacilitatorsAction,
): FacilitatorsState => {
  switch (action.type) {
    case FacilitatorsActionTypes.getFacilitatorError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedFacilitator: null,
        selectedErrorMessage: action.payload,
      };

    case FacilitatorsActionTypes.getFacilitatorPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case FacilitatorsActionTypes.getFacilitatorSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedFacilitator: action.payload,
        selectedErrorMessage: null,
      };

    case FacilitatorsActionTypes.getFacilitatorsByCustomerError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case FacilitatorsActionTypes.getFacilitatorsByCustomerPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case FacilitatorsActionTypes.getFacilitatorsByCustomerSuccess:
      return {
        ...state,
        facilitators: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    case FacilitatorsActionTypes.getFacilitatorsError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case FacilitatorsActionTypes.getFacilitatorsPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case FacilitatorsActionTypes.getFacilitatorsSuccess:
      return {
        ...state,
        facilitators: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    case FacilitatorsActionTypes.registerFacilitatorError:
      return {
        ...state,
        isRegisterError: true,
        isRegisterPending: false,
        isRegisterSuccess: false,
        registerErrorMessage: action.payload,
      };

    case FacilitatorsActionTypes.registerFacilitatorPending:
      return {
        ...state,
        isRegisterError: false,
        isRegisterPending: true,
        isRegisterSuccess: false,
        registerErrorMessage: null,
      };

    case FacilitatorsActionTypes.registerFacilitatorSuccess:
      return {
        ...state,
        isRegisterError: false,
        isRegisterPending: false,
        isRegisterSuccess: true,
        registerErrorMessage: null,
      };

    default:
      return state;
  }
};

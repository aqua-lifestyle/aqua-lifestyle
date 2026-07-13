import type { AreaSpacesState } from "./context";
import { AreaSpacesActionTypes } from "./actions";
import type { AreaSpacesAction } from "./actions";

export const areaSpacesReducer = (
  state: AreaSpacesState,
  action: AreaSpacesAction,
): AreaSpacesState => {
  switch (action.type) {
    case AreaSpacesActionTypes.applyAreaSpaceError:
      return {
        ...state,
        applyErrorMessage: action.payload,
        isApplyError: true,
        isApplyPending: false,
        isApplySuccess: false,
      };

    case AreaSpacesActionTypes.applyAreaSpacePending:
      return {
        ...state,
        applyErrorMessage: null,
        isApplyError: false,
        isApplyPending: true,
        isApplySuccess: false,
      };

    case AreaSpacesActionTypes.applyAreaSpaceSuccess:
      return {
        ...state,
        applyErrorMessage: null,
        isApplyError: false,
        isApplyPending: false,
        isApplySuccess: true,
      };

    case AreaSpacesActionTypes.approveAreaSpaceError:
      return {
        ...state,
        approveErrorMessage: action.payload,
        isApproveError: true,
        isApprovePending: false,
        isApproveSuccess: false,
      };

    case AreaSpacesActionTypes.approveAreaSpacePending:
      return {
        ...state,
        approveErrorMessage: null,
        isApproveError: false,
        isApprovePending: true,
        isApproveSuccess: false,
      };

    case AreaSpacesActionTypes.approveAreaSpaceSuccess:
      return {
        ...state,
        areaSpaces: state.areaSpaces.map((areaSpace) =>
          areaSpace.id === action.payload.id ? action.payload : areaSpace,
        ),
        approveErrorMessage: null,
        isApproveError: false,
        isApprovePending: false,
        isApproveSuccess: true,
        selectedAreaSpace:
          state.selectedAreaSpace?.id === action.payload.id
            ? action.payload
            : state.selectedAreaSpace,
      };

    case AreaSpacesActionTypes.getAreaSpaceError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedAreaSpace: null,
        selectedErrorMessage: action.payload,
      };

    case AreaSpacesActionTypes.getAreaSpacePending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case AreaSpacesActionTypes.getAreaSpaceSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedAreaSpace: action.payload,
        selectedErrorMessage: null,
      };

    case AreaSpacesActionTypes.getAreaSpacesError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case AreaSpacesActionTypes.getAreaSpacesPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case AreaSpacesActionTypes.getAreaSpacesSuccess:
      return {
        ...state,
        areaSpaces: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    case AreaSpacesActionTypes.recordPresentationError:
      return {
        ...state,
        isApproveError: true,
        isApprovePending: false,
        isApproveSuccess: false,
        approveErrorMessage: action.payload,
      };

    case AreaSpacesActionTypes.recordPresentationPending:
      return {
        ...state,
        isApproveError: false,
        isApprovePending: true,
        isApproveSuccess: false,
        approveErrorMessage: null,
      };

    case AreaSpacesActionTypes.recordPresentationSuccess:
      return {
        ...state,
        areaSpaces: state.areaSpaces.map((areaSpace) =>
          areaSpace.id === action.payload.id ? action.payload : areaSpace,
        ),
        isApproveError: false,
        isApprovePending: false,
        isApproveSuccess: true,
        selectedAreaSpace:
          state.selectedAreaSpace?.id === action.payload.id
            ? action.payload
            : state.selectedAreaSpace,
      };

    case AreaSpacesActionTypes.recordStartupOrderError:
      return {
        ...state,
        isApproveError: true,
        isApprovePending: false,
        isApproveSuccess: false,
        approveErrorMessage: action.payload,
      };

    case AreaSpacesActionTypes.recordStartupOrderPending:
      return {
        ...state,
        isApproveError: false,
        isApprovePending: true,
        isApproveSuccess: false,
        approveErrorMessage: null,
      };

    case AreaSpacesActionTypes.recordStartupOrderSuccess:
      return {
        ...state,
        areaSpaces: state.areaSpaces.map((areaSpace) =>
          areaSpace.id === action.payload.id ? action.payload : areaSpace,
        ),
        isApproveError: false,
        isApprovePending: false,
        isApproveSuccess: true,
        selectedAreaSpace:
          state.selectedAreaSpace?.id === action.payload.id
            ? action.payload
            : state.selectedAreaSpace,
      };

    case AreaSpacesActionTypes.startReviewError:
      return {
        ...state,
        isApproveError: true,
        isApprovePending: false,
        isApproveSuccess: false,
        approveErrorMessage: action.payload,
      };

    case AreaSpacesActionTypes.startReviewPending:
      return {
        ...state,
        isApproveError: false,
        isApprovePending: true,
        isApproveSuccess: false,
        approveErrorMessage: null,
      };

    case AreaSpacesActionTypes.startReviewSuccess:
      return {
        ...state,
        areaSpaces: state.areaSpaces.map((areaSpace) =>
          areaSpace.id === action.payload.id ? action.payload : areaSpace,
        ),
        isApproveError: false,
        isApprovePending: false,
        isApproveSuccess: true,
        selectedAreaSpace:
          state.selectedAreaSpace?.id === action.payload.id
            ? action.payload
            : state.selectedAreaSpace,
      };

    case AreaSpacesActionTypes.suspendAreaSpaceError:
      return {
        ...state,
        suspendErrorMessage: action.payload,
        isSuspendError: true,
        isSuspendPending: false,
        isSuspendSuccess: false,
      };

    case AreaSpacesActionTypes.suspendAreaSpacePending:
      return {
        ...state,
        suspendErrorMessage: null,
        isSuspendError: false,
        isSuspendPending: true,
        isSuspendSuccess: false,
      };

    case AreaSpacesActionTypes.suspendAreaSpaceSuccess:
      return {
        ...state,
        areaSpaces: state.areaSpaces.map((areaSpace) =>
          areaSpace.id === action.payload.id ? action.payload : areaSpace,
        ),
        isSuspendError: false,
        isSuspendPending: false,
        isSuspendSuccess: true,
        selectedAreaSpace:
          state.selectedAreaSpace?.id === action.payload.id
            ? action.payload
            : state.selectedAreaSpace,
      };

    default:
      return state;
  }
};

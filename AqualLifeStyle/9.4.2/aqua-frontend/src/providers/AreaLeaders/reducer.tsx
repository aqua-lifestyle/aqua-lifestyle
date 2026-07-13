import type { AreaLeadersAction } from "./actions";
import { AreaLeadersActionTypes } from "./actions";
import type { AreaLeadersState } from "./context";

export const areaLeadersReducer = (
  state: AreaLeadersState,
  action: AreaLeadersAction,
): AreaLeadersState => {
  switch (action.type) {
    case AreaLeadersActionTypes.applyAreaLeaderError:
      return {
        ...state,
        applyErrorMessage: action.payload,
        isApplyError: true,
        isApplyPending: false,
        isApplySuccess: false,
      };

    case AreaLeadersActionTypes.applyAreaLeaderPending:
      return {
        ...state,
        applyErrorMessage: null,
        isApplyError: false,
        isApplyPending: true,
        isApplySuccess: false,
      };

    case AreaLeadersActionTypes.applyAreaLeaderSuccess:
      return {
        ...state,
        applyErrorMessage: null,
        isApplyError: false,
        isApplyPending: false,
        isApplySuccess: true,
      };

    case AreaLeadersActionTypes.getAreaLeaderError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedAreaLeader: null,
        selectedErrorMessage: action.payload,
      };

    case AreaLeadersActionTypes.getAreaLeaderPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case AreaLeadersActionTypes.getAreaLeaderSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedAreaLeader: action.payload,
        selectedErrorMessage: null,
      };

    case AreaLeadersActionTypes.getAreaLeadersError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case AreaLeadersActionTypes.getAreaLeadersPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case AreaLeadersActionTypes.getAreaLeadersSuccess:
      return {
        ...state,
        areaLeaders: action.payload,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
      };

    case AreaLeadersActionTypes.promoteAreaLeaderError:
      return {
        ...state,
        isPromoteError: true,
        isPromotePending: false,
        isPromoteSuccess: false,
        promoteErrorMessage: action.payload,
      };

    case AreaLeadersActionTypes.promoteAreaLeaderPending:
      return {
        ...state,
        isPromoteError: false,
        isPromotePending: true,
        isPromoteSuccess: false,
        promoteErrorMessage: null,
      };

    case AreaLeadersActionTypes.promoteAreaLeaderSuccess:
      return {
        ...state,
        areaLeaders: state.areaLeaders.map((areaLeader) =>
          areaLeader.id === action.payload.id ? action.payload : areaLeader,
        ),
        isPromoteError: false,
        isPromotePending: false,
        isPromoteSuccess: true,
        promoteErrorMessage: null,
        selectedAreaLeader:
          state.selectedAreaLeader?.id === action.payload.id
            ? action.payload
            : state.selectedAreaLeader,
      };

    case AreaLeadersActionTypes.recordStartupOrderError:
      return {
        ...state,
        isRecordStartupOrderError: true,
        isRecordStartupOrderPending: false,
        isRecordStartupOrderSuccess: false,
        recordStartupOrderErrorMessage: action.payload,
      };

    case AreaLeadersActionTypes.recordStartupOrderPending:
      return {
        ...state,
        isRecordStartupOrderError: false,
        isRecordStartupOrderPending: true,
        isRecordStartupOrderSuccess: false,
        recordStartupOrderErrorMessage: null,
      };

    case AreaLeadersActionTypes.recordStartupOrderSuccess:
      return {
        ...state,
        isRecordStartupOrderError: false,
        isRecordStartupOrderPending: false,
        isRecordStartupOrderSuccess: true,
        recordStartupOrderErrorMessage: null,
      };

    default:
      return state;
  }
};

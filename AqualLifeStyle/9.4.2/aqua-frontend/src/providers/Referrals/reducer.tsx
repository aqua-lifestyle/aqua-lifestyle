import type { ReferralsState } from "./context";
import { ReferralsActionTypes } from "./actions";
import type { ReferralsAction } from "./actions";

export const referralsReducer = (
  state: ReferralsState,
  action: ReferralsAction,
): ReferralsState => {
  switch (action.type) {
    case ReferralsActionTypes.confirmAwardError:
      return {
        ...state,
        confirmErrorMessage: action.payload,
        isConfirmError: true,
        isConfirmPending: false,
        isConfirmSuccess: false,
      };

    case ReferralsActionTypes.confirmAwardPending:
      return {
        ...state,
        confirmErrorMessage: null,
        isConfirmError: false,
        isConfirmPending: true,
        isConfirmSuccess: false,
      };

    case ReferralsActionTypes.confirmAwardSuccess:
      return {
        ...state,
        referrals: state.referrals.map((referral) =>
          referral.id === action.payload.id ? action.payload : referral,
        ),
        confirmErrorMessage: null,
        isConfirmError: false,
        isConfirmPending: false,
        isConfirmSuccess: true,
        selectedReferral:
          state.selectedReferral?.id === action.payload.id
            ? action.payload
            : state.selectedReferral,
      };

    case ReferralsActionTypes.getReferralError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedReferral: null,
        selectedErrorMessage: action.payload,
      };

    case ReferralsActionTypes.getReferralPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
      };

    case ReferralsActionTypes.getReferralSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedReferral: action.payload,
        selectedErrorMessage: null,
      };

    case ReferralsActionTypes.getReferralsByEnquiryError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case ReferralsActionTypes.getReferralsByEnquiryPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case ReferralsActionTypes.getReferralsByEnquirySuccess:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
        referrals: action.payload,
      };

    case ReferralsActionTypes.getReferralsError:
      return {
        ...state,
        isLoadError: true,
        isLoadPending: false,
        isLoadSuccess: false,
        loadErrorMessage: action.payload,
      };

    case ReferralsActionTypes.getReferralsPending:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: true,
        isLoadSuccess: false,
        loadErrorMessage: null,
      };

    case ReferralsActionTypes.getReferralsSuccess:
      return {
        ...state,
        isLoadError: false,
        isLoadPending: false,
        isLoadSuccess: true,
        loadErrorMessage: null,
        referrals: action.payload,
      };

    default:
      return state;
  }
};

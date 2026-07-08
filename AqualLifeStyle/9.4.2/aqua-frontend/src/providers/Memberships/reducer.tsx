import type { MembershipsAction } from "./actions";
import { MembershipsActionTypes } from "./actions";
import type { MembershipsState } from "./context";

export const membershipsReducer = (
  state: MembershipsState,
  action: MembershipsAction,
): MembershipsState => {
  switch (action.type) {
    case MembershipsActionTypes.getMembershipError:
      return {
        ...state,
        isSelectedError: true,
        isSelectedPending: false,
        isSelectedSuccess: false,
        selectedErrorMessage: action.payload,
        selectedMembership: null,
      };

    case MembershipsActionTypes.getMembershipPending:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: true,
        isSelectedSuccess: false,
        selectedErrorMessage: null,
        tierBenefits: null,
      };

    case MembershipsActionTypes.getMembershipSuccess:
      return {
        ...state,
        isSelectedError: false,
        isSelectedPending: false,
        isSelectedSuccess: true,
        selectedErrorMessage: null,
        selectedMembership: action.payload,
      };

    case MembershipsActionTypes.getMembershipsError:
      return {
        ...state,
        errorMessage: action.payload,
        isError: true,
        isPending: false,
        isSuccess: false,
      };

    case MembershipsActionTypes.getMembershipsPending:
      return {
        ...state,
        errorMessage: null,
        isError: false,
        isPending: true,
        isSuccess: false,
      };

    case MembershipsActionTypes.getMembershipsSuccess:
      return {
        ...state,
        errorMessage: null,
        isError: false,
        isPending: false,
        isSuccess: true,
        memberships: action.payload,
      };

    case MembershipsActionTypes.getTierBenefitsError:
      return {
        ...state,
        isTierBenefitsError: true,
        isTierBenefitsPending: false,
        isTierBenefitsSuccess: false,
        tierBenefits: null,
        tierBenefitsErrorMessage: action.payload,
      };

    case MembershipsActionTypes.getTierBenefitsPending:
      return {
        ...state,
        isTierBenefitsError: false,
        isTierBenefitsPending: true,
        isTierBenefitsSuccess: false,
        tierBenefitsErrorMessage: null,
      };

    case MembershipsActionTypes.getTierBenefitsSuccess:
      return {
        ...state,
        isTierBenefitsError: false,
        isTierBenefitsPending: false,
        isTierBenefitsSuccess: true,
        tierBenefits: action.payload,
        tierBenefitsErrorMessage: null,
      };

    default:
      return state;
  }
};

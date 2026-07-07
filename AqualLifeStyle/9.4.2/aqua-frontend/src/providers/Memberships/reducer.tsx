import type { MembershipsAction } from "./actions";
import { MembershipsActionTypes } from "./actions";
import type { MembershipsState } from "./context";

export const membershipsReducer = (
  state: MembershipsState,
  action: MembershipsAction,
): MembershipsState => {
  switch (action.type) {
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

    default:
      return state;
  }
};

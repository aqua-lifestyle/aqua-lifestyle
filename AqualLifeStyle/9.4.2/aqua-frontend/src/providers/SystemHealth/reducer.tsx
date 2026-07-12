import type { SystemHealthAction } from "./actions";
import { SystemHealthActionTypes } from "./actions";
import type { SystemHealthState } from "./context";

export const systemHealthReducer = (
  state: SystemHealthState,
  action: SystemHealthAction,
): SystemHealthState => {
  switch (action.type) {
    case SystemHealthActionTypes.checkHealthError:
      return {
        ...state,
        errorMessage: action.payload,
        isError: true,
        isPending: false,
        isSuccess: false,
      };

    case SystemHealthActionTypes.checkHealthPending:
      return {
        ...state,
        errorMessage: null,
        isError: false,
        isPending: true,
        isSuccess: false,
      };

    case SystemHealthActionTypes.checkHealthSuccess:
      return {
        ...state,
        errorMessage: null,
        health: action.payload,
        isError: false,
        isPending: false,
        isSuccess: true,
      };

    default:
      return state;
  }
};

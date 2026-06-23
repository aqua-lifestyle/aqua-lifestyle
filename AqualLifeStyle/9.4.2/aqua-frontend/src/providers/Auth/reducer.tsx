import type { AuthAction } from "./actions";
import { AuthActionTypes } from "./actions";
import type { AuthState } from "./context";

export const authReducer = (
  state: AuthState,
  action: AuthAction,
): AuthState => {
  switch (action.type) {
    case AuthActionTypes.clearSession:
      return {
        ...state,
        isAuthenticated: false,
        session: null,
      };

    case AuthActionTypes.setReady:
      return {
        ...state,
        isReady: action.payload,
      };

    case AuthActionTypes.setSession:
      return {
        ...state,
        isAuthenticated: true,
        isReady: true,
        session: action.payload,
      };

    default:
      return state;
  }
};

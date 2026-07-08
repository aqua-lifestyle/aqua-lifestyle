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

    case AuthActionTypes.setSession:
      return {
        ...state,
        isAuthenticated: true,
        session: action.payload,
      };

    default:
      return state;
  }
};

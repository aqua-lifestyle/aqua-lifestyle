import type { AuthAction } from "./actions";
import { AuthActionTypes } from "./actions";
import type { AuthState } from "./context";

export const authReducer = (
  state: AuthState,
  action: AuthAction,
): AuthState => {
  switch (action.type) {
    case AuthActionTypes.sessionError:
      return {
        ...state,
        isAuthenticated: false,
        isReady: true,
        session: null,
        status: "error",
      };

    case AuthActionTypes.clearSession:
      return {
        ...state,
        isAuthenticated: false,
        isReady: true,
        session: null,
        status: "anonymous",
      };

    case AuthActionTypes.setReady:
      return {
        ...state,
        isReady: action.payload,
        status: action.payload ? state.status : "bootstrapping",
      };

    case AuthActionTypes.setSession:
      return action.payload.user
        ? {
            ...state,
            isAuthenticated: true,
            isReady: true,
            session: action.payload,
            status: "authenticated",
          }
        : {
            ...state,
            isAuthenticated: false,
            isReady: true,
            session: null,
            status: "anonymous",
          };

    default:
      return state;
  }
};

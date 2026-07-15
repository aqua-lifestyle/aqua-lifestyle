import type { AuthSession } from "./context";

export const AuthActionTypes = {
  clearSession: "auth/clearSession",
  setReady: "auth/setReady",
  setSession: "auth/setSession",
} as const;

export type AuthAction =
  | {
      type: typeof AuthActionTypes.clearSession;
    }
  | {
      type: typeof AuthActionTypes.setReady;
      payload: boolean;
    }
  | {
      type: typeof AuthActionTypes.setSession;
      payload: AuthSession;
    };

export const clearAuthSession = (): AuthAction => ({
  type: AuthActionTypes.clearSession,
});

export const setReady = (ready: boolean): AuthAction => ({
  type: AuthActionTypes.setReady,
  payload: ready,
});

export const setAuthSession = (session: AuthSession): AuthAction => ({
  type: AuthActionTypes.setSession,
  payload: session,
});

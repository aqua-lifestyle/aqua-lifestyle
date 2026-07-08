"use client";

import {
  type ReactNode,
  useContext,
  useEffect,
  useMemo,
  useReducer,
} from "react";

import { setAccessTokenProvider } from "@/src/shared/api";
import { clearAuthSession, setAuthSession } from "./actions";
import {
  AuthActionsContext,
  AuthStateContext,
  type AuthSession,
  initialAuthState,
} from "./context";
import { authReducer } from "./reducer";

type AuthProviderProps = {
  children: ReactNode;
};

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [state, dispatch] = useReducer(authReducer, initialAuthState);

  useEffect(() => {
    setAccessTokenProvider(() => state.session?.accessToken ?? null);
  }, [state.session?.accessToken]);

  const actions = useMemo(
    () => ({
      clearSession: () => dispatch(clearAuthSession()),
      setSession: (session: AuthSession) => dispatch(setAuthSession(session)),
    }),
    [],
  );

  return (
    <AuthStateContext.Provider value={state}>
      <AuthActionsContext.Provider value={actions}>
        {children}
      </AuthActionsContext.Provider>
    </AuthStateContext.Provider>
  );
};

export const useAuthState = () => useContext(AuthStateContext);

export const useAuthActions = () => {
  const context = useContext(AuthActionsContext);

  if (!context) {
    throw new Error("useAuthActions must be used within AuthProvider.");
  }

  return context;
};

export type { AuthSession, AuthState, AuthUser } from "./context";

"use client";

import {
  type ReactNode,
  useContext,
  useEffect,
  useMemo,
  useReducer,
  useRef,
} from "react";

import { setRefreshTokenProvider } from "@/src/shared/api";
import { authSessionError, clearAuthSession, setAuthSession, setReady } from "./actions";
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
  const sessionMutation = useRef(0);

  useEffect(() => {
    let active = true;
    const initialMutation = sessionMutation.current;
    void fetch("/api/auth/session", { cache: "no-store" })
      .then(async (response) => {
        if (!active || sessionMutation.current !== initialMutation) return;
        if (!response.ok) {
          dispatch(authSessionError());
          return;
        }
        const session = (await response.json()) as AuthSession | null;
        dispatch(session ? setAuthSession(session) : clearAuthSession());
      })
      .catch(() => {
        if (active && sessionMutation.current === initialMutation) dispatch(authSessionError());
      });
    return () => { active = false; };
  }, []);

  useEffect(() => {
    setRefreshTokenProvider(async () => null);
  }, []);

  const actions = useMemo(
    () => ({
      clearSession: async () => {
        sessionMutation.current += 1;
        try {
          await fetch("/api/auth/logout", { method: "POST" });
        } finally {
          dispatch(clearAuthSession());
        }
      },
      setReady: (ready: boolean) => dispatch(setReady(ready)),
      setSession: (session: AuthSession) => {
        sessionMutation.current += 1;
        dispatch(setAuthSession(session));
      },
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

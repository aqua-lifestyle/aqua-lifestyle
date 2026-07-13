"use client";

import {
  type ReactNode,
  useContext,
  useEffect,
  useMemo,
  useReducer,
} from "react";

import {
  setAccessTokenProvider,
  setRefreshTokenProvider,
} from "@/src/shared/api";
import { refreshToken as refreshTokenApi } from "@/src/shared/api/auth-service";
import { clearAuthSession, setAuthSession, setReady } from "./actions";
import {
  AuthActionsContext,
  AuthStateContext,
  type AuthSession,
  initialAuthState,
} from "./context";
import { authReducer } from "./reducer";

const STORAGE_KEY = "aqua.authSession";

type AuthProviderProps = {
  children: ReactNode;
};

export const AuthProvider = ({ children }: AuthProviderProps) => {
  const [state, dispatch] = useReducer(authReducer, initialAuthState);

  // Restore session from localStorage on mount
  useEffect(() => {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const session = JSON.parse(stored) as AuthSession;
        if (
          session.expiresAt &&
          new Date(session.expiresAt) > new Date()
        ) {
          dispatch(setAuthSession(session));
        } else {
          localStorage.removeItem(STORAGE_KEY);
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    } finally {
      dispatch(setReady(true));
    }
  }, []);

  // Persist session to localStorage whenever it changes
  useEffect(() => {
    if (state.session) {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(state.session));
    } else {
      localStorage.removeItem(STORAGE_KEY);
    }
  }, [state.session]);

  // Provide the access token to the axios client
  useEffect(() => {
    setAccessTokenProvider(() => state.session?.accessToken ?? null);
  }, [state.session?.accessToken]);

  // Provide token refresh capability to the axios 401 interceptor
  useEffect(() => {
    setRefreshTokenProvider(async () => {
      if (!state.session?.refreshToken) return null;
      const result = await refreshTokenApi(state.session.refreshToken);
      if (result.ok) {
        dispatch(setAuthSession(result.session));
        return result.session.accessToken;
      }
      dispatch(clearAuthSession());
      return null;
    });
  }, [state.session?.refreshToken]);

  const actions = useMemo(
    () => ({
      clearSession: () => {
        localStorage.removeItem(STORAGE_KEY);
        dispatch(clearAuthSession());
      },
      setReady: (ready: boolean) => dispatch(setReady(ready)),
      setSession: (session: AuthSession) => {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
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

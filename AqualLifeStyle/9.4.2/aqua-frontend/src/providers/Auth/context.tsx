import { createContext } from "react";

export type AuthUser = {
  email: string | null;
  id: string;
  name: string | null;
};

export type AuthSession = {
  accessToken: string;
  expiresAt: string | null;
  user: AuthUser | null;
};

export type AuthState = {
  isAuthenticated: boolean;
  isReady: boolean;
  session: AuthSession | null;
};

export type AuthActions = {
  clearSession: () => void;
  setSession: (session: AuthSession) => void;
};

export const initialAuthState: AuthState = {
  isAuthenticated: false,
  isReady: true,
  session: null,
};

export const AuthStateContext = createContext<AuthState>(initialAuthState);

export const AuthActionsContext = createContext<AuthActions | null>(null);

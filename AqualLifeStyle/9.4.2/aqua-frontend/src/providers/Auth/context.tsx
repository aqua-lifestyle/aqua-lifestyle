import { createContext } from "react";

export type AuthUser = {
  email: string | null;
  id: number;
  name: string | null;
  permissions: string[];
  role: string;
  tenantId?: number | null;
};

export type AuthSession = {
  accessToken: string;
  expiresAt: string | null;
  refreshToken?: string | null;
  user: AuthUser | null;
};

export type AuthState = {
  isAuthenticated: boolean;
  isReady: boolean;
  session: AuthSession | null;
};

export type AuthActions = {
  clearSession: () => void;
  setReady: (ready: boolean) => void;
  setSession: (session: AuthSession) => void;
};

export const initialAuthState: AuthState = {
  isAuthenticated: false,
  isReady: false,
  session: null,
};

export const AuthStateContext = createContext<AuthState>(initialAuthState);

export const AuthActionsContext = createContext<AuthActions | null>(null);

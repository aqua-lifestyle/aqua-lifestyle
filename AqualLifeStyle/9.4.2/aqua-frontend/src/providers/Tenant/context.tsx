import { createContext } from "react";

export type TenantState = {
  currentTenant: string | null;
  isHost: boolean;
};

export type TenantActions = {
  clearTenant: () => void;
  setTenant: (tenant: string) => void;
};

export const initialTenantState: TenantState = {
  currentTenant: null,
  isHost: true,
};

export const TenantStateContext =
  createContext<TenantState>(initialTenantState);

export const TenantActionsContext = createContext<TenantActions | null>(null);

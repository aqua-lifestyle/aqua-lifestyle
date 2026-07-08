"use client";

import {
  type ReactNode,
  useContext,
  useEffect,
  useMemo,
  useReducer,
} from "react";

import { setTenantProvider } from "@/src/shared/api";
import { clearTenant, setTenant } from "./actions";
import {
  TenantActionsContext,
  TenantStateContext,
  initialTenantState,
} from "./context";
import { tenantReducer } from "./reducer";

type TenantProviderProps = {
  children: ReactNode;
};

export const TenantProvider = ({ children }: TenantProviderProps) => {
  const [state, dispatch] = useReducer(tenantReducer, initialTenantState);

  useEffect(() => {
    setTenantProvider(() => state.currentTenant);
  }, [state.currentTenant]);

  const actions = useMemo(
    () => ({
      clearTenant: () => dispatch(clearTenant()),
      setTenant: (tenant: string) => {
        const trimmedTenant = tenant.trim();

        if (trimmedTenant.length === 0) {
          dispatch(clearTenant());
          return;
        }

        dispatch(setTenant(trimmedTenant));
      },
    }),
    [],
  );

  return (
    <TenantStateContext.Provider value={state}>
      <TenantActionsContext.Provider value={actions}>
        {children}
      </TenantActionsContext.Provider>
    </TenantStateContext.Provider>
  );
};

export const useTenantState = () => useContext(TenantStateContext);

export const useTenantActions = () => {
  const context = useContext(TenantActionsContext);

  if (!context) {
    throw new Error("useTenantActions must be used within TenantProvider.");
  }

  return context;
};

export type { TenantState } from "./context";

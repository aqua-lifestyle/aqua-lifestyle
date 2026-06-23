"use client";

import {
  type ReactNode,
  useContext,
  useEffect,
  useMemo,
  useReducer,
} from "react";
import { z } from "zod";

import { setTenantProvider } from "@/src/shared/api";
import { clearTenant, setTenant } from "./actions";
import {
  TenantActionsContext,
  TenantStateContext,
  initialTenantState,
} from "./context";
import { tenantReducer } from "./reducer";

const TENANT_STORAGE_KEY = "aqua.currentTenant";

const tenantStorageSchema = z
  .string()
  .trim()
  .max(64, "Tenant name must be 64 characters or fewer.")
  .regex(
    /^[a-zA-Z0-9][a-zA-Z0-9._-]*$/,
    "Use letters, numbers, dots, underscores, or hyphens.",
  );

const initTenantState = (state: typeof initialTenantState) => {
  if (typeof window === "undefined") {
    return state;
  }

  const stored = window.localStorage.getItem(TENANT_STORAGE_KEY);
  const parsed = tenantStorageSchema.safeParse(stored);

  if (parsed.success) {
    return { ...state, currentTenant: parsed.data, isHost: false };
  }

  if (stored) {
    window.localStorage.removeItem(TENANT_STORAGE_KEY);
  }

  return state;
};

type TenantProviderProps = {
  children: ReactNode;
};

export const TenantProvider = ({ children }: TenantProviderProps) => {
  const [state, dispatch] = useReducer(
    tenantReducer,
    initialTenantState,
    initTenantState,
  );

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

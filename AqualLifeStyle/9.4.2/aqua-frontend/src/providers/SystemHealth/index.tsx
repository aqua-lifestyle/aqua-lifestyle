"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { AbpHttpError, apiEndpoints, httpClient } from "@/src/shared/api";
import {
  checkHealthError,
  checkHealthPending,
  checkHealthSuccess,
} from "./actions";
import {
  type SystemHealth,
  SystemHealthActionsContext,
  SystemHealthStateContext,
  initialSystemHealthState,
} from "./context";
import { systemHealthReducer } from "./reducer";

type SystemHealthProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to reach the backend health endpoint.";
};

export const SystemHealthProvider = ({
  children,
}: SystemHealthProviderProps) => {
  const [state, dispatch] = useReducer(
    systemHealthReducer,
    initialSystemHealthState,
  );

  const checkHealth = useCallback(async () => {
    dispatch(checkHealthPending());

    try {
      const health = await httpClient.get<SystemHealth>(apiEndpoints.health.get);
      dispatch(checkHealthSuccess(health));
    } catch (error) {
      dispatch(checkHealthError(getErrorMessage(error)));
    }
  }, []);

  const actions = useMemo(
    () => ({
      checkHealth,
    }),
    [checkHealth],
  );

  return (
    <SystemHealthStateContext.Provider value={state}>
      <SystemHealthActionsContext.Provider value={actions}>
        {children}
      </SystemHealthActionsContext.Provider>
    </SystemHealthStateContext.Provider>
  );
};

export const useSystemHealthState = () => {
  return useContext(SystemHealthStateContext);
};

export const useSystemHealthActions = () => {
  const context = useContext(SystemHealthActionsContext);

  if (!context) {
    throw new Error(
      "useSystemHealthActions must be used within a SystemHealthProvider.",
    );
  }

  return context;
};

export type { SystemHealth } from "./context";

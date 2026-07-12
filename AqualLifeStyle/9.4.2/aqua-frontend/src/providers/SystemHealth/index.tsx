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
  SystemHealthActionsContext,
  SystemHealthStateContext,
  initialSystemHealthState,
} from "./context";
import { isSystemHealthContractError, parseSystemHealth } from "./contract";
import { systemHealthReducer } from "./reducer";

type SystemHealthProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (isSystemHealthContractError(error)) {
    return "Backend health response did not match the expected contract.";
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
      const response = await httpClient.get<unknown>(apiEndpoints.health.get);
      const health = parseSystemHealth(response);
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

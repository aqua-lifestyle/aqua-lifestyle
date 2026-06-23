import { createContext } from "react";

import type { SystemHealth } from "./contract";

export type SystemHealthState = {
  errorMessage: string | null;
  health: SystemHealth | null;
  isError: boolean;
  isPending: boolean;
  isSuccess: boolean;
};

export type SystemHealthActions = {
  checkHealth: () => Promise<void>;
};

export const initialSystemHealthState: SystemHealthState = {
  errorMessage: null,
  health: null,
  isError: false,
  isPending: false,
  isSuccess: false,
};

export const SystemHealthStateContext =
  createContext<SystemHealthState>(initialSystemHealthState);

export const SystemHealthActionsContext =
  createContext<SystemHealthActions | null>(null);

export type { SystemHealth } from "./contract";

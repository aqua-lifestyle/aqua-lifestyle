import type { SystemHealth } from "./context";

export const SystemHealthActionTypes = {
  checkHealthError: "systemHealth/checkHealthError",
  checkHealthPending: "systemHealth/checkHealthPending",
  checkHealthSuccess: "systemHealth/checkHealthSuccess",
} as const;

export type SystemHealthAction =
  | {
      type: typeof SystemHealthActionTypes.checkHealthError;
      payload: string;
    }
  | {
      type: typeof SystemHealthActionTypes.checkHealthPending;
    }
  | {
      type: typeof SystemHealthActionTypes.checkHealthSuccess;
      payload: SystemHealth;
    };

export const checkHealthError = (message: string): SystemHealthAction => ({
  type: SystemHealthActionTypes.checkHealthError,
  payload: message,
});

export const checkHealthPending = (): SystemHealthAction => ({
  type: SystemHealthActionTypes.checkHealthPending,
});

export const checkHealthSuccess = (
  health: SystemHealth,
): SystemHealthAction => ({
  type: SystemHealthActionTypes.checkHealthSuccess,
  payload: health,
});

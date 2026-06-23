import type { AreaLeader } from "./context";

export const AreaLeadersActionTypes = {
  applyAreaLeaderError: "areaLeaders/applyAreaLeaderError",
  applyAreaLeaderPending: "areaLeaders/applyAreaLeaderPending",
  applyAreaLeaderSuccess: "areaLeaders/applyAreaLeaderSuccess",
  getAreaLeaderError: "areaLeaders/getAreaLeaderError",
  getAreaLeaderPending: "areaLeaders/getAreaLeaderPending",
  getAreaLeaderSuccess: "areaLeaders/getAreaLeaderSuccess",
  getAreaLeadersError: "areaLeaders/getAreaLeadersError",
  getAreaLeadersPending: "areaLeaders/getAreaLeadersPending",
  getAreaLeadersSuccess: "areaLeaders/getAreaLeadersSuccess",
  promoteAreaLeaderError: "areaLeaders/promoteAreaLeaderError",
  promoteAreaLeaderPending: "areaLeaders/promoteAreaLeaderPending",
  promoteAreaLeaderSuccess: "areaLeaders/promoteAreaLeaderSuccess",
  recordStartupOrderError: "areaLeaders/recordStartupOrderError",
  recordStartupOrderPending: "areaLeaders/recordStartupOrderPending",
  recordStartupOrderSuccess: "areaLeaders/recordStartupOrderSuccess",
} as const;

export type AreaLeadersAction =
  | { type: typeof AreaLeadersActionTypes.applyAreaLeaderError; payload: string }
  | { type: typeof AreaLeadersActionTypes.applyAreaLeaderPending }
  | { type: typeof AreaLeadersActionTypes.applyAreaLeaderSuccess }
  | { type: typeof AreaLeadersActionTypes.getAreaLeaderError; payload: string }
  | { type: typeof AreaLeadersActionTypes.getAreaLeaderPending }
  | { type: typeof AreaLeadersActionTypes.getAreaLeaderSuccess; payload: AreaLeader }
  | { type: typeof AreaLeadersActionTypes.getAreaLeadersError; payload: string }
  | { type: typeof AreaLeadersActionTypes.getAreaLeadersPending }
  | { type: typeof AreaLeadersActionTypes.getAreaLeadersSuccess; payload: AreaLeader[] }
  | { type: typeof AreaLeadersActionTypes.promoteAreaLeaderError; payload: string }
  | { type: typeof AreaLeadersActionTypes.promoteAreaLeaderPending }
  | { type: typeof AreaLeadersActionTypes.promoteAreaLeaderSuccess; payload: AreaLeader }
  | { type: typeof AreaLeadersActionTypes.recordStartupOrderError; payload: string }
  | { type: typeof AreaLeadersActionTypes.recordStartupOrderPending }
  | { type: typeof AreaLeadersActionTypes.recordStartupOrderSuccess };

export const applyAreaLeaderError = (message: string): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.applyAreaLeaderError,
  payload: message,
});

export const applyAreaLeaderPending = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.applyAreaLeaderPending,
});

export const applyAreaLeaderSuccess = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.applyAreaLeaderSuccess,
});

export const getAreaLeaderError = (message: string): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeaderError,
  payload: message,
});

export const getAreaLeaderPending = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeaderPending,
});

export const getAreaLeaderSuccess = (areaLeader: AreaLeader): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeaderSuccess,
  payload: areaLeader,
});

export const getAreaLeadersError = (message: string): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeadersError,
  payload: message,
});

export const getAreaLeadersPending = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeadersPending,
});

export const getAreaLeadersSuccess = (areaLeaders: AreaLeader[]): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.getAreaLeadersSuccess,
  payload: areaLeaders,
});

export const promoteAreaLeaderError = (message: string): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.promoteAreaLeaderError,
  payload: message,
});

export const promoteAreaLeaderPending = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.promoteAreaLeaderPending,
});

export const promoteAreaLeaderSuccess = (areaLeader: AreaLeader): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.promoteAreaLeaderSuccess,
  payload: areaLeader,
});

export const recordStartupOrderError = (message: string): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.recordStartupOrderError,
  payload: message,
});

export const recordStartupOrderPending = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.recordStartupOrderPending,
});

export const recordStartupOrderSuccess = (): AreaLeadersAction => ({
  type: AreaLeadersActionTypes.recordStartupOrderSuccess,
});

import type { AreaSpace } from "./context";

export const AreaSpacesActionTypes = {
  applyAreaSpaceError: "areaSpaces/applyAreaSpaceError",
  applyAreaSpacePending: "areaSpaces/applyAreaSpacePending",
  applyAreaSpaceSuccess: "areaSpaces/applyAreaSpaceSuccess",
  approveAreaSpaceError: "areaSpaces/approveAreaSpaceError",
  approveAreaSpacePending: "areaSpaces/approveAreaSpacePending",
  approveAreaSpaceSuccess: "areaSpaces/approveAreaSpaceSuccess",
  getAreaSpaceError: "areaSpaces/getAreaSpaceError",
  getAreaSpacePending: "areaSpaces/getAreaSpacePending",
  getAreaSpaceSuccess: "areaSpaces/getAreaSpaceSuccess",
  getAreaSpacesError: "areaSpaces/getAreaSpacesError",
  getAreaSpacesPending: "areaSpaces/getAreaSpacesPending",
  getAreaSpacesSuccess: "areaSpaces/getAreaSpacesSuccess",
  recordPresentationError: "areaSpaces/recordPresentationError",
  recordPresentationPending: "areaSpaces/recordPresentationPending",
  recordPresentationSuccess: "areaSpaces/recordPresentationSuccess",
  recordStartupOrderError: "areaSpaces/recordStartupOrderError",
  recordStartupOrderPending: "areaSpaces/recordStartupOrderPending",
  recordStartupOrderSuccess: "areaSpaces/recordStartupOrderSuccess",
  startReviewError: "areaSpaces/startReviewError",
  startReviewPending: "areaSpaces/startReviewPending",
  startReviewSuccess: "areaSpaces/startReviewSuccess",
  suspendAreaSpaceError: "areaSpaces/suspendAreaSpaceError",
  suspendAreaSpacePending: "areaSpaces/suspendAreaSpacePending",
  suspendAreaSpaceSuccess: "areaSpaces/suspendAreaSpaceSuccess",
} as const;

export type AreaSpacesAction =
  | { type: typeof AreaSpacesActionTypes.applyAreaSpaceError; payload: string }
  | { type: typeof AreaSpacesActionTypes.applyAreaSpacePending }
  | { type: typeof AreaSpacesActionTypes.applyAreaSpaceSuccess }
  | { type: typeof AreaSpacesActionTypes.approveAreaSpaceError; payload: string }
  | { type: typeof AreaSpacesActionTypes.approveAreaSpacePending }
  | { type: typeof AreaSpacesActionTypes.approveAreaSpaceSuccess; payload: AreaSpace }
  | { type: typeof AreaSpacesActionTypes.getAreaSpaceError; payload: string }
  | { type: typeof AreaSpacesActionTypes.getAreaSpacePending }
  | { type: typeof AreaSpacesActionTypes.getAreaSpaceSuccess; payload: AreaSpace }
  | { type: typeof AreaSpacesActionTypes.getAreaSpacesError; payload: string }
  | { type: typeof AreaSpacesActionTypes.getAreaSpacesPending }
  | { type: typeof AreaSpacesActionTypes.getAreaSpacesSuccess; payload: AreaSpace[] }
  | { type: typeof AreaSpacesActionTypes.recordPresentationError; payload: string }
  | { type: typeof AreaSpacesActionTypes.recordPresentationPending }
  | { type: typeof AreaSpacesActionTypes.recordPresentationSuccess; payload: AreaSpace }
  | { type: typeof AreaSpacesActionTypes.recordStartupOrderError; payload: string }
  | { type: typeof AreaSpacesActionTypes.recordStartupOrderPending }
  | { type: typeof AreaSpacesActionTypes.recordStartupOrderSuccess; payload: AreaSpace }
  | { type: typeof AreaSpacesActionTypes.startReviewError; payload: string }
  | { type: typeof AreaSpacesActionTypes.startReviewPending }
  | { type: typeof AreaSpacesActionTypes.startReviewSuccess; payload: AreaSpace }
  | { type: typeof AreaSpacesActionTypes.suspendAreaSpaceError; payload: string }
  | { type: typeof AreaSpacesActionTypes.suspendAreaSpacePending }
  | { type: typeof AreaSpacesActionTypes.suspendAreaSpaceSuccess; payload: AreaSpace };

export const applyAreaSpaceError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.applyAreaSpaceError,
  payload: message,
});

export const applyAreaSpacePending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.applyAreaSpacePending,
});

export const applyAreaSpaceSuccess = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.applyAreaSpaceSuccess,
});

export const approveAreaSpaceError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.approveAreaSpaceError,
  payload: message,
});

export const approveAreaSpacePending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.approveAreaSpacePending,
});

export const approveAreaSpaceSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.approveAreaSpaceSuccess,
  payload: areaSpace,
});

export const getAreaSpaceError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpaceError,
  payload: message,
});

export const getAreaSpacePending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpacePending,
});

export const getAreaSpaceSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpaceSuccess,
  payload: areaSpace,
});

export const getAreaSpacesError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpacesError,
  payload: message,
});

export const getAreaSpacesPending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpacesPending,
});

export const getAreaSpacesSuccess = (areaSpaces: AreaSpace[]): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.getAreaSpacesSuccess,
  payload: areaSpaces,
});

export const recordPresentationError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordPresentationError,
  payload: message,
});

export const recordPresentationPending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordPresentationPending,
});

export const recordPresentationSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordPresentationSuccess,
  payload: areaSpace,
});

export const recordStartupOrderError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordStartupOrderError,
  payload: message,
});

export const recordStartupOrderPending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordStartupOrderPending,
});

export const recordStartupOrderSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.recordStartupOrderSuccess,
  payload: areaSpace,
});

export const startReviewError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.startReviewError,
  payload: message,
});

export const startReviewPending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.startReviewPending,
});

export const startReviewSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.startReviewSuccess,
  payload: areaSpace,
});

export const suspendAreaSpaceError = (message: string): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.suspendAreaSpaceError,
  payload: message,
});

export const suspendAreaSpacePending = (): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.suspendAreaSpacePending,
});

export const suspendAreaSpaceSuccess = (areaSpace: AreaSpace): AreaSpacesAction => ({
  type: AreaSpacesActionTypes.suspendAreaSpaceSuccess,
  payload: areaSpace,
});

import type { Facilitator } from "./context";

export const FacilitatorsActionTypes = {
  getFacilitatorError: "facilitators/getFacilitatorError",
  getFacilitatorPending: "facilitators/getFacilitatorPending",
  getFacilitatorSuccess: "facilitators/getFacilitatorSuccess",
  getFacilitatorsByCustomerError: "facilitators/getFacilitatorsByCustomerError",
  getFacilitatorsByCustomerPending: "facilitators/getFacilitatorsByCustomerPending",
  getFacilitatorsByCustomerSuccess: "facilitators/getFacilitatorsByCustomerSuccess",
  getFacilitatorsError: "facilitators/getFacilitatorsError",
  getFacilitatorsPending: "facilitators/getFacilitatorsPending",
  getFacilitatorsSuccess: "facilitators/getFacilitatorsSuccess",
  registerFacilitatorError: "facilitators/registerFacilitatorError",
  registerFacilitatorPending: "facilitators/registerFacilitatorPending",
  registerFacilitatorSuccess: "facilitators/registerFacilitatorSuccess",
} as const;

export type FacilitatorsAction =
  | { type: typeof FacilitatorsActionTypes.getFacilitatorError; payload: string }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorPending }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorSuccess; payload: Facilitator }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsByCustomerError; payload: string }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsByCustomerPending }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsByCustomerSuccess; payload: Facilitator[] }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsError; payload: string }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsPending }
  | { type: typeof FacilitatorsActionTypes.getFacilitatorsSuccess; payload: Facilitator[] }
  | { type: typeof FacilitatorsActionTypes.registerFacilitatorError; payload: string }
  | { type: typeof FacilitatorsActionTypes.registerFacilitatorPending }
  | { type: typeof FacilitatorsActionTypes.registerFacilitatorSuccess };

export const getFacilitatorError = (message: string): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorError,
  payload: message,
});

export const getFacilitatorPending = (): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorPending,
});

export const getFacilitatorSuccess = (facilitator: Facilitator): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorSuccess,
  payload: facilitator,
});

export const getFacilitatorsByCustomerError = (message: string): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsByCustomerError,
  payload: message,
});

export const getFacilitatorsByCustomerPending = (): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsByCustomerPending,
});

export const getFacilitatorsByCustomerSuccess = (facilitators: Facilitator[]): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsByCustomerSuccess,
  payload: facilitators,
});

export const getFacilitatorsError = (message: string): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsError,
  payload: message,
});

export const getFacilitatorsPending = (): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsPending,
});

export const getFacilitatorsSuccess = (facilitators: Facilitator[]): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.getFacilitatorsSuccess,
  payload: facilitators,
});

export const registerFacilitatorError = (message: string): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.registerFacilitatorError,
  payload: message,
});

export const registerFacilitatorPending = (): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.registerFacilitatorPending,
});

export const registerFacilitatorSuccess = (): FacilitatorsAction => ({
  type: FacilitatorsActionTypes.registerFacilitatorSuccess,
});

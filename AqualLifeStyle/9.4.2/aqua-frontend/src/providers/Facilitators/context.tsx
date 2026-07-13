import { createContext } from "react";

export type Facilitator = {
  id: number;
  tenantId: number;
  customerId: number;
  areaLeaderId: number;
  rank: number;
  directReferrals: number;
  indirectReferrals: number;
  awardBalance: number;
};

export type FacilitatorRegisterInput = {
  customerId: number;
  areaLeaderId: number;
};

export type FacilitatorsState = {
  facilitators: Facilitator[];
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  isRegisterError: boolean;
  isRegisterPending: boolean;
  isRegisterSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  loadErrorMessage: string | null;
  registerErrorMessage: string | null;
  selectedFacilitator: Facilitator | null;
  selectedErrorMessage: string | null;
};

export type FacilitatorsActions = {
  getFacilitator: (id: number) => Promise<void>;
  getFacilitators: () => Promise<void>;
  getFacilitatorsByCustomer: (customerId: number) => Promise<void>;
  registerFacilitator: (input: FacilitatorRegisterInput) => Promise<boolean>;
};

export const initialFacilitatorsState: FacilitatorsState = {
  facilitators: [],
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  isRegisterError: false,
  isRegisterPending: false,
  isRegisterSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  loadErrorMessage: null,
  registerErrorMessage: null,
  selectedFacilitator: null,
  selectedErrorMessage: null,
};

export const FacilitatorsStateContext =
  createContext<FacilitatorsState>(initialFacilitatorsState);

export const FacilitatorsActionsContext =
  createContext<FacilitatorsActions | null>(null);

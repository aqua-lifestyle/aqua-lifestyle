import { createContext } from "react";

export type AreaSpace = {
  id: number;
  tenantId: number;
  areaLeaderId: number;
  addressLine: string;
  capacity: string;
  interestedMembers: number;
  status: number;
  reviewStartedAt: string | null;
  presentationsCompleted: number;
  startupOrdersCompleted: number;
  approvedAt: string | null;
};

export type AreaSpaceApplyInput = {
  areaLeaderId: number;
  addressLine: string;
  capacity: string;
  interestedMembers: number;
};

export type AreaSpacesState = {
  areaSpaces: AreaSpace[];
  isApplyError: boolean;
  isApplyPending: boolean;
  isApplySuccess: boolean;
  isApproveError: boolean;
  isApprovePending: boolean;
  isApproveSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  isSuspendError: boolean;
  isSuspendPending: boolean;
  isSuspendSuccess: boolean;
  applyErrorMessage: string | null;
  approveErrorMessage: string | null;
  loadErrorMessage: string | null;
  selectedAreaSpace: AreaSpace | null;
  selectedErrorMessage: string | null;
  suspendErrorMessage: string | null;
};

export type AreaSpacesActions = {
  applyAreaSpace: (input: AreaSpaceApplyInput) => Promise<boolean>;
  approveAreaSpace: (id: number) => Promise<boolean>;
  getAreaSpace: (id: number) => Promise<void>;
  getAreaSpaces: () => Promise<void>;
  startReview: (id: number) => Promise<boolean>;
  recordPresentation: (id: number) => Promise<boolean>;
  recordStartupOrder: (id: number) => Promise<boolean>;
  suspendAreaSpace: (id: number) => Promise<boolean>;
};

export const initialAreaSpacesState: AreaSpacesState = {
  areaSpaces: [],
  isApplyError: false,
  isApplyPending: false,
  isApplySuccess: false,
  isApproveError: false,
  isApprovePending: false,
  isApproveSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuspendError: false,
  isSuspendPending: false,
  isSuspendSuccess: false,
  applyErrorMessage: null,
  approveErrorMessage: null,
  loadErrorMessage: null,
  selectedAreaSpace: null,
  selectedErrorMessage: null,
  suspendErrorMessage: null,
};

export const AreaSpacesStateContext =
  createContext<AreaSpacesState>(initialAreaSpacesState);

export const AreaSpacesActionsContext =
  createContext<AreaSpacesActions | null>(null);

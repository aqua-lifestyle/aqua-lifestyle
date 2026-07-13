import { createContext } from "react";

export type AreaLeader = {
  id: number;
  tenantId: number;
  customerId: number;
  licenseType: number;
  licenseFee: number;
  rank: number;
  areaSpaceId: number | null;
  monthlySubscription: number;
  directReferrals: number;
  indirectReferrals: number;
  orderTarget: number;
};

export type AreaLeaderApplyInput = {
  customerId: number;
  licenseType: number;
};

export type AreaLeadersState = {
  areaLeaders: AreaLeader[];
  isApplyError: boolean;
  isApplyPending: boolean;
  isApplySuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  isPromoteError: boolean;
  isPromotePending: boolean;
  isPromoteSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  applyErrorMessage: string | null;
  loadErrorMessage: string | null;
  promoteErrorMessage: string | null;
  selectedAreaLeader: AreaLeader | null;
  selectedErrorMessage: string | null;
};

export type AreaLeadersActions = {
  applyAreaLeader: (input: AreaLeaderApplyInput) => Promise<boolean>;
  getAreaLeader: (id: number) => Promise<void>;
  getAreaLeaders: () => Promise<void>;
  promoteAreaLeader: (id: number) => Promise<boolean>;
};

export const initialAreaLeadersState: AreaLeadersState = {
  areaLeaders: [],
  isApplyError: false,
  isApplyPending: false,
  isApplySuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  isPromoteError: false,
  isPromotePending: false,
  isPromoteSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  applyErrorMessage: null,
  loadErrorMessage: null,
  promoteErrorMessage: null,
  selectedAreaLeader: null,
  selectedErrorMessage: null,
};

export const AreaLeadersStateContext =
  createContext<AreaLeadersState>(initialAreaLeadersState);

export const AreaLeadersActionsContext =
  createContext<AreaLeadersActions | null>(null);

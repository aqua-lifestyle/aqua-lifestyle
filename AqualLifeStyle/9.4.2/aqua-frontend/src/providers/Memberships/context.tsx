import { createContext } from "react";

export type MembershipType = 0 | 1 | 2 | 3;

export type Membership = {
  id: number;
  name: string;
  description: string | null;
  isActive: boolean;
  membershipType: MembershipType;
  activationDate: string | null;
  monthlyObligationAmount: number;
  lastObligationMetDate: string | null;
};

export type TierBenefits = {
  tier: MembershipType;
  tierName: string;
  monthlyObligation: number;
  orderWindowStartDay: number;
  orderWindowEndDay: number;
  savingsWindowOpenDay: number;
  savingsWindowCloseDay: number;
  productPricingDiscount: number;
  interestRate: number;
  maxConcurrentOrders: number;
  referralCommissionRate: number;
  profitSharePercentage: number;
  isOrderWindowOpen: boolean;
  isSavingsWindowOpen: boolean;
};

export type SavingsWindowStatus = {
  tier: MembershipType;
  tierName: string;
  savingsWindowOpenDay: number;
  savingsWindowCloseDay: number;
  currentDay: number;
  asOfDate: string;
  isSavingsWindowOpen: boolean;
  statusLabel: string;
};

export type MembershipsState = {
  errorMessage: string | null;
  isError: boolean;
  isPending: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  isSuccess: boolean;
  memberships: Membership[];
  selectedErrorMessage: string | null;
  selectedMembership: Membership | null;
  tierBenefits: TierBenefits | null;
  tierBenefitsErrorMessage: string | null;
  isTierBenefitsError: boolean;
  isTierBenefitsPending: boolean;
  isTierBenefitsSuccess: boolean;
  savingsWindowStatuses: SavingsWindowStatus[];
  savingsWindowStatusesErrorMessage: string | null;
  isSavingsWindowStatusesError: boolean;
  isSavingsWindowStatusesPending: boolean;
  isSavingsWindowStatusesSuccess: boolean;
};

export type MembershipsActions = {
  getMembership: (id: number) => Promise<void>;
  getMemberships: () => Promise<void>;
  getSavingsWindowStatuses: () => Promise<void>;
  getTierBenefits: (id: number) => Promise<void>;
};

export const initialMembershipsState: MembershipsState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSuccess: false,
  memberships: [],
  selectedErrorMessage: null,
  selectedMembership: null,
  tierBenefits: null,
  tierBenefitsErrorMessage: null,
  isTierBenefitsError: false,
  isTierBenefitsPending: false,
  isTierBenefitsSuccess: false,
  savingsWindowStatuses: [],
  savingsWindowStatusesErrorMessage: null,
  isSavingsWindowStatusesError: false,
  isSavingsWindowStatusesPending: false,
  isSavingsWindowStatusesSuccess: false,
};

export const MembershipsStateContext =
  createContext<MembershipsState>(initialMembershipsState);

export const MembershipsActionsContext =
  createContext<MembershipsActions | null>(null);

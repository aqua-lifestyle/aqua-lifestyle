import { createContext } from "react";

export type Referral = {
  id: number;
  tenantId: number;
  referrerFacilitatorId: number | null;
  referrerAreaLeaderId: number | null;
  referredCustomerId: number;
  sourceEnquiryId: number;
  type: number;
  awardAmount: number;
  awardIssued: boolean;
  confirmedAt: string | null;
  convertedAt: string | null;
};

export type ReferralsState = {
  isConfirmError: boolean;
  isConfirmPending: boolean;
  isConfirmSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  confirmErrorMessage: string | null;
  loadErrorMessage: string | null;
  referrals: Referral[];
  selectedReferral: Referral | null;
  selectedErrorMessage: string | null;
};

export type ReferralsActions = {
  confirmAward: (id: number) => Promise<boolean>;
  getReferrals: () => Promise<void>;
  getReferralsByEnquiry: (enquiryId: number) => Promise<void>;
};

export const initialReferralsState: ReferralsState = {
  isConfirmError: false,
  isConfirmPending: false,
  isConfirmSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  confirmErrorMessage: null,
  loadErrorMessage: null,
  referrals: [],
  selectedReferral: null,
  selectedErrorMessage: null,
};

export const ReferralsStateContext =
  createContext<ReferralsState>(initialReferralsState);

export const ReferralsActionsContext =
  createContext<ReferralsActions | null>(null);

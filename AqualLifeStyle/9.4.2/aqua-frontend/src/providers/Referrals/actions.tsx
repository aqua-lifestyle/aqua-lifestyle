import type { Referral } from "./context";

export const ReferralsActionTypes = {
  confirmAwardError: "referrals/confirmAwardError",
  confirmAwardPending: "referrals/confirmAwardPending",
  confirmAwardSuccess: "referrals/confirmAwardSuccess",
  getReferralError: "referrals/getReferralError",
  getReferralPending: "referrals/getReferralPending",
  getReferralSuccess: "referrals/getReferralSuccess",
  getReferralsByEnquiryError: "referrals/getReferralsByEnquiryError",
  getReferralsByEnquiryPending: "referrals/getReferralsByEnquiryPending",
  getReferralsByEnquirySuccess: "referrals/getReferralsByEnquirySuccess",
  getReferralsError: "referrals/getReferralsError",
  getReferralsPending: "referrals/getReferralsPending",
  getReferralsSuccess: "referrals/getReferralsSuccess",
} as const;

export type ReferralsAction =
  | { type: typeof ReferralsActionTypes.confirmAwardError; payload: string }
  | { type: typeof ReferralsActionTypes.confirmAwardPending }
  | { type: typeof ReferralsActionTypes.confirmAwardSuccess; payload: Referral }
  | { type: typeof ReferralsActionTypes.getReferralError; payload: string }
  | { type: typeof ReferralsActionTypes.getReferralPending }
  | { type: typeof ReferralsActionTypes.getReferralSuccess; payload: Referral }
  | { type: typeof ReferralsActionTypes.getReferralsByEnquiryError; payload: string }
  | { type: typeof ReferralsActionTypes.getReferralsByEnquiryPending }
  | { type: typeof ReferralsActionTypes.getReferralsByEnquirySuccess; payload: Referral[] }
  | { type: typeof ReferralsActionTypes.getReferralsError; payload: string }
  | { type: typeof ReferralsActionTypes.getReferralsPending }
  | { type: typeof ReferralsActionTypes.getReferralsSuccess; payload: Referral[] };

export const confirmAwardError = (message: string): ReferralsAction => ({
  type: ReferralsActionTypes.confirmAwardError,
  payload: message,
});

export const confirmAwardPending = (): ReferralsAction => ({
  type: ReferralsActionTypes.confirmAwardPending,
});

export const confirmAwardSuccess = (referral: Referral): ReferralsAction => ({
  type: ReferralsActionTypes.confirmAwardSuccess,
  payload: referral,
});

export const getReferralError = (message: string): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralError,
  payload: message,
});

export const getReferralPending = (): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralPending,
});

export const getReferralSuccess = (referral: Referral): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralSuccess,
  payload: referral,
});

export const getReferralsByEnquiryError = (message: string): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsByEnquiryError,
  payload: message,
});

export const getReferralsByEnquiryPending = (): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsByEnquiryPending,
});

export const getReferralsByEnquirySuccess = (referrals: Referral[]): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsByEnquirySuccess,
  payload: referrals,
});

export const getReferralsError = (message: string): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsError,
  payload: message,
});

export const getReferralsPending = (): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsPending,
});

export const getReferralsSuccess = (referrals: Referral[]): ReferralsAction => ({
  type: ReferralsActionTypes.getReferralsSuccess,
  payload: referrals,
});

import type { Membership, TierBenefits } from "./context";

export const MembershipsActionTypes = {
  getMembershipError: "memberships/getMembershipError",
  getMembershipPending: "memberships/getMembershipPending",
  getMembershipSuccess: "memberships/getMembershipSuccess",
  getMembershipsError: "memberships/getMembershipsError",
  getMembershipsPending: "memberships/getMembershipsPending",
  getMembershipsSuccess: "memberships/getMembershipsSuccess",
  getTierBenefitsError: "memberships/getTierBenefitsError",
  getTierBenefitsPending: "memberships/getTierBenefitsPending",
  getTierBenefitsSuccess: "memberships/getTierBenefitsSuccess",
} as const;

export type MembershipsAction =
  | {
      type: typeof MembershipsActionTypes.getMembershipError;
      payload: string;
    }
  | {
      type: typeof MembershipsActionTypes.getMembershipPending;
    }
  | {
      type: typeof MembershipsActionTypes.getMembershipSuccess;
      payload: Membership;
    }
  | {
      type: typeof MembershipsActionTypes.getMembershipsError;
      payload: string;
    }
  | {
      type: typeof MembershipsActionTypes.getMembershipsPending;
    }
  | {
      type: typeof MembershipsActionTypes.getMembershipsSuccess;
      payload: Membership[];
    }
  | {
      type: typeof MembershipsActionTypes.getTierBenefitsError;
      payload: string;
    }
  | {
      type: typeof MembershipsActionTypes.getTierBenefitsPending;
    }
  | {
      type: typeof MembershipsActionTypes.getTierBenefitsSuccess;
      payload: TierBenefits;
    };

export const getMembershipError = (message: string): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipError,
  payload: message,
});

export const getMembershipPending = (): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipPending,
});

export const getMembershipSuccess = (
  membership: Membership,
): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipSuccess,
  payload: membership,
});

export const getMembershipsError = (message: string): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipsError,
  payload: message,
});

export const getMembershipsPending = (): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipsPending,
});

export const getMembershipsSuccess = (
  memberships: Membership[],
): MembershipsAction => ({
  type: MembershipsActionTypes.getMembershipsSuccess,
  payload: memberships,
});

export const getTierBenefitsError = (message: string): MembershipsAction => ({
  type: MembershipsActionTypes.getTierBenefitsError,
  payload: message,
});

export const getTierBenefitsPending = (): MembershipsAction => ({
  type: MembershipsActionTypes.getTierBenefitsPending,
});

export const getTierBenefitsSuccess = (
  tierBenefits: TierBenefits,
): MembershipsAction => ({
  type: MembershipsActionTypes.getTierBenefitsSuccess,
  payload: tierBenefits,
});

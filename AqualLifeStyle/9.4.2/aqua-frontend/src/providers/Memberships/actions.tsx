import type {
  Membership,
  SavingsWindowStatus,
  TierBenefits,
} from "./context";

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
  getSavingsWindowStatusesError: "memberships/getSavingsWindowStatusesError",
  getSavingsWindowStatusesPending:
    "memberships/getSavingsWindowStatusesPending",
  getSavingsWindowStatusesSuccess:
    "memberships/getSavingsWindowStatusesSuccess",
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
    }
  | {
      type: typeof MembershipsActionTypes.getSavingsWindowStatusesError;
      payload: string;
    }
  | {
      type: typeof MembershipsActionTypes.getSavingsWindowStatusesPending;
    }
  | {
      type: typeof MembershipsActionTypes.getSavingsWindowStatusesSuccess;
      payload: SavingsWindowStatus[];
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

export const getSavingsWindowStatusesError = (
  message: string,
): MembershipsAction => ({
  type: MembershipsActionTypes.getSavingsWindowStatusesError,
  payload: message,
});

export const getSavingsWindowStatusesPending = (): MembershipsAction => ({
  type: MembershipsActionTypes.getSavingsWindowStatusesPending,
});

export const getSavingsWindowStatusesSuccess = (
  statuses: SavingsWindowStatus[],
): MembershipsAction => ({
  type: MembershipsActionTypes.getSavingsWindowStatusesSuccess,
  payload: statuses,
});

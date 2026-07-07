import type { Membership } from "./context";

export const MembershipsActionTypes = {
  getMembershipsError: "memberships/getMembershipsError",
  getMembershipsPending: "memberships/getMembershipsPending",
  getMembershipsSuccess: "memberships/getMembershipsSuccess",
} as const;

export type MembershipsAction =
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
    };

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

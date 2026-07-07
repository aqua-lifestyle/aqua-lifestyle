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

export type MembershipsState = {
  errorMessage: string | null;
  isError: boolean;
  isPending: boolean;
  isSuccess: boolean;
  memberships: Membership[];
};

export type MembershipsActions = {
  getMemberships: () => Promise<void>;
};

export const initialMembershipsState: MembershipsState = {
  errorMessage: null,
  isError: false,
  isPending: false,
  isSuccess: false,
  memberships: [],
};

export const MembershipsStateContext =
  createContext<MembershipsState>(initialMembershipsState);

export const MembershipsActionsContext =
  createContext<MembershipsActions | null>(null);

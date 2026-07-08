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
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  isSuccess: boolean;
  memberships: Membership[];
  selectedErrorMessage: string | null;
  selectedMembership: Membership | null;
};

export type MembershipsActions = {
  getMembership: (id: number) => Promise<void>;
  getMemberships: () => Promise<void>;
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
};

export const MembershipsStateContext =
  createContext<MembershipsState>(initialMembershipsState);

export const MembershipsActionsContext =
  createContext<MembershipsActions | null>(null);

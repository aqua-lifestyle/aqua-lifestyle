"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { apiEndpoints, getErrorMessage, httpClient } from "@/src/shared/api";
import {
  getMembershipError,
  getMembershipPending,
  getMembershipSuccess,
  getMembershipsError,
  getMembershipsPending,
  getMembershipsSuccess,
  getSavingsWindowStatusesError,
  getSavingsWindowStatusesPending,
  getSavingsWindowStatusesSuccess,
  getTierBenefitsError,
  getTierBenefitsPending,
  getTierBenefitsSuccess,
} from "./actions";
import {
  type Membership,
  MembershipsActionsContext,
  MembershipsStateContext,
  type SavingsWindowStatus,
  type TierBenefits,
  initialMembershipsState,
} from "./context";
import { membershipsReducer } from "./reducer";

type MembershipsProviderProps = {
  children: ReactNode;
};

export const MembershipsProvider = ({ children }: MembershipsProviderProps) => {
  const [state, dispatch] = useReducer(
    membershipsReducer,
    initialMembershipsState,
  );

  const getMemberships = useCallback(async () => {
    dispatch(getMembershipsPending());

    try {
      const memberships = await httpClient.get<Membership[]>(
        apiEndpoints.memberships.getAll,
      );
      dispatch(getMembershipsSuccess(memberships));
    } catch (error) {
      dispatch(getMembershipsError(getErrorMessage(error, "Unable to load memberships.")));
    }
  }, []);

  const getMembership = useCallback(async (id: number) => {
    dispatch(getMembershipPending());

    try {
      const membership = await httpClient.get<Membership>(
        apiEndpoints.memberships.getById(id),
      );
      dispatch(getMembershipSuccess(membership));
    } catch (error) {
      dispatch(getMembershipError(getErrorMessage(error, "Unable to load memberships.")));
    }
  }, []);

  const getTierBenefits = useCallback(async (id: number) => {
    dispatch(getTierBenefitsPending());

    try {
      const tierBenefits = await httpClient.get<TierBenefits>(
        apiEndpoints.memberships.getTierBenefits(id),
      );
      dispatch(getTierBenefitsSuccess(tierBenefits));
    } catch (error) {
      dispatch(getTierBenefitsError(getErrorMessage(error, "Unable to load memberships.")));
    }
  }, []);

  const getSavingsWindowStatuses = useCallback(async () => {
    dispatch(getSavingsWindowStatusesPending());

    try {
      const statuses = await httpClient.get<SavingsWindowStatus[]>(
        apiEndpoints.memberships.getSavingsWindowStatuses,
      );
      dispatch(getSavingsWindowStatusesSuccess(statuses));
    } catch (error) {
      dispatch(getSavingsWindowStatusesError(getErrorMessage(error, "Unable to load memberships.")));
    }
  }, []);

  const actions = useMemo(
    () => ({
      getMembership,
      getMemberships,
      getSavingsWindowStatuses,
      getTierBenefits,
    }),
    [getMembership, getMemberships, getSavingsWindowStatuses, getTierBenefits],
  );

  return (
    <MembershipsStateContext.Provider value={state}>
      <MembershipsActionsContext.Provider value={actions}>
        {children}
      </MembershipsActionsContext.Provider>
    </MembershipsStateContext.Provider>
  );
};

export const useMembershipsState = () => {
  return useContext(MembershipsStateContext);
};

export const useMembershipsActions = () => {
  const context = useContext(MembershipsActionsContext);

  if (!context) {
    throw new Error(
      "useMembershipsActions must be used within a MembershipsProvider.",
    );
  }

  return context;
};

export type {
  Membership,
  MembershipType,
  SavingsWindowStatus,
  TierBenefits,
} from "./context";

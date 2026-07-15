"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { apiEndpoints, getRequestErrorMessage, httpClient } from "@/src/shared/api";
import {
  confirmAwardError,
  confirmAwardPending,
  confirmAwardSuccess,
  getReferralsByEnquiryError,
  getReferralsByEnquiryPending,
  getReferralsByEnquirySuccess,
  getReferralsError,
  getReferralsPending,
  getReferralsSuccess,
} from "./actions";
import {
  ReferralsActionsContext,
  ReferralsStateContext,
  initialReferralsState,
  type Referral,
  type ReferralsActions,
  type ReferralsState,
} from "./context";
import { referralsReducer } from "./reducer";

type ReferralsProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  return getRequestErrorMessage(error, "Unable to complete the referral request.");
};

export const ReferralsProvider = ({ children }: ReferralsProviderProps) => {
  const [state, dispatch] = useReducer(referralsReducer, initialReferralsState);

  const getReferrals = useCallback(async () => {
    dispatch(getReferralsPending());

    try {
      const referrals = await httpClient.get<ReferralsState["referrals"]>(
        apiEndpoints.referrals.getAll,
      );
      dispatch(getReferralsSuccess(referrals));
    } catch (error) {
      dispatch(getReferralsError(getErrorMessage(error)));
    }
  }, []);

  const getReferralsByEnquiry = useCallback(async (enquiryId: number) => {
    dispatch(getReferralsByEnquiryPending());

    try {
      const referrals = await httpClient.get<ReferralsState["referrals"]>(
        apiEndpoints.referrals.getByEnquiry(enquiryId),
      );
      dispatch(getReferralsByEnquirySuccess(referrals));
    } catch (error) {
      dispatch(getReferralsByEnquiryError(getErrorMessage(error)));
    }
  }, []);

  const confirmAward = useCallback(async (id: number) => {
    dispatch(confirmAwardPending());

    try {
      const referral = await httpClient.post<Referral, null>(
        apiEndpoints.referrals.confirmAward(id),
        null,
      );
      dispatch(confirmAwardSuccess(referral));
      return true;
    } catch (error) {
      dispatch(confirmAwardError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo<ReferralsActions>(
    () => ({
      confirmAward,
      getReferrals,
      getReferralsByEnquiry,
    }),
    [confirmAward, getReferrals, getReferralsByEnquiry],
  );

  return (
    <ReferralsStateContext.Provider value={state}>
      <ReferralsActionsContext.Provider value={actions}>
        {children}
      </ReferralsActionsContext.Provider>
    </ReferralsStateContext.Provider>
  );
};

export const useReferralsState = () => {
  return useContext(ReferralsStateContext);
};

export const useReferralsActions = () => {
  const context = useContext(ReferralsActionsContext);

  if (!context) {
    throw new Error("useReferralsActions must be used within a ReferralsProvider.");
  }

  return context;
};

export type { ReferralsActions, ReferralsState, Referral } from "./context";

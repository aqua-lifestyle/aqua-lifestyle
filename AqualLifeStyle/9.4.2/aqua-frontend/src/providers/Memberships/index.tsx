"use client";

import {
  type ReactNode,
  useCallback,
  useContext,
  useMemo,
  useReducer,
} from "react";

import { AbpHttpError, apiEndpoints, httpClient } from "@/src/shared/api";
import {
  getMembershipsError,
  getMembershipsPending,
  getMembershipsSuccess,
} from "./actions";
import {
  type Membership,
  MembershipsActionsContext,
  MembershipsStateContext,
  initialMembershipsState,
} from "./context";
import { membershipsReducer } from "./reducer";

type MembershipsProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to load memberships.";
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
      dispatch(getMembershipsError(getErrorMessage(error)));
    }
  }, []);

  const actions = useMemo(
    () => ({
      getMemberships,
    }),
    [getMemberships],
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

export type { Membership, MembershipType } from "./context";

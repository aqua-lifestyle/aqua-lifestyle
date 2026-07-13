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
  applyAreaLeaderError,
  applyAreaLeaderPending,
  applyAreaLeaderSuccess,
  getAreaLeaderError,
  getAreaLeaderPending,
  getAreaLeaderSuccess,
  getAreaLeadersError,
  getAreaLeadersPending,
  getAreaLeadersSuccess,
  promoteAreaLeaderError,
  promoteAreaLeaderPending,
  promoteAreaLeaderSuccess,
} from "./actions";
import {
  AreaLeadersActionsContext,
  AreaLeadersStateContext,
  initialAreaLeadersState,
  type AreaLeader,
  type AreaLeadersActions,
  type AreaLeadersState,
} from "./context";
import { areaLeadersReducer } from "./reducer";

type AreaLeadersProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to complete the area leader request.";
};

export const AreaLeadersProvider = ({ children }: AreaLeadersProviderProps) => {
  const [state, dispatch] = useReducer(areaLeadersReducer, initialAreaLeadersState);

  const getAreaLeaders = useCallback(async () => {
    dispatch(getAreaLeadersPending());

    try {
      const areaLeaders = await httpClient.get<AreaLeadersState["areaLeaders"]>(
        apiEndpoints.areaLeaders.getAll,
      );
      dispatch(getAreaLeadersSuccess(areaLeaders));
    } catch (error) {
      dispatch(getAreaLeadersError(getErrorMessage(error)));
    }
  }, []);

  const getAreaLeader = useCallback(async (id: number) => {
    dispatch(getAreaLeaderPending());

    try {
      const areaLeader = await httpClient.get<AreaLeader>(
        apiEndpoints.areaLeaders.getById(id),
      );
      dispatch(getAreaLeaderSuccess(areaLeader));
    } catch (error) {
      dispatch(getAreaLeaderError(getErrorMessage(error)));
    }
  }, []);

  const applyAreaLeader = useCallback(async (input: { customerId: number; licenseType: number }) => {
    dispatch(applyAreaLeaderPending());

    try {
      await httpClient.post<void, { customerId: number; licenseType: number }>(
        apiEndpoints.areaLeaders.apply,
        input,
      );
      dispatch(applyAreaLeaderSuccess());
      return true;
    } catch (error) {
      dispatch(applyAreaLeaderError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const promoteAreaLeader = useCallback(async (id: number) => {
    dispatch(promoteAreaLeaderPending());

    try {
      const areaLeader = await httpClient.post<AreaLeader, null>(
        apiEndpoints.areaLeaders.promote(id),
        null,
      );
      dispatch(promoteAreaLeaderSuccess(areaLeader));
      return true;
    } catch (error) {
      dispatch(promoteAreaLeaderError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo<AreaLeadersActions>(
    () => ({
      applyAreaLeader,
      getAreaLeader,
      getAreaLeaders,
      promoteAreaLeader,
    }),
    [applyAreaLeader, getAreaLeader, getAreaLeaders, promoteAreaLeader],
  );

  return (
    <AreaLeadersStateContext.Provider value={state}>
      <AreaLeadersActionsContext.Provider value={actions}>
        {children}
      </AreaLeadersActionsContext.Provider>
    </AreaLeadersStateContext.Provider>
  );
};

export const useAreaLeadersState = () => {
  return useContext(AreaLeadersStateContext);
};

export const useAreaLeadersActions = () => {
  const context = useContext(AreaLeadersActionsContext);

  if (!context) {
    throw new Error("useAreaLeadersActions must be used within an AreaLeadersProvider.");
  }

  return context;
};

export type { AreaLeadersActions, AreaLeadersState, AreaLeader } from "./context";

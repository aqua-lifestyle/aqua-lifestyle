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
  applyAreaSpaceError,
  applyAreaSpacePending,
  applyAreaSpaceSuccess,
  approveAreaSpaceError,
  approveAreaSpacePending,
  approveAreaSpaceSuccess,
  getAreaSpaceError,
  getAreaSpacePending,
  getAreaSpaceSuccess,
  getAreaSpacesError,
  getAreaSpacesPending,
  getAreaSpacesSuccess,
  recordPresentationError,
  recordPresentationPending,
  recordPresentationSuccess,
  recordStartupOrderError,
  recordStartupOrderPending,
  recordStartupOrderSuccess,
  startReviewError,
  startReviewPending,
  startReviewSuccess,
  suspendAreaSpaceError,
  suspendAreaSpacePending,
  suspendAreaSpaceSuccess,
} from "./actions";
import {
  AreaSpacesActionsContext,
  AreaSpacesStateContext,
  initialAreaSpacesState,
  type AreaSpace,
  type AreaSpacesActions,
  type AreaSpacesState,
} from "./context";
import { areaSpacesReducer } from "./reducer";

type AreaSpacesProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  return getRequestErrorMessage(error, "Unable to complete the area space request.");
};

export const AreaSpacesProvider = ({ children }: AreaSpacesProviderProps) => {
  const [state, dispatch] = useReducer(areaSpacesReducer, initialAreaSpacesState);

  const getAreaSpaces = useCallback(async () => {
    dispatch(getAreaSpacesPending());

    try {
      const areaSpaces = await httpClient.get<AreaSpacesState["areaSpaces"]>(
        apiEndpoints.areaSpaces.getAll,
      );
      dispatch(getAreaSpacesSuccess(areaSpaces));
    } catch (error) {
      dispatch(getAreaSpacesError(getErrorMessage(error)));
    }
  }, []);

  const getAreaSpace = useCallback(async (id: number) => {
    dispatch(getAreaSpacePending());

    try {
      const areaSpace = await httpClient.get<AreaSpace>(
        apiEndpoints.areaSpaces.getById(id),
      );
      dispatch(getAreaSpaceSuccess(areaSpace));
    } catch (error) {
      dispatch(getAreaSpaceError(getErrorMessage(error)));
    }
  }, []);

  const applyAreaSpace = useCallback(async (input: {
    areaLeaderId: number;
    addressLine: string;
    capacity: string;
    interestedMembers: number;
  }) => {
    dispatch(applyAreaSpacePending());

    try {
      await httpClient.post<void, {
        areaLeaderId: number;
        addressLine: string;
        capacity: string;
        interestedMembers: number;
      }>(apiEndpoints.areaSpaces.apply, input);
      dispatch(applyAreaSpaceSuccess());
      return true;
    } catch (error) {
      dispatch(applyAreaSpaceError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const startReview = useCallback(async (id: number) => {
    dispatch(startReviewPending());

    try {
      const areaSpace = await httpClient.post<AreaSpace, null>(
        apiEndpoints.areaSpaces.startReview(id),
        null,
      );
      dispatch(startReviewSuccess(areaSpace));
      return true;
    } catch (error) {
      dispatch(startReviewError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const recordPresentation = useCallback(async (id: number) => {
    dispatch(recordPresentationPending());

    try {
      const areaSpace = await httpClient.post<AreaSpace, null>(
        apiEndpoints.areaSpaces.recordPresentation(id),
        null,
      );
      dispatch(recordPresentationSuccess(areaSpace));
      return true;
    } catch (error) {
      dispatch(recordPresentationError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const recordStartupOrder = useCallback(async (id: number) => {
    dispatch(recordStartupOrderPending());

    try {
      const areaSpace = await httpClient.post<AreaSpace, null>(
        apiEndpoints.areaSpaces.recordStartupOrder(id),
        null,
      );
      dispatch(recordStartupOrderSuccess(areaSpace));
      return true;
    } catch (error) {
      dispatch(recordStartupOrderError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const approveAreaSpace = useCallback(async (id: number) => {
    dispatch(approveAreaSpacePending());

    try {
      const areaSpace = await httpClient.post<AreaSpace, null>(
        apiEndpoints.areaSpaces.approve(id),
        null,
      );
      dispatch(approveAreaSpaceSuccess(areaSpace));
      return true;
    } catch (error) {
      dispatch(approveAreaSpaceError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const suspendAreaSpace = useCallback(async (id: number) => {
    dispatch(suspendAreaSpacePending());

    try {
      const areaSpace = await httpClient.post<AreaSpace, null>(
        apiEndpoints.areaSpaces.suspend(id),
        null,
      );
      dispatch(suspendAreaSpaceSuccess(areaSpace));
      return true;
    } catch (error) {
      dispatch(suspendAreaSpaceError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo<AreaSpacesActions>(
    () => ({
      applyAreaSpace,
      approveAreaSpace,
      getAreaSpace,
      getAreaSpaces,
      recordPresentation,
      recordStartupOrder,
      startReview,
      suspendAreaSpace,
    }),
    [
      applyAreaSpace,
      approveAreaSpace,
      getAreaSpace,
      getAreaSpaces,
      recordPresentation,
      recordStartupOrder,
      startReview,
      suspendAreaSpace,
    ],
  );

  return (
    <AreaSpacesStateContext.Provider value={state}>
      <AreaSpacesActionsContext.Provider value={actions}>
        {children}
      </AreaSpacesActionsContext.Provider>
    </AreaSpacesStateContext.Provider>
  );
};

export const useAreaSpacesState = () => {
  return useContext(AreaSpacesStateContext);
};

export const useAreaSpacesActions = () => {
  const context = useContext(AreaSpacesActionsContext);

  if (!context) {
    throw new Error("useAreaSpacesActions must be used within an AreaSpacesProvider.");
  }

  return context;
};

export type { AreaSpacesActions, AreaSpacesState, AreaSpace } from "./context";

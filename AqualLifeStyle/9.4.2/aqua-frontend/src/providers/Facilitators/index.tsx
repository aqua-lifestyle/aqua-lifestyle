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
  getFacilitatorError,
  getFacilitatorPending,
  getFacilitatorSuccess,
  getFacilitatorsByCustomerError,
  getFacilitatorsByCustomerPending,
  getFacilitatorsByCustomerSuccess,
  getFacilitatorsError,
  getFacilitatorsPending,
  getFacilitatorsSuccess,
  registerFacilitatorError,
  registerFacilitatorPending,
  registerFacilitatorSuccess,
} from "./actions";
import {
  FacilitatorsActionsContext,
  FacilitatorsStateContext,
  initialFacilitatorsState,
  type Facilitator,
  type FacilitatorsActions,
  type FacilitatorsState,
} from "./context";
import { facilitatorsReducer } from "./reducer";

type FacilitatorsProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  return getRequestErrorMessage(error, "Unable to complete the facilitator request.");
};

export const FacilitatorsProvider = ({ children }: FacilitatorsProviderProps) => {
  const [state, dispatch] = useReducer(facilitatorsReducer, initialFacilitatorsState);

  const getFacilitators = useCallback(async () => {
    dispatch(getFacilitatorsPending());

    try {
      const facilitators = await httpClient.get<FacilitatorsState["facilitators"]>(
        apiEndpoints.facilitators.getAll,
      );
      dispatch(getFacilitatorsSuccess(facilitators));
    } catch (error) {
      dispatch(getFacilitatorsError(getErrorMessage(error)));
    }
  }, []);

  const getFacilitator = useCallback(async (id: number) => {
    dispatch(getFacilitatorPending());

    try {
      const facilitator = await httpClient.get<Facilitator>(
        apiEndpoints.facilitators.getById(id),
      );
      dispatch(getFacilitatorSuccess(facilitator));
    } catch (error) {
      dispatch(getFacilitatorError(getErrorMessage(error)));
    }
  }, []);

  const getFacilitatorsByCustomer = useCallback(async (customerId: number) => {
    dispatch(getFacilitatorsByCustomerPending());

    try {
      const facilitators = await httpClient.get<FacilitatorsState["facilitators"]>(
        apiEndpoints.facilitators.getByCustomer(customerId),
      );
      dispatch(getFacilitatorsByCustomerSuccess(facilitators));
    } catch (error) {
      dispatch(getFacilitatorsByCustomerError(getErrorMessage(error)));
    }
  }, []);

  const registerFacilitator = useCallback(async (input: {
    customerId: number;
    areaLeaderId: number;
  }) => {
    dispatch(registerFacilitatorPending());

    try {
      await httpClient.post<void, { customerId: number; areaLeaderId: number }>(
        apiEndpoints.facilitators.register,
        input,
      );
      dispatch(registerFacilitatorSuccess());
      return true;
    } catch (error) {
      dispatch(registerFacilitatorError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo<FacilitatorsActions>(
    () => ({
      getFacilitator,
      getFacilitators,
      getFacilitatorsByCustomer,
      registerFacilitator,
    }),
    [getFacilitator, getFacilitators, getFacilitatorsByCustomer, registerFacilitator],
  );

  return (
    <FacilitatorsStateContext.Provider value={state}>
      <FacilitatorsActionsContext.Provider value={actions}>
        {children}
      </FacilitatorsActionsContext.Provider>
    </FacilitatorsStateContext.Provider>
  );
};

export const useFacilitatorsState = () => {
  return useContext(FacilitatorsStateContext);
};

export const useFacilitatorsActions = () => {
  const context = useContext(FacilitatorsActionsContext);

  if (!context) {
    throw new Error("useFacilitatorsActions must be used within a FacilitatorsProvider.");
  }

  return context;
};

export type { FacilitatorsActions, FacilitatorsState, Facilitator } from "./context";

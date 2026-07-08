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
  createEnquiryError,
  createEnquiryPending,
  createEnquirySuccess,
  enquiryActionError,
  enquiryActionPending,
  enquiryActionSuccess,
  getEnquiriesError,
  getEnquiriesPending,
  getEnquiriesSuccess,
  getEnquiryError,
  getEnquiryPending,
  getEnquirySuccess,
} from "./actions";
import {
  type CreateEnquiryInput,
  type CreateEnquiryFollowUpInput,
  type Enquiry,
  EnquiriesActionsContext,
  EnquiriesStateContext,
  initialEnquiriesState,
  type RespondToEnquiryInput,
} from "./context";
import { enquiriesReducer } from "./reducer";

type EnquiriesProviderProps = {
  children: ReactNode;
};

const getErrorMessage = (error: unknown): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return "Unable to complete the enquiry request.";
};

export const EnquiriesProvider = ({ children }: EnquiriesProviderProps) => {
  const [state, dispatch] = useReducer(enquiriesReducer, initialEnquiriesState);

  const getEnquiries = useCallback(async () => {
    dispatch(getEnquiriesPending());

    try {
      const enquiries = await httpClient.get<Enquiry[]>(
        apiEndpoints.enquiries.getAll,
      );
      dispatch(getEnquiriesSuccess(enquiries));
    } catch (error) {
      dispatch(getEnquiriesError(getErrorMessage(error)));
    }
  }, []);

  const createEnquiry = useCallback(async (input: CreateEnquiryInput) => {
    dispatch(createEnquiryPending());

    try {
      await httpClient.post<void, CreateEnquiryInput>(
        apiEndpoints.enquiries.create,
        input,
      );
      dispatch(createEnquirySuccess());
      return true;
    } catch (error) {
      dispatch(createEnquiryError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const getEnquiry = useCallback(async (id: number) => {
    dispatch(getEnquiryPending());

    try {
      const enquiry = await httpClient.get<Enquiry>(
        apiEndpoints.enquiries.getById(id),
      );
      dispatch(getEnquirySuccess(enquiry));
    } catch (error) {
      dispatch(getEnquiryError(getErrorMessage(error)));
    }
  }, []);

  const respondToEnquiry = useCallback(
    async (id: number, input: RespondToEnquiryInput) => {
      dispatch(enquiryActionPending());

      try {
        const enquiry = await httpClient.post<Enquiry, RespondToEnquiryInput>(
          apiEndpoints.enquiries.respond(id),
          input,
        );
        dispatch(enquiryActionSuccess(enquiry));
        return true;
      } catch (error) {
        dispatch(enquiryActionError(getErrorMessage(error)));
        return false;
      }
    },
    [],
  );

  const recordFollowUp = useCallback(
    async (id: number, input: CreateEnquiryFollowUpInput) => {
      dispatch(enquiryActionPending());

      try {
        await httpClient.post<void, CreateEnquiryFollowUpInput>(
          apiEndpoints.enquiries.recordFollowUp(id),
          input,
        );
        const enquiry = await httpClient.get<Enquiry>(
          apiEndpoints.enquiries.getById(id),
        );
        dispatch(enquiryActionSuccess(enquiry));
        return true;
      } catch (error) {
        dispatch(enquiryActionError(getErrorMessage(error)));
        return false;
      }
    },
    [],
  );

  const closeEnquiry = useCallback(async (id: number) => {
    dispatch(enquiryActionPending());

    try {
      const enquiry = await httpClient.post<Enquiry, Record<string, never>>(
        apiEndpoints.enquiries.close(id),
        {},
      );
      dispatch(enquiryActionSuccess(enquiry));
      return true;
    } catch (error) {
      dispatch(enquiryActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const reopenEnquiry = useCallback(async (id: number) => {
    dispatch(enquiryActionPending());

    try {
      const enquiry = await httpClient.post<Enquiry, Record<string, never>>(
        apiEndpoints.enquiries.reopen(id),
        {},
      );
      dispatch(enquiryActionSuccess(enquiry));
      return true;
    } catch (error) {
      dispatch(enquiryActionError(getErrorMessage(error)));
      return false;
    }
  }, []);

  const actions = useMemo(
    () => ({
      closeEnquiry,
      createEnquiry,
      getEnquiries,
      getEnquiry,
      recordFollowUp,
      reopenEnquiry,
      respondToEnquiry,
    }),
    [
      closeEnquiry,
      createEnquiry,
      getEnquiries,
      getEnquiry,
      recordFollowUp,
      reopenEnquiry,
      respondToEnquiry,
    ],
  );

  return (
    <EnquiriesStateContext.Provider value={state}>
      <EnquiriesActionsContext.Provider value={actions}>
        {children}
      </EnquiriesActionsContext.Provider>
    </EnquiriesStateContext.Provider>
  );
};

export const useEnquiriesState = () => {
  return useContext(EnquiriesStateContext);
};

export const useEnquiriesActions = () => {
  const context = useContext(EnquiriesActionsContext);

  if (!context) {
    throw new Error("useEnquiriesActions must be used within an EnquiriesProvider.");
  }

  return context;
};

export type {
  CreateEnquiryFollowUpInput,
  CreateEnquiryInput,
  Enquiry,
  EnquiryFollowUpOutcome,
  EnquiryFollowUp,
  EnquiryStatus,
  RespondToEnquiryInput,
} from "./context";

import type { Enquiry } from "./context";

export const EnquiriesActionTypes = {
  enquiryActionError: "enquiries/enquiryActionError",
  enquiryActionPending: "enquiries/enquiryActionPending",
  enquiryActionSuccess: "enquiries/enquiryActionSuccess",
  createEnquiryError: "enquiries/createEnquiryError",
  createEnquiryPending: "enquiries/createEnquiryPending",
  createEnquirySuccess: "enquiries/createEnquirySuccess",
  getEnquiriesError: "enquiries/getEnquiriesError",
  getEnquiriesPending: "enquiries/getEnquiriesPending",
  getEnquiriesSuccess: "enquiries/getEnquiriesSuccess",
  getEnquiryError: "enquiries/getEnquiryError",
  getEnquiryPending: "enquiries/getEnquiryPending",
  getEnquirySuccess: "enquiries/getEnquirySuccess",
  getSalesReadyEnquiriesError: "enquiries/getSalesReadyEnquiriesError",
  getSalesReadyEnquiriesPending: "enquiries/getSalesReadyEnquiriesPending",
  getSalesReadyEnquiriesSuccess: "enquiries/getSalesReadyEnquiriesSuccess",
} as const;

export type EnquiriesAction =
  | {
      type: typeof EnquiriesActionTypes.enquiryActionError;
      payload: string;
    }
  | {
      type: typeof EnquiriesActionTypes.enquiryActionPending;
    }
  | {
      type: typeof EnquiriesActionTypes.enquiryActionSuccess;
      payload: Enquiry;
    }
  | {
      type: typeof EnquiriesActionTypes.createEnquiryError;
      payload: string;
    }
  | {
      type: typeof EnquiriesActionTypes.createEnquiryPending;
    }
  | {
      type: typeof EnquiriesActionTypes.createEnquirySuccess;
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquiriesError;
      payload: string;
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquiriesPending;
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquiriesSuccess;
      payload: Enquiry[];
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquiryError;
      payload: string;
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquiryPending;
    }
  | {
      type: typeof EnquiriesActionTypes.getEnquirySuccess;
      payload: Enquiry;
    }
  | {
      type: typeof EnquiriesActionTypes.getSalesReadyEnquiriesError;
      payload: string;
    }
  | {
      type: typeof EnquiriesActionTypes.getSalesReadyEnquiriesPending;
    }
  | {
      type: typeof EnquiriesActionTypes.getSalesReadyEnquiriesSuccess;
      payload: Enquiry[];
    };

export const enquiryActionError = (message: string): EnquiriesAction => ({
  type: EnquiriesActionTypes.enquiryActionError,
  payload: message,
});

export const enquiryActionPending = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.enquiryActionPending,
});

export const enquiryActionSuccess = (enquiry: Enquiry): EnquiriesAction => ({
  type: EnquiriesActionTypes.enquiryActionSuccess,
  payload: enquiry,
});

export const createEnquiryError = (message: string): EnquiriesAction => ({
  type: EnquiriesActionTypes.createEnquiryError,
  payload: message,
});

export const createEnquiryPending = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.createEnquiryPending,
});

export const createEnquirySuccess = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.createEnquirySuccess,
});

export const getEnquiriesError = (message: string): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquiriesError,
  payload: message,
});

export const getEnquiriesPending = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquiriesPending,
});

export const getEnquiriesSuccess = (enquiries: Enquiry[]): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquiriesSuccess,
  payload: enquiries,
});

export const getEnquiryError = (message: string): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquiryError,
  payload: message,
});

export const getEnquiryPending = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquiryPending,
});

export const getEnquirySuccess = (enquiry: Enquiry): EnquiriesAction => ({
  type: EnquiriesActionTypes.getEnquirySuccess,
  payload: enquiry,
});

export const getSalesReadyEnquiriesError = (
  message: string,
): EnquiriesAction => ({
  type: EnquiriesActionTypes.getSalesReadyEnquiriesError,
  payload: message,
});

export const getSalesReadyEnquiriesPending = (): EnquiriesAction => ({
  type: EnquiriesActionTypes.getSalesReadyEnquiriesPending,
});

export const getSalesReadyEnquiriesSuccess = (
  enquiries: Enquiry[],
): EnquiriesAction => ({
  type: EnquiriesActionTypes.getSalesReadyEnquiriesSuccess,
  payload: enquiries,
});

import type { Enquiry } from "./context";

export const EnquiriesActionTypes = {
  createEnquiryError: "enquiries/createEnquiryError",
  createEnquiryPending: "enquiries/createEnquiryPending",
  createEnquirySuccess: "enquiries/createEnquirySuccess",
  getEnquiriesError: "enquiries/getEnquiriesError",
  getEnquiriesPending: "enquiries/getEnquiriesPending",
  getEnquiriesSuccess: "enquiries/getEnquiriesSuccess",
} as const;

export type EnquiriesAction =
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
    };

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

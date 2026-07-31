import { createContext } from "react";

export type EnquiryStatus = 0 | 1 | 2;
export type EnquiryFollowUpOutcome = 0 | 1 | 2 | 3 | 4;

export type EnquiryFollowUp = {
  id: number;
  enquiryId: number;
  followUpDate: string;
  followUpByMemberId: number | null;
  followUpNotes: string;
  outcome: EnquiryFollowUpOutcome;
  outcomeText: string;
  conversionProbability: number;
  isResolved: boolean;
};

export type Enquiry = {
  id: number;
  customerId: number;
  productId: number;
  message: string;
  response: string | null;
  status: EnquiryStatus;
  createdAt: string;
  isClosed: boolean;
  isPending: boolean;
  assignedToMemberId: number | null;
  isConverted: boolean;
  convertedAt: string | null;
  conversionProbability: number;
  lastFollowUpDate: string | null;
  followUpCount: number;
  isSalesReady: boolean;
  followUps: EnquiryFollowUp[];
};

export type CreateEnquiryInput = {
  customerId: number;
  productId: number;
  message: string;
};

export type RespondToEnquiryInput = {
  response: string;
};

export type CreateEnquiryFollowUpInput = {
  followUpByMemberId: number | null;
  followUpNotes: string;
  outcome: EnquiryFollowUpOutcome;
};

export type EnquiriesState = {
  actionErrorMessage: string | null;
  createErrorMessage: string | null;
  enquiries: Enquiry[];
  isActionError: boolean;
  isActionPending: boolean;
  isActionSuccess: boolean;
  isCreateError: boolean;
  isCreatePending: boolean;
  isCreateSuccess: boolean;
  isSelectedError: boolean;
  isSelectedPending: boolean;
  isSelectedSuccess: boolean;
  isSalesReadyError: boolean;
  isSalesReadyPending: boolean;
  isSalesReadySuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  loadErrorMessage: string | null;
  salesReadyEnquiries: Enquiry[];
  salesReadyErrorMessage: string | null;
  selectedEnquiry: Enquiry | null;
  selectedErrorMessage: string | null;
};

export type EnquiriesActions = {
  closeEnquiry: (id: number) => Promise<boolean>;
  convertEnquiryToCustomer: (id: number) => Promise<boolean>;
  createEnquiry: (input: CreateEnquiryInput) => Promise<boolean>;
  getEnquiries: () => Promise<void>;
  getMyEnquiries: () => Promise<void>;
  getEnquiry: (id: number) => Promise<void>;
  getSalesReadyEnquiries: () => Promise<void>;
  recordFollowUp: (
    id: number,
    input: CreateEnquiryFollowUpInput,
  ) => Promise<boolean>;
  reopenEnquiry: (id: number) => Promise<boolean>;
  respondToEnquiry: (
    id: number,
    input: RespondToEnquiryInput,
  ) => Promise<boolean>;
};

export const initialEnquiriesState: EnquiriesState = {
  actionErrorMessage: null,
  createErrorMessage: null,
  enquiries: [],
  isActionError: false,
  isActionPending: false,
  isActionSuccess: false,
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: false,
  isSalesReadyError: false,
  isSalesReadyPending: false,
  isSalesReadySuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  loadErrorMessage: null,
  salesReadyEnquiries: [],
  salesReadyErrorMessage: null,
  selectedEnquiry: null,
  selectedErrorMessage: null,
};

export const EnquiriesStateContext =
  createContext<EnquiriesState>(initialEnquiriesState);

export const EnquiriesActionsContext =
  createContext<EnquiriesActions | null>(null);

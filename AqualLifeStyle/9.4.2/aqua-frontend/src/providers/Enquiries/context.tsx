import { createContext } from "react";

export type EnquiryStatus = 0 | 1 | 2;

export type EnquiryFollowUp = {
  id: number;
  enquiryId: number;
  followUpDate: string;
  followUpByMemberId: number | null;
  followUpNotes: string;
  outcome: number;
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

export type EnquiriesState = {
  createErrorMessage: string | null;
  enquiries: Enquiry[];
  isCreateError: boolean;
  isCreatePending: boolean;
  isCreateSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  loadErrorMessage: string | null;
};

export type EnquiriesActions = {
  createEnquiry: (input: CreateEnquiryInput) => Promise<boolean>;
  getEnquiries: () => Promise<void>;
};

export const initialEnquiriesState: EnquiriesState = {
  createErrorMessage: null,
  enquiries: [],
  isCreateError: false,
  isCreatePending: false,
  isCreateSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  loadErrorMessage: null,
};

export const EnquiriesStateContext =
  createContext<EnquiriesState>(initialEnquiriesState);

export const EnquiriesActionsContext =
  createContext<EnquiriesActions | null>(null);

import { createContext } from "react";

export type OrderIntentStatus = 0 | 1 | 2 | 3;

export type OrderIntent = {
  id: number;
  customerId: number;
  productId: number;
  enquiryId: number | null;
  unitPrice: number;
  reservedPrice: number;
  status: OrderIntentStatus;
  statusText: string;
  createdAt: string;
  reservedAt: string | null;
  cancelledAt: string | null;
  completedAt: string | null;
};

export type OrderIntentsState = {
  actionErrorMessage: string | null;
  isActionError: boolean;
  isActionPending: boolean;
  isActionSuccess: boolean;
  isLoadError: boolean;
  isLoadPending: boolean;
  isLoadSuccess: boolean;
  loadErrorMessage: string | null;
  orderIntents: OrderIntent[];
};

export type OrderIntentsActions = {
  cancelOrderIntent: (id: number) => Promise<boolean>;
  completeOrderIntent: (id: number) => Promise<boolean>;
  createFromEnquiry: (enquiryId: number) => Promise<boolean>;
  getOrderIntents: () => Promise<void>;
};

export const initialOrderIntentsState: OrderIntentsState = {
  actionErrorMessage: null,
  isActionError: false,
  isActionPending: false,
  isActionSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: false,
  loadErrorMessage: null,
  orderIntents: [],
};

export const OrderIntentsStateContext =
  createContext<OrderIntentsState>(initialOrderIntentsState);

export const OrderIntentsActionsContext =
  createContext<OrderIntentsActions | null>(null);

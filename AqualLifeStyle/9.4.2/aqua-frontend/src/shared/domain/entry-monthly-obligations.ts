export type EntryMonthlyObligation = {
  id: string;
  tenantId: number;
  customerId: number;
  customerName: string;
  email: string;
  periodYear: number;
  periodMonth: number;
  amountDue: number;
  outstandingAmount: number;
  currency: string;
  termsVersion: string;
  dueAt: string;
  gracePeriodEndsAt: string;
  status: string;
  markedOverdueAt: string | null;
  paymentId: string | null;
  paidAt: string | null;
  isOwnPayoutEligible: boolean;
};

export type EntryMonthlyObligationCheckout = {
  checkoutId: string;
  obligationId: string;
  periodYear: number;
  periodMonth: number;
  amount: number;
  currency: string;
  checkoutUrl: string;
};

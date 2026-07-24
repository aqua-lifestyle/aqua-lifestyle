export type OnyxLoanWeeklyRequirement = {
  requirementNumber: number;
  minimumAmount: number;
  creditedAmount: number;
  dueAt: string;
  status: string;
  satisfiedAt: string | null;
  markedOverdueAt: string | null;
};

export type OnyxLoanRepayment = {
  paymentId: string;
  amount: number;
  weeklyRequirementNumber: number | null;
  receivedAt: string;
};

export type OnyxLoanAgreement = {
  id: string;
  tenantId: number;
  customerId: number;
  customerName: string;
  email: string;
  status: string;
  termsVersion: string;
  principalAmount: number;
  interestRatePercent: number;
  totalPayableAmount: number;
  repaidAmount: number;
  outstandingAmount: number;
  currency: string;
  offeredAt: string;
  memberAcceptedAt: string | null;
  approvedAt: string | null;
  effectiveAt: string | null;
  repaymentDeadlineAt: string | null;
  settledAt: string | null;
  requiresPayoutHold: boolean;
  weeklyRequirements: OnyxLoanWeeklyRequirement[];
  repayments: OnyxLoanRepayment[];
};

export type MyOnyxLoanAgreements = {
  items: OnyxLoanAgreement[];
};

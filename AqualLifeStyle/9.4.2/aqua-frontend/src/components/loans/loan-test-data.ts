import type { OnyxLoanAgreement } from "@/src/shared/domain/loans";

export const activeLoanAgreement: OnyxLoanAgreement = {
  approvedAt: "2026-07-04T00:00:00Z",
  currency: "ZAR",
  customerId: 10,
  customerName: "Lethabo Mokoena",
  effectiveAt: "2026-07-04T00:00:00Z",
  email: "lethabo@example.com",
  id: "loan-1",
  interestRatePercent: 30,
  memberAcceptedAt: "2026-07-03T00:00:00Z",
  offeredAt: "2026-07-02T00:00:00Z",
  outstandingAmount: 7756,
  principalAmount: 6120,
  repaidAmount: 200,
  repaymentDeadlineAt: "2026-10-04T00:00:00Z",
  repayments: [
    {
      amount: 200,
      paymentId: "payment-1",
      receivedAt: "2026-07-05T00:00:00Z",
      weeklyRequirementNumber: 1,
    },
  ],
  requiresPayoutHold: false,
  settledAt: null,
  status: "Active",
  tenantId: 1,
  termsVersion: "2026-07",
  totalPayableAmount: 7956,
  weeklyRequirements: [
    {
      creditedAmount: 200,
      dueAt: "2026-07-11T00:00:00Z",
      markedOverdueAt: null,
      minimumAmount: 200,
      requirementNumber: 1,
      satisfiedAt: "2026-07-05T00:00:00Z",
      status: "Paid",
    },
  ],
};

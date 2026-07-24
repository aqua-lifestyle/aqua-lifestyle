import type { EntryMonthlyObligation } from "@/src/shared/domain/entry-monthly-obligations";

export const overdueEntryCommitment: EntryMonthlyObligation = {
  amountDue: 600,
  currency: "ZAR",
  customerId: 10,
  customerName: "Lethabo Mokoena",
  dueAt: "2026-07-10T00:00:00Z",
  email: "lethabo@example.com",
  gracePeriodEndsAt: "2026-07-17T00:00:00Z",
  id: "commitment-1",
  isOwnPayoutEligible: false,
  markedOverdueAt: "2026-07-18T00:00:00Z",
  outstandingAmount: 600,
  paidAt: null,
  paymentId: null,
  periodMonth: 7,
  periodYear: 2026,
  status: "Overdue",
  tenantId: 1,
  termsVersion: "2026-07",
};

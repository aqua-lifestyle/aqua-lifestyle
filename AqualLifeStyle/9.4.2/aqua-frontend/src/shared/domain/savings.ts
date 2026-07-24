export type SavingsContribution = {
  amount: number;
  contributedAt: string;
  interestAmount: number;
  interestRatePercent: number;
  paymentId: string;
};

export type SavingsAccount = {
  contributionWindowEndDay: number;
  contributionWindowStartDay: number;
  contributions: SavingsContribution[];
  currency: string;
  customerId: number;
  customerName: string;
  email: string;
  id: string;
  maturedAt: string | null;
  maturityInterestAmount: number | null;
  maturityInterestRatePercent: number;
  maturityPayoutAmount: number | null;
  maturityPrincipalAmount: number | null;
  maturesAt: string;
  minimumContributionAmount: number;
  openedAt: string;
  principalBalance: number;
  projectedInterestAmount: number;
  projectedMaturityAmount: number;
  requiresMaturityProcessing: boolean;
  status: "Active" | "Matured" | "Maturity processing due";
  tenantId: number;
  termsVersion: string;
};

export type MySavingsAccount = {
  account: SavingsAccount | null;
};

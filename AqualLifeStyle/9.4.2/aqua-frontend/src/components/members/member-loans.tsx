"use client";

import { HandCoins } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type {
  MyOnyxLoanAgreements,
  OnyxLoanAgreement,
  OnyxLoanRepayment,
  OnyxLoanWeeklyRequirement,
} from "@/src/shared/domain/loans";
import {
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

const VIEW_PERMISSION = "Aqua.Loans.ViewSelf";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA");

const loanTone = (loan: OnyxLoanAgreement) =>
  loan.requiresPayoutHold
    ? "error"
    : loan.status === "Paid in full"
      ? "success"
      : loan.status === "Active"
        ? "info"
        : "warning";

const LoanDetails = ({ loan }: { loan: OnyxLoanAgreement }) => {
  const requirementColumns = [
    {
      header: "Week",
      key: "requirementNumber",
      render: (item: OnyxLoanWeeklyRequirement) =>
        `Week ${item.requirementNumber}`,
    },
    {
      header: "Due date",
      key: "dueAt",
      render: (item: OnyxLoanWeeklyRequirement) => formatDate(item.dueAt),
    },
    {
      header: "Minimum payment",
      key: "minimumAmount",
      render: (item: OnyxLoanWeeklyRequirement) =>
        formatCurrency(item.minimumAmount, loan.currency),
    },
    {
      header: "Amount credited",
      key: "creditedAmount",
      render: (item: OnyxLoanWeeklyRequirement) =>
        formatCurrency(item.creditedAmount, loan.currency),
    },
    {
      header: "Status",
      key: "status",
      render: (item: OnyxLoanWeeklyRequirement) => (
        <Badge
          tone={
            item.status === "Paid"
              ? "success"
              : item.status === "Overdue"
                ? "error"
                : "warning"
          }
        >
          {item.status}
        </Badge>
      ),
    },
  ];
  const repaymentColumns = [
    {
      header: "Payment date",
      key: "receivedAt",
      render: (item: OnyxLoanRepayment) => formatDate(item.receivedAt),
    },
    {
      header: "Amount",
      key: "amount",
      render: (item: OnyxLoanRepayment) =>
        formatCurrency(item.amount, loan.currency),
    },
    {
      header: "Applied to",
      key: "weeklyRequirementNumber",
      render: (item: OnyxLoanRepayment) =>
        item.weeklyRequirementNumber
          ? `Week ${item.weeklyRequirementNumber}`
          : "Overall loan balance",
    },
  ];

  return (
    <Card className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-xl font-bold">Onyx loan</h2>
          <p className="text-sm text-muted-foreground">
            Offered {formatDate(loan.offeredAt)} · Terms {loan.termsVersion}
          </p>
        </div>
        <Badge tone={loanTone(loan)}>{loan.status}</Badge>
      </div>
      {loan.requiresPayoutHold ? (
        <StatusMessage tone="error">
          Your own earnings payout is on hold because this loan is overdue.
          Your network placement is unchanged.
        </StatusMessage>
      ) : null}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <p className="text-sm text-muted-foreground">Amount borrowed</p>
          <p className="mt-1 text-xl font-bold">
            {formatCurrency(loan.principalAmount, loan.currency)}
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Total to repay</p>
          <p className="mt-1 text-xl font-bold">
            {formatCurrency(loan.totalPayableAmount, loan.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            Includes {loan.interestRatePercent}% interest
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Paid</p>
          <p className="mt-1 text-xl font-bold">
            {formatCurrency(loan.repaidAmount, loan.currency)}
          </p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Still to pay</p>
          <p className="mt-1 text-xl font-bold">
            {formatCurrency(loan.outstandingAmount, loan.currency)}
          </p>
        </div>
      </div>
      <div>
        <h3 className="font-semibold">First four weekly payments</h3>
        <p className="mb-3 text-sm text-muted-foreground">
          Each week is tracked separately; a later payment does not
          automatically clear an earlier missed week.
        </p>
        <DataTable
          columns={requirementColumns}
          data={loan.weeklyRequirements}
          emptyState="Weekly payments begin after Club approval."
          keyExtractor={(item) => String(item.requirementNumber)}
          pageSize={4}
        />
      </div>
      <div>
        <h3 className="font-semibold">Confirmed repayment history</h3>
        <p className="mb-3 text-sm text-muted-foreground">
          Only confirmed payments appear here.
        </p>
        <DataTable
          columns={repaymentColumns}
          data={loan.repayments}
          emptyState="No confirmed repayments are recorded."
          keyExtractor={(item) => item.paymentId}
          pageSize={10}
        />
      </div>
    </Card>
  );
};

export const MemberLoans = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [loans, setLoans] = useState<OnyxLoanAgreement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadLoans = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<MyOnyxLoanAgreements>(
        apiEndpoints.loans.getMyAgreements,
      );
      setLoans(result.items);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Your loan information could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadLoans(), 0);
    return () => window.clearTimeout(task);
  }, [loadLoans]);

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          Your account does not have access to loan information.
        </StatusMessage>
      </main>
    );
  }

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/member", label: "Club Member" },
              { label: "My loans" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">My loans</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Review your Onyx loan terms, weekly payment requirements, confirmed
            repayments, and remaining balance.
          </p>
        </header>
        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {loading ? (
          <Skeleton className="h-96" />
        ) : loans.length ? (
          loans.map((loan) => <LoanDetails key={loan.id} loan={loan} />)
        ) : (
          <EmptyState
            description="No Onyx loan is recorded for your Club Member account."
            icon={HandCoins}
            title="No loan agreements"
          />
        )}
      </div>
    </main>
  );
};

"use client";

import { PiggyBank } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type {
  MySavingsAccount,
  SavingsAccount,
  SavingsContribution,
} from "@/src/shared/domain/savings";
import {
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

const VIEW_PERMISSION = "Aqua.Savings.ViewSelf";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA");

const SummaryCard = ({
  label,
  value,
}: {
  label: string;
  value: string;
}) => (
  <Card>
    <p className="text-sm font-medium text-muted-foreground">{label}</p>
    <p className="mt-2 text-2xl font-bold">{value}</p>
  </Card>
);

const SavingsAccountDetails = ({ account }: { account: SavingsAccount }) => {
  const contributionColumns = [
    {
      header: "Contribution date",
      key: "contributedAt",
      render: (item: SavingsContribution) => formatDate(item.contributedAt),
      sortable: true,
    },
    {
      header: "Amount saved",
      key: "amount",
      render: (item: SavingsContribution) =>
        formatCurrency(item.amount, account.currency),
      sortable: true,
    },
    {
      header: "Interest at maturity",
      key: "interestAmount",
      render: (item: SavingsContribution) => (
        <div>
          <p className="font-medium">
            {formatCurrency(item.interestAmount, account.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            {item.interestRatePercent}% of this contribution
          </p>
        </div>
      ),
      sortable: true,
    },
  ];

  return (
    <>
      {account.requiresMaturityProcessing ? (
        <StatusMessage tone="warning">
          This account has reached its maturity date. Its final payout snapshot
          still needs to be processed; this does not mean money has been sent.
        </StatusMessage>
      ) : null}
      <div className="grid gap-4 md:grid-cols-3">
        <SummaryCard
          label="Amount saved"
          value={formatCurrency(account.principalBalance, account.currency)}
        />
        <SummaryCard
          label="Interest at maturity"
          value={formatCurrency(
            account.projectedInterestAmount,
            account.currency,
          )}
        />
        <SummaryCard
          label="Projected maturity amount"
          value={formatCurrency(
            account.projectedMaturityAmount,
            account.currency,
          )}
        />
      </div>

      <Card className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <p className="text-sm text-muted-foreground">Account status</p>
          <Badge
            className="mt-2"
            tone={account.status === "Active" ? "success" : "warning"}
          >
            {account.status}
          </Badge>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Opened</p>
          <p className="mt-2 font-semibold">{formatDate(account.openedAt)}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Maturity date</p>
          <p className="mt-2 font-semibold">{formatDate(account.maturesAt)}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Contribution window</p>
          <p className="mt-2 font-semibold">
            Days {account.contributionWindowStartDay}–
            {account.contributionWindowEndDay} of each month
          </p>
        </div>
      </Card>

      <Card className="flex flex-col gap-4">
        <div>
          <h2 className="text-xl font-bold">Contribution history</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Only payments confirmed by the payment provider appear here.
          </p>
        </div>
        <DataTable
          columns={contributionColumns}
          data={account.contributions}
          emptyState="No confirmed savings contributions are recorded."
          keyExtractor={(item) => item.paymentId}
          pageSize={10}
        />
      </Card>
    </>
  );
};

export const MemberSavings = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [account, setAccount] = useState<SavingsAccount | null>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadAccount = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<MySavingsAccount>(
        apiEndpoints.savings.getMyAccount,
      );
      setAccount(result.account);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Your savings account could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadAccount(), 0);
    return () => window.clearTimeout(task);
  }, [loadAccount]);

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          Your account does not have access to Club Member savings.
        </StatusMessage>
      </main>
    );
  }

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/member", label: "Club Member" },
              { label: "My savings" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">My savings</h1>
          <p className="mt-2 max-w-3xl text-base text-muted-foreground">
            Review confirmed contributions, projected interest, and your
            12-month maturity date.
          </p>
        </header>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {loading ? (
          <Skeleton className="h-96" />
        ) : account ? (
          <SavingsAccountDetails account={account} />
        ) : (
          <EmptyState
            description="No savings account is currently recorded. Contact the club team if you expected to see one."
            icon={PiggyBank}
            title="No savings account"
          />
        )}
      </div>
    </main>
  );
};

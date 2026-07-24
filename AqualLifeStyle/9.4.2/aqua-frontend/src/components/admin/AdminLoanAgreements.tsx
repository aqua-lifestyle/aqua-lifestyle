"use client";

import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { OnyxLoanAgreement } from "@/src/shared/domain/loans";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type PagedLoans = { items: OnyxLoanAgreement[]; totalCount: number };
const VIEW_PERMISSION = "Aqua.Admin.Loans.View";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { currency, style: "currency" }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA");

export const AdminLoanAgreements = () => {
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
      const result = await httpClient.get<PagedLoans>(
        `${apiEndpoints.loans.getAdminAgreements}?MaxResultCount=100`,
      );
      setLoans(result.items);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Loan agreements could not be loaded.",
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
          You do not have permission to view loan agreements.
        </StatusMessage>
      </main>
    );
  }

  const columns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (loan: OnyxLoanAgreement) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={loan.customerName} size="sm" />
          <div>
            <p className="font-semibold">{loan.customerName}</p>
            <p className="text-xs text-muted-foreground">{loan.email}</p>
          </div>
        </div>
      ),
    },
    {
      header: "Area",
      key: "tenantId",
      render: (loan: OnyxLoanAgreement) => `Area ${loan.tenantId}`,
    },
    {
      header: "Status",
      key: "status",
      render: (loan: OnyxLoanAgreement) => (
        <Badge
          tone={
            loan.requiresPayoutHold
              ? "error"
              : loan.status === "Paid in full"
                ? "success"
                : loan.status === "Active"
                  ? "info"
                  : "warning"
          }
        >
          {loan.status}
        </Badge>
      ),
    },
    {
      header: "Total to repay",
      key: "totalPayableAmount",
      render: (loan: OnyxLoanAgreement) => (
        <div>
          <p className="font-semibold">
            {formatCurrency(loan.totalPayableAmount, loan.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            {loan.interestRatePercent}% interest
          </p>
        </div>
      ),
    },
    {
      header: "Still to pay",
      key: "outstandingAmount",
      render: (loan: OnyxLoanAgreement) =>
        formatCurrency(loan.outstandingAmount, loan.currency),
    },
    {
      header: "Repayment deadline",
      key: "repaymentDeadlineAt",
      render: (loan: OnyxLoanAgreement) =>
        loan.repaymentDeadlineAt
          ? formatDate(loan.repaymentDeadlineAt)
          : "Starts after Club approval",
    },
  ];

  return (
    <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/admin/dashboard", label: "Administration" },
              { label: "Loan agreements" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Loan agreements</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Reconcile Onyx loan balances, payment deadlines, and overdue
            accounts. Offers, approvals, and payments cannot be recorded here.
          </p>
        </header>
        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        <Card>
          {loading ? (
            <Skeleton className="h-80" />
          ) : (
            <DataTable
              columns={columns}
              data={loans}
              emptyState="No persisted Onyx loan agreements were found."
              keyExtractor={(loan) => loan.id}
              pageSize={10}
              searchFn={(loan, query) =>
                `${loan.customerName} ${loan.email} ${loan.status}`
                  .toLowerCase()
                  .includes(query.toLowerCase())
              }
            />
          )}
        </Card>
      </div>
    </main>
  );
};

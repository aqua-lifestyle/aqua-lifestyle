"use client";

import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { SavingsAccount } from "@/src/shared/domain/savings";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type PagedSavingsAccounts = {
  items: SavingsAccount[];
  totalCount: number;
};

const VIEW_PERMISSION = "Aqua.Admin.Savings.View";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

const formatDate = (value: string) =>
  new Date(value).toLocaleDateString("en-ZA");

export const AdminSavingsAccounts = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [accounts, setAccounts] = useState<SavingsAccount[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadAccounts = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }

    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedSavingsAccounts>(
        `${apiEndpoints.savings.getAdminAccounts}?MaxResultCount=100`,
      );
      setAccounts(result.items);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Savings accounts could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadAccounts(), 0);
    return () => window.clearTimeout(task);
  }, [loadAccounts]);

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to view savings accounts.
        </StatusMessage>
      </main>
    );
  }

  const columns = [
    {
      header: "Club Member",
      key: "customerName",
      render: (account: SavingsAccount) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={account.customerName} size="sm" />
          <div>
            <p className="font-semibold">{account.customerName}</p>
            <p className="text-xs text-muted-foreground">{account.email}</p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Area",
      key: "tenantId",
      render: (account: SavingsAccount) => `Area ${account.tenantId}`,
      sortable: true,
    },
    {
      header: "Account status",
      key: "status",
      render: (account: SavingsAccount) => (
        <Badge
          tone={
            account.status === "Active"
              ? "success"
              : account.requiresMaturityProcessing
                ? "warning"
                : "neutral"
          }
        >
          {account.status}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Savings balance",
      key: "principalBalance",
      render: (account: SavingsAccount) => (
        <div>
          <p className="font-semibold">
            {formatCurrency(account.principalBalance, account.currency)}
          </p>
          <p className="text-xs text-muted-foreground">
            {formatCurrency(
              account.projectedInterestAmount,
              account.currency,
            )}{" "}
            projected interest
          </p>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Projected maturity amount",
      key: "projectedMaturityAmount",
      render: (account: SavingsAccount) =>
        formatCurrency(account.projectedMaturityAmount, account.currency),
      sortable: true,
    },
    {
      header: "Maturity date",
      key: "maturesAt",
      render: (account: SavingsAccount) => (
        <div>
          <p className="font-medium">{formatDate(account.maturesAt)}</p>
          <p className="text-xs text-muted-foreground">
            {account.contributions.length} confirmed contribution
            {account.contributions.length === 1 ? "" : "s"}
          </p>
        </div>
      ),
      sortable: true,
    },
  ];

  return (
    <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/admin/dashboard", label: "Administration" },
              { label: "Savings accounts" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Savings accounts</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Reconcile confirmed Club Member savings, projected interest, and
            upcoming maturity dates. Contributions and payouts cannot be
            recorded from this screen.
          </p>
        </header>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        <Card>
          {loading ? (
            <Skeleton className="h-80" />
          ) : (
            <DataTable
              columns={columns}
              data={accounts}
              emptyState="No persisted savings accounts were found."
              keyExtractor={(account) => account.id}
              pageSize={10}
              searchFn={(account, query) =>
                `${account.customerName} ${account.email} ${account.status}`
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

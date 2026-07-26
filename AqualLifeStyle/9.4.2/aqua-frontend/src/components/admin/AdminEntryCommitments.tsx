"use client";

import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { EntryMonthlyObligation } from "@/src/shared/domain/entry-monthly-obligations";
import {
  Breadcrumb,
  Card,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { EntryCommitmentsTable } from "../entry-commitments/EntryCommitmentsTable";
import { TruncatedResultsWarning } from "./TruncatedResultsWarning";

type PagedCommitments = {
  items: EntryMonthlyObligation[];
  totalCount: number;
};
const VIEW_PERMISSION = "Aqua.Admin.EntryMonthlyObligations.View";

export const AdminEntryCommitments = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [items, setItems] = useState<EntryMonthlyObligation[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const load = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedCommitments>(
        `${apiEndpoints.entryMonthlyObligations.getAdminObligations}?MaxResultCount=100`,
      );
      setItems(result.items);
      setTotalCount(result.totalCount);
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "AQGreen commitments could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(task);
  }, [load]);

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          You do not have permission to view AQGreen commitments.
        </StatusMessage>
      </main>
    );
  }

  return (
    <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/admin/dashboard", label: "Administration" },
              { label: "AQGreen commitments" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">AQGreen commitments</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Reconcile recorded monthly commitments, grace periods, and overdue
            balances. Payments cannot be recorded from this screen.
          </p>
        </header>
        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        <TruncatedResultsWarning
          loadedCount={items.length}
          totalCount={totalCount}
        />
        <Card>
          {loading ? (
            <Skeleton className="h-80" />
          ) : (
            <EntryCommitmentsTable
              obligations={items}
              showClubMember
            />
          )}
        </Card>
      </div>
    </main>
  );
};

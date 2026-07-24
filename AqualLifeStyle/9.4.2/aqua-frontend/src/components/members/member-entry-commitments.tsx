"use client";

import { CalendarCheck } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { EntryMonthlyObligation } from "@/src/shared/domain/entry-monthly-obligations";
import {
  Breadcrumb,
  Card,
  EmptyState,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { EntryCommitmentsTable } from "../entry-commitments/EntryCommitmentsTable";

const VIEW_PERMISSION = "Aqua.EntryMonthlyObligations.ViewSelf";

export const MemberEntryCommitments = () => {
  const { session } = useAuthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [items, setItems] = useState<EntryMonthlyObligation[]>([]);
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
      setItems(
        await httpClient.get<EntryMonthlyObligation[]>(
          apiEndpoints.entryMonthlyObligations.getMyObligations,
        ),
      );
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Your Entry commitments could not be loaded.",
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
          Your account does not have access to Entry commitments.
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
              { href: "/member/programmes", label: "My programmes" },
              { label: "Entry commitments" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold">Entry commitments</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Review recorded monthly commitments, due dates, grace periods, and
            confirmed payment status.
          </p>
        </header>
        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {loading ? (
          <Skeleton className="h-80" />
        ) : items.length ? (
          <Card>
            <EntryCommitmentsTable obligations={items} />
          </Card>
        ) : (
          <EmptyState
            description="No Entry monthly commitments are currently recorded."
            icon={CalendarCheck}
            title="No Entry commitments"
          />
        )}
      </div>
    </main>
  );
};

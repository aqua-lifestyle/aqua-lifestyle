"use client";

import { PiggyBank } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useAuthState,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

export const MemberSavings = () => {
  const { getSavingsWindowStatuses } = useMembershipsActions();
  const {
    isSavingsWindowStatusesError,
    isSavingsWindowStatusesPending,
    savingsWindowStatuses,
    savingsWindowStatusesErrorMessage,
  } = useMembershipsState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.Memberships") ?? false;

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view savings.
          </StatusMessage>
        </div>
      </main>
    );
  }

  useEffect(() => {
    void getSavingsWindowStatuses();
  }, [getSavingsWindowStatuses]);

  const customerSavings = useMemo(() => {
    const currentUserId = session?.user?.id ?? null;
    if (currentUserId === null) return [];
    return savingsWindowStatuses.filter((s) => s.tier === currentUserId % 4);
  }, [savingsWindowStatuses, session?.user?.id]);

  const tableColumns = [
    {
      header: "Tier",
      key: "tierName",
      render: (savings: typeof customerSavings[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={savings.tierName} size="sm" />
          <div>
            <p className="font-semibold text-foreground">{savings.tierName}</p>
            <p className="text-xs text-muted-foreground">
              Tier {savings.tier}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "statusLabel",
      render: (savings: typeof customerSavings[number]) => (
        <Badge tone={savings.isSavingsWindowOpen ? "success" : "neutral"}>
          {savings.statusLabel}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Window",
      key: "savingsWindowOpenDay",
      render: (savings: typeof customerSavings[number]) => (
        <span className="text-sm">
          Day {savings.savingsWindowOpenDay} - {savings.savingsWindowCloseDay}
        </span>
      ),
      sortable: true,
    },
    {
      header: "As of",
      key: "asOfDate",
      render: (savings: typeof customerSavings[number]) => (
        <span className="text-sm">
          {new Date(savings.asOfDate).toLocaleDateString()}
        </span>
      ),
      sortable: true,
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/member", label: "Member" },
                { label: "My savings" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">My savings</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              View your savings window status and track interest.
            </p>
          </div>
        </header>

        {isSavingsWindowStatusesPending ? (
          <Skeleton className="h-96" />
        ) : isSavingsWindowStatusesError ? (
          <StatusMessage tone="error">
            {savingsWindowStatusesErrorMessage ?? "Unable to load savings."}
          </StatusMessage>
        ) : customerSavings.length === 0 ? (
          <EmptyState
            description="You have no savings records."
            icon={PiggyBank}
            title="No savings"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <DataTable
              columns={tableColumns}
              data={customerSavings}
              emptyState="You have no savings records."
              keyExtractor={(savings) => savings.tier}
              pageSize={10}
              searchFn={(savings, query) =>
                `${savings.tierName} Tier ${savings.tier}`
                  .toLowerCase()
                  .includes(query.toLowerCase())
              }
            />
          </Card>
        )}
      </div>
    </main>
  );
};

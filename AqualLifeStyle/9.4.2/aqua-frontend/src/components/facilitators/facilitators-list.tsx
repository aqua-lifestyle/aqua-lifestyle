"use client";

import { UserPlus } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useFacilitatorsActions,
  useFacilitatorsState,
  useAuthState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type FacilitatorRankFilter = "all" | "0" | "1" | "2" | "3" | "4" | "5" | "6";

const rankLabel = (value: number) => {
  const ranks = [
    "Bronze",
    "Gold",
    "Pearl",
    "Sapphire",
    "Ruby",
    "Platinum",
    "Premier T60",
  ];
  return ranks[value] ?? `Rank ${value}`;
};

const rankTone = (value: number): "neutral" | "success" | "info" | "warning" => {
  if (value >= 5) return "success";
  if (value >= 3) return "info";
  if (value >= 1) return "warning";
  return "neutral";
};

export const FacilitatorsList = () => {
  const [rankFilter, setRankFilter] = useState<FacilitatorRankFilter>("all");
  const { getFacilitators } = useFacilitatorsActions();
  const {
    facilitators,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useFacilitatorsState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.Facilitators") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getFacilitators();
  }, [getFacilitators]);

  const filteredFacilitators = useMemo(() => {
    return facilitators.filter((facilitator) => {
      const matchesRank =
        rankFilter === "all" || facilitator.rank === Number(rankFilter);
      return matchesRank;
    });
  }, [facilitators, rankFilter]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view Facilitators.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const tableColumns = [
    {
      header: "Facilitator",
      key: "customerId",
      render: (facilitator: typeof filteredFacilitators[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`F ${facilitator.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">
              Customer #{facilitator.customerId}
            </p>
            <p className="text-xs text-muted-foreground">
              Area Leader #{facilitator.areaLeaderId}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Rank",
      key: "rank",
      render: (facilitator: typeof filteredFacilitators[number]) => (
        <Badge tone={rankTone(facilitator.rank)}>
          {rankLabel(facilitator.rank)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Referrals",
      key: "directReferrals",
      render: (facilitator: typeof filteredFacilitators[number]) => (
        <span className="text-sm">
          {facilitator.directReferrals} direct / {facilitator.indirectReferrals} indirect
        </span>
      ),
      sortable: true,
    },
    {
      header: "Award Balance",
      key: "awardBalance",
      render: (facilitator: typeof filteredFacilitators[number]) => (
        <span className="text-sm">{facilitator.awardBalance.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (facilitator: typeof filteredFacilitators[number]) => (
        <div className="flex items-center gap-2">
          <LinkButton href={`/facilitator/${facilitator.id}`} size="sm" variant="outline">
            Open
          </LinkButton>
        </div>
      ),
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[{ href: "/", label: "Dashboard" }, { label: "Facilitators" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Facilitators</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Manage facilitators, track referrals, and monitor commissions.
            </p>
          </div>
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <UserPlus className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Facilitators</p>
              <p className="text-2xl font-bold">{facilitators.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <UserPlus className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Referrals</p>
              <p className="text-2xl font-bold">
                {facilitators.reduce((sum, f) => sum + f.directReferrals, 0)}
              </p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <UserPlus className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Awards</p>
              <p className="text-2xl font-bold">
                {facilitators.reduce((sum, f) => sum + f.awardBalance, 0).toFixed(2)}
              </p>
            </div>
          </Card>
        </section>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load facilitators."}
          </StatusMessage>
        ) : facilitators.length === 0 ? (
          <EmptyState
            description="No facilitators found."
            icon={UserPlus}
            title="No facilitators"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Rank"
                name="rankFilter"
                onChange={(event) => setRankFilter(event.target.value as FacilitatorRankFilter)}
                value={rankFilter}
              >
                <option value="all">All ranks</option>
                <option value="0">Bronze</option>
                <option value="1">Gold</option>
                <option value="2">Pearl</option>
                <option value="3">Sapphire</option>
                <option value="4">Ruby</option>
                <option value="5">Platinum</option>
                <option value="6">Premier T60</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredFacilitators}
              emptyState="No facilitators match these filters."
              keyExtractor={(facilitator) => facilitator.id}
              pageSize={10}
              searchFn={(facilitator, query) =>
                `Customer #${facilitator.customerId} ${rankLabel(facilitator.rank)}`
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

"use client";

import { Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useAreaLeadersActions,
  useAreaLeadersState,
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

type LicenseTypeOption = { label: string; value: string };

const licenseTypeOptions: LicenseTypeOption[] = [
  { label: "Entre Level", value: "0" },
  { label: "Area Independent Leader", value: "1" },
];

const licenseTypeLabel = (value: number) =>
  value === 0 ? "Entre Level" : "Area Independent Leader";

const rankLabel = (value: number) => {
  const ranks = [
    "Ruby",
    "Emerald",
    "Premier",
    "Diamond",
    "VIP",
    "Presidential",
    "Chairman's Circle",
    "Ambassador",
  ];
  return ranks[value] ?? `Rank ${value}`;
};

export const AreaLeadersList = () => {
  const [licenseFilter, setLicenseFilter] = useState("all");
  const { getAreaLeaders } = useAreaLeadersActions();
  const {
    areaLeaders,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useAreaLeadersState();

  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaLeaders") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getAreaLeaders();
  }, [getAreaLeaders]);

  const filteredAreaLeaders = useMemo(() => {
    return areaLeaders.filter((areaLeader) => {
      const matchesLicense =
        licenseFilter === "all" || areaLeader.licenseType === Number(licenseFilter);
      return matchesLicense;
    });
  }, [areaLeaders, licenseFilter]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view Area Leaders.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const tableColumns = [
    {
      header: "Area Leader",
      key: "customerId",
      render: (areaLeader: typeof filteredAreaLeaders[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`AL ${areaLeader.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">Customer #{areaLeader.customerId}</p>
            <p className="text-xs text-muted-foreground">
              License: {licenseTypeLabel(areaLeader.licenseType)}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Rank",
      key: "rank",
      render: (areaLeader: typeof filteredAreaLeaders[number]) => (
        <Badge tone={areaLeader.rank >= 4 ? "success" : "neutral"}>
          {rankLabel(areaLeader.rank)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Referrals",
      key: "directReferrals",
      render: (areaLeader: typeof filteredAreaLeaders[number]) => (
        <span className="text-sm">
          {areaLeader.directReferrals} direct / {areaLeader.indirectReferrals} indirect
        </span>
      ),
      sortable: true,
    },
    {
      header: "Order Target",
      key: "orderTarget",
      render: (areaLeader: typeof filteredAreaLeaders[number]) => (
        <span className="text-sm">{areaLeader.orderTarget}</span>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (areaLeader: typeof filteredAreaLeaders[number]) => (
        <div className="flex items-center gap-2">
          <LinkButton href={`/area-leader/${areaLeader.id}`} size="sm" variant="outline">
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
              items={[{ href: "/", label: "Dashboard" }, { label: "Area Leaders" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Area Leaders</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Manage area leaders, review performance, and promote ranks.
            </p>
          </div>
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Area Leaders</p>
              <p className="text-2xl font-bold">{areaLeaders.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Direct Referrals</p>
              <p className="text-2xl font-bold">
                {areaLeaders.reduce((sum, al) => sum + al.directReferrals, 0)}
              </p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Indirect Referrals</p>
              <p className="text-2xl font-bold">
                {areaLeaders.reduce((sum, al) => sum + al.indirectReferrals, 0)}
              </p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-muted p-3 text-muted-foreground">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Order Target</p>
              <p className="text-2xl font-bold">
                {areaLeaders.reduce((sum, al) => sum + al.orderTarget, 0)}
              </p>
            </div>
          </Card>
        </section>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load area leaders."}
          </StatusMessage>
        ) : areaLeaders.length === 0 ? (
          <EmptyState
            description="No area leaders found."
            icon={Users}
            title="No area leaders"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="License Type"
                name="licenseFilter"
                onChange={(event) => setLicenseFilter(event.target.value)}
                value={licenseFilter}
              >
                <option value="all">All license types</option>
                {licenseTypeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredAreaLeaders}
              emptyState="No area leaders match these filters."
              keyExtractor={(areaLeader) => areaLeader.id}
              pageSize={10}
              searchFn={(areaLeader, query) =>
                `Customer #${areaLeader.customerId} ${rankLabel(areaLeader.rank)}`
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

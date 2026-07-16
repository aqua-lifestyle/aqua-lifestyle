"use client";

import { Building2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useAreaSpacesActions,
  useAreaSpacesState,
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

type AreaSpaceStatusFilter = "all" | "applied" | "under_review" | "approved" | "suspended";

const statusLabel = (value: number) => {
  const labels = ["Applied", "Under Review", "Approved", "Suspended"];
  return labels[value] ?? `Status ${value}`;
};

const statusTone = (value: number): "neutral" | "info" | "success" | "error" => {
  if (value === 2) return "success";
  if (value === 3) return "error";
  if (value === 1) return "info";
  return "neutral";
};

export const AreaSpacesList = () => {
  const [statusFilter, setStatusFilter] = useState<AreaSpaceStatusFilter>("all");
  const { getAreaSpaces } = useAreaSpacesActions();
  const {
    areaSpaces,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useAreaSpacesState();

  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.AreaSpaces") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getAreaSpaces();
  }, [getAreaSpaces]);

  const filteredAreaSpaces = useMemo(() => {
    return areaSpaces.filter((areaSpace) => {
      const matchesStatus =
        statusFilter === "all" || areaSpace.status === Number(statusFilter);
      return matchesStatus;
    });
  }, [areaSpaces, statusFilter]);

  if (!hasPermission) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You do not have permission to view Area Spaces.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const tableColumns = [
    {
      header: "Area Space",
      key: "addressLine",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`AS ${areaSpace.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">{areaSpace.addressLine}</p>
            <p className="text-xs text-muted-foreground">
              Capacity: {areaSpace.capacity}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "status",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <Badge tone={statusTone(areaSpace.status)}>
          {statusLabel(areaSpace.status)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Interested club members",
      key: "interestedMembers",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <span className="text-sm">{areaSpace.interestedMembers}</span>
      ),
      sortable: true,
    },
    {
      header: "Presentations",
      key: "presentationsCompleted",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <span className="text-sm">
          {areaSpace.presentationsCompleted} / 4
        </span>
      ),
      sortable: true,
    },
    {
      header: "Startup Orders",
      key: "startupOrdersCompleted",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <span className="text-sm">
          {areaSpace.startupOrdersCompleted} / 20
        </span>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (areaSpace: typeof filteredAreaSpaces[number]) => (
        <div className="flex items-center gap-2">
          <LinkButton href={`/area-leader/area-spaces/${areaSpace.id}`} size="sm" variant="outline">
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
              items={[{ href: "/", label: "Dashboard" }, { label: "Area Spaces" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Area Spaces</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Manage area space applications, review progress, and approve spaces.
            </p>
          </div>
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Building2 className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total Spaces</p>
              <p className="text-2xl font-bold">{areaSpaces.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-info/10 p-3 text-info">
              <Building2 className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Under Review</p>
              <p className="text-2xl font-bold">
                {areaSpaces.filter((as) => as.status === 1).length}
              </p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Building2 className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Approved</p>
              <p className="text-2xl font-bold">
                {areaSpaces.filter((as) => as.status === 2).length}
              </p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-error/10 p-3 text-error">
              <Building2 className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Suspended</p>
              <p className="text-2xl font-bold">
                {areaSpaces.filter((as) => as.status === 3).length}
              </p>
            </div>
          </Card>
        </section>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load area spaces."}
          </StatusMessage>
        ) : areaSpaces.length === 0 ? (
          <EmptyState
            description="No area spaces found."
            icon={Building2}
            title="No area spaces"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Status"
                name="statusFilter"
                onChange={(event) =>
                  setStatusFilter(event.target.value as AreaSpaceStatusFilter)
                }
                value={statusFilter}
              >
                <option value="all">All statuses</option>
                <option value="applied">Applied</option>
                <option value="under_review">Under Review</option>
                <option value="approved">Approved</option>
                <option value="suspended">Suspended</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredAreaSpaces}
              emptyState="No area spaces match these filters."
              keyExtractor={(areaSpace) => areaSpace.id}
              pageSize={10}
              searchFn={(areaSpace, query) =>
                `${areaSpace.addressLine} Area Space #${areaSpace.id}`
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

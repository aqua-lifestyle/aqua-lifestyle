"use client";

import { KeyRound } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";

type AccessLevel = { description: string | null; displayName: string; grantedPermissions: string[]; id: number; name: string };
type PagedAccessLevels = { items: AccessLevel[]; totalCount: number };

const businessNames: Record<string, string> = {
  Admin: "Area administrator",
  AreaLeader: "Area leader",
  Facilitator: "Facilitator",
  Guest: "Customer",
  Member: "Club member",
  SystemAdmin: "Platform administrator",
};

const describeAccessLevel = (accessLevel: AccessLevel) => accessLevel.description?.trim()
  || `${businessNames[accessLevel.name] ?? accessLevel.displayName} access for the responsibilities assigned to this account type.`;

export const AdminAccessLevels = () => {
  const { session } = useAuthState();
  const canView = session?.user?.permissions?.includes("Pages.Roles") ?? false;
  const [accessLevels, setAccessLevels] = useState<AccessLevel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();

  const loadAccessLevels = useCallback(async () => {
    if (!canView) return;
    setLoading(true);
    try {
      const result = await httpClient.get<PagedAccessLevels>("/api/services/app/Role/GetAll?MaxResultCount=100");
      setAccessLevels(result.items);
      setError(undefined);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "Access levels could not be loaded."));
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadAccessLevels(), 0);
    return () => window.clearTimeout(task);
  }, [loadAccessLevels]);

  if (!canView) return <main className="p-6"><StatusMessage tone="error">Your account does not have access to access-level management.</StatusMessage></main>;

  const columns = [
    { header: "Access level", key: "displayName", sortable: true, render: (accessLevel: AccessLevel) => <div><p className="font-semibold">{businessNames[accessLevel.name] ?? accessLevel.displayName}</p><p className="text-xs text-muted-foreground">{describeAccessLevel(accessLevel)}</p></div> },
    { header: "Responsibilities", key: "grantedPermissions", sortable: true, render: (accessLevel: AccessLevel) => <Badge tone="neutral">{accessLevel.grantedPermissions.length} assigned</Badge> },
  ];

  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Access levels" }]} /><h1 className="mt-2 text-3xl font-bold">Access levels</h1><p className="mt-2 text-muted-foreground">Review the account types available in this area. Assign access from User accounts &amp; access.</p></header>
    <Card><div className="flex items-center gap-3"><KeyRound className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Available access levels</p><p className="text-2xl font-bold">{accessLevels.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={accessLevels} emptyState="No access levels found." keyExtractor={(accessLevel) => accessLevel.id} searchFn={(accessLevel, query) => `${businessNames[accessLevel.name] ?? accessLevel.displayName} ${accessLevel.description ?? ""}`.toLowerCase().includes(query)} />}
  </div></main>;
};

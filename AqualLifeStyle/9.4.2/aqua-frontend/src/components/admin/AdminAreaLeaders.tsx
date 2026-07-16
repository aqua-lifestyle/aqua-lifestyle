"use client";

import { Network } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";

type AdminAreaLeader = {
  id: number; tenantId: number; customerName: string; email: string; licenseType: number;
  rank: number; isApproved: boolean; directReferrals: number; indirectReferrals: number; orderTarget: number;
};
type PagedAreaLeaders = { items: AdminAreaLeader[]; totalCount: number };
type AreaLeaderMutation = "Approve" | "Promote" | "Demote" | "Remove";
const rankNames = ["Ruby", "Emerald", "Premier", "Diamond", "VIP", "Presidential", "Chairman's Circle", "Ambassador"];

export const AdminAreaLeaders = () => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [areaLeaders, setAreaLeaders] = useState<AdminAreaLeader[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const permissions = session?.user?.permissions ?? [];
  const canView = permissions.includes("Aqua.Admin.AreaLeaders.View");
  const can = (operation: AreaLeaderMutation) => permissions.includes(`Aqua.Admin.AreaLeaders.${operation}`);

  const loadAreaLeaders = useCallback(async () => {
    if (!canView) return;
    setLoading(true); setError(undefined);
    try {
      const result = await httpClient.get<PagedAreaLeaders>("/api/services/app/AdminAreaLeader/GetAll?MaxResultCount=100");
      setAreaLeaders(result.items);
    } catch (requestError) { setError(getRequestErrorMessage(requestError, "Area leaders could not be loaded.")); }
    finally { setLoading(false); }
  }, [canView]);
  useEffect(() => {
    const task = window.setTimeout(() => void loadAreaLeaders(), 0);
    return () => window.clearTimeout(task);
  }, [loadAreaLeaders]);

  const mutateAreaLeader = async (operation: AreaLeaderMutation, areaLeader: AdminAreaLeader, justification: string) => {
    const endpoint = `/api/services/app/AdminAreaLeader/${operation}`;
    if (operation === "Remove") await httpClient.delete(endpoint, { id: areaLeader.id, justification });
    else await httpClient.post(endpoint, { id: areaLeader.id, justification });
    toast({ message: `${areaLeader.customerName}'s area leader record was updated.`, title: "Area leader updated", type: "success" });
    await loadAreaLeaders();
  };

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view area leaders.</StatusMessage></main>;
  const columns = [
    { header: "Area leader", key: "customerName", sortable: true, render: (leader: AdminAreaLeader) => <div className="flex items-center gap-3"><Avatar fallback={leader.customerName} size="sm" /><div><p className="font-semibold">{leader.customerName}</p><p className="text-xs text-muted-foreground">{leader.email}</p></div></div> },
    { header: "Area", key: "tenantId", sortable: true, render: (leader: AdminAreaLeader) => `Area ${leader.tenantId}` },
    { header: "Rank", key: "rank", sortable: true, render: (leader: AdminAreaLeader) => rankNames[leader.rank] ?? "Unknown" },
    { header: "Referrals", key: "directReferrals", render: (leader: AdminAreaLeader) => `${leader.directReferrals} direct / ${leader.indirectReferrals} indirect` },
    { header: "Status", key: "isApproved", sortable: true, render: (leader: AdminAreaLeader) => <Badge tone={leader.isApproved ? "success" : "warning"}>{leader.isApproved ? "Approved" : "Pending"}</Badge> },
    { header: "Actions", key: "actions", render: (leader: AdminAreaLeader) => <div className="flex flex-wrap gap-2">
      {!leader.isApproved && can("Approve") ? <AdminJustificationDialog confirmLabel="Approve" description={`Approve ${leader.customerName}'s application and grant area leader access.`} onConfirm={(reason) => mutateAreaLeader("Approve", leader, reason)} title="Approve area leader" triggerLabel="Approve" /> : null}
      {leader.isApproved && can("Promote") ? <AdminJustificationDialog confirmLabel="Promote" description={`Promote ${leader.customerName} to the rank earned by their order target.`} onConfirm={(reason) => mutateAreaLeader("Promote", leader, reason)} title="Promote area leader" triggerLabel="Promote" /> : null}
      {leader.isApproved && can("Demote") ? <AdminJustificationDialog confirmLabel="Demote" description={`Demote ${leader.customerName} by one rank.`} onConfirm={(reason) => mutateAreaLeader("Demote", leader, reason)} title="Demote area leader" triggerLabel="Demote" /> : null}
      {can("Remove") ? <AdminJustificationDialog confirmLabel="Remove" description={`Remove ${leader.customerName} from area-leader administration.`} onConfirm={(reason) => mutateAreaLeader("Remove", leader, reason)} title="Remove area leader" triggerLabel="Remove" variant="danger" /> : null}
    </div> },
  ];

  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Area leaders" }]} /><h1 className="mt-2 text-3xl font-bold">Area leaders</h1><p className="mt-2 text-muted-foreground">Review applications, manage progression, and monitor referral activity.</p></header>
    <Card><div className="flex items-center gap-3"><Network className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Area leaders</p><p className="text-2xl font-bold">{areaLeaders.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={areaLeaders} keyExtractor={(leader) => leader.id} searchFn={(leader, query) => `${leader.customerName} ${leader.email}`.toLowerCase().includes(query)} />}
  </div></main>;
};

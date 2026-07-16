"use client";

import { UserCheck } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { formatCurrency } from "@/src/features/admin/model/dashboard";

type AdminFacilitator = {
  id: number; tenantId: number; customerName: string; email: string; areaLeaderId: number;
  rank: number; isApproved: boolean; directReferrals: number; indirectReferrals: number; awardBalance: number;
};
type PagedFacilitators = { items: AdminFacilitator[]; totalCount: number };
type FacilitatorMutation = "Approve" | "Promote" | "Demote" | "Remove";
const facilitatorRankNames = ["Bronze", "Gold", "Pearl", "Sapphire", "Ruby", "Platinum", "Premier T/60"];

export const AdminFacilitators = () => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [facilitators, setFacilitators] = useState<AdminFacilitator[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const permissions = session?.user?.permissions ?? [];
  const canView = permissions.includes("Aqua.Admin.Facilitators.View");
  const can = (operation: FacilitatorMutation) => permissions.includes(`Aqua.Admin.Facilitators.${operation}`);

  const loadFacilitators = useCallback(async () => {
    if (!canView) return;
    setLoading(true); setError(undefined);
    try {
      const result = await httpClient.get<PagedFacilitators>("/api/services/app/AdminFacilitator/GetAll?MaxResultCount=100");
      setFacilitators(result.items);
    } catch (requestError) { setError(getRequestErrorMessage(requestError, "Facilitators could not be loaded.")); }
    finally { setLoading(false); }
  }, [canView]);
  useEffect(() => {
    const task = window.setTimeout(() => void loadFacilitators(), 0);
    return () => window.clearTimeout(task);
  }, [loadFacilitators]);

  const mutateFacilitator = async (operation: FacilitatorMutation, facilitator: AdminFacilitator, justification: string) => {
    await httpClient.post(`/api/services/app/AdminFacilitator/${operation}`, { id: facilitator.id, justification });
    toast({ message: `${facilitator.customerName}'s facilitator record was updated.`, title: "Facilitator updated", type: "success" });
    await loadFacilitators();
  };

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view facilitators.</StatusMessage></main>;
  const columns = [
    { header: "Facilitator", key: "customerName", sortable: true, render: (facilitator: AdminFacilitator) => <div className="flex items-center gap-3"><Avatar fallback={facilitator.customerName} size="sm" /><div><p className="font-semibold">{facilitator.customerName}</p><p className="text-xs text-muted-foreground">{facilitator.email}</p></div></div> },
    { header: "Area leader", key: "areaLeaderId", sortable: true, render: (facilitator: AdminFacilitator) => `Leader ${facilitator.areaLeaderId}` },
    { header: "Rank", key: "rank", sortable: true, render: (facilitator: AdminFacilitator) => facilitatorRankNames[facilitator.rank] ?? "Unknown" },
    { header: "Referrals", key: "directReferrals", render: (facilitator: AdminFacilitator) => `${facilitator.directReferrals} direct / ${facilitator.indirectReferrals} indirect` },
    { header: "Awards", key: "awardBalance", sortable: true, render: (facilitator: AdminFacilitator) => formatCurrency(facilitator.awardBalance) },
    { header: "Status", key: "isApproved", sortable: true, render: (facilitator: AdminFacilitator) => <Badge tone={facilitator.isApproved ? "success" : "warning"}>{facilitator.isApproved ? "Approved" : "Pending"}</Badge> },
    { header: "Actions", key: "actions", render: (facilitator: AdminFacilitator) => <div className="flex flex-wrap gap-2">
      {!facilitator.isApproved && can("Approve") ? <AdminJustificationDialog confirmLabel="Approve" description={`Approve ${facilitator.customerName}'s application and grant facilitator access.`} onConfirm={(reason) => mutateFacilitator("Approve", facilitator, reason)} title="Approve facilitator" triggerLabel="Approve" /> : null}
      {facilitator.isApproved && can("Promote") ? <AdminJustificationDialog confirmLabel="Promote" description={`Promote ${facilitator.customerName} to the rank earned by direct referrals.`} onConfirm={(reason) => mutateFacilitator("Promote", facilitator, reason)} title="Promote facilitator" triggerLabel="Promote" /> : null}
      {facilitator.isApproved && can("Demote") ? <AdminJustificationDialog confirmLabel="Demote" description={`Demote ${facilitator.customerName} by one rank.`} onConfirm={(reason) => mutateFacilitator("Demote", facilitator, reason)} title="Demote facilitator" triggerLabel="Demote" /> : null}
      {can("Remove") ? <AdminJustificationDialog confirmLabel="Remove" description={`Remove ${facilitator.customerName} from facilitator administration.`} onConfirm={(reason) => mutateFacilitator("Remove", facilitator, reason)} title="Remove facilitator" triggerLabel="Remove" variant="danger" /> : null}
    </div> },
  ];

  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Facilitators" }]} /><h1 className="mt-2 text-3xl font-bold">Facilitators</h1><p className="mt-2 text-muted-foreground">Review applications, manage progression, and track referral awards.</p></header>
    <Card><div className="flex items-center gap-3"><UserCheck className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Facilitators</p><p className="text-2xl font-bold">{facilitators.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={facilitators} keyExtractor={(facilitator) => facilitator.id} searchFn={(facilitator, query) => `${facilitator.customerName} ${facilitator.email}`.toLowerCase().includes(query)} />}
  </div></main>;
};

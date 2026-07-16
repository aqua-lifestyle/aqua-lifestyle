"use client";

import { UsersRound } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { ChangeMemberTierDialog } from "./ChangeMemberTierDialog";
import { EditMemberProfileDialog } from "./EditMemberProfileDialog";

type AdminMember = {
  creationTime: string;
  email: string;
  firstName: string;
  id: number;
  isActive: boolean;
  lastName: string;
  membershipId: number;
  membershipName: string;
  membershipType: number;
  tenantId: number;
  userId: number;
};
type PagedMembers = { items: AdminMember[]; totalCount: number };

export const AdminMembers = () => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [members, setMembers] = useState<AdminMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const permissions = session?.user?.permissions ?? [];
  const canView = permissions.includes("Aqua.Admin.Members.View");
  const canEdit = permissions.includes("Aqua.Admin.Members.Edit");
  const canSuspend = permissions.includes("Aqua.Admin.Members.Suspend");
  const canChangeTier = permissions.includes("Aqua.Admin.Members.ChangeTier");

  const loadMembers = useCallback(async () => {
    if (!canView) return;
    setLoading(true);
    setError(undefined);
    try {
      const result = await httpClient.get<PagedMembers>("/api/services/app/AdminMember/GetAll?MaxResultCount=100");
      setMembers(result.items);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "Club members could not be loaded."));
    } finally {
      setLoading(false);
    }
  }, [canView]);
  useEffect(() => {
    const task = window.setTimeout(() => void loadMembers(), 0);
    return () => window.clearTimeout(task);
  }, [loadMembers]);

  const suspendMember = async (member: AdminMember, justification: string) => {
    await httpClient.post("/api/services/app/AdminMember/Suspend", { id: member.id, justification });
    toast({ message: `${member.firstName} ${member.lastName} has been suspended.`, title: "Club member suspended", type: "success" });
    await loadMembers();
  };
  const refreshAfterMutation = async (message: string) => {
    toast({ message, title: "Club member updated", type: "success" });
    await loadMembers();
  };

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view members.</StatusMessage></main>;
  const columns = [
    { header: "Club member", key: "firstName", sortable: true, render: (member: AdminMember) => <div className="flex items-center gap-3"><Avatar fallback={`${member.firstName} ${member.lastName}`} size="sm" /><div><p className="font-semibold">{member.firstName} {member.lastName}</p><p className="text-xs text-muted-foreground">{member.email}</p></div></div> },
    { header: "Area", key: "tenantId", sortable: true, render: (member: AdminMember) => `Area ${member.tenantId}` },
    { header: "Membership", key: "membershipName", sortable: true, render: (member: AdminMember) => member.membershipName },
    { header: "Status", key: "isActive", sortable: true, render: (member: AdminMember) => <Badge tone={member.isActive ? "success" : "warning"}>{member.isActive ? "Active" : "Suspended"}</Badge> },
    { header: "Joined", key: "creationTime", sortable: true, render: (member: AdminMember) => new Date(member.creationTime).toLocaleDateString() },
    { header: "Actions", key: "actions", render: (member: AdminMember) => <div className="flex flex-wrap gap-2">
      {canEdit ? <EditMemberProfileDialog member={member} onUpdated={() => refreshAfterMutation(`${member.firstName} ${member.lastName}'s profile was updated.`)} /> : null}
      {canChangeTier ? <ChangeMemberTierDialog currentMembershipId={member.membershipId} memberId={member.id} memberName={`${member.firstName} ${member.lastName}`} onChanged={() => refreshAfterMutation(`${member.firstName} ${member.lastName}'s membership plan was changed.`)} /> : null}
      {canSuspend && member.isActive ? <AdminJustificationDialog confirmLabel="Suspend" description={`Suspend ${member.firstName} ${member.lastName}'s customer and login access while retaining their membership record.`} onConfirm={(justification) => suspendMember(member, justification)} title="Suspend club member" triggerLabel="Suspend" variant="danger" /> : null}
    </div> },
  ];

  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Club members" }]} /><h1 className="mt-2 text-3xl font-bold">Club members</h1><p className="mt-2 text-muted-foreground">Maintain club member profiles, membership plans, and account access.</p></header>
    <Card><div className="flex items-center gap-3"><UsersRound className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Club members</p><p className="text-2xl font-bold">{members.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={members} emptyState="No club members found." keyExtractor={(member) => member.id} searchFn={(member, query) => `${member.firstName} ${member.lastName} ${member.email} ${member.membershipName}`.toLowerCase().includes(query)} />}
  </div></main>;
};

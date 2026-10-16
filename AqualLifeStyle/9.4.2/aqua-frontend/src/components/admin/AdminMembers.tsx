"use client";

import { UsersRound } from "lucide-react";
import { useCallback, useEffect, useRef, useState } from "react";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Button, Card, DataTable, Skeleton, StatusMessage, TextField } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { ChangeMemberTierDialog } from "./ChangeMemberTierDialog";
import { EditMemberProfileDialog } from "./EditMemberProfileDialog";
import { AdminActionDisclosure } from "./AdminActionDisclosure";

type AdminMember = {
  areaId: string | null;
  areaName: string | null;
  contactNumber: string | null;
  creationTime: string;
  email: string;
  firstName: string;
  homeAddress: string | null;
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

const PAGE_SIZE = 20;

export const AdminMembers = () => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [members, setMembers] = useState<AdminMember[]>([]);
  const [page, setPage] = useState(1);
  const [keyword, setKeyword] = useState("");
  const [refreshGeneration, setRefreshGeneration] = useState(0);
  const [totalCount, setTotalCount] = useState<number>();
  const latestRequest = useRef(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const permissions = session?.user?.permissions ?? [];
  const canView = permissions.includes("Aqua.Admin.Members.View");
  const canEdit = permissions.includes("Aqua.Admin.Members.Edit");
  const canSuspend = permissions.includes("Aqua.Admin.Members.Suspend");
  const canChangeTier = permissions.includes("Aqua.Admin.Members.ChangeTier");

  const scopeKey = `${session?.user?.id}:${session?.user?.tenantId}:${permissions.join(",")}`;
  const invalidateRequests = useCallback(() => { latestRequest.current++; }, []);
  const loadMembers = useCallback(async () => {
    if (!canView) return;
    const requestId = ++latestRequest.current;
    setLoading(true);
    setError(undefined);
    setTotalCount(undefined);
    setMembers([]);
    try {
      const query = new URLSearchParams({
        MaxResultCount: String(PAGE_SIZE),
        SkipCount: String((page - 1) * PAGE_SIZE),
        Keyword: keyword.trim(),
      });
      const result = await httpClient.get<PagedMembers>(`/api/services/app/AdminMember/GetAll?${query}`);
      if (requestId !== latestRequest.current) return;
      const lastPage = Math.max(1, Math.ceil(result.totalCount / PAGE_SIZE));
      if (page > lastPage) {
        setPage(lastPage);
        return;
      }
      setMembers(result.items);
      setTotalCount(result.totalCount);
    } catch (requestError) {
      if (requestId === latestRequest.current)
        setError(getRequestErrorMessage(requestError, "Club members could not be loaded."));
    } finally {
      if (requestId === latestRequest.current) setLoading(false);
    }
  }, [canView, page, keyword]);
  useEffect(() => {
    const task = window.setTimeout(() => void loadMembers(), 0);
    return () => {
      window.clearTimeout(task);
      invalidateRequests();
    };
  }, [loadMembers, scopeKey, invalidateRequests, refreshGeneration]);

  const suspendMember = async (member: AdminMember, justification: string) => {
    await httpClient.post("/api/services/app/AdminMember/Suspend", { id: member.id, justification });
    toast({ message: `${member.firstName} ${member.lastName} has been suspended.`, title: "Club member suspended", type: "success" });
    setRefreshGeneration((current) => current + 1);
  };
  const refreshAfterMutation = async (message: string) => {
    toast({ message, title: "Club member updated", type: "success" });
    setRefreshGeneration((current) => current + 1);
  };

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view members.</StatusMessage></main>;
  const columns = [
    { header: "Club member", key: "firstName", sortable: true, render: (member: AdminMember) => <div className="flex items-center gap-3"><Avatar fallback={`${member.firstName} ${member.lastName}`} size="sm" /><div><p className="font-semibold">{member.firstName} {member.lastName}</p><p className="text-xs text-muted-foreground">{member.email}</p><p className="text-xs text-muted-foreground">{member.contactNumber ?? "No contact number"}</p></div></div> },
    { header: "Area", key: "areaName", sortable: true, render: (member: AdminMember) => member.areaName ?? "Area unavailable" },
    { header: "Tenant", key: "tenantId", sortable: true },
    { header: "Membership", key: "membershipName", sortable: true, render: (member: AdminMember) => member.membershipName },
    { header: "Status", key: "isActive", sortable: true, render: (member: AdminMember) => <Badge tone={member.isActive ? "success" : "warning"}>{member.isActive ? "Active" : "Suspended"}</Badge> },
    { header: "Joined", key: "creationTime", sortable: true, render: (member: AdminMember) => new Date(member.creationTime).toLocaleDateString() },
    { header: "Actions", key: "actions", render: (member: AdminMember) => canEdit || canChangeTier || (canSuspend && member.isActive) ? <AdminActionDisclosure accessibleLabel={`Manage member for ${member.firstName} ${member.lastName}`} label="Manage member">
      {canEdit ? <EditMemberProfileDialog member={member} onUpdated={() => refreshAfterMutation(`${member.firstName} ${member.lastName}'s profile was updated.`)} /> : null}
      {canChangeTier ? <ChangeMemberTierDialog currentMembershipId={member.membershipId} memberId={member.id} memberName={`${member.firstName} ${member.lastName}`} onChanged={() => refreshAfterMutation(`${member.firstName} ${member.lastName}'s membership plan was changed.`)} /> : null}
      {canSuspend && member.isActive ? <AdminJustificationDialog confirmLabel="Suspend" description={`Suspend ${member.firstName} ${member.lastName}'s customer and login access while retaining their membership record.`} onConfirm={(justification) => suspendMember(member, justification)} title="Suspend club member" triggerLabel="Suspend" variant="danger" /> : null}
    </AdminActionDisclosure> : null },
  ];

  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Club members" }]} /><h1 className="mt-2 text-3xl font-bold">Club members</h1><p className="mt-2 text-muted-foreground">Maintain club member profiles, membership plans, and account access.</p></header>
    <Card><div className="flex items-center gap-3"><UsersRound className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Club members</p><p className="text-2xl font-bold">{loading ? "Loading…" : error ? "Unavailable" : totalCount ?? "Unavailable"}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    <TextField label="Search club members" name="memberSearch" type="search" value={keyword} onChange={(event) => { setKeyword(event.target.value); setPage(1); }} />
    <p className="text-sm text-muted-foreground">Search covers all accessible members. Column sorting applies to this page.</p>
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={members} emptyState="No club members found." keyExtractor={(member) => member.id} label="Club members" loadError={Boolean(error)} onRetry={() => void loadMembers()} pageSize={PAGE_SIZE} />}
    {!loading && !error && totalCount !== undefined ? <div aria-label="Member pages" className="flex items-center justify-between gap-3">
      <p role="status">{totalCount === 0 ? "0 accessible members" : `Showing ${(page - 1) * PAGE_SIZE + 1}–${(page - 1) * PAGE_SIZE + members.length} of ${totalCount} accessible members`}</p>
      <div className="flex gap-2">
        <Button disabled={page === 1} onClick={() => setPage((current) => current - 1)} variant="outline">Previous members</Button>
        <Button disabled={page * PAGE_SIZE >= totalCount} onClick={() => setPage((current) => current + 1)} variant="outline">Next members</Button>
      </div>
    </div> : null}
  </div></main>;
};

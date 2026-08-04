"use client";

import { Users } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { UserDialog } from "./UserDialog";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { ChangeUserAccessLevelDialog, EditUserAccountDialog, ResetUserPasswordDialog } from "./UserAccountManagementDialogs";

type InvitationStatus = "Accepted" | "Expired" | "Pending" | "Revoked";
type AdminUser = { id: number; tenantId: number; firstName: string; lastName: string; email: string; isActive: boolean; role: number; creationTime: string; invitationExpiresAt: string | null; invitationStatus: InvitationStatus | null; requiresPasswordSetup: boolean };
type PagedUsers = { items: AdminUser[]; totalCount: number };
const roleNames = ["Customer", "Club member", "Facilitator", "Area leader", "Area administrator"];
const invitationTones = { Accepted: "success", Expired: "warning", Pending: "info", Revoked: "error" } as const;
const formatExpiry = (value: string) => new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium", timeStyle: "short" }).format(new Date(value));

export const AdminUsers = () => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const canView = session?.user?.permissions?.includes("Aqua.Admin.Users.View") ?? false;
  const can = (operation: string) => session?.user?.permissions?.includes(`Aqua.Admin.Users.${operation}`) ?? false;
  const load = useCallback(async () => {
    if (!canView) return;
    setLoading(true); setError(null);
    try {
      const result = await httpClient.get<PagedUsers>("/api/services/app/AdminUser/GetAll?MaxResultCount=100");
      setUsers(result.items);
    } catch (requestError) { setError(getRequestErrorMessage(requestError, "Users could not be loaded.")); }
    finally { setLoading(false); }
  }, [canView]);
  useEffect(() => {
    const task = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(task);
  }, [load]);
  const updated = async (message: string) => { toast({ message, title: "User account updated", type: "success" }); await load(); };
  const remove = async (user: AdminUser, justification: string) => { await httpClient.delete("/api/services/app/AdminUser/Delete", { id: user.id, justification }); await updated(`${user.firstName} ${user.lastName}'s account was removed.`); };
  const invitationAction = async (user: AdminUser, action: "ResendInvitation" | "RevokeInvitation", justification: string) => { await httpClient.post(`/api/services/app/AdminUser/${action}`, { id: user.id, justification }); await updated(action === "ResendInvitation" ? "A new invitation email was queued." : "The invitation was revoked."); };

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view users.</StatusMessage></main>;
  const columns = [
    { header: "User", key: "firstName", render: (user: AdminUser) => <div className="flex items-center gap-3"><Avatar fallback={`${user.firstName} ${user.lastName}`} size="sm" /><div><p className="font-semibold">{user.firstName} {user.lastName}</p><p className="text-xs text-muted-foreground">{user.email}</p></div></div>, sortable: true },
    { header: "Area", key: "tenantId", render: (user: AdminUser) => `Area ${user.tenantId}`, sortable: true },
    { header: "Access level", key: "role", render: (user: AdminUser) => roleNames[user.role] ?? "Unknown", sortable: true },
    { header: "Lifecycle", key: "invitationStatus", render: (user: AdminUser) => {
      const invitationStatus = user.invitationStatus === "Accepted" ? null : user.invitationStatus;
      return <div className="flex flex-col items-start gap-1"><Badge tone={invitationStatus ? invitationTones[invitationStatus] : user.requiresPasswordSetup ? "warning" : user.isActive ? "success" : "neutral"}>{invitationStatus ? `Invitation ${invitationStatus.toLowerCase()}` : user.requiresPasswordSetup ? "Setup required" : user.isActive ? "Active" : "Inactive"}</Badge>{invitationStatus === "Pending" && user.invitationExpiresAt ? <span className="text-xs text-muted-foreground">Expires {formatExpiry(user.invitationExpiresAt)}</span> : null}</div>;
    }, sortable: true },
    { header: "Actions", key: "actions", render: (user: AdminUser) => {
      const setupPending = user.invitationStatus === "Pending";
      const setupIncomplete = user.requiresPasswordSetup || Boolean(user.invitationStatus && user.invitationStatus !== "Accepted");
      return <div className="flex flex-wrap gap-2">
        {can("Edit") && !setupPending ? <EditUserAccountDialog onUpdated={() => updated("Account details saved.")} user={user} /> : null}
        {can("AssignRole") && !setupPending ? <ChangeUserAccessLevelDialog onUpdated={() => updated("Access level updated.")} user={user} /> : null}
        {can("ResetPassword") && user.isActive && !setupIncomplete ? <ResetUserPasswordDialog onUpdated={() => updated("Password reset email queued.")} user={user} /> : null}
        {can("Invite") && setupIncomplete ? <AdminJustificationDialog confirmLabel="Resend invitation" description={`Invalidate the previous link and email a new invitation to ${user.email}.`} onConfirm={(justification) => invitationAction(user, "ResendInvitation", justification)} title="Resend account invitation" triggerLabel="Resend invitation" /> : null}
        {can("Invite") && setupPending ? <AdminJustificationDialog confirmLabel="Revoke invitation" description={`Revoke ${user.firstName} ${user.lastName}'s pending invitation. Its setup link will stop working.`} onConfirm={(justification) => invitationAction(user, "RevokeInvitation", justification)} title="Revoke account invitation" triggerLabel="Revoke invitation" variant="danger" /> : null}
        {can("Delete") ? <AdminJustificationDialog confirmLabel="Remove account" description={`Remove ${user.firstName} ${user.lastName}'s access. This action is recorded for accountability.`} onConfirm={(justification) => remove(user, justification)} title="Remove user account" triggerLabel="Remove" variant="danger" /> : null}
      </div>;
    } },
  ];
  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"><div><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "User accounts" }]} /><h1 className="mt-2 text-3xl font-bold">User accounts</h1><p className="mt-2 text-muted-foreground">New users receive a secure invitation to choose their own password. Track setup, resend or revoke invitations, and help active users regain access.</p></div><UserDialog onCreated={load} /></header>
    <Card><div className="flex items-center gap-3"><Users className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Total users</p><p className="text-2xl font-bold">{users.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={users} keyExtractor={(user) => user.id} searchFn={(user, query) => `${user.firstName} ${user.lastName} ${user.email}`.toLowerCase().includes(query)} />}
  </div></main>;
};

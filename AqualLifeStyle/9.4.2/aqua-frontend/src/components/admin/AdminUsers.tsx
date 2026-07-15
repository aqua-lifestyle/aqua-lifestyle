"use client";

import { Users } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { UserDialog } from "./UserDialog";

type AdminUser = { id: number; tenantId: number; firstName: string; lastName: string; email: string; isActive: boolean; role: number; creationTime: string };
type PagedUsers = { items: AdminUser[]; totalCount: number };
const roleNames = ["Guest", "Member", "Facilitator", "Area leader", "System admin"];

export const AdminUsers = () => {
  const { session } = useAuthState();
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const canView = session?.user?.permissions?.includes("Aqua.Admin.Users.View") ?? false;
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

  if (!canView) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view users.</StatusMessage></main>;
  const columns = [
    { header: "User", key: "firstName", render: (user: AdminUser) => <div className="flex items-center gap-3"><Avatar fallback={`${user.firstName} ${user.lastName}`} size="sm" /><div><p className="font-semibold">{user.firstName} {user.lastName}</p><p className="text-xs text-muted-foreground">{user.email}</p></div></div>, sortable: true },
    { header: "Tenant", key: "tenantId", render: (user: AdminUser) => `#${user.tenantId}`, sortable: true },
    { header: "Role", key: "role", render: (user: AdminUser) => roleNames[user.role] ?? "Unknown", sortable: true },
    { header: "Status", key: "isActive", render: (user: AdminUser) => <Badge tone={user.isActive ? "success" : "neutral"}>{user.isActive ? "Active" : "Inactive"}</Badge>, sortable: true },
  ];
  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6">
    <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between"><div><Breadcrumb items={[{ href: "/admin/dashboard", label: "Admin" }, { label: "Users" }]} /><h1 className="mt-2 text-3xl font-bold">Users</h1><p className="mt-2 text-muted-foreground">Manage tenant accounts, access roles, and account status.</p></div><UserDialog onCreated={load} /></header>
    <Card><div className="flex items-center gap-3"><Users className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Total users</p><p className="text-2xl font-bold">{users.length}</p></div></div></Card>
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={users} keyExtractor={(user) => user.id} searchFn={(user, query) => `${user.firstName} ${user.lastName} ${user.email}`.toLowerCase().includes(query)} />}
  </div></main>;
};

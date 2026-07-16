"use client";

import { Users } from "lucide-react";
import { useCallback, useEffect, useState } from "react";
import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Avatar, Badge, Breadcrumb, Card, DataTable, Skeleton, StatusMessage } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { CustomerDialog } from "./CustomerDialog";
import { EditCustomerAccountDialog } from "./EditCustomerAccountDialog";
import { ImportCustomersDialog } from "./ImportCustomersDialog";

type AdminCustomer = { creationTime: string; email: string; firstName: string; id: number; isActive: boolean; lastName: string; membershipId: number | null; membershipName: string | null; name: string; tenantId: number; userId: number };
type PagedCustomers = { items: AdminCustomer[]; totalCount: number };
export const AdminCustomers = () => {
  const { session } = useAuthState(); const { toast } = useToast(); const permissions = session?.user?.permissions ?? [];
  const can = (operation: string) => permissions.includes(`Aqua.Admin.Customers.${operation}`);
  const canView = permissions.includes("Aqua.Admin.Customers.View");
  const [customers, setCustomers] = useState<AdminCustomer[]>([]); const [loading, setLoading] = useState(true); const [error, setError] = useState<string>();
  const load = useCallback(async () => { if (!canView) return; setLoading(true); try { setCustomers((await httpClient.get<PagedCustomers>("/api/services/app/AdminCustomer/GetAll?MaxResultCount=100")).items); setError(undefined); } catch (requestError) { setError(getRequestErrorMessage(requestError, "Customer accounts could not be loaded.")); } finally { setLoading(false); } }, [canView]);
  useEffect(() => { const task = window.setTimeout(() => void load(), 0); return () => window.clearTimeout(task); }, [load]);
  const refreshed = async (message: string) => { toast({ message, title: "Customer account updated", type: "success" }); await load(); };
  const remove = async (customer: AdminCustomer, justification: string) => { await httpClient.post("/api/services/app/AdminCustomer/Delete", { id: customer.id, justification }); await refreshed(`${customer.name}'s account was removed.`); };
  if (!canView) return <main className="p-6"><StatusMessage tone="error">Your account does not have access to customer management.</StatusMessage></main>;
  const columns = [
    { header: "Customer", key: "name", sortable: true, render: (customer: AdminCustomer) => <div className="flex items-center gap-3"><Avatar fallback={customer.name} size="sm" /><div><p className="font-semibold">{customer.name}</p><p className="text-xs text-muted-foreground">{customer.email}</p></div></div> },
    { header: "Area", key: "tenantId", sortable: true, render: (customer: AdminCustomer) => `Area ${customer.tenantId}` },
    { header: "Membership", key: "membershipName", sortable: true, render: (customer: AdminCustomer) => customer.membershipName ?? "Not yet enrolled" },
    { header: "Account status", key: "isActive", sortable: true, render: (customer: AdminCustomer) => <Badge tone={customer.isActive ? "success" : "neutral"}>{customer.isActive ? "Active" : "Inactive"}</Badge> },
    { header: "Actions", key: "actions", render: (customer: AdminCustomer) => <div className="flex flex-wrap gap-2">{can("Edit") ? <EditCustomerAccountDialog customer={customer} onUpdated={() => refreshed("Customer details saved.")} /> : null}{can("Delete") ? <AdminJustificationDialog confirmLabel="Remove account" description={`Remove ${customer.name}'s customer account and sign-in access.`} onConfirm={(justification) => remove(customer, justification)} title="Remove customer account" triggerLabel="Remove" variant="danger" /> : null}</div> },
  ];
  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6"><header className="flex flex-wrap items-end justify-between gap-4"><div><Breadcrumb items={[{ href: "/admin/dashboard", label: "Administration" }, { label: "Customer accounts" }]} /><h1 className="mt-2 text-3xl font-bold">Customer accounts</h1><p className="mt-2 text-muted-foreground">Welcome new customers, maintain their details, and manage membership access.</p></div><div className="flex flex-wrap gap-3">{can("Import") ? <ImportCustomersDialog onImported={load} /> : null}{can("Create") ? <CustomerDialog onCreated={load} /> : null}</div></header><Card><div className="flex items-center gap-3"><Users className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Customer accounts</p><p className="text-2xl font-bold">{customers.length}</p></div></div></Card>{error ? <StatusMessage tone="error">{error}</StatusMessage> : null}{loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={customers} emptyState="No customer accounts found." keyExtractor={(customer) => customer.id} searchFn={(customer, query) => `${customer.name} ${customer.email}`.toLowerCase().includes(query)} />}</div></main>;
};

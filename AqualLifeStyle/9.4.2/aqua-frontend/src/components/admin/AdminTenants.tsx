"use client";

import { Building2, Plus } from "lucide-react";
import { type FormEvent, useCallback, useEffect, useState } from "react";
import { z } from "zod";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Badge, Breadcrumb, Button, Card, DataTable, Dialog, SelectField, Skeleton, StatusMessage, TextAreaField, TextField } from "@/src/shared/ui";
import { AdminJustificationDialog } from "./AdminJustificationDialog";
import { adminAuditJustificationSchema } from "./admin-action-validation";

type AdminTenant = { areaLeaderId: number | null; areaLeaderName: string | null; id: number; isActive: boolean; name: string; tenancyName: string };
type Paged<T> = { items: T[]; totalCount: number };
type ApprovedLeader = { customerName: string; id: number; isApproved: boolean; tenantId: number };
const tenantProfileSchema = z.object({
  adminEmailAddress: z.union([z.literal(""), z.string().trim().email("Enter a valid admin email.")]),
  justification: adminAuditJustificationSchema,
  name: z.string().trim().min(1, "Tenant name is required.").max(128),
  tenancyName: z.string().trim().regex(/^[A-Za-z][A-Za-z0-9_-]*$/, "Use a valid tenancy name.").max(64),
});

const TenantProfileDialog = ({ onSaved, tenant }: { onSaved: () => Promise<void>; tenant?: AdminTenant }) => {
  const [open, setOpen] = useState(false); const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string>(); const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const close = () => { setOpen(false); setError(undefined); setFieldErrors({}); };
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault(); const form = event.currentTarget; const data = new FormData(form);
    const parsed = tenantProfileSchema.safeParse({ adminEmailAddress: tenant ? "" : data.get("adminEmailAddress"), justification: data.get("justification"), name: data.get("name"), tenancyName: data.get("tenancyName") });
    if (!parsed.success) { setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message]))); return; }
    setSubmitting(true); setError(undefined); setFieldErrors({});
    try {
      if (tenant) await httpClient.post("/api/services/app/AdminTenant/Edit", { id: tenant.id, justification: parsed.data.justification, name: parsed.data.name, tenancyName: parsed.data.tenancyName });
      else await httpClient.post("/api/services/app/AdminTenant/Create", { ...parsed.data, isActive: true });
      await onSaved(); close();
    } catch (requestError) { setError(getRequestErrorMessage(requestError, `The tenant could not be ${tenant ? "updated" : "created"}.`)); }
    finally { setSubmitting(false); }
  };
  return <><Button onClick={() => setOpen(true)} size={tenant ? "sm" : "md"} variant={tenant ? "outline" : "primary"}>{tenant ? "Edit" : <><Plus className="size-4" /> Add tenant</>}</Button>
    <Dialog onClose={close} open={open} size="lg" title={tenant ? "Edit tenant" : "Add tenant"}><form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}>
      <TextField defaultValue={tenant?.tenancyName} errorMessage={fieldErrors.tenancyName} label="Tenancy name" name="tenancyName" required />
      <TextField defaultValue={tenant?.name} errorMessage={fieldErrors.name} label="Display name" name="name" required />
      {!tenant ? <TextField className="sm:col-span-2" errorMessage={fieldErrors.adminEmailAddress} label="Initial admin email" name="adminEmailAddress" required type="email" /> : null}
      <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Audit justification" maxLength={500} name="justification" required rows={3} />
      {error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}
      <div className="flex justify-end gap-3 sm:col-span-2"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} type="submit">{tenant ? "Save tenant" : "Create tenant"}</Button></div>
    </form></Dialog></>;
};

const AssignTenantLeaderDialog = ({ onAssigned, tenant }: { onAssigned: () => Promise<void>; tenant: AdminTenant }) => {
  const [open, setOpen] = useState(false); const [leaders, setLeaders] = useState<ApprovedLeader[]>([]); const [error, setError] = useState<string>(); const [submitting, setSubmitting] = useState(false);
  useEffect(() => { if (!open) return; void httpClient.get<Paged<ApprovedLeader>>(`/api/services/app/AdminAreaLeader/GetAll?TenantId=${tenant.id}&IsApproved=true&MaxResultCount=100`).then((result) => setLeaders(result.items)).catch((requestError) => setError(getRequestErrorMessage(requestError, "Approved leaders could not be loaded."))); }, [open, tenant.id]);
  const close = () => { setOpen(false); setError(undefined); };
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); const leaderId = Number(data.get("areaLeaderId")); const justification = adminAuditJustificationSchema.safeParse(data.get("justification")); if (!leaderId || !justification.success) { setError("Select an approved leader and provide an audit justification."); return; } setSubmitting(true); try { await httpClient.post("/api/services/app/AdminTenant/AssignAreaLeader", { areaLeaderId: leaderId, id: tenant.id, justification: justification.data }); await onAssigned(); close(); } catch (requestError) { setError(getRequestErrorMessage(requestError, "The leader could not be assigned.")); } finally { setSubmitting(false); } };
  return <><Button onClick={() => setOpen(true)} size="sm" variant="outline">Assign leader</Button><Dialog onClose={close} open={open} title="Assign tenant leader"><form className="flex flex-col gap-4" onSubmit={submit}><SelectField label="Approved area leader" name="areaLeaderId" required><option value="">Select a leader</option>{leaders.map((leader) => <option key={leader.id} value={leader.id}>{leader.customerName}</option>)}</SelectField><TextAreaField label="Audit justification" maxLength={500} name="justification" required rows={3} />{error ? <StatusMessage tone="error">{error}</StatusMessage> : null}<div className="flex justify-end gap-3"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} type="submit">Assign leader</Button></div></form></Dialog></>;
};

export const AdminTenants = () => {
  const { session } = useAuthState(); const { toast } = useToast(); const permissions = session?.user?.permissions ?? [];
  const can = (operation: string) => permissions.includes(`Aqua.Admin.Tenants.${operation}`);
  const [tenants, setTenants] = useState<AdminTenant[]>([]); const [loading, setLoading] = useState(true); const [error, setError] = useState<string>();
  const loadTenants = useCallback(async () => { if (!can("View")) return; setLoading(true); try { setTenants((await httpClient.get<Paged<AdminTenant>>("/api/services/app/AdminTenant/GetAll?MaxResultCount=100")).items); setError(undefined); } catch (requestError) { setError(getRequestErrorMessage(requestError, "Tenants could not be loaded.")); } finally { setLoading(false); } }, [permissions]);
  useEffect(() => { const task = window.setTimeout(() => void loadTenants(), 0); return () => window.clearTimeout(task); }, [loadTenants]);
  const refreshed = async (message: string) => { toast({ message, title: "Tenant updated", type: "success" }); await loadTenants(); };
  if (!can("View")) return <main className="p-6"><StatusMessage tone="error">You do not have permission to view tenants.</StatusMessage></main>;
  const columns = [
    { header: "Tenant", key: "name", sortable: true, render: (tenant: AdminTenant) => <div><p className="font-semibold">{tenant.name}</p><p className="text-xs text-muted-foreground">{tenant.tenancyName} · #{tenant.id}</p></div> },
    { header: "Area leader", key: "areaLeaderName", sortable: true, render: (tenant: AdminTenant) => tenant.areaLeaderName ?? "Not assigned" },
    { header: "Status", key: "isActive", sortable: true, render: (tenant: AdminTenant) => <Badge tone={tenant.isActive ? "success" : "warning"}>{tenant.isActive ? "Active" : "Inactive"}</Badge> },
    { header: "Actions", key: "actions", render: (tenant: AdminTenant) => <div className="flex flex-wrap gap-2">{can("Edit") ? <TenantProfileDialog onSaved={() => refreshed(`${tenant.name} was updated.`)} tenant={tenant} /> : null}{can("AssignLeader") ? <AssignTenantLeaderDialog onAssigned={() => refreshed(`${tenant.name}'s leader was assigned.`)} tenant={tenant} /> : null}{can("Activate") ? <AdminJustificationDialog confirmLabel={tenant.isActive ? "Deactivate" : "Activate"} description={`${tenant.isActive ? "Deactivate" : "Activate"} ${tenant.name}.`} onConfirm={async (justification) => { await httpClient.post("/api/services/app/AdminTenant/SetActivation", { id: tenant.id, isActive: !tenant.isActive, justification }); await refreshed(`${tenant.name} was ${tenant.isActive ? "deactivated" : "activated"}.`); }} title={`${tenant.isActive ? "Deactivate" : "Activate"} tenant`} triggerLabel={tenant.isActive ? "Deactivate" : "Activate"} variant={tenant.isActive ? "danger" : "outline"} /> : null}</div> },
  ];
  return <main className="min-h-dvh px-4 py-6 sm:px-6 lg:px-8"><div className="mx-auto flex max-w-7xl flex-col gap-6"><header className="flex flex-wrap items-end justify-between gap-4"><div><Breadcrumb items={[{ href: "/admin/dashboard", label: "Admin" }, { label: "Tenants" }]} /><h1 className="mt-2 text-3xl font-bold">Tenants</h1><p className="mt-2 text-muted-foreground">Provision tenants, control access, and assign approved area leaders.</p></div>{can("Create") ? <TenantProfileDialog onSaved={() => refreshed("Tenant created successfully.")} /> : null}</header><Card><div className="flex items-center gap-3"><Building2 className="size-5 text-accent" /><div><p className="text-sm text-muted-foreground">Tenants</p><p className="text-2xl font-bold">{tenants.length}</p></div></div></Card>{error ? <StatusMessage tone="error">{error}</StatusMessage> : null}{loading ? <Skeleton className="h-72" /> : <DataTable columns={columns} data={tenants} emptyState="No tenants found." keyExtractor={(tenant) => tenant.id} searchFn={(tenant, query) => `${tenant.name} ${tenant.tenancyName} ${tenant.areaLeaderName ?? ""}`.toLowerCase().includes(query)} />}</div></main>;
};

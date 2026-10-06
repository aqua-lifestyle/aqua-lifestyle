"use client";

import { useState, type FormEvent } from "react";
import { useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Card, StatusMessage, TextField, TextAreaField } from "@/src/shared/ui";

export function PilotRootAdmission() {
  const { session } = useAuthState();
  const [tenantId, setTenantId] = useState("");
  const [customerId, setCustomerId] = useState("");
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState<string>();
  const [error, setError] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [customers, setCustomers] = useState<{ customerId: number; name: string; clubMemberNumber: string }[]>([]);
  const permissions = session?.user?.permissions ?? [];
  if (!permissions.includes("Aqua.Admin.ProgrammeParticipations.BootstrapAQGreenRoot") ||
      !permissions.includes("Aqua.Admin.AllTenants")) return null;
  const submit = async (event: FormEvent) => {
    event.preventDefault(); setBusy(true); setError(undefined); setMessage(undefined);
    try {
      await httpClient.post("/api/services/app/AQGreenPilot/AuthorizeProspectiveRoot", {
        tenantId: Number(tenantId), customerId: Number(customerId), reason,
      });
      setMessage("Prospective root admission recorded. The member must complete test payment, followed by authorized Area approval.");
    } catch (error) { setError(getRequestErrorMessage(error, "Root admission was refused.")); }
    finally { setBusy(false); }
  };
  return <Card><form className="flex flex-col gap-4" onSubmit={submit}>
    <h2 className="text-xl font-bold">Authorize a prospective pilot root</h2>
    <p>A privileged, audited admission establishes root attribution. It does not activate participation.</p>
    <TextField name="pilot-root-tenant" label="Exact Tenant ID" value={tenantId} onChange={(event) => setTenantId(event.target.value)} type="number" required min={1} />
    <Button type="button" variant="outline" onClick={() => {
      setError(undefined); setMessage(undefined);
      void httpClient.post("/api/services/app/AdminCommissionBootstrap/BootstrapInitialCommissionTerms", { dryRun: false })
        .then(() => httpClient.post("/api/services/app/AdminTenant/ObserveActivationState", { id: Number(tenantId), justification: "Prospective TEST-MONEY pilot baseline" }))
        .then(() => setMessage("Existing AQGreen commission terms and a prospective Tenant baseline are recorded. No historical state, commission or payout was created."))
        .catch((error) => setError(getRequestErrorMessage(error, "Commercial configuration was refused. No commission was created.")));
    }}>Initialize test commission configuration</Button>
    <Button type="button" variant="outline" onClick={() => {
      setCustomers([]); setCustomerId(""); setError(undefined);
      void httpClient.get<{ customerId: number; name: string; clubMemberNumber: string }[]>(
        `/api/services/app/AQGreenPilot/GetProspectiveCustomers?tenantId=${encodeURIComponent(tenantId)}`,
      ).then(setCustomers).catch((error) => setError(getRequestErrorMessage(error, "Prospective customers could not be loaded.")));
    }}>Find prospective customers in this Tenant</Button>
    {customers.length > 0 && <label>Prospective customer
      <select aria-label="Prospective customer" value={customerId} onChange={(event) => setCustomerId(event.target.value)}>
        <option value="">Select an exact customer</option>
        {customers.map((customer) => <option key={customer.customerId} value={customer.customerId}>{customer.name} · {customer.clubMemberNumber}</option>)}
      </select>
    </label>}
    <TextField name="pilot-root-customer" label="Exact prospective customer ID" value={customerId} onChange={(event) => setCustomerId(event.target.value)} type="number" required min={1} />
    <TextAreaField name="pilot-root-reason" label="Root admission reason" value={reason} onChange={(event) => setReason(event.target.value)} required maxLength={500} />
    <Button type="submit" isLoading={busy}>Authorize prospective root</Button>
    {error && <StatusMessage tone="error">{error}</StatusMessage>}
    {message && <StatusMessage tone="success">{message}</StatusMessage>}
  </form></Card>;
}

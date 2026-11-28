"use client";

import Link from "next/link";
import { useEffect, useState, type FormEvent } from "react";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, SelectField, StatusMessage, TextField } from "@/src/shared/ui";
import { AdminAreaSelectionField } from "./AdminAreaSelectionField";

const api = "/api/services/app/AdminProgrammeParticipation/";
type Tree = { scopeId: string; rootMemberNumber: string; historicalHold: boolean };
type Customer = { id: number; name: string; email: string; tenantId: number; isActive: boolean };
const template = JSON.stringify([{ originalReference: "0001", customerId: 1, participantId: "", parentParticipantId: "",
  slot: 1, startedAtUtc: "", activatedAtUtc: "", placedAtUtc: "" }], null, 2);

export function AQGreenAdmissionForm({ mode, fixedTenantId }: { mode: "company" | "historical"; fixedTenantId?: number }) {
  const [tenant, setTenant] = useState("");
  const tenantId = fixedTenantId ?? Number(tenant);

  return <div className="space-y-3">
    <AdminAreaSelectionField fixedAreaId={fixedTenantId} scopeLabel="Tenant" value={tenant} onChange={setTenant} />
    {tenantId > 0 ? <AdmissionForTenant key={`${mode}:${tenantId}`} mode={mode} tenantId={tenantId} /> : null}
  </div>;
}

function AdmissionForTenant({ mode, tenantId }: { mode: "company" | "historical"; tenantId: number }) {
  const [trees, setTrees] = useState<Tree[]>([]);
  const [scopeId, setScopeId] = useState("");
  const [customerId, setCustomerId] = useState("");
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [file, setFile] = useState<File>();
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState("");
  const [error, setError] = useState("");
  const historical = mode === "historical";

  useEffect(() => {
    let current = true;
    void httpClient.get<Tree[]>(`${api}GetAQGreenAdmissionTrees?TenantId=${tenantId}`).then(result => {
      if (current) setTrees(result);
    }).catch(reason => { if (current) setError(getRequestErrorMessage(reason, "Eligible networks could not be loaded.")); });
    return () => { current = false; };
  }, [tenantId]);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const values = new FormData(event.currentTarget);
    const action = (event.nativeEvent as SubmitEvent).submitter?.getAttribute("value");
    if (!action || busy) return;
    const evidenceReference = String(values.get("evidence") ?? "").trim();
    setBusy(true); setError(""); setNotice("");
    try {
      if (action === "find") {
        setCustomerId(""); setCustomers([]);
        const keyword = String(values.get("customerSearch") ?? "").trim();
        if (!keyword) throw new Error("Enter a customer name or email to search.");
        const query = new URLSearchParams({ TenantId: String(tenantId), MaxResultCount: "25", Keyword: keyword });
        const data = await httpClient.get<{ items: Customer[] }>(`/api/services/app/AdminCustomer/GetAll?${query}`);
        const matches = data.items.filter(item => item.tenantId === tenantId && item.isActive);
        setCustomers(matches);
        if (!matches.length) setNotice("No matching active customer was found in your scope.");
      } else if (historical && action === "hold") {
        const originalReferences = String(values.get("registerReferences") ?? "").split(/\r?\n/).map(value => value.trim()).filter(Boolean);
        await httpClient.post(`${api}OpenAQGreenHistoricalReview`, {
          tenantId, scopeId, evidenceReference, expectedMembers: Number(values.get("expectedMembers")),
          originalReferences: originalReferences.length ? originalReferences : null,
          allowPrecautionaryCancellation: values.has("precautionary"),
        });
        setTrees(current => current.map(tree => tree.scopeId === scopeId ? { ...tree, historicalHold: true } : tree));
        setNotice("Network held. Bind the independently reviewed register before approving clearance.");
      } else if (historical && action === "cancel") {
        await httpClient.post(`${api}CancelAQGreenHistoricalReview`, {
          tenantId, scopeId, evidenceReference, reason: String(values.get("cancellationReason") ?? "").trim(),
          noConflictEvidenceReference: String(values.get("noConflictEvidence") ?? "").trim(),
          noOutstandingConflictConfirmed: values.has("noConflictConfirmed"),
        });
        setTrees(current => current.map(tree => tree.scopeId === scopeId ? { ...tree, historicalHold: false } : tree));
        setNotice("The precautionary hold was cancelled. Existing placements are unchanged.");
      } else if (historical) {
        if (!file || file.size > 1024 * 1024) throw new Error("Select the verified placement file (up to 1 MB).");
        const members: unknown = JSON.parse(await file.text());
        if (!Array.isArray(members) || !members.length || members.length > 1000) throw new Error("Use an array of 1–1,000 verified rows.");
        await httpClient.post(`${api}CompleteAQGreenHistoricalReview`, { tenantId, scopeId, evidenceReference, members });
        setTrees(current => current.map(tree => tree.scopeId === scopeId ? { ...tree, historicalHold: false } : tree));
        setNotice("The reviewed positions were recorded and the historical placement hold was cleared.");
      } else {
        await httpClient.post(`${api}RegisterAQGreenCompanyAdmission`, { tenantId, scopeId, customerId: Number(customerId), acquisitionEvidence: evidenceReference });
        setNotice("Registration recorded. Ask the member to sign in and accept it under My programmes, then complete joining payment. Approve the verified payment in Programme participations.");
      }
    } catch (reason) { setError(getRequestErrorMessage(reason, "The request could not be completed.")); }
    finally { setBusy(false); }
  }

  return <form aria-label={historical ? "Historical placement review" : "Company AQGreen registration"} className="space-y-3" onSubmit={submit}>
    {historical ? <p className="text-sm">Hold the affected network before resolving disputed positions. Approve only the complete verified roster for this review.</p> :
      <><p className="text-sm">Use an existing account or <Link className="underline" href="/admin/customers">create the customer and assign their Area</Link> first. The network is selected here; the allocator chooses the position.</p>
        <TextField label="Find customer by name or email" name="customerSearch" />
        <Button type="submit" value="find" disabled={busy} variant="outline" formNoValidate>Find customer</Button>
        <SelectField label="Customer for registration" name="companyCustomer" value={customerId} onChange={e => setCustomerId(e.target.value)} required>
          <option value="">Select a customer</option>
          {customers.map(c => <option key={c.id} value={c.id}>{c.name} ({c.email})</option>)}
        </SelectField></>}
    <SelectField label={historical ? "Network to reconcile" : "Network for registration"} name="network" value={scopeId} onChange={e => setScopeId(e.target.value)} required>
      <option value="">Select a network by root member number</option>
      {trees.map(t => <option key={t.scopeId} value={t.scopeId} disabled={!historical && t.historicalHold}>{t.rootMemberNumber}{t.historicalHold ? " (historical hold)" : ""}</option>)}
    </SelectField>
    <TextField label={historical ? "Reviewed roster evidence reference" : "Acquisition evidence reference"} name="evidence" maxLength={historical ? 128 : 500} required />
    {historical ? <>
      <TextField label="Member count from the reviewed register" name="expectedMembers" type="number" min="1" max="1000" required />
      <label className="block text-sm">Original references from the reviewed register (one per line)
        <textarea className="block w-full rounded border bg-background p-2" name="registerReferences" rows={3} maxLength={130000} />
      </label>
      <p className="text-sm">Use the separately verified register for the whole affected tree and cutoff. Do not derive this list or count from the placement upload.</p>
      <label className="flex items-start gap-2 text-sm"><input type="checkbox" name="precautionary" /> This hold is precautionary only; no outstanding placement conflict is known.</label>
      <Button type="submit" value="hold" disabled={busy || !scopeId} variant="outline">Hold network or update review evidence</Button>
      <a className="block text-sm underline" download="aqgreen-verified-placements.json" href={`data:application/json;charset=utf-8,${encodeURIComponent(template)}`}>Download verified placement template</a>
      <label className="block text-sm">Verified placement file <input className="block" type="file" accept=".json,application/json" onChange={e => setFile(e.target.files?.[0])} /></label>
      <p className="text-sm">Starting approval makes the hold non-cancellable even if the placement file fails validation.</p>
      <Button type="submit" value="clear" disabled={busy || !file || !scopeId} formNoValidate>Approve placement and clear hold</Button>
      <details className="space-y-3">
        <summary className="cursor-pointer">Cancel an incomplete precautionary hold</summary>
        <TextField label="Separate no-conflict evidence reference" name="noConflictEvidence" maxLength={128} />
        <TextField label="Cancellation reason" name="cancellationReason" maxLength={500} />
        <label className="flex items-start gap-2 text-sm"><input type="checkbox" name="noConflictConfirmed" /> I verified there is no outstanding placement conflict or integrity issue requiring this hold.</label>
        <Button type="submit" value="cancel" disabled={busy || !scopeId} variant="outline" formNoValidate>Cancel precautionary hold</Button>
      </details>
    </> : <Button type="submit" value="register" disabled={busy || !scopeId || !customerId}>Register company recruit</Button>}
    {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
    {notice ? <StatusMessage tone="info">{notice}</StatusMessage> : null}
  </form>;
}

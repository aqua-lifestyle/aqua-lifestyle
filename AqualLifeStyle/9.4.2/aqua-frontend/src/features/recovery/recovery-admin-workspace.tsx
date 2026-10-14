"use client";

import Link from "next/link";
import { useEffect, useState } from "react";
import { useAuthState } from "@/src/providers";
import { isSystemAdmin } from "@/src/shared/auth/roles";
import { DISCOVERY_KINDS, EVIDENCE_ISSUES, type RecoveryB6Result } from "./b6-contract";

export function RecoveryAdminWorkspace({ discovery = false }: { discovery?: boolean }) {
  const { isReady, isAuthenticated, session, status } = useAuthState();
  const [state, setState] = useState<{ key?: string; result?: RecoveryB6Result; failed?: string }>({});
  const userId = session?.user?.id;
  const admin = isSystemAdmin(session?.user?.role);
  const requestKey = JSON.stringify([discovery, userId, session?.user?.tenantId, session?.tenant, session?.expiresAt]);
  useEffect(() => {
    if (!isReady || !isAuthenticated || !admin || status === "error") return;
    const controller = new AbortController();
    void fetch(`/api/recovery/b6?operation=${discovery ? "discovery" : "scope"}`, {
      cache: "no-store", credentials: "same-origin", signal: controller.signal,
    }).then(async response => {
      const result = await response.json() as RecoveryB6Result;
      if (!controller.signal.aborted) setState({ key: requestKey, result });
    }).catch(() => {
      if (!controller.signal.aborted) setState({ key: requestKey, failed: "Recovery access could not be checked. Refresh to retry." });
    });
    return () => controller.abort();
  }, [admin, discovery, isAuthenticated, isReady, requestKey, status]);

  const { result, failed } = state.key === requestKey ? state : {};
  const scope = result?.scope;
  const inventory = result?.inventory;
  const accessProblem = status === "error" ? "Your browser session could not be verified. Sign in again."
    : isReady && (!isAuthenticated || !admin) ? "An authenticated administrator session is required." : null;
  return <main className="mx-auto w-full max-w-4xl space-y-6 p-6">
    <h1 className="text-2xl font-bold">{discovery ? "B6 historical discovery" : "Recovery administrator workspace"}</h1>
    <p>Read-only historical discovery. Membership, payments, placement changes and financial processing remain suspended.</p>
    {accessProblem ? <section role="alert"><p>{accessProblem}</p><Link href="/login">Sign in</Link></section>
      : failed ? <p role="alert">{failed}</p>
      : !result ? <p role="status">{isReady ? "Verifying recovery and the approved Tenant/Area scope…" : "Checking your existing session…"}</p>
      : <>
        <p>{result.recoveryVerified ? "Restricted API recovery verified." : "Recovery status is not verified."}</p>
        {scope && <section aria-label="Authenticated scope" className="space-y-2">
          <h2 className="text-lg font-semibold">Authenticated scope</h2>
          <p>{scope.tenant ? `Tenant ${scope.tenant.id}: ${scope.tenant.name} (${scope.tenant.tenancyName})` : "Tenant: host session"}</p>
          <p>Administrator ID: {scope.userId}</p>
          <h3 className="font-semibold">Assigned Areas</h3>
          {scope.areas === null ? <p>Assigned Areas have not been examined.</p>
            : scope.areas.length === 0 ? <p>No active Area assignments.</p>
            : <ul className="space-y-2">{scope.areas.map(area => <li key={area.areaId}>
              {area.name} ({area.code}): <code className="break-all">{area.areaId}</code>
            </li>)}</ul>}
        </section>}
        {result.blocker && <p role="alert">{result.blocker}</p>}
        {!discovery && result.scopeVerified && <Link className="inline-block rounded border px-4 py-2" href="/admin/b6-discovery">Open bounded B6 discovery</Link>}
        {discovery && <Link href="/admin/dashboard">Recovery workspace</Link>}
        {inventory && <section aria-label="Discovery inventory" className="space-y-4">
          <h2 className="text-lg font-semibold">Observed historical inventory</h2>
          <p>Examined {inventory.examined} of {inventory.totalCount} current-scope records. Limit: {inventory.limit}.</p>
          <p>{inventory.wholeCurrentScopeExamined ? "All records in this current Area scope were examined in this run."
            : "Partial population: the bounded limit was reached. This is not a whole-population result."}</p>
          <table className="w-full text-left"><caption className="text-left font-semibold">Population classifications</caption><thead><tr><th scope="col">Classification</th><th scope="col">Records</th></tr></thead>
            <tbody>{DISCOVERY_KINDS.map((kind, index) => <tr key={kind}><td>{kind}</td><td>{inventory.classifications[index]}</td></tr>)}</tbody>
          </table>
          <table className="w-full text-left"><caption className="text-left font-semibold">Evidence blockers flagged by discovery</caption><thead><tr><th scope="col">Issue</th><th scope="col">Records</th></tr></thead>
            <tbody>{Object.entries(inventory.evidenceIssues).map(([code, count]) => <tr key={code}><td>{EVIDENCE_ISSUES[code as keyof typeof EVIDENCE_ISSUES]} <span className="block break-all text-xs">{code}</span></td><td>{count}</td></tr>)}</tbody>
          </table>
          <p>Observed at {inventory.observedAt}. This is a live discovery inventory, not frozen evidence or reconciliation approval.</p>
          <h3 className="font-semibold">Next reconciliation decisions</h3>
          <p>Obtain authoritative membership provenance, sponsor/root history, joining-payment finality, and approval/activation evidence for flagged records. Current Area assignment does not establish historical ownership. Missing evidence does not prove that an event did not happen.</p>
          <p>Unflagged records are not automatically accepted. Detailed approval and placement source references require the separately authorized frozen evidence review.</p>
        </section>}
      </>}
  </main>;
}

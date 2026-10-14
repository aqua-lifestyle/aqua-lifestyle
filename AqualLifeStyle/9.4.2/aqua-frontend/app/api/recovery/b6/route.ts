import { NextResponse } from "next/server";
import { z } from "zod";

import { publicEnv } from "@/src/shared/config";
import { readSession, projectSession } from "@/src/shared/auth/session";
import { isSystemAdmin } from "@/src/shared/auth/roles";
import {
  B6_PAGE_SIZE, B6_RECORD_LIMIT, EVIDENCE_ISSUES, ORIGINAL_JOHANNESBURG_AREA_ID,
  type RecoveryB6Result, type RecoveryScope,
} from "@/src/features/recovery/b6-contract";

export const dynamic = "force-dynamic";

const guid = z.string().regex(/^[\da-f]{8}-(?:[\da-f]{4}-){3}[\da-f]{12}$/i)
  .transform(value => value.toLowerCase());
const identitySchema = z.object({
  tenant: z.object({ id: z.number().int().positive(), name: z.string(), tenancyName: z.string() }).nullable(),
  user: z.object({ id: z.number().int().positive().max(Number.MAX_SAFE_INTEGER) }).nullable(),
});
const areasSchema = z.array(z.object({ areaId: guid, code: z.string(), name: z.string() }));
const pageSchema = z.object({
  totalCount: z.number().int().nonnegative(),
  items: z.array(z.object({
    discoveryId: z.string(), customerId: z.number().int().positive(),
    tenantId: z.number().int().positive(), currentAreaId: guid.nullable(),
    discoveryKind: z.number().int().min(1).max(5),
    participationVersionEvidence: z.number().int().min(1).max(3),
    historicalEvidenceIssues: z.array(z.enum(Object.keys(EVIDENCE_ISSUES) as [keyof typeof EVIDENCE_ISSUES, ...Array<keyof typeof EVIDENCE_ISSUES>])),
  })).max(B6_PAGE_SIZE),
});

class Blocked extends Error {
  constructor(message: string, readonly status = 403) { super(message); }
}

/** Fixed-scope GET relay. Records are aggregated in memory, never returned to the browser. */
export async function GET(request: Request) {
  let recoveryVerified = false;
  let scope: RecoveryScope | null = null;
  let scopeVerified = false;
  const reply = (body: RecoveryB6Result, status = 200) => NextResponse.json(body, {
    status, headers: { "Cache-Control": "private, no-store", "X-Content-Type-Options": "nosniff" },
  });
  try {
    const query = new URL(request.url).searchParams;
    const operation = query.get("operation") ?? "scope";
    if (!["scope", "discovery"].includes(operation) || [...query.keys()].some(key => key !== "operation") ||
        query.getAll("operation").length > 1)
      throw new Blocked("Only the fixed approved recovery scope can be requested.", 400);

    const session = await readSession();
    const user = session ? projectSession(session).user : null;
    if (!session || !user) throw new Blocked("Sign in with your existing administrator account to continue.", 401);
    if (!isSystemAdmin(user.role)) throw new Blocked("An authenticated administrator session is required.");

    const signal = AbortSignal.any([request.signal, AbortSignal.timeout(25_000)]);
    const headers: Record<string, string> = { Accept: "application/json", Authorization: `Bearer ${session.accessToken}` };
    if (session.tenant) headers.__tenant = session.tenant;
    const read = async (path: string) => {
      const response = await fetch(`${publicEnv.NEXT_PUBLIC_ABP_API_URL}${path}`, {
        method: "GET", headers, cache: "no-store", redirect: "error", signal,
      });
      if (response.headers.get("X-Aqua-Recovery-Suspended") !== "true")
        throw new Blocked("Restricted API recovery could not be verified. Discovery is stopped.", 503);
      if (!response.ok) {
        if (response.status === 401) throw new Blocked("The API could not verify this session. Sign in again.", 401);
        if (response.status === 403) throw new Blocked(
          path.includes("GetLegacyAQGreenReconciliation")
            ? "The application denied B6 discovery permission (ViewLegacyPaymentReconciliation). No inventory is available."
            : "The application denied assigned-Area access (ProgrammeParticipations.View). Discovery is stopped.");
        throw new Blocked(`The read-only application request failed (HTTP ${response.status}). Discovery is stopped.`, 502);
      }
      return response;
    };
    const result = async (path: string): Promise<unknown> => {
      const payload = await (await read(path)).json();
      if (!payload || payload.success !== true || payload.unAuthorizedRequest === true || !("result" in payload))
        throw new Blocked("The application response cannot be verified. No inventory is available.", 502);
      return payload.result;
    };

    await read("/api/health");
    recoveryVerified = true;
    const identity = identitySchema.parse(await result("/api/services/app/Session/GetCurrentLoginInformations"));
    if (!identity.user || identity.user.id !== user.id)
      throw new Blocked("The application's authenticated identity does not match this session.", 401);
    scope = { tenant: identity.tenant, userId: identity.user.id, areas: null };
    if (identity.tenant?.id !== 1 || user.tenantId !== 1)
      throw new Blocked(identity.tenant === null
        ? "This is a host-scoped session. B6 requires a Tenant 1 administrator assigned only to the original Johannesburg Area."
        : "This session is not verified for Tenant 1. Discovery is stopped.");

    const verifyAreas = async () => {
      const areas = areasSchema.parse(await result("/api/services/app/AdminProgrammeParticipation/GetAssignedAreas"));
      scope!.areas = areas;
      scopeVerified = false;
      if (areas.length !== 1) throw new Blocked(areas.length === 0
        ? "No active Area assignment is available. Assign an authorized Tenant 1 administrator to the original Johannesburg Area."
        : "This account has multiple Area assignments. The discovery endpoint cannot narrow its Area scope. Use an account assigned only to the original Johannesburg Area.");
      if (areas[0].areaId !== ORIGINAL_JOHANNESBURG_AREA_ID || areas[0].code !== "JHB")
        throw new Blocked("The assigned Area does not match the original Johannesburg system-introduction identity. Its exact scope must be resolved before discovery.");
      scopeVerified = true;
    };
    await verifyAreas();
    if (operation === "scope") return reply({ recoveryVerified, scope, scopeVerified });

    const classifications = [0, 0, 0, 0, 0];
    const evidenceIssues: NonNullable<RecoveryB6Result["inventory"]>["evidenceIssues"] = {};
    let totalCount: number | undefined;
    let examined = 0;
    let lastCustomerId = 0;
    // Recheck the application's live assignment envelope at every page boundary.
    // The backend independently authorizes each request and applies its Area filter.
    for (let offset = 0; offset < B6_RECORD_LIMIT; offset += B6_PAGE_SIZE) {
      await verifyAreas();
      const page = pageSchema.parse(await result(
        `/api/services/app/AdminProgrammeParticipation/GetLegacyAQGreenReconciliation?TenantId=1&SkipCount=${offset}&MaxResultCount=${B6_PAGE_SIZE}`,
      ));
      await verifyAreas();
      if (totalCount !== undefined && totalCount !== page.totalCount)
        throw new Blocked("The live population count changed during pagination. No consistent inventory is available; retry a bounded run.", 409);
      totalCount = page.totalCount;
      if (page.items.length !== Math.min(B6_PAGE_SIZE, Math.max(0, totalCount - offset)))
        throw new Blocked("The discovery page is incomplete or its count is inconsistent. No inventory is available.", 409);
      for (const row of page.items) {
        if (row.tenantId !== 1 || row.currentAreaId !== ORIGINAL_JOHANNESBURG_AREA_ID)
          throw new Blocked("Discovery returned a record outside the approved Tenant/Area envelope. The entire run was rejected.");
        if (row.customerId <= lastCustomerId || row.discoveryId !== `aqgreen:1:${row.customerId}`)
          throw new Blocked("Discovery identities are duplicated, mismatched or out of order. The entire run was rejected.", 409);
        lastCustomerId = row.customerId;
        examined += 1;
        classifications[row.discoveryKind - 1] += 1;
        for (const issue of new Set(row.historicalEvidenceIssues))
          evidenceIssues[issue] = (evidenceIssues[issue] ?? 0) + 1;
      }
      if (examined >= totalCount) break;
    }
    if (totalCount === undefined) throw new Blocked("The discovery population count is unavailable.", 502);
    return reply({ recoveryVerified, scope, scopeVerified, inventory: {
      examined, totalCount, limit: B6_RECORD_LIMIT, wholeCurrentScopeExamined: examined === totalCount,
      classifications, evidenceIssues, observedAt: new Date().toISOString(),
    } });
  } catch (error) {
    const blocker = error instanceof Blocked ? error.message
      : error instanceof z.ZodError ? "Application identity or discovery evidence did not match the required contract. Discovery is stopped."
      : "The read-only examination failed or timed out. No verified inventory is available; retry after access is restored.";
    return reply({ recoveryVerified, scope, scopeVerified: false, blocker }, error instanceof Blocked ? error.status : 502);
  }
}

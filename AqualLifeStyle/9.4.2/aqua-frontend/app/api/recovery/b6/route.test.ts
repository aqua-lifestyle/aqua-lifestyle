// @vitest-environment node
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ORIGINAL_JOHANNESBURG_AREA_ID as areaId } from "@/src/features/recovery/b6-contract";

const { readSession, projectSession } = vi.hoisted(() => ({ readSession: vi.fn(), projectSession: vi.fn() }));
vi.mock("@/src/shared/auth/session", () => ({ readSession, projectSession }));
vi.mock("@/src/shared/config", () => ({ publicEnv: { NEXT_PUBLIC_ABP_API_URL: "https://api.example.test" } }));
import { GET } from "./route";

const area = { areaId, code: "JHB", name: "Johannesburg" };
const tenant = { id: 1, name: "Synthetic Tenant", tenancyName: "Default" };
const row = (id: number) => ({
  discoveryId: `aqgreen:1:${id}`, customerId: id, tenantId: 1, currentAreaId: areaId,
  discoveryKind: 2, participationVersionEvidence: 1,
  historicalEvidenceIssues: ["SPONSOR_HISTORY_UNVERIFIABLE", "ACTIVATION_HISTORY_UNVERIFIABLE"],
  clubMemberNumber: "SENSITIVE_SYNTHETIC_MARKER", joiningAmount: 9999,
  verifiedPayments: [{ providerReference: "SENSITIVE_SYNTHETIC_MARKER" }],
});
const envelope = (result: unknown, status = 200, recovery = true) => new Response(JSON.stringify({
  result, success: true, __abp: true, unAuthorizedRequest: false,
}), { status, headers: recovery ? { "X-Aqua-Recovery-Suspended": "true" } : {} });
const request = (operation = "discovery") => new Request(`https://portal.example.test/api/recovery/b6?operation=${operation}`);

let areas: typeof area[];
let liveTenant: typeof tenant | null;
let records: ReturnType<typeof row>[];
let denied: string | undefined;

describe("fixed-scope B6 recovery read relay", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    areas = [area]; liveTenant = tenant; records = [row(1), row(2), row(3)]; denied = undefined;
    readSession.mockResolvedValue({ accessToken: "synthetic-token", expiresAt: "2099-01-01", tenant: "Default" });
    projectSession.mockReturnValue({ user: { id: 4, tenantId: 1, role: "SystemAdmin" } });
    vi.stubGlobal("fetch", vi.fn(async (input: string) => {
      const url = new URL(input);
      if (denied && url.pathname.includes(denied)) return envelope(null, 403);
      if (url.pathname === "/api/health") return envelope({ status: "Healthy" });
      if (url.pathname.endsWith("GetCurrentLoginInformations")) return envelope({ tenant: liveTenant, user: { id: 4, emailAddress: "synthetic@example.test" } });
      if (url.pathname.endsWith("GetAssignedAreas")) return envelope(areas);
      if (url.pathname.endsWith("GetLegacyAQGreenReconciliation")) {
        const offset = Number(url.searchParams.get("SkipCount"));
        return envelope({ totalCount: records.length, items: records.slice(offset, offset + 25) });
      }
      throw new Error("Unexpected synthetic request");
    }));
  });
  afterEach(() => vi.unstubAllGlobals());
  const discoveryCalls = () => vi.mocked(fetch).mock.calls.filter(([url]) => String(url).includes("GetLegacyAQGreenReconciliation"));

  it("checks live identity and the actual original Area, then returns only aggregate evidence", async () => {
    records[1] = { ...row(2), discoveryKind: 1, participationVersionEvidence: 3, historicalEvidenceIssues: ["JOINING_PAYMENT_LINKS_CONFLICT"] };
    const response = await GET(request());
    const body = await response.json();
    expect(response.status).toBe(200);
    expect(response.headers.get("Cache-Control")).toContain("no-store");
    expect(body.scope.tenant.id).toBe(1);
    expect(body.scope.areas).toEqual([area]);
    expect(body.inventory).toMatchObject({ examined: 3, totalCount: 3, wholeCurrentScopeExamined: true, classifications: [1, 2, 0, 0, 0], evidenceIssues: { SPONSOR_HISTORY_UNVERIFIABLE: 2, JOINING_PAYMENT_LINKS_CONFLICT: 1 } });
    expect(JSON.stringify(body)).not.toContain("SENSITIVE_SYNTHETIC_MARKER");
    expect(JSON.stringify(body)).not.toContain("synthetic-token");
    for (const [url, options] of vi.mocked(fetch).mock.calls) {
      expect(options).toMatchObject({ method: "GET", cache: "no-store" });
      expect(String(url)).not.toMatch(/Export|Checkout|Approve|Activate|TokenAuth/);
    }
  });
  it("reads scope without reading historical records", async () => {
    const body = await (await GET(request("scope"))).json();
    expect(body.scopeVerified).toBe(true);
    expect(body.inventory).toBeUndefined();
    expect(discoveryCalls()).toHaveLength(0);
  });
  it("denies an anonymous session without any API call", async () => {
    readSession.mockResolvedValue(null);
    expect((await GET(request())).status).toBe(401);
    expect(fetch).not.toHaveBeenCalled();
  });
  it("denies non-administrator UI access without any API call", async () => {
    projectSession.mockReturnValue({ user: { id: 4, tenantId: 1, role: "Member" } });
    expect((await GET(request())).status).toBe(403);
    expect(fetch).not.toHaveBeenCalled();
  });
  it("does not equate unavailable capability status with verified recovery", async () => {
    vi.mocked(fetch).mockResolvedValue(envelope(null, 200, false));
    const body = await (await GET(request())).json();
    expect(body.recoveryVerified).toBe(false);
    expect(body.inventory).toBeUndefined();
    expect(discoveryCalls()).toHaveLength(0);
  });
  it.each([null, { ...tenant, id: 2 }])("rejects unapproved Tenant/host identity %j before Area access", async value => {
    liveTenant = value;
    const response = await GET(request());
    expect(response.status).toBe(403);
    expect((await response.json()).scope.areas).toBeNull();
    expect(vi.mocked(fetch).mock.calls.some(([url]) => String(url).includes("GetAssignedAreas"))).toBe(false);
    expect(discoveryCalls()).toHaveLength(0);
  });
  it("rejects a live user identity that differs from the session", async () => {
    projectSession.mockReturnValue({ user: { id: 5, tenantId: 1, role: "SystemAdmin" } });
    expect((await GET(request())).status).toBe(401);
    expect(discoveryCalls()).toHaveLength(0);
  });
  it.each([
    { value: [] }, { value: [area, { ...area, areaId: "a0000000-0000-0000-0000-000000000002", code: "OTHER" }] },
    { value: [{ ...area, areaId: "a0000000-0000-0000-0000-000000000002" }] }, { value: [{ ...area, code: "OTHER" }] },
  ])(
    "rejects an unverifiable, missing or wider assigned envelope %j before discovery", async ({ value }) => {
      areas = value;
      const response = await GET(request());
      expect(response.status).toBe(403);
      const body = await response.json();
      expect(body.scope.areas).toEqual(value);
      expect(body.scopeVerified).toBe(false);
      expect(discoveryCalls()).toHaveLength(0);
    },
  );
  it.each(["GetAssignedAreas", "GetLegacyAQGreenReconciliation"])("preserves authoritative permission refusal for %s", async value => {
    denied = value;
    const response = await GET(request());
    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.blocker).toContain("application denied");
    expect(body.inventory).toBeUndefined();
  });
  it("bounds pagination at 100 records and clearly reports a partial population", async () => {
    records = Array.from({ length: 105 }, (_, i) => row(i + 1));
    const body = await (await GET(request())).json();
    expect(body.inventory).toMatchObject({ examined: 100, totalCount: 105, wholeCurrentScopeExamined: false });
    expect(discoveryCalls().map(([url]) => new URL(String(url)).searchParams.get("SkipCount"))).toEqual(["0", "25", "50", "75"]);
    expect(body.inventory.classifications).toEqual([0, 100, 0, 0, 0]);
  });
  it.each(["duplicate", "crossTenant", "crossArea", "identity", "unknownIssue", "missingIssue"])("rejects %s evidence without returning a partial inventory", async scenario => {
    if (scenario === "duplicate") records[1] = row(1);
    if (scenario === "crossTenant") records[1].tenantId = 2;
    if (scenario === "crossArea") records[1].currentAreaId = "a0000000-0000-0000-0000-000000000002";
    if (scenario === "identity") records[1].discoveryId = "aqgreen:1:999";
    if (scenario === "unknownIssue") records[1].historicalEvidenceIssues = ["UNKNOWN_CONTRACT_ISSUE"];
    if (scenario === "missingIssue") Reflect.deleteProperty(records[1], "historicalEvidenceIssues");
    const response = await GET(request());
    expect(response.status).toBe({ duplicate: 409, crossTenant: 403, crossArea: 403, identity: 409, unknownIssue: 502, missingIssue: 502 }[scenario]);
    expect((await response.json()).inventory).toBeUndefined();
    expect(discoveryCalls()).toHaveLength(1);
  });
  it("halts if the assigned envelope widens during the read", async () => {
    const implementation = vi.mocked(fetch).getMockImplementation()!;
    let checks = 0;
    vi.mocked(fetch).mockImplementation(async (input, options) => {
      if (String(input).includes("GetAssignedAreas") && ++checks === 3)
        return envelope([area, { ...area, areaId: "a0000000-0000-0000-0000-000000000002" }]);
      return implementation(input, options);
    });
    const response = await GET(request());
    expect(response.status).toBe(403);
    expect((await response.json()).inventory).toBeUndefined();
    expect(discoveryCalls()).toHaveLength(1);
  });
  it("rejects changing page counts", async () => {
    records = Array.from({ length: 30 }, (_, i) => row(i + 1));
    const implementation = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, options) => String(input).includes("SkipCount=25")
      ? envelope({ totalCount: 31, items: records.slice(25) }) : implementation(input, options));
    expect((await GET(request())).status).toBe(409);
  });
  it("rejects incomplete pages", async () => {
    const implementation = vi.mocked(fetch).getMockImplementation()!;
    vi.mocked(fetch).mockImplementation(async (input, options) => String(input).includes("GetLegacyAQGreenReconciliation")
      ? envelope({ totalCount: 30, items: [row(1)] }) : implementation(input, options));
    expect((await GET(request())).status).toBe(409);
  });
  it("reports a verified empty inventory without inventing historical conclusions", async () => {
    records = [];
    const body = await (await GET(request())).json();
    expect(body.inventory).toMatchObject({ examined: 0, totalCount: 0, wholeCurrentScopeExamined: true });
  });
  it("rejects client scope overrides before reading any source", async () => {
    expect((await GET(new Request("https://portal.example.test/api/recovery/b6?TenantId=2"))).status).toBe(400);
    expect(fetch).not.toHaveBeenCalled();
  });
});

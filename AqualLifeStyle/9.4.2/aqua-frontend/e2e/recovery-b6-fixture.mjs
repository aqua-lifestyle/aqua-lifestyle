// Synthetic-only recovery responses in the existing browser test backend.
const originalArea = { areaId: "a0000000-0000-0000-0000-000000000001", code: "JHB", name: "Johannesburg" };
const reads = new Map();
const send = (response, status, result) => {
  response.writeHead(status, { "Content-Type": "application/json", "X-Aqua-Recovery-Suspended": "true" });
  response.end(JSON.stringify({ result, success: status === 200, __abp: true, unAuthorizedRequest: status === 401 }));
};
export function handleRecoveryB6(request, response, url, createAccessToken) {
  if (process.env.MOCK_RECOVERY_B6 !== "1") return false;
  if (url.pathname === "/api/health") {
    send(response, 200, { status: "Healthy", isDatabaseReachable: true, version: "9.3.0", buildId: "synthetic-recovery", paymentContractVersion: "aqgreen-flexible-joining-v1", contractCapabilities: [] });
    return true;
  }
  if (url.pathname === "/__recovery-test-observation") {
    send(response, 200, { discoveryReads: reads.get(url.searchParams.get("scenario")) ?? 0 });
    return true;
  }
  if (url.pathname === "/api/TokenAuth/Authenticate" && request.method === "POST") {
    let body = "";
    request.on("data", chunk => { body += chunk; });
    request.on("end", () => {
      const email = JSON.parse(body).userNameOrEmailAddress;
      const scenario = email.split("@")[0];
      const tenantId = scenario.endsWith("host") ? undefined : scenario.endsWith("wrong-tenant") ? "2" : "1";
      send(response, 200, { accessToken: createAccessToken({ email, name: "Synthetic Administrator", role: "SystemAdmin", sub: "4", tenantId, permissions: ["Aqua.Admin.ProgrammeParticipations.View"] }), expireInSeconds: 3600 });
    });
    return true;
  }
  let claims;
  try { claims = JSON.parse(Buffer.from(request.headers.authorization?.split(".")[1] ?? "", "base64url").toString()); } catch { /* public capability request */ }
  const scenario = claims?.email?.split("@")[0];
  if (request.method === "GET" && url.pathname.endsWith("GetCurrentLoginInformations")) {
    send(response, claims ? 200 : 401, claims ? { user: { id: 4 }, tenant: claims.tenantId ? { id: Number(claims.tenantId), name: "Synthetic Tenant", tenancyName: "Default" } : null } : null);
    return true;
  }
  if (request.method === "GET" && url.pathname.endsWith("GetAssignedAreas")) {
    if (!claims || scenario.endsWith("no-area-permission")) { send(response, claims ? 403 : 401, null); return true; }
    const otherArea = { areaId: "a0000000-0000-0000-0000-000000000002", code: "OTHER", name: "Synthetic other Area" };
    send(response, 200, scenario.endsWith("wide") ? [originalArea, otherArea] : scenario.endsWith("wrong-area") ? [otherArea] : [originalArea]);
    return true;
  }
  if (request.method === "GET" && url.pathname.endsWith("GetLegacyAQGreenReconciliation")) {
    if (!claims || scenario.endsWith("no-discovery-permission")) { send(response, claims ? 403 : 401, null); return true; }
    reads.set(scenario, (reads.get(scenario) ?? 0) + 1);
    const issues = [
      ["SPONSOR_HISTORY_UNVERIFIABLE", "HISTORICAL_JOINING_PAYMENT_UNVERIFIABLE", "ACTIVATION_HISTORY_UNVERIFIABLE"],
      ["JOINING_PAYMENT_LINKS_CONFLICT", "ATTRIBUTION_CONFIRMATION_CONFLICT"],
      ["MEMBERSHIP_ASSIGNMENT_UNVERIFIABLE"],
    ];
    send(response, 200, { totalCount: 3, items: [1, 2, 3].map((id, index) => ({
      discoveryId: `aqgreen:1:${id}`, customerId: id, tenantId: 1, currentAreaId: originalArea.areaId,
      discoveryKind: [2, 1, 4][index], participationVersionEvidence: [1, 3, 2][index], historicalEvidenceIssues: issues[index],
      clubMemberNumber: "SENSITIVE_SYNTHETIC_MARKER", joiningAmount: 9999,
    })) });
    return true;
  }
  send(response, 503, null);
  return true;
}

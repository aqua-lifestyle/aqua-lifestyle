import { createServer } from "node:http";

const port = Number(process.env.MOCK_BACKEND_PORT ?? 3200);

const encode = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
const accessToken = `${encode({ alg: "none", typ: "JWT" })}.${encode({
  sub: "42",
  email: "member@example.test",
  name: "Test Member",
  role: "Member",
  tenantId: "7",
  permissions: [
    "Aqua.ProgrammeParticipations.ViewSelf",
    "Aqua.Savings.ViewSelf",
  ],
})}.test-signature`;

const participation = {
  areaId: "area-7",
  areaName: "Test Area",
  canJoinEntry: true,
  canJoinOnyxDirectly: true,
  clubMemberNumber: "TEST-42",
  entry: null,
  funeralCover: null,
  onyx: null,
  pendingAQGreenCheckout: null,
  pendingDirectOnyxCheckout: null,
  travelBenefit: null,
};

const activationSteps = [
  ["joined", "Join", "Current", "Choose how you want to join."],
  ["paid", "Payment", "Upcoming", "Complete the joining payment."],
  ["reviewed", "Review", "Upcoming", "The Area team reviews the participation."],
  ["active", "Active", "Upcoming", "The programme becomes active."],
].map(([code, label, state, explanation]) => ({ code, explanation, label, state }));

const makeLevels = (requirements) => requirements.map((requiredCount, index) => ({
  achievedCount: 0,
  commissionComponentAmount: 0,
  commissionRate: null,
  commissionRateLabel: "Not available yet",
  isStructurallyComplete: false,
  label: `Level ${index + 1}`,
  level: index + 1,
  measureLabel: "Build your network",
  progressPercent: 0,
  remainingCount: requiredCount,
  requiredCount,
  state: index === 0 ? "Current" : index === 1 ? "Next" : "Locked",
}));

const makeJourney = (programmeCode, programmeName, requirements) => ({
  activatedAt: null,
  activationSteps,
  benefits: [{
    amount: null,
    availableAt: null,
    code: `${programmeCode}_BENEFIT`,
    currency: null,
    description: "Available after activation.",
    name: `${programmeName} benefit`,
    state: "Locked",
    unlockedAt: null,
  }],
  currency: "ZAR",
  decisionReason: null,
  earnings: {
    currency: "ZAR",
    earnedAwaitingRelease: 0,
    latestRecordedWeek: null,
    onHold: 0,
    recentWeeks: [],
    recordedAsPaid: 0,
    releasedAwaitingPayment: 0,
    totalEarned: 0,
  },
  hasParticipation: false,
  isActive: false,
  joining: {
    completedAt: null,
    isComplete: false,
    kind: "Joining payment",
    paidAmount: 0,
    progressPercent: 0,
    remainingAmount: programmeCode === "AQGREEN" ? 1200 : 7000,
    requiredAmount: programmeCode === "AQGREEN" ? 1200 : 7000,
    scheduleLabel: "Not started",
  },
  levels: makeLevels(requirements),
  maximumLevel: requirements.length,
  monthlySubscription: null,
  nextActionBody: `Start your ${programmeName} participation when you are ready.`,
  nextActionCode: "JoinProgramme",
  nextActionTitle: `Join ${programmeName}`,
  participationStatus: "Not joined",
  programmeCode,
  programmeName,
  qualifiedLevel: 0,
  startedAt: null,
});

const responses = new Map([
  ["/api/health", {
    buildId: "e2e",
    checkedAtUtc: "2026-08-12T00:00:00.000Z",
    contractCapabilities: [
      "aqgreen-flexible-joining-v1",
      "programme-approval-queue-v1",
      "direct-onyx-checkout-v1",
      "member-programme-journey-v1",
    ],
    databaseStatus: "Healthy",
    environment: "E2E",
    imageId: "e2e",
    isDatabaseReachable: true,
    paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
    releaseDate: "2026-08-12",
    status: "Healthy",
    traceId: "e2e-trace",
    version: "e2e",
  }],
  ["/api/services/app/Customer/GetMyCustomer", {
    email: "member@example.test",
    id: 42,
    isActive: true,
    membershipId: null,
    name: "Test Member",
    tenantId: 7,
    userId: 42,
  }],
  ["/api/services/app/Product/GetAllForCustomer", [{
    id: 5,
    isActive: true,
    membershipId: null,
    name: "Water filter",
    price: 149,
  }]],
  ["/api/services/app/Membership/GetActiveTiers", []],
  ["/api/services/app/Membership/GetSavingsWindowStatuses", []],
  ["/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations", participation],
  ["/api/services/app/ClubMemberProgrammeProgress/GetMyJourney", {
    programmes: [
      makeJourney("AQGREEN", "AQGreen", [5, 25, 125]),
      makeJourney("ONYX", "Onyx", [5, 25, 125, 625, 3125]),
    ],
    projectedAt: "2026-08-12T00:00:00.000Z",
  }],
]);

const sendJson = (response, status, body) => {
  response.writeHead(status, {
    "Access-Control-Allow-Headers": "Content-Type,__tenant",
    "Access-Control-Allow-Origin": "http://127.0.0.1:3100",
    "Content-Type": "application/json",
  });
  response.end(JSON.stringify(body));
};

createServer((request, response) => {
  const url = new URL(request.url ?? "/", `http://127.0.0.1:${port}`);
  if (request.method === "OPTIONS") return sendJson(response, 204, null);

  if (request.method === "POST" && url.pathname === "/api/TokenAuth/Authenticate") {
    let body = "";
    request.on("data", (chunk) => { body += chunk; });
    request.on("end", () => {
      const credentials = JSON.parse(body);
      sendJson(response, 200, {
        result: {
          accessToken,
          expireInSeconds: credentials.userNameOrEmailAddress === "expiring@example.test" ? 1 : 3600,
        },
      });
    });
    return;
  }

  if (url.pathname === "/api/services/app/Account/GetTenantSelfRegistrationAvailability") {
    return sendJson(response, 200, { result: { isSelfRegistrationEnabled: false } });
  }

  if (!request.headers.authorization?.startsWith("Bearer ")) {
    return sendJson(response, 401, { error: { message: "Authentication required." } });
  }
  const result = responses.get(url.pathname);
  return result === undefined
    ? sendJson(response, 404, { error: { message: `No E2E fixture for ${url.pathname}.` } })
    : sendJson(response, 200, result);
}).listen(port, "127.0.0.1");

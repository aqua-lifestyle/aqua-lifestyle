import { createServer } from "node:http";

const port = Number(process.env.MOCK_BACKEND_PORT ?? 3200);

const encode = (value) => Buffer.from(JSON.stringify(value)).toString("base64url");
const createAccessToken = ({ email, name, permissions, role, sub, tenantId }) =>
  `${encode({ alg: "none", typ: "JWT" })}.${encode({
    sub,
    email,
    name,
    role,
    ...(tenantId ? { tenantId } : {}),
    permissions,
  })}.test-signature`;

const memberAccessToken = createAccessToken({
  email: "member@example.test",
  name: "Test Member",
  permissions: [
    "Aqua.ProgrammeParticipations.Invite",
    "Aqua.ProgrammeParticipations.ViewSelf",
    "Aqua.Savings.ViewSelf",
  ],
  role: "Member",
  sub: "42",
  tenantId: "7",
});

const createAdminAccessToken = (email) => createAccessToken({
  email,
  name: "Host Administrator",
  permissions: [
    "Aqua.Admin.AllTenants",
    "Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility",
    "Aqua.Admin.Commissions.View",
    "Aqua.Admin.ProgrammeParticipations.View",
  ],
  role: "SystemAdmin",
  sub: "1",
});

const adminAccessToken = createAdminAccessToken("admin@example.test");

const participation = {
  areaId: "area-7",
  areaName: "Test Area",
  canJoinEntry: true,
  canJoinOnyxDirectly: true,
  clubMemberNumber: "TEST-42",
  entry: {
    activatedAt: "2026-08-01T08:00:00.000Z",
    canRecruitForThisProgramme: true,
    currency: "ZAR",
    isActive: true,
    joinedIndependently: true,
    nextPaymentAmount: null,
    nextPaymentDescription: null,
    programmeCode: "AQGREEN",
    programmeName: "AQGreen",
    recruiterClubMemberNumber: null,
    startedAt: "2026-07-01T08:00:00.000Z",
    status: "Active",
    statusCode: "Active",
  },
  funeralCover: null,
  onyx: {
    activatedAt: "2026-08-28T08:00:00.000Z",
    canRecruitForThisProgramme: true,
    currency: "ZAR",
    isActive: true,
    joinedIndependently: true,
    nextPaymentAmount: null,
    nextPaymentDescription: null,
    programmeCode: "ONYX",
    programmeName: "Onyx",
    recruiterClubMemberNumber: null,
    startedAt: "2026-08-28T08:00:00.000Z",
    status: "Active",
    statusCode: "Active",
  },
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

const activeSteps = [
  ["joined", "Join", "Complete", "Programme joining was recorded."],
  ["paid", "Payment", "Complete", "The admission requirement was confirmed."],
  ["reviewed", "Review", "Complete", "The participation was approved."],
  ["active", "Active", "Complete", "The programme is active."],
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

const aqGreenJourney = {
  ...makeJourney("AQGREEN", "AQGreen", [5, 25, 125]),
  activationSteps: activeSteps,
  earnings: {
    currency: "ZAR",
    earnedAwaitingRelease: 400,
    latestRecordedWeek: {
      commissionedLevel: 2,
      components: [{ amount: 150, level: 1 }, { amount: 250, level: 2 }],
      holdReason: null,
      periodEnd: "2026-08-27T21:59:59.999Z",
      periodStart: "2026-08-20T22:00:00.000Z",
      qualifiedLevel: 2,
      status: "Earned — awaiting release",
      totalAmount: 400,
      zeroReason: null,
    },
    onHold: 0,
    recentWeeks: [],
    recordedAsPaid: 0,
    releasedAwaitingPayment: 0,
    totalEarned: 400,
  },
  hasParticipation: true,
  isActive: true,
  levels: makeLevels([5, 25, 125]).map((level) => level.level <= 2
    ? {
        ...level,
        achievedCount: level.requiredCount,
        isStructurallyComplete: true,
        progressPercent: 100,
        remainingCount: 0,
        state: "Complete",
      }
    : { ...level, state: "Current" }),
  nextActionBody: "Build toward Level 3 by growing your qualifying placement network.",
  nextActionCode: "InviteMembers",
  nextActionTitle: "Build toward Level 3",
  participationStatus: "Active",
  qualifiedLevel: 2,
};

const onyxGraduationJourney = {
  ...makeJourney("ONYX", "Onyx", [5, 25, 125, 625, 3125]),
  activationSteps: activeSteps,
  hasParticipation: true,
  isActive: true,
  joining: {
    completedAt: "2026-08-28T08:00:00.000Z",
    isComplete: true,
    kind: "AQGreen graduation with an Onyx loan",
    paidAmount: 0,
    progressPercent: 100,
    remainingAmount: 0,
    requiredAmount: 0,
    scheduleLabel: "Loan-backed admission",
  },
  participationStatus: "Active",
};

const activeAQGreenParticipation = {
  activatedAt: "2026-08-01T08:00:00.000Z",
  areaName: "Test Area",
  clubMemberNumber: "TEST-42",
  confirmedPayments: [],
  currency: "ZAR",
  customerName: "Test Member",
  email: "member@example.test",
  expectedJoiningAmount: 1200,
  isActive: true,
  joinedIndependently: true,
  nextPaymentAmount: null,
  nextPaymentDescription: null,
  participationId: "11111111-1111-1111-1111-111111111111",
  programmeName: "AQGreen",
  recruiterClubMemberNumber: null,
  startedAt: "2026-07-01T08:00:00.000Z",
  status: "Active",
  tenantId: 7,
};

const initialWeeklySalesReview = {
  areaId: "77777777-7777-7777-7777-777777777777",
  areaName: "Test Area",
  clubMemberNumber: "TEST-42",
  commissionWeekEndUtc: "2026-08-27T21:59:59.999Z",
  commissionWeekStartUtc: "2026-08-20T22:00:00.000Z",
  customerName: "Test Member",
  decisionId: "22222222-2222-2222-2222-222222222222",
  email: "member@example.test",
  evidenceReferences: [],
  participantId: activeAQGreenParticipation.participationId,
  rejectionReason: null,
  reviewStatus: 1,
  reviewedAt: null,
  reviewedByUserId: null,
  reviewedFiveLitreQuantity: null,
  reviewedOneLitreQuantity: null,
  reviewedSprayQuantity: null,
  salesEligibilityRulesVersion: "AQGreenWeeklySalesEligibilityV1",
  tenantId: 7,
  thresholdResult: null,
  timeZoneId: "Africa/Johannesburg",
};

const weeklySalesReviews = new Map([
  [adminAccessToken, structuredClone(initialWeeklySalesReview)],
]);

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
      aqGreenJourney,
      onyxGraduationJourney,
    ],
    projectedAt: "2026-08-12T00:00:00.000Z",
  }],
  ["/api/services/app/AdminProgrammeParticipation/GetAll", {
    items: [activeAQGreenParticipation],
    totalCount: 1,
  }],
  ["/api/services/app/AdminProgrammeParticipation/GetPendingApprovalSummary", {
    aqGreenCount: 0,
    onyxCount: 0,
    totalCount: 0,
  }],
  ["/api/services/app/AdminCommission/GetAll", {
    items: [{
      calculatedAt: "2026-08-28T08:05:00.000Z",
      components: [{ amount: 150, level: 1 }, { amount: 250, level: 2 }],
      currency: "ZAR",
      customerId: 42,
      customerName: "Test Member",
      email: "member@example.test",
      highestCommissionedLevel: 2,
      highestQualifiedLevel: 2,
      holdReason: null,
      id: "33333333-3333-3333-3333-333333333333",
      paidAt: null,
      paymentReference: null,
      periodEnd: "2026-08-27T21:59:59.999Z",
      periodStart: "2026-08-20T22:00:00.000Z",
      programmeName: "AQGreen",
      releasedAt: null,
      releaseReason: null,
      status: "Earned — awaiting release",
      tenantId: 7,
      totalAmount: 400,
    }],
    totalCount: 1,
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
  if (url.pathname === "/api/health") {
    return sendJson(response, 200, responses.get(url.pathname));
  }

  if (request.method === "POST" && url.pathname === "/api/TokenAuth/Authenticate") {
    let body = "";
    request.on("data", (chunk) => { body += chunk; });
    request.on("end", () => {
      const credentials = JSON.parse(body);
      const isAdmin = credentials.userNameOrEmailAddress.startsWith("admin");
      const accessToken = isAdmin
        ? createAdminAccessToken(credentials.userNameOrEmailAddress)
        : memberAccessToken;
      if (isAdmin) {
        weeklySalesReviews.set(
          accessToken,
          structuredClone(initialWeeklySalesReview),
        );
      }
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
    return sendJson(response, 200, { result: { isSelfRegistrationEnabled: true } });
  }

  if (url.pathname === "/api/services/app/ProgrammeInvitation/GetPreview") {
    return sendJson(response, 200, {
      areaName: "Test Area",
      inviteCode: url.searchParams.get("InviteCode"),
      programmeKey: "AQGREEN",
      programmeName: "AQGreen",
      recruiterClubMemberNumber: "TEST-RECRUITER",
      recruiterEligible: true,
      recruiterName: "Test Recruiter",
      tenancyName: "Default",
    });
  }

  if (!request.headers.authorization?.startsWith("Bearer ")) {
    return sendJson(response, 401, { error: { message: "Authentication required." } });
  }
  const accessToken = request.headers.authorization.slice("Bearer ".length);
  const weeklySalesReview = weeklySalesReviews.get(accessToken) ??
    initialWeeklySalesReview;
  if (url.pathname === "/api/services/app/AdminAQGreenWeeklySalesEligibility/GetAll") {
    return sendJson(response, 200, { items: [weeklySalesReview], totalCount: 1 });
  }
  if (url.pathname === "/api/services/app/AdminAQGreenWeeklySalesEligibility/Get" ||
      url.pathname === "/api/services/app/AdminAQGreenWeeklySalesEligibility/GetLatestClosedWeek") {
    return sendJson(response, 200, weeklySalesReview);
  }
  if (request.method === "POST" &&
      url.pathname === "/api/services/app/AdminAQGreenWeeklySalesEligibility/Confirm") {
    let body = "";
    request.on("data", (chunk) => { body += chunk; });
    request.on("end", () => {
      const input = JSON.parse(body);
      const finalizedReview = {
        ...weeklySalesReview,
        evidenceReferences: input.evidenceReferences,
        reviewStatus: 2,
        reviewedAt: "2026-08-28T08:00:00.000Z",
        reviewedByUserId: 1,
        reviewedFiveLitreQuantity: input.fiveLitreQuantity,
        reviewedOneLitreQuantity: input.oneLitreQuantity,
        reviewedSprayQuantity: input.sprayQuantity,
        thresholdResult:
          input.sprayQuantity >= 5 &&
          input.oneLitreQuantity >= 5 &&
          input.fiveLitreQuantity >= 5 ? 1 : 2,
      };
      weeklySalesReviews.set(accessToken, finalizedReview);
      sendJson(response, 200, { id: finalizedReview.decisionId });
    });
    return;
  }
  const result = responses.get(url.pathname);
  return result === undefined
    ? sendJson(response, 404, { error: { message: `No E2E fixture for ${url.pathname}.` } })
    : sendJson(response, 200, result);
}).listen(port, "127.0.0.1");

// Synthetic presentation fixtures only. No authoritative financial/domain evidence.
export const uiPermissions = [
  "Pages.Roles",
  ...Object.entries({
    Customers: ["View", "Create", "Import", "Edit", "Delete"],
    Users: ["View", "Create", "Edit", "AssignRole", "ResetPassword", "Invite", "Delete"],
    Tenants: ["View", "Create", "Edit", "AssignLeader", "Activate"],
    Members: ["View", "Edit", "Suspend", "ChangeTier"],
    AreaLeaders: ["View", "Approve", "Promote", "Demote", "Remove"],
    Facilitators: ["View", "Approve", "Promote", "Demote", "Remove"],
    ProgrammeParticipations: ["View", "Approve", "CorrectRecruiter", "ViewPaymentCheckouts", "TerminatePaymentCheckouts", "GraduateToOnyx"],
    Commissions: ["View", "Calculate", "Release", "RecordPayment", "ReviewAQGreenWeeklySalesEligibility"],
    Savings: ["View"], Loans: ["View"], EntryMonthlyObligations: ["View"],
  }).flatMap(([resource, operations]) => operations.map(operation => `Aqua.Admin.${resource}.${operation}`)),
];

export const longName = "Alexandria".repeat(10);
const date = "2026-10-01T08:00:00.000Z";
const person = (id) => ({
  id, tenantId: 7, userId: id, customerId: id,
  firstName: id === 1 ? "Test" : longName, lastName: "Member",
  name: id === 1 ? "Test Member" : `${longName} Member`,
  customerName: id === 1 ? "Test Member" : `${longName} Member`,
  email: id === 1 ? "member@example.test" : `${"long-email".repeat(12)}@example.test`,
  contactNumber: "+27 00 000 0000", homeAddress: "Synthetic test address",
  isActive: true, creationTime: date, createdAt: date,
  membershipId: id % 2 === 0 ? 2 : 1, membershipName: id % 2 === 0 ? "Onyx" : "AQGreen", membershipType: id % 2,
});
const people = Array.from({ length: 12 }, (_, i) => person(i + 1));
const paged = items => ({ items, totalCount: items.length });
const memberships = [{ id: 1, name: "AQGreen" }, { id: 2, name: "Onyx" }];

export const uiFixtures = {
  "/api/services/app/AdminCustomer/GetAll": paged(people),
  "/api/services/app/AdminUser/GetAll": paged(people.map((p, i) => ({ ...p, role: i % 5, invitationStatus: i % 3 === 0 ? "Accepted" : "Pending", requiresPasswordSetup: i % 3 !== 0, invitationExpiresAt: date }))),
  "/api/services/app/AdminMember/GetAll": paged(people),
  "/api/services/app/AdminTenant/GetAll": paged([{ id: 7, name: "Test Area", tenancyName: "Default", areaLeaderName: null, areaLeaderId: null, isActive: true, hasActivationHistory: false, activationHistoryBeginsAt: null }]),
  "/api/services/app/AdminAreaLeader/GetAll": paged(people.map((p, i) => ({ ...p, licenseType: 0, rank: i % 8, isApproved: i % 2 === 0, directReferrals: 10, indirectReferrals: 20, orderTarget: 50 }))),
  "/api/services/app/AdminFacilitator/GetAll": paged(people.map((p, i) => ({ ...p, areaLeaderId: 1, rank: i % 7, isApproved: i % 2 === 0, directReferrals: 10, indirectReferrals: 20, awardBalance: 1234567890123.45 }))),
  "/api/services/app/Role/GetAll": paged([{ id: 1, name: "Admin", displayName: "Administrator", description: "Area administration responsibilities", grantedPermissions: uiPermissions }]),
  "/api/services/app/AdminCustomer/GetMembershipOptions": memberships,
  "/api/services/app/AdminMember/GetMembershipOptions": memberships,
  "/api/services/app/Membership/GetActiveTiers": memberships,
  "/api/services/app/Customer/GetAll": people,
  "/api/services/app/Membership/GetAll": memberships,
  "/api/services/app/Enquiry/GetAll": [],
  "/api/services/app/OrderIntent/GetAll": [{ id: 1, customerId: 2, createdAt: date, reservedPrice: 123456789012345.67, statusText: "Reserved" }],
  "/api/services/app/AreaLeader/GetAll": [],
  "/api/services/app/Facilitator/GetAll": [],
  "/api/services/app/Referral/GetAll": [],
  "/api/services/app/AdminProgrammeParticipation/GetAll": paged(people.map((p, i) => ({ ...p, participationId: `synthetic-${p.id}`, clubMemberNumber: `TEST-${p.id}`, programmeName: "AQGreen", areaName: "Test Area", currency: "ZAR", expectedJoiningAmount: 1200, status: i % 2 === 0 ? "Active" : "Awaiting Area approval", isActive: i % 2 === 0, joinedIndependently: true, recruiterClubMemberNumber: null, startedAt: date, activatedAt: i % 2 === 0 ? date : null, nextPaymentAmount: null, nextPaymentDescription: null, confirmedPayments: [] }))),
  "/api/services/app/AdminProgrammeParticipation/GetPendingApprovalSummary": { aqGreenCount: 6, onyxCount: 0, totalCount: 6 },
  "/api/services/app/AdminProgrammeParticipation/GetAQGreenJoiningCheckouts": paged([]),
  "/api/services/app/AdminCommission/GetAll": paged(people.map(p => ({ ...p, id: `earning-${p.id}`, currency: "ZAR", programmeName: "AQGreen", status: "Earned — awaiting release", totalAmount: 123456789012345.67, calculatedAt: date, periodStart: date, periodEnd: date, components: [{ level: 1, amount: 150 }], highestQualifiedLevel: 1, highestCommissionedLevel: 1, holdReason: null, paidAt: null, paymentReference: null, releasedAt: null, releaseReason: null }))),
  "/api/services/app/AdminSavings/GetAll": paged(people.map(p => ({ ...p, id: `saving-${p.id}`, currency: "ZAR", status: "Active", principalBalance: 123456789012345.67, projectedInterestAmount: 500, projectedMaturityAmount: 123456789012845.67, maturedAt: null, maturityInterestAmount: null, maturityPayoutAmount: null, maturesAt: date, requiresMaturityProcessing: false, contributions: [] }))),
  "/api/services/app/AdminOnyxLoan/GetAll": paged(people.map(p => ({ ...p, id: `loan-${p.id}`, currency: "ZAR", status: "Active", requiresPayoutHold: false, totalPayableAmount: 123456789012345.67, outstandingAmount: 123456789012345.67, interestRatePercent: 10, repaymentDeadlineAt: date, memberAcceptedAt: date, approvedAt: date }))),
  "/api/services/app/AdminEntryMonthlyObligation/GetAll": paged(people.map(p => ({ ...p, id: `obligation-${p.id}`, currency: "ZAR", amountDue: 123456789012345.67, outstandingAmount: 123456789012345.67, periodYear: 2026, periodMonth: 10, dueAt: date, gracePeriodEndsAt: date, status: "Overdue", paymentId: null }))),
  "/api/services/app/AdminAQGreenWeeklySalesEligibility/GetAll": paged([{ ...person(2), decisionId: "synthetic-review", participantId: "synthetic-participant", clubMemberNumber: `TEST-${"1234".repeat(20)}`, areaName: "Test Area", areaId: "synthetic-area", commissionWeekStartUtc: date, commissionWeekEndUtc: date, reviewStatus: 1, thresholdResult: null, reviewedAt: null, reviewedByUserId: null, reviewedSprayQuantity: null, reviewedOneLitreQuantity: null, reviewedFiveLitreQuantity: null, evidenceReferences: [], rejectionReason: null, salesEligibilityRulesVersion: "AQGreenWeeklySalesEligibilityV1", timeZoneId: "Africa/Johannesburg" }]),
};

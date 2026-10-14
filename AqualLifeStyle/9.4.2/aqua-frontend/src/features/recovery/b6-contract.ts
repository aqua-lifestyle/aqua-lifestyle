export const ORIGINAL_JOHANNESBURG_AREA_ID = "a0000000-0000-0000-0000-000000000001";
// The original system-introduction identity in 20260811150251_SeparateAreaFromTenantBoundary.
// Actual assignments must still be read and verified through the application.
export const B6_RECORD_LIMIT = 100;
export const B6_PAGE_SIZE = 25;

export type RecoveryScope = {
  tenant: { id: number; name: string; tenancyName: string } | null;
  userId: number;
  areas: { areaId: string; code: string; name: string }[] | null;
};

export const DISCOVERY_KINDS = [
  "Legacy assignment with recorded V2 participation",
  "Legacy assignment without V2 participation",
  "V2 participation requiring reconciliation",
  "Historical information unverifiable",
  "V2 recorded facts",
] as const;

export const EVIDENCE_ISSUES = {
  MEMBERSHIP_ASSIGNMENT_UNVERIFIABLE: "Membership assignment cannot be verified",
  HISTORICAL_MEMBERSHIP_PROVENANCE_UNVERIFIABLE: "Historical membership provenance is unverified",
  CURRENT_AREA_UNASSIGNED: "Current Area is unassigned",
  NO_LIVE_AQGREEN_PARTICIPATION: "No live AQGreen participation record",
  PARTICIPATION_VERSION_UNVERIFIABLE: "Participation version is unverified",
  SPONSOR_HISTORY_UNVERIFIABLE: "Sponsor history is unverified",
  ATTRIBUTION_RULES_UNSUPPORTED: "Recruitment attribution rules are unsupported",
  ATTRIBUTION_CONFIRMATION_MISSING: "Recruitment attribution confirmation is missing",
  ATTRIBUTION_CONFIRMATION_CONFLICT: "Recruitment attribution confirmation conflicts",
  PLACEMENT_RULES_UNSUPPORTED: "Placement rules are unsupported",
  ACTIVE_V2_PLACEMENT_MISSING: "Active recorded V2 participation has no placement",
  ACTIVATION_HISTORY_UNVERIFIABLE: "Approval and activation history is unverified",
  HISTORICAL_JOINING_PAYMENT_UNVERIFIABLE: "Historical joining payment is unverified",
  JOINING_PAYMENT_LINKS_CONFLICT: "Joining payment links conflict",
  LINKED_JOINING_PAYMENT_UNVERIFIABLE: "Linked joining payment cannot be verified",
  LINKED_JOINING_PAYMENT_CONFIRMATION_UNVERIFIABLE: "Linked joining payment confirmation is unverified",
  LINKED_JOINING_PAYMENT_FACTS_CONFLICT: "Linked joining payment facts conflict",
  CONFIRMED_PAYMENT_TIME_MISSING: "Recorded confirmed payment has no confirmation time",
  CHECKOUT_OWNERSHIP_UNVERIFIABLE: "Checkout ownership is unverified",
  CHECKOUT_PAYMENT_LINK_UNVERIFIABLE: "Checkout payment link is unverified",
} as const;

export type RecoveryB6Result = {
  recoveryVerified: boolean;
  scope: RecoveryScope | null;
  scopeVerified: boolean;
  blocker?: string;
  inventory?: {
    examined: number;
    totalCount: number;
    limit: number;
    wholeCurrentScopeExamined: boolean;
    classifications: number[];
    evidenceIssues: Partial<Record<keyof typeof EVIDENCE_ISSUES, number>>;
    observedAt: string;
  };
};

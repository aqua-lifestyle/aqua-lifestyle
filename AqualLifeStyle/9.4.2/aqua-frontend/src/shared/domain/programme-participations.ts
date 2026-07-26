export type ProgrammeParticipation = {
  activatedAt: string | null;
  canRecruitForThisProgramme: boolean;
  currency: string;
  isActive: boolean;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: "AQGreen" | "Onyx";
  recruiterClubMemberNumber: string | null;
  startedAt: string;
  status: string;
};

export type MyProgrammeParticipations = {
  canJoinEntry: boolean;
  canJoinOnyxDirectly: boolean;
  clubMemberNumber: string;
  entry: ProgrammeParticipation | null;
  onyx: ProgrammeParticipation | null;
  pendingDirectOnyxCheckout: PendingDirectOnyxCheckout | null;
  travelBenefit: OnyxTravelBenefit | null;
};

export type DirectOnyxCheckout = {
  amount: number;
  checkoutUrl: string;
  currency: string;
};

export type PendingDirectOnyxCheckout = DirectOnyxCheckout & {
  status: "Awaiting payment";
};

export type OnyxTravelBenefit = {
  activatedAt: string | null;
  eligibleAt: string;
  memberTripContributionPercent: number;
  status: "Available" | "Waiting period";
  waitingPeriodEndsAt: string;
};

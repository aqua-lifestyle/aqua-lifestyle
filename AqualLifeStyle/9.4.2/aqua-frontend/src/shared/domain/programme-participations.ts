export type ProgrammeParticipation = {
  activatedAt: string | null;
  canRecruitForThisProgramme: boolean;
  currency: string;
  id: string;
  isActive: boolean;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: "AQGreen" | "Onyx";
  recruiterCustomerId: number | null;
  startedAt: string;
  status: string;
};

export type MyProgrammeParticipations = {
  canJoinEntry: boolean;
  canJoinOnyxDirectly: boolean;
  customerId: number;
  entry: ProgrammeParticipation | null;
  onyx: ProgrammeParticipation | null;
  travelBenefit: OnyxTravelBenefit | null;
};

export type OnyxTravelBenefit = {
  activatedAt: string | null;
  eligibleAt: string;
  memberTripContributionPercent: number;
  status: "Available" | "Waiting period";
  waitingPeriodEndsAt: string;
};

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
  pendingAQGreenCheckout: PendingProgrammeCheckout | null;
  pendingDirectOnyxCheckout: PendingProgrammeCheckout | null;
  travelBenefit: OnyxTravelBenefit | null;
};

export type ProgrammeCheckout = {
  amount: number;
  checkoutUrl: string;
  currency: string;
};

export type PendingProgrammeCheckout = ProgrammeCheckout & {
  status: "Awaiting payment";
};

export type OnyxTravelBenefit = {
  activatedAt: string | null;
  eligibleAt: string;
  memberTripContributionPercent: number;
  status: "Available" | "Waiting period";
  waitingPeriodEndsAt: string;
};

export const getActiveProgrammeNames = (
  participations: MyProgrammeParticipations | undefined,
) =>
  [participations?.entry, participations?.onyx]
    .filter((participation) => participation?.isActive)
    .map((participation) => participation!.programmeName);

export const getPendingProgrammeNames = (
  participations: MyProgrammeParticipations | undefined,
) =>
  [participations?.entry, participations?.onyx]
    .filter((participation) => participation && !participation.isActive)
    .map((participation) => participation!.programmeName);

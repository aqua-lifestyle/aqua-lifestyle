export type ProgrammeParticipation = {
  activatedAt: string | null;
  canRecruitForThisProgramme: boolean;
  currency: string;
  isActive: boolean;
  joiningCompletedAt?: string | null;
  joiningOutstandingAmount?: number;
  joiningPaidAmount?: number;
  joiningSchedule?: AQGreenJoiningPaymentSchedule | null;
  joiningTotalAmount?: number;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: "AQGreen" | "Onyx";
  programmeCode?: "AQGREEN" | "ONYX";
  recruiterClubMemberNumber: string | null;
  startedAt: string;
  status: string;
  monthlyGracePeriodDays?: number;
  monthlySubscriptionAmount?: number;
};

export type AQGreenJoiningPaymentSchedule = 0 | 1;
export type AQGreenJoiningPaymentStage = 0 | 1 | 2;

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
  joiningSchedule?: AQGreenJoiningPaymentSchedule | null;
  joiningStage?: AQGreenJoiningPaymentStage | null;
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

export const getProgrammeStatusLabel = (
  participations: MyProgrammeParticipations | undefined,
  membershipName: string | null | undefined,
  fallbackLabel: string,
) => {
  const activeProgrammeNames = getActiveProgrammeNames(participations);
  if (activeProgrammeNames.length > 0) return activeProgrammeNames.join(" and ");
  if (membershipName) return membershipName;
  if (getPendingProgrammeNames(participations).length > 0) return "Activation pending";
  return fallbackLabel;
};

export const getProgrammeStatusDescription = (
  participations: MyProgrammeParticipations | undefined,
  membershipName: string | null | undefined,
) => {
  const activeProgrammeNames = getActiveProgrammeNames(participations);
  if (activeProgrammeNames.length > 0) return "Active programme participation";
  if (membershipName) return "Active membership plan";

  const pendingProgrammeNames = getPendingProgrammeNames(participations);
  if (pendingProgrammeNames.length > 0) {
    return `${pendingProgrammeNames.join(" and ")} payment confirmation pending`;
  }
  return "No active membership or programme";
};

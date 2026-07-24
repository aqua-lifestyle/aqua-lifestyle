export type ProgrammeParticipation = {
  activatedAt: string | null;
  canRecruitForThisProgramme: boolean;
  currency: string;
  id: string;
  isActive: boolean;
  joinedIndependently: boolean;
  nextPaymentAmount: number | null;
  nextPaymentDescription: string | null;
  programmeName: "Entry" | "Onyx";
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
};

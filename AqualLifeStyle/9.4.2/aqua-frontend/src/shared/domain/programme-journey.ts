export type ProgrammeLevelState = "Complete" | "Current" | "Next" | "Locked";

export type ProgrammeLevelProgress = {
  level: number;
  label: string;
  state: ProgrammeLevelState;
  measureLabel: string;
  achievedCount: number;
  requiredCount: number;
  remainingCount: number;
  progressPercent: number;
  isStructurallyComplete: boolean;
  commissionRate: number | null;
  commissionRateLabel: string;
  commissionComponentAmount: number;
};

export type ProgrammeActivationStep = {
  code: string;
  label: string;
  state: "Complete" | "Current" | "Upcoming" | "Declined";
  explanation: string;
};

export type ProgrammeJoiningProgress = {
  kind: string;
  requiredAmount: number;
  paidAmount: number;
  remainingAmount: number;
  progressPercent: number;
  scheduleLabel: string;
  isComplete: boolean;
  completedAt: string | null;
};

export type ProgrammeMonthlySubscription = {
  status: string;
  monthlyAmount: number;
  outstandingAmount: number | null;
  dueAt: string | null;
  explanation: string;
  requiresAction: boolean;
};

export type ProgrammeEarningComponent = {
  level: number;
  amount: number;
};

export type ProgrammeCycleEarning = {
  periodStart: string;
  periodEnd: string;
  totalAmount: number;
  status: string;
  holdReason: string | null;
  zeroReason: string | null;
  qualifiedLevel: number;
  commissionedLevel: number;
  components: ProgrammeEarningComponent[];
};

export type ProgrammeEarnings = {
  currency: string;
  totalEarned: number;
  earnedAwaitingRelease: number;
  onHold: number;
  releasedAwaitingPayment: number;
  recordedAsPaid: number;
  latestRecordedWeek: ProgrammeCycleEarning | null;
  recentWeeks: ProgrammeCycleEarning[];
};

export type ProgrammeBenefit = {
  code: string;
  name: string;
  state: "Unlocked" | "Available" | "Waiting period" | "Locked" | "Pending record";
  description: string;
  amount: number | null;
  currency: string | null;
  unlockedAt: string | null;
  availableAt: string | null;
};

export type MemberProgrammeJourney = {
  programmeCode: "AQGREEN" | "ONYX";
  programmeName: "AQGreen" | "Onyx";
  hasParticipation: boolean;
  participationStatus: string;
  decisionReason: string | null;
  isActive: boolean;
  startedAt: string | null;
  activatedAt: string | null;
  currency: string;
  qualifiedLevel: number;
  maximumLevel: number;
  activationSteps: ProgrammeActivationStep[];
  levels: ProgrammeLevelProgress[];
  joining: ProgrammeJoiningProgress;
  monthlySubscription: ProgrammeMonthlySubscription | null;
  earnings: ProgrammeEarnings;
  benefits: ProgrammeBenefit[];
  nextActionCode: string;
  nextActionTitle: string;
  nextActionBody: string;
};

export type MyProgrammeJourney = {
  projectedAt: string;
  programmes: MemberProgrammeJourney[];
};

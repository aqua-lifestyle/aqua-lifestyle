export type MyProgrammeProgress = {
  hasEntryParticipation: boolean;
  qualifiedLevelLabel: string;
  qualifiedLevel: number;
  nextLevelLabel: string | null;
  directRecruits: number;
  directRecruitsRequired: number;
  recruitsRemaining: number;
  recruitmentProgressPercent: number;
  structuralProgress?: AQGreenStructuralProgress;
  currency: string;
  totalEarned: number;
  earnedAwaitingRelease: number;
  onHold: number;
  releasedAwaitingPayment: number;
  paid: number;
  recentEarnings: MemberWeeklyEarning[];
  monthlyObligationStatus: string | null;
  monthlyObligationAmount: number | null;
  monthlyObligationDueAt: string | null;
  monthlyObligationOutstanding: number | null;
  nextAction: string | null;
  nextActionAmount: number | null;
  funeralCoverIncluded: boolean;
  funeralCoverBenefitAmount: number;
  education: ProgrammeEducationItem[];
};

export type AQGreenStructuralProgress = {
  completedLevel: number;
  targetLevel: number | null;
  achievedCount: number;
  requiredCount: number;
  remainingCount: number;
  progressPercent: number;
  measureLabel: "Qualifying placement occupants";
  cutoff: string;
  rulesVersion: string;
};

export type MemberWeeklyEarning = {
  periodStart: string;
  periodEnd: string;
  totalAmount: number;
  status: string;
  holdReason: string | null;
  highestLevel: number;
  highestQualifiedLevel: number;
  highestCommissionedLevel: number;
  calculatedAt: string;
  components: MemberEarningComponent[];
};

export type MemberEarningComponent = {
  level: number;
  amount: number;
};

export type ProgrammeEducationItem = {
  title: string;
  body: string;
};

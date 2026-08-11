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
  state: "Included" | "Available" | "Waiting period" | "Locked" | "Pending record";
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

const activationStepSchema = z.object({
  code: z.string().min(1),
  explanation: z.string().min(1),
  label: z.string().min(1),
  state: z.enum(["Complete", "Current", "Upcoming", "Declined"]),
});

const levelSchema = z.object({
  achievedCount: z.number().int().nonnegative(),
  commissionComponentAmount: z.number().nonnegative(),
  commissionRate: z.number().nonnegative().nullable(),
  commissionRateLabel: z.string().min(1),
  isStructurallyComplete: z.boolean(),
  label: z.string().min(1),
  level: z.number().int().positive(),
  measureLabel: z.string().min(1),
  progressPercent: z.number().int().min(0).max(100),
  remainingCount: z.number().int().nonnegative(),
  requiredCount: z.number().int().positive(),
  state: z.enum(["Complete", "Current", "Next", "Locked"]),
});

const cycleEarningSchema = z.object({
  commissionedLevel: z.number().int().nonnegative(),
  components: z.array(z.object({
    amount: z.number().nonnegative(),
    level: z.number().int().positive(),
  })),
  holdReason: z.string().nullable(),
  periodEnd: z.string().min(1),
  periodStart: z.string().min(1),
  qualifiedLevel: z.number().int().nonnegative(),
  status: z.string().min(1),
  totalAmount: z.number().nonnegative(),
  zeroReason: z.string().nullable(),
});

const programmeJourneySchema = z.object({
  activatedAt: z.string().nullable(),
  activationSteps: z.array(activationStepSchema).length(4),
  benefits: z.array(z.object({
    amount: z.number().nonnegative().nullable(),
    availableAt: z.string().nullable(),
    code: z.string().min(1),
    currency: z.string().min(1).nullable(),
    description: z.string().min(1),
    name: z.string().min(1),
    state: z.enum(["Included", "Available", "Waiting period", "Locked", "Pending record"]),
    unlockedAt: z.string().nullable(),
  })).min(1),
  currency: z.string().min(1),
  decisionReason: z.string().nullable(),
  earnings: z.object({
    currency: z.string().min(1),
    earnedAwaitingRelease: z.number().nonnegative(),
    latestRecordedWeek: cycleEarningSchema.nullable(),
    onHold: z.number().nonnegative(),
    recentWeeks: z.array(cycleEarningSchema),
    recordedAsPaid: z.number().nonnegative(),
    releasedAwaitingPayment: z.number().nonnegative(),
    totalEarned: z.number().nonnegative(),
  }),
  hasParticipation: z.boolean(),
  isActive: z.boolean(),
  joining: z.object({
    completedAt: z.string().nullable(),
    isComplete: z.boolean(),
    kind: z.string().min(1),
    paidAmount: z.number().nonnegative(),
    progressPercent: z.number().int().min(0).max(100),
    remainingAmount: z.number().nonnegative(),
    requiredAmount: z.number().nonnegative(),
    scheduleLabel: z.string().min(1),
  }),
  levels: z.array(levelSchema),
  maximumLevel: z.number().int().positive(),
  monthlySubscription: z.object({
    dueAt: z.string().nullable(),
    explanation: z.string().min(1),
    monthlyAmount: z.number().nonnegative(),
    outstandingAmount: z.number().nonnegative().nullable(),
    requiresAction: z.boolean(),
    status: z.string().min(1),
  }).nullable(),
  nextActionBody: z.string().min(1),
  nextActionCode: z.string().min(1),
  nextActionTitle: z.string().min(1),
  participationStatus: z.string().min(1),
  programmeCode: z.enum(["AQGREEN", "ONYX"]),
  programmeName: z.enum(["AQGreen", "Onyx"]),
  qualifiedLevel: z.number().int().nonnegative(),
  startedAt: z.string().nullable(),
});

const programmeJourneyResponseSchema = z.object({
  programmes: z.array(programmeJourneySchema).length(2),
  projectedAt: z.string().min(1),
}).superRefine((response, context) => {
  const expected = {
    AQGREEN: [5, 25, 125],
    ONYX: [5, 25, 125, 625, 3125],
  } as const;
  const seen = new Set<string>();

  response.programmes.forEach((programme, programmeIndex) => {
    const required = expected[programme.programmeCode];
    if (seen.has(programme.programmeCode)) {
      context.addIssue({
        code: "custom",
        message: `Duplicate ${programme.programmeCode} journey.`,
        path: ["programmes", programmeIndex, "programmeCode"],
      });
    }
    seen.add(programme.programmeCode);

    if (programme.maximumLevel !== required.length ||
      programme.qualifiedLevel > required.length ||
      programme.levels.length !== required.length ||
      programme.levels.some((level, index) =>
        level.level !== index + 1 || level.requiredCount !== required[index])) {
      context.addIssue({
        code: "custom",
        message: `${programme.programmeCode} progression contract is incompatible.`,
        path: ["programmes", programmeIndex, "levels"],
      });
    }
  });
});

export const parseMyProgrammeJourney = (value: unknown): MyProgrammeJourney =>
  programmeJourneyResponseSchema.parse(value);
import { z } from "zod";

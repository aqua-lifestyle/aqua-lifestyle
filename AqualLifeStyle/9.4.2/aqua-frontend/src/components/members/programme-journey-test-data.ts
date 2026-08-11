import type {
  MemberProgrammeJourney,
  MyProgrammeJourney,
  ProgrammeLevelProgress,
} from "@/src/shared/domain/programme-journey";

const requirements = {
  AQGREEN: [5, 25, 125],
  ONYX: [5, 25, 125, 625, 3125],
} as const;

const rates = [50, 20, 12.62, 5, 4];
const aqGreenComponents = [150, 250, 1250];
const aqGreenCumulative = [150, 400, 1650];

const levels = (
  code: "AQGREEN" | "ONYX",
  qualifiedLevel: number,
  achieved: number[],
): ProgrammeLevelProgress[] =>
  requirements[code].map((requiredCount, index) => {
    const level = index + 1;
    const state = level <= qualifiedLevel
      ? "Complete"
      : level === qualifiedLevel + 1
        ? "Current"
        : level === qualifiedLevel + 2
          ? "Next"
          : "Locked";
    const achievedCount = achieved[index] ?? 0;
    return {
      achievedCount,
      commissionComponentAmount: code === "ONYX" ? requiredCount * rates[index] : aqGreenComponents[index],
      commissionRate: code === "ONYX" ? rates[index] : null,
      commissionRateLabel: code === "ONYX" ? "per qualifying person" : `R${aqGreenCumulative[index]} cumulative weekly commission`,
      isStructurallyComplete: level <= qualifiedLevel,
      label: `Level ${level}`,
      level,
      measureLabel: level === 1 ? "Direct recruits" : "Qualifying network members",
      progressPercent: Math.round((achievedCount / requiredCount) * 100),
      remainingCount: Math.max(0, requiredCount - achievedCount),
      requiredCount,
      state,
    };
  });

export const createProgrammeJourney = (
  code: "AQGREEN" | "ONYX",
  overrides: Partial<MemberProgrammeJourney> = {},
): MemberProgrammeJourney => {
  const isAQGreen = code === "AQGREEN";
  return {
    activatedAt: null,
    activationSteps: [
      { code: "Started", explanation: "Choose a programme to begin.", label: "Joining started", state: "Current" },
      { code: "Payment", explanation: "Complete the joining requirement.", label: "Joining payment", state: "Upcoming" },
      { code: "Approval", explanation: "Available after payment is complete.", label: "Area approval", state: "Upcoming" },
      { code: "Active", explanation: "Network progression begins after activation.", label: "Programme active", state: "Upcoming" },
    ],
    benefits: [{
      amount: isAQGreen ? 30000 : null,
      availableAt: null,
      code: isAQGreen ? "AQGREEN_FUNERAL_COVER" : "ONYX_TRAVEL",
      currency: isAQGreen ? "ZAR" : null,
      description: isAQGreen
        ? "An internal inclusion record can be created after the full AQGreen joining payment is confirmed."
        : "Unlocks after completing Onyx Level 3.",
      name: isAQGreen ? "Funeral-cover inclusion" : "Travel benefit",
      state: "Locked",
      unlockedAt: null,
    }],
    currency: "ZAR",
    decisionReason: null,
    earnings: {
      currency: "ZAR",
      earnedAwaitingRelease: 0,
      latestRecordedWeek: null,
      onHold: 0,
      recentWeeks: [],
      recordedAsPaid: 0,
      releasedAwaitingPayment: 0,
      totalEarned: 0,
    },
    hasParticipation: false,
    isActive: false,
    joining: {
      completedAt: null,
      isComplete: false,
      kind: isAQGreen ? "One-time AQGreen joining requirement" : "One-time direct Onyx joining requirement",
      paidAmount: 0,
      progressPercent: 0,
      remainingAmount: isAQGreen ? 1200 : 6120,
      requiredAmount: isAQGreen ? 1200 : 6120,
      scheduleLabel: isAQGreen ? "Choose one payment or two instalments" : "One full payment only",
    },
    levels: levels(code, 0, []),
    maximumLevel: isAQGreen ? 3 : 5,
    monthlySubscription: isAQGreen ? {
      dueAt: null,
      explanation: "This recurring monthly subscription is separate from joining.",
      monthlyAmount: 600,
      outstandingAmount: null,
      requiresAction: false,
      status: "No obligation recorded",
    } : null,
    nextActionBody: `Review the ${isAQGreen ? "AQGreen" : "Onyx"} joining requirement and begin when ready.`,
    nextActionCode: "JoinProgramme",
    nextActionTitle: `Explore ${isAQGreen ? "AQGreen" : "Onyx"}`,
    participationStatus: "Not joined",
    programmeCode: code,
    programmeName: isAQGreen ? "AQGreen" : "Onyx",
    qualifiedLevel: 0,
    startedAt: null,
    ...overrides,
  };
};

export const createJourneyResponse = (
  aqGreen = createProgrammeJourney("AQGREEN"),
  onyx = createProgrammeJourney("ONYX"),
): MyProgrammeJourney => ({
  programmes: [aqGreen, onyx],
  projectedAt: "2026-08-11T10:00:00Z",
});

export const aqGreenLevelTwoPartial = createProgrammeJourney("AQGREEN", {
  activatedAt: "2026-07-01T10:00:00Z",
  activationSteps: [
    { code: "Started", explanation: "Created.", label: "Joining started", state: "Complete" },
    { code: "Payment", explanation: "Confirmed.", label: "Joining payment", state: "Complete" },
    { code: "Approval", explanation: "Approved.", label: "Area approval", state: "Complete" },
    { code: "Active", explanation: "Active.", label: "Programme active", state: "Complete" },
  ],
  hasParticipation: true,
  isActive: true,
  joining: {
    completedAt: "2026-06-30T10:00:00Z",
    isComplete: true,
    kind: "One-time AQGreen joining requirement",
    paidAmount: 1200,
    progressPercent: 100,
    remainingAmount: 0,
    requiredAmount: 1200,
    scheduleLabel: "Two instalments",
  },
  levels: levels("AQGREEN", 1, [5, 17, 0]),
  nextActionBody: "Share your programme invitation to grow your qualifying network.",
  nextActionCode: "InviteMembers",
  nextActionTitle: "Build toward Level 2",
  participationStatus: "Active",
  qualifiedLevel: 1,
  startedAt: "2026-06-01T10:00:00Z",
});

export const onyxLevelThreePartial = createProgrammeJourney("ONYX", {
  activatedAt: "2026-07-01T10:00:00Z",
  activationSteps: [
    { code: "Started", explanation: "Created.", label: "Joining started", state: "Complete" },
    { code: "Payment", explanation: "Confirmed.", label: "Joining payment", state: "Complete" },
    { code: "Approval", explanation: "Approved.", label: "Area approval", state: "Complete" },
    { code: "Active", explanation: "Active.", label: "Programme active", state: "Complete" },
  ],
  hasParticipation: true,
  isActive: true,
  joining: {
    completedAt: null,
    isComplete: true,
    kind: "One-time direct Onyx joining requirement",
    paidAmount: 6120,
    progressPercent: 100,
    remainingAmount: 0,
    requiredAmount: 6120,
    scheduleLabel: "One full payment only",
  },
  levels: levels("ONYX", 2, [5, 25, 84, 0, 0]),
  nextActionBody: "Share your programme invitation to grow your qualifying network.",
  nextActionCode: "InviteMembers",
  nextActionTitle: "Build toward Level 3",
  participationStatus: "Active",
  qualifiedLevel: 2,
  startedAt: "2026-06-01T10:00:00Z",
});

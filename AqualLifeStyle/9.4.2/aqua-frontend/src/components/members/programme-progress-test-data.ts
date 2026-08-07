import type { MyProgrammeProgress } from "@/src/shared/domain/programme-progress";

export const levelOneProgress: MyProgrammeProgress = {
  currency: "ZAR",
  directRecruits: 5,
  directRecruitsRequired: 5,
  earnedAwaitingRelease: 150,
  education: [
    {
      title: "Build your network",
      body: "Every level needs 5 active direct recruits.",
    },
    {
      title: "Weekly earnings",
      body: "Completed levels earn a weekly component.",
    },
    {
      title: "Your monthly subscription",
      body: "A R600 monthly subscription falls due each month.",
    },
    {
      title: "Your R1,200 joining payment",
      body: "Completing joining includes the funeral-cover benefit.",
    },
  ],
  funeralCoverBenefitAmount: 30000,
  funeralCoverIncluded: true,
  hasEntryParticipation: true,
  monthlyObligationAmount: 600,
  monthlyObligationDueAt: "2026-08-01T00:00:00Z",
  monthlyObligationOutstanding: 600,
  monthlyObligationStatus: "Payment due",
  nextAction: "Pay your AQGreen monthly subscription.",
  nextActionAmount: 600,
  nextLevelLabel: "Level 2",
  onHold: 0,
  paid: 0,
  qualifiedLevel: 1,
  qualifiedLevelLabel: "Level 1",
  recentEarnings: [
    {
      calculatedAt: "2026-07-13T00:00:00Z",
      components: [{ amount: 150, level: 1 }],
      highestLevel: 1,
      holdReason: null,
      periodEnd: "2026-07-12T00:00:00Z",
      periodStart: "2026-07-06T00:00:00Z",
      status: "Earned — awaiting release",
      totalAmount: 150,
    },
  ],
  recruitsRemaining: 0,
  recruitmentProgressPercent: 100,
  releasedAwaitingPayment: 0,
  totalEarned: 150,
};

export const heldProgress: MyProgrammeProgress = {
  ...levelOneProgress,
  earnedAwaitingRelease: 0,
  monthlyObligationStatus: "Overdue",
  nextAction:
    "Pay your overdue AQGreen subscription to restore your weekly earnings eligibility.",
  onHold: 150,
  recentEarnings: [
    {
      ...levelOneProgress.recentEarnings[0],
      holdReason: "AQGreen monthly commitment is overdue.",
      status: "On hold",
    },
  ],
};

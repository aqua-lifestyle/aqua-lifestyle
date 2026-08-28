import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ProgrammeJourneyOverview } from "./programme-journey-overview";
import {
  aqGreenLevelTwoPartial,
  createProgrammeJourney,
  onyxLevelThreePartial,
} from "./programme-journey-test-data";

describe("ProgrammeJourneyOverview", () => {
  it("shows AQGreen activation, joining, Level 2 progress, subscription, and next action", () => {
    render(
      <ProgrammeJourneyOverview
        canInvite
        journey={aqGreenLevelTwoPartial}
      />,
    );

    const programme = screen.getByRole("article", { name: "AQGreen" });
    const activation = within(programme).getByRole("region", { name: "Activation journey" });
    const levels = within(programme).getByRole("region", { name: "Programme levels" });
    expect(within(activation).getAllByRole("listitem")).toHaveLength(4);
    expect(within(levels).getAllByRole("listitem")).toHaveLength(3);
    expect(within(levels).queryByText("Level 4")).not.toBeInTheDocument();
    expect(within(programme).getByText("17 / 25")).toBeInTheDocument();
    expect(within(programme).getByText("8 more qualifying network members needed."))
      .toBeInTheDocument();
    expect(within(programme).getByRole("progressbar", { name: "68% of Level 2" }))
      .toHaveAttribute("aria-valuenow", "68");
    expect(within(programme).getByText("Joining payment confirmed")).toBeInTheDocument();
    expect(within(programme).getByText("Recurring monthly subscription"))
      .toBeInTheDocument();
    expect(within(programme).getByRole("link", { name: "Invite someone" }))
      .toHaveAttribute("href", "/member/invitations");
    expect(within(programme).getByText(/R\s*400.*cumulative weekly commission/i))
      .toBeInTheDocument();
    expect(within(programme).getByText(/R\s*1[,.\s]*650.*cumulative weekly commission/i))
      .toBeInTheDocument();
  });

  it("shows all five Onyx levels and the current Level 3 target", () => {
    render(
      <ProgrammeJourneyOverview
        canInvite={false}
        journey={onyxLevelThreePartial}
      />,
    );

    const programme = screen.getByRole("article", { name: "Onyx" });
    const levels = within(programme).getByRole("region", { name: "Programme levels" });
    expect(within(levels).getAllByRole("listitem")).toHaveLength(5);
    expect(within(programme).getByText("84 / 125")).toBeInTheDocument();
    expect(within(programme).getByText("41 more qualifying network members needed."))
      .toBeInTheDocument();
    expect(within(programme).getByRole("progressbar", { name: "67% of Level 3" }))
      .toHaveAttribute("aria-valuenow", "67");
    expect(within(programme).getByText(/12[,.]62 per qualifying person/))
      .toBeInTheDocument();
  });

  it("labels V2 AQGreen journey counts as placement occupancy", () => {
    const journey = createProgrammeJourney("AQGREEN", {
      hasParticipation: true,
      isActive: true,
      levels: aqGreenLevelTwoPartial.levels.map((level) => ({
        ...level,
        measureLabel: "Qualifying placement occupants",
      })),
      qualifiedLevel: 1,
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    const programme = screen.getByRole("article", { name: "AQGreen" });
    expect(within(programme).getAllByText(/qualifying placement occupants/i).length)
      .toBeGreaterThan(0);
    expect(within(programme).getByText(/more qualifying placement occupants needed/))
      .toBeInTheDocument();
    expect(within(programme).queryByText(/more qualifying network members needed/))
      .not.toBeInTheDocument();
  });

  it("shows a paid earning with its authoritative level components", () => {
    const journey = createProgrammeJourney("AQGREEN", {
      earnings: {
        currency: "ZAR",
        earnedAwaitingRelease: 0,
        latestRecordedWeek: {
          commissionedLevel: 2,
          components: [{ amount: 150, level: 1 }, { amount: 250, level: 2 }],
          holdReason: null,
          periodEnd: "2026-08-09T00:00:00Z",
          periodStart: "2026-08-03T00:00:00Z",
          qualifiedLevel: 2,
          status: "Paid",
          totalAmount: 400,
          zeroReason: null,
        },
        onHold: 0,
        recentWeeks: [
          {
            commissionedLevel: 2,
            components: [{ amount: 150, level: 1 }, { amount: 250, level: 2 }],
            holdReason: null,
            periodEnd: "2026-08-09T00:00:00Z",
            periodStart: "2026-08-03T00:00:00Z",
            qualifiedLevel: 2,
            status: "Paid",
            totalAmount: 400,
            zeroReason: null,
          },
          {
            commissionedLevel: 0,
            components: [],
            holdReason: null,
            periodEnd: "2026-08-02T00:00:00Z",
            periodStart: "2026-07-27T00:00:00Z",
            qualifiedLevel: 0,
            status: "Not earned",
            totalAmount: 0,
            zeroReason: "No complete network level was achieved when this week closed.",
          },
          {
            commissionedLevel: 1,
            components: [{ amount: 150, level: 1 }],
            holdReason: "Monthly subscription was overdue when this week closed.",
            periodEnd: "2026-07-26T00:00:00Z",
            periodStart: "2026-07-20T00:00:00Z",
            qualifiedLevel: 1,
            status: "On hold",
            totalAmount: 150,
            zeroReason: null,
          },
        ],
        recordedAsPaid: 400,
        releasedAwaitingPayment: 0,
        totalEarned: 400,
      },
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText("Level 1 component")).toBeInTheDocument();
    expect(screen.getByText("Level 2 component")).toBeInTheDocument();
    expect(screen.getByText("Paid")).toBeInTheDocument();
    expect(screen.getByText("Recent recorded weeks")).toBeInTheDocument();
    expect(screen.getByText("Not earned")).toBeInTheDocument();
    expect(screen.getAllByText("On hold").length).toBeGreaterThan(0);
    expect(screen.getByText("Monthly subscription was overdue when this week closed."))
      .toBeInTheDocument();
    expect(screen.queryByText("Why this week is R0")).not.toBeInTheDocument();
  });

  it.each([
    ["Not earned", null, "No structural level was qualified for this recorded week.", "Why this week is R0"],
    ["On hold", "Monthly subscription is overdue.", null, "Why this earning is held"],
  ])("explains an authoritative %s earning record", (status, holdReason, zeroReason, explanationHeading) => {
    const journey = createProgrammeJourney("AQGREEN", {
      earnings: {
        currency: "ZAR",
        earnedAwaitingRelease: 0,
        latestRecordedWeek: {
          commissionedLevel: 0,
          components: [],
          holdReason,
          periodEnd: "2026-08-09T00:00:00Z",
          periodStart: "2026-08-03T00:00:00Z",
          qualifiedLevel: 0,
          status,
          totalAmount: 0,
          zeroReason,
        },
        onHold: holdReason ? 400 : 0,
        recentWeeks: [],
        recordedAsPaid: 0,
        releasedAwaitingPayment: 0,
        totalEarned: 0,
      },
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText(explanationHeading)).toBeInTheDocument();
    expect(screen.getByText(holdReason ?? zeroReason!)).toBeInTheDocument();
  });

  it("distinguishes a persisted inclusion from joining completion", () => {
    const journey = createProgrammeJourney("AQGREEN", {
      benefits: [{
        amount: 30000,
        availableAt: null,
        code: "AQGREEN_FUNERAL_COVER",
        currency: "ZAR",
        description: "Included with completed joining; insurer enrolment remains separate.",
        name: "Funeral-cover inclusion",
        state: "Included",
        unlockedAt: "2026-08-09T00:00:00Z",
      }],
      joining: {
        completedAt: "2026-08-09T00:00:00Z",
        isComplete: true,
        kind: "One-time AQGreen joining requirement",
        paidAmount: 1200,
        progressPercent: 100,
        remainingAmount: 0,
        requiredAmount: 1200,
        scheduleLabel: "Paid once",
      },
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText("Joining payment confirmed")).toBeInTheDocument();
    expect(screen.getByText("Included")).toBeInTheDocument();
    expect(screen.getByText(/insurer enrolment remains separate/i)).toBeInTheDocument();
  });

  it("does not present loan-backed Onyx graduation as a direct payment", () => {
    const journey = createProgrammeJourney("ONYX", {
      activationSteps: [
        { code: "Started", explanation: "Created.", label: "Joining started", state: "Complete" },
        { code: "Admission", explanation: "The approved Onyx loan-backed admission is confirmed.", label: "Loan-backed admission", state: "Complete" },
        { code: "Approval", explanation: "Approved.", label: "Area approval", state: "Complete" },
        { code: "Active", explanation: "Active.", label: "Programme active", state: "Complete" },
      ],
      hasParticipation: true,
      isActive: true,
      joining: {
        completedAt: null,
        isComplete: true,
        kind: "AQGreen graduation with an Onyx loan",
        paidAmount: 0,
        progressPercent: 100,
        remainingAmount: 0,
        requiredAmount: 0,
        scheduleLabel: "Loan-backed admission",
      },
      participationStatus: "Active",
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText("AQGreen graduation with an Onyx loan")).toBeInTheDocument();
    expect(screen.getByText("Loan-backed admission confirmed")).toBeInTheDocument();
    expect(screen.queryByText(/R\s*6[\s,.]*120/)).not.toBeInTheDocument();
  });

  it("shows the authoritative Onyx waiting-period end date", () => {
    const journey = createProgrammeJourney("ONYX", {
      benefits: [{
        amount: null,
        availableAt: "2026-10-20T10:00:00Z",
        code: "ONYX_TRAVEL",
        currency: null,
        description: "Earned after completing Onyx Level 3.",
        name: "Travel benefit",
        state: "Waiting period",
        unlockedAt: "2026-07-20T10:00:00Z",
      }],
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText("Qualified from 20 Jul 2026")).toBeInTheDocument();
    expect(screen.getByText("Waiting period ends 20 Oct 2026")).toBeInTheDocument();
  });

  it("shows when an active Onyx travel benefit became available", () => {
    const journey = createProgrammeJourney("ONYX", {
      benefits: [{
        amount: null,
        availableAt: "2026-10-20T10:00:00Z",
        code: "ONYX_TRAVEL",
        currency: null,
        description: "Available after completing the waiting period.",
        name: "Travel benefit",
        state: "Available",
        unlockedAt: "2026-07-20T10:00:00Z",
      }],
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText("Qualified from 20 Jul 2026")).toBeInTheDocument();
    expect(screen.getByText("Available since 20 Oct 2026")).toBeInTheDocument();
  });

  it("shows declined Area approval as terminal", () => {
    const journey = createProgrammeJourney("AQGREEN", {
      activationSteps: [
        { code: "Started", explanation: "Created.", label: "Joining started", state: "Complete" },
        { code: "Payment", explanation: "Confirmed.", label: "Joining payment", state: "Complete" },
        { code: "Approval", explanation: "Area approval was declined. Review the recorded decision reason.", label: "Area approval", state: "Declined" },
        { code: "Active", explanation: "Activation did not occur.", label: "Programme active", state: "Declined" },
      ],
      decisionReason: "Identity evidence requires correction.",
      hasParticipation: true,
      participationStatus: "Declined",
    });

    render(<ProgrammeJourneyOverview canInvite={false} journey={journey} />);

    expect(screen.getByText(/Area approval was declined/)).toBeInTheDocument();
    expect(screen.getByText("Activation did not occur.")).toBeInTheDocument();
    expect(screen.getByText(/Identity evidence requires correction/)).toBeInTheDocument();
  });
});

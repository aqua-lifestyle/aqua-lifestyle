using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AqualLifeStyle.Application.ProgrammeParticipations.Dto
{
    public class MyProgrammeJourneyDto
    {
        public DateTime ProjectedAt { get; set; }
        public IReadOnlyList<MemberProgrammeJourneyDto> Programmes { get; set; }
    }

    public class MemberProgrammeJourneyDto
    {
        public string ProgrammeCode { get; set; }
        public string ProgrammeName { get; set; }
        public bool HasParticipation { get; set; }
        public string ParticipationStatus { get; set; }
        public string DecisionReason { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public string Currency { get; set; }
        public int QualifiedLevel { get; set; }
        public int MaximumLevel { get; set; }
        public IReadOnlyList<MemberActivationStepDto> ActivationSteps { get; set; }
        public IReadOnlyList<MemberLevelProgressDto> Levels { get; set; }
        public MemberJoiningProgressDto Joining { get; set; }
        public MemberMonthlyObligationSummaryDto MonthlySubscription { get; set; }
        public MemberProgrammeEarningsDto Earnings { get; set; }
        public IReadOnlyList<MemberProgrammeBenefitDto> Benefits { get; set; }
        public string NextActionCode { get; set; }
        public string NextActionTitle { get; set; }
        public string NextActionBody { get; set; }
    }

    public class MemberActivationStepDto
    {
        public string Code { get; set; }
        public string Label { get; set; }
        public string State { get; set; }
        public string Explanation { get; set; }
    }

    public class MemberLevelProgressDto
    {
        public int Level { get; set; }
        public string Label { get; set; }
        public string State { get; set; }
        public string MeasureLabel { get; set; }
        public int AchievedCount { get; set; }
        public int RequiredCount { get; set; }
        public int RemainingCount { get; set; }
        public int ProgressPercent { get; set; }
        public bool IsStructurallyComplete { get; set; }
        public decimal? CommissionRate { get; set; }
        public string CommissionRateLabel { get; set; }
        public decimal CommissionComponentAmount { get; set; }
    }

    public class MemberJoiningProgressDto
    {
        public string Kind { get; set; }
        public decimal RequiredAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public int ProgressPercent { get; set; }
        public string ScheduleLabel { get; set; }
        public bool IsComplete { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class MemberMonthlyObligationSummaryDto
    {
        public string Status { get; set; }
        public decimal MonthlyAmount { get; set; }
        public decimal? OutstandingAmount { get; set; }
        public DateTime? DueAt { get; set; }
        public string Explanation { get; set; }
        public bool RequiresAction { get; set; }
    }

    public class MemberProgrammeEarningsDto
    {
        public string Currency { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal EarnedAwaitingRelease { get; set; }
        public decimal OnHold { get; set; }
        public decimal ReleasedAwaitingPayment { get; set; }
        public decimal RecordedAsPaid { get; set; }
        public MemberProgrammeCycleEarningDto LatestRecordedWeek { get; set; }
        public IReadOnlyList<MemberProgrammeCycleEarningDto> RecentWeeks { get; set; }
    }

    public class MemberProgrammeCycleEarningDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string HoldReason { get; set; }
        public string ZeroReason { get; set; }
        public int QualifiedLevel { get; set; }
        public int CommissionedLevel { get; set; }
        public IReadOnlyList<MemberEarningComponentDto> Components { get; set; }
    }

    public class MemberProgrammeBenefitDto
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public string Description { get; set; }
        public decimal? Amount { get; set; }
        public string Currency { get; set; }
        public DateTime? UnlockedAt { get; set; }
        public DateTime? AvailableAt { get; set; }
    }

    public class MyProgrammeProgressDto
    {
        public bool HasEntryParticipation { get; set; }
        public string QualifiedLevelLabel { get; set; }
        public int QualifiedLevel { get; set; }
        public string NextLevelLabel { get; set; }
        public int DirectRecruits { get; set; }
        public int DirectRecruitsRequired { get; set; }
        public int RecruitsRemaining { get; set; }
        public int RecruitmentProgressPercent { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AQGreenStructuralProgressDto StructuralProgress { get; set; }
        public string Currency { get; set; }
        public decimal TotalEarned { get; set; }
        public decimal EarnedAwaitingRelease { get; set; }
        public decimal OnHold { get; set; }
        public decimal ReleasedAwaitingPayment { get; set; }
        public decimal Paid { get; set; }
        public IReadOnlyList<MemberWeeklyEarningDto> RecentEarnings { get; set; }
        public string MonthlyObligationStatus { get; set; }
        public decimal? MonthlyObligationAmount { get; set; }
        public DateTime? MonthlyObligationDueAt { get; set; }
        public decimal? MonthlyObligationOutstanding { get; set; }
        public string NextAction { get; set; }
        public decimal? NextActionAmount { get; set; }
        public bool FuneralCoverIncluded { get; set; }
        public decimal FuneralCoverBenefitAmount { get; set; }
        public IReadOnlyList<ProgrammeEducationItemDto> Education { get; set; }
    }

    /// <summary>
    /// Current V2 placement-occupancy progress toward the next incomplete
    /// AQGreen structural level. Recruitment and financial eligibility are
    /// deliberately not represented by these fields.
    /// </summary>
    public class AQGreenStructuralProgressDto
    {
        public int CompletedLevel { get; set; }
        public int? TargetLevel { get; set; }
        public int AchievedCount { get; set; }
        public int RequiredCount { get; set; }
        public int RemainingCount { get; set; }
        public int ProgressPercent { get; set; }
        public string MeasureLabel { get; set; }
        public DateTime Cutoff { get; set; }
        public string RulesVersion { get; set; }
    }

    public class MemberWeeklyEarningDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string HoldReason { get; set; }
        // Backward-compatible commissioned depth used by the existing UI.
        public int HighestLevel { get; set; }
        public int HighestQualifiedLevel { get; set; }
        public int HighestCommissionedLevel { get; set; }
        public DateTime CalculatedAt { get; set; }
        public IReadOnlyList<MemberEarningComponentDto> Components { get; set; }
    }

    public class MemberEarningComponentDto
    {
        public int Level { get; set; }
        public decimal Amount { get; set; }
    }

    public class ProgrammeEducationItemDto
    {
        public string Title { get; set; }
        public string Body { get; set; }
    }
}

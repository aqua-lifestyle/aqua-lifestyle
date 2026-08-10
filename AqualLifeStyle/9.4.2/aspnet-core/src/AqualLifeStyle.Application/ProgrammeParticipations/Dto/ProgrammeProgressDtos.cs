using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.ProgrammeParticipations.Dto
{
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

    public class MemberWeeklyEarningDto
    {
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; }
        public string HoldReason { get; set; }
        public int HighestLevel { get; set; }
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

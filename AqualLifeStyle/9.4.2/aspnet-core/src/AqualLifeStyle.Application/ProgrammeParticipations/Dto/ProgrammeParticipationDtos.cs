using System;
using System.ComponentModel.DataAnnotations;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Recruitment;

namespace AqualLifeStyle.Application.ProgrammeParticipations.Dto
{
    public class StartEntryParticipationInput
    {
        public int? RecruiterCustomerId { get; set; }

        [StringLength(ProgrammeInvitation.CodeLength, MinimumLength = ProgrammeInvitation.CodeLength)]
        public string InviteCode { get; set; }
    }

    public class CreateDirectOnyxCheckoutInput
    {
        public int? RecruiterCustomerId { get; set; }

        [StringLength(ProgrammeInvitation.CodeLength, MinimumLength = ProgrammeInvitation.CodeLength)]
        public string InviteCode { get; set; }
    }

    public class CreateAQGreenJoiningCheckoutInput
    {
        [Required]
        public AQGreenJoiningPaymentSchedule? Schedule { get; set; }
    }

    public class ProgrammeCheckoutDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string CheckoutUrl { get; set; }
    }

    public class PendingProgrammeCheckoutDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string CheckoutUrl { get; set; }
        public string Status { get; set; }
        public AQGreenJoiningPaymentSchedule? JoiningSchedule { get; set; }
        public AQGreenJoiningPaymentStage? JoiningStage { get; set; }
    }

    public class ProgrammeParticipationDto
    {
        public string ProgrammeCode { get; set; }
        public string ProgrammeName { get; set; }
        public string StatusCode { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public bool JoinedIndependently { get; set; }
        public string RecruiterClubMemberNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public DateTime? DecidedAt { get; set; }
        public string DecisionReason { get; set; }
        public decimal? NextPaymentAmount { get; set; }
        public string NextPaymentDescription { get; set; }
        public string Currency { get; set; }
        public bool CanRecruitForThisProgramme { get; set; }
        public AQGreenJoiningPaymentSchedule? JoiningSchedule { get; set; }
        public decimal? JoiningTotalAmount { get; set; }
        public decimal JoiningPaidAmount { get; set; }
        public decimal? JoiningOutstandingAmount { get; set; }
        public DateTime? JoiningCompletedAt { get; set; }
        public decimal? MonthlySubscriptionAmount { get; set; }
        public int? MonthlyGracePeriodDays { get; set; }
    }

    public class MyProgrammeParticipationsDto
    {
        public string ClubMemberNumber { get; set; }
        public Guid? AreaId { get; set; }
        public string AreaName { get; set; }
        public ProgrammeParticipationDto Entry { get; set; }
        public ProgrammeParticipationDto Onyx { get; set; }
        public PendingProgrammeCheckoutDto PendingAQGreenCheckout { get; set; }
        public PendingProgrammeCheckoutDto PendingDirectOnyxCheckout { get; set; }
        public AQGreenFuneralCoverDto FuneralCover { get; set; }
        public OnyxTravelBenefitDto TravelBenefit { get; set; }
        public bool CanJoinEntry => Entry == null;
        public bool CanJoinOnyxDirectly => Onyx == null && PendingDirectOnyxCheckout == null;
    }

    public class AQGreenFuneralCoverDto
    {
        public string Status { get; set; }
        public decimal CoverAmount { get; set; }
        public string Currency { get; set; }
        public DateTime IncludedAt { get; set; }
    }

    public class OnyxTravelBenefitDto
    {
        public string Status { get; set; }
        public DateTime EligibleAt { get; set; }
        public DateTime WaitingPeriodEndsAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public decimal MemberTripContributionPercent { get; set; }
    }
}

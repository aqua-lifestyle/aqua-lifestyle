using System;
using System.ComponentModel.DataAnnotations;
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

    public class DirectOnyxCheckoutDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string CheckoutUrl { get; set; }
    }

    public class PendingDirectOnyxCheckoutDto
    {
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string CheckoutUrl { get; set; }
        public string Status { get; set; }
    }

    public class ProgrammeParticipationDto
    {
        public string ProgrammeName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public bool JoinedIndependently { get; set; }
        public string RecruiterClubMemberNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public decimal? NextPaymentAmount { get; set; }
        public string NextPaymentDescription { get; set; }
        public string Currency { get; set; }
        public bool CanRecruitForThisProgramme { get; set; }
    }

    public class MyProgrammeParticipationsDto
    {
        public string ClubMemberNumber { get; set; }
        public ProgrammeParticipationDto Entry { get; set; }
        public ProgrammeParticipationDto Onyx { get; set; }
        public PendingDirectOnyxCheckoutDto PendingDirectOnyxCheckout { get; set; }
        public OnyxTravelBenefitDto TravelBenefit { get; set; }
        public bool CanJoinEntry => Entry == null;
        public bool CanJoinOnyxDirectly => Onyx == null && PendingDirectOnyxCheckout == null;
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

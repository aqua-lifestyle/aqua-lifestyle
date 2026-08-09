using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto
{
    public enum AdminProgrammeType
    {
        Entry = 0,
        Onyx = 1
    }

    public class AdminProgrammeParticipationListInput : PagedResultRequestDto
    {
        [StringLength(256)]
        public string Keyword { get; set; }

        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }

        public AdminProgrammeType Programme { get; set; }

        public bool AwaitingApprovalOnly { get; set; }
    }

    public class PendingProgrammeApprovalSummaryInput
    {
        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }
    }

    public class PendingProgrammeApprovalSummaryDto
    {
        public int AQGreenCount { get; set; }
        public int OnyxCount { get; set; }
        public int TotalCount => AQGreenCount + OnyxCount;
    }

    public class AdminProgrammePaymentDto
    {
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Provider { get; set; }
        public string ProviderReference { get; set; }
        public DateTime ConfirmedAt { get; set; }
    }

    public class AdminProgrammeParticipationDto
    {
        public Guid ParticipationId { get; set; }
        public string AreaName { get; set; }
        public string ClubMemberNumber { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string ProgrammeName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public bool JoinedIndependently { get; set; }
        public string RecruiterClubMemberNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public decimal ExpectedJoiningAmount { get; set; }
        public decimal? NextPaymentAmount { get; set; }
        public string NextPaymentDescription { get; set; }
        public string Currency { get; set; }
        public IReadOnlyList<AdminProgrammePaymentDto> ConfirmedPayments { get; set; }
    }

    public class CorrectProgrammeRecruiterInput
    {
        public AdminProgrammeType Programme { get; set; }

        [Required, StringLength(16, MinimumLength = 5)]
        public string ClubMemberNumber { get; set; }

        [StringLength(16, MinimumLength = 5)]
        public string NewRecruiterClubMemberNumber { get; set; }

        [Required, StringLength(1000, MinimumLength = 3)]
        public string Reason { get; set; }
    }

    public class GraduateAQGreenToOnyxInput
    {
        public Guid LoanAgreementId { get; set; }

        [Required, StringLength(2000, MinimumLength = 3)]
        public string Justification { get; set; }
    }

    public class ApproveProgrammeParticipationInput
    {
        public AdminProgrammeType Programme { get; set; }

        public Guid ParticipationId { get; set; }
    }

    public class RejectProgrammeParticipationInput
    {
        public AdminProgrammeType Programme { get; set; }

        public Guid ParticipationId { get; set; }

        [Required, StringLength(1000, MinimumLength = 3)]
        public string Reason { get; set; }
    }

    public class OnyxGraduationDecisionDto
    {
        public Guid DecisionId { get; set; }
        public Guid AQGreenParticipationId { get; set; }
        public Guid LoanAgreementId { get; set; }
        public Guid OnyxParticipationId { get; set; }
        public long AdministratorUserId { get; set; }
        public DateTime DecidedAt { get; set; }
        public string Justification { get; set; }
        public EntryNetworkLevel EvaluatedNetworkLevel { get; set; }
    }

    public class TerminateAQGreenJoiningCheckoutInput
    {
        public Guid CheckoutId { get; set; }

        [Required, StringLength(1000, MinimumLength = 3)]
        public string Evidence { get; set; }
    }

    public class AQGreenJoiningCheckoutListInput : PagedResultRequestDto
    {
        [StringLength(256)]
        public string Keyword { get; set; }

        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }
    }

    public class AQGreenJoiningCheckoutRecoveryDto
    {
        public Guid CheckoutId { get; set; }
        public int TenantId { get; set; }
        public string AreaName { get; set; }
        public string ClubMemberNumber { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public HostedPaymentCheckoutStatus Status { get; set; }
        public AQGreenJoiningPaymentSchedule Schedule { get; set; }
        public AQGreenJoiningPaymentStage Stage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CheckoutCreatedAt { get; set; }
        public string ProviderCheckoutId { get; set; }
        public Guid? PaymentId { get; set; }
        public string LockReason { get; set; }
    }

    public class LegacyAQGreenReconciliationListInput : PagedResultRequestDto
    {
        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }
    }

    public class LegacyAQGreenCheckoutFactDto
    {
        public Guid CheckoutId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public HostedPaymentCheckoutStatus Status { get; set; }
        public string ProviderCheckoutId { get; set; }
        public Guid? PaymentId { get; set; }
    }

    public class LegacyAQGreenPaymentFactDto
    {
        public Guid PaymentId { get; set; }
        public MemberPaymentPurpose Purpose { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public string Provider { get; set; }
        public string ProviderReference { get; set; }
        public DateTime ConfirmedAt { get; set; }
    }

    public class LegacyAQGreenReconciliationDto
    {
        public int TenantId { get; set; }
        public Guid ParticipationId { get; set; }
        public string ClubMemberNumber { get; set; }
        public string TermsVersion { get; set; }
        public decimal JoiningAmount { get; set; }
        public decimal RegistrationAmount { get; set; }
        public decimal ActivationAmount { get; set; }
        public decimal MonthlySubscriptionAmount { get; set; }
        public Guid? JoiningPaymentId { get; set; }
        public Guid? RegistrationPaymentId { get; set; }
        public Guid? ActivationPaymentId { get; set; }
        public IReadOnlyList<LegacyAQGreenPaymentFactDto> VerifiedPayments { get; set; }
        public IReadOnlyList<LegacyAQGreenCheckoutFactDto> CheckoutAttempts { get; set; }
    }
}

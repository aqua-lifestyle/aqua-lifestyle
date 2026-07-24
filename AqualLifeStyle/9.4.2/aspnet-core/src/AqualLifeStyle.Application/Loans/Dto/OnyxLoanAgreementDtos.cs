using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.Loans.Dto
{
    public class OnyxLoanWeeklyRequirementDto
    {
        public int RequirementNumber { get; set; }
        public decimal MinimumAmount { get; set; }
        public decimal CreditedAmount { get; set; }
        public DateTime DueAt { get; set; }
        public string Status { get; set; }
        public DateTime? SatisfiedAt { get; set; }
        public DateTime? MarkedOverdueAt { get; set; }
    }

    public class OnyxLoanRepaymentDto
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public int? WeeklyRequirementNumber { get; set; }
        public DateTime ReceivedAt { get; set; }
    }

    public class OnyxLoanAgreementDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string Status { get; set; }
        public string TermsVersion { get; set; }
        public decimal PrincipalAmount { get; set; }
        public decimal InterestRatePercent { get; set; }
        public decimal TotalPayableAmount { get; set; }
        public decimal RepaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Currency { get; set; }
        public DateTime OfferedAt { get; set; }
        public DateTime? MemberAcceptedAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? EffectiveAt { get; set; }
        public DateTime? RepaymentDeadlineAt { get; set; }
        public DateTime? SettledAt { get; set; }
        public bool RequiresPayoutHold { get; set; }
        public IReadOnlyList<OnyxLoanWeeklyRequirementDto> WeeklyRequirements { get; set; }
        public IReadOnlyList<OnyxLoanRepaymentDto> Repayments { get; set; }
    }

    public class MyOnyxLoanAgreementsDto
    {
        public IReadOnlyList<OnyxLoanAgreementDto> Items { get; set; }
    }

    public class AdminOnyxLoanAgreementListInput : PagedResultRequestDto
    {
        public int? TenantId { get; set; }
        public string Keyword { get; set; }
    }

    internal static class OnyxLoanAgreementDtoMapper
    {
        public static OnyxLoanAgreementDto Map(
            OnyxLoanAgreement agreement,
            string customerName,
            string email)
        {
            return new OnyxLoanAgreementDto
            {
                Id = agreement.Id,
                TenantId = agreement.TenantId,
                CustomerId = agreement.CustomerId,
                CustomerName = customerName,
                Email = email,
                Status = GetStatusLabel(agreement.Status),
                TermsVersion = agreement.TermsVersion,
                PrincipalAmount = agreement.PrincipalAmount,
                InterestRatePercent = agreement.InterestRatePercent,
                TotalPayableAmount = agreement.TotalPayableAmount,
                RepaidAmount =
                    agreement.TotalPayableAmount - agreement.OutstandingAmount,
                OutstandingAmount = agreement.OutstandingAmount,
                Currency = agreement.Currency,
                OfferedAt = agreement.OfferedAt,
                MemberAcceptedAt = agreement.MemberAcceptedAt,
                ApprovedAt = agreement.ApprovedAt,
                EffectiveAt = agreement.EffectiveAt,
                RepaymentDeadlineAt = agreement.RepaymentDeadlineAt,
                SettledAt = agreement.SettledAt,
                RequiresPayoutHold = agreement.RequiresPayoutHold,
                WeeklyRequirements = agreement.WeeklyRequirements
                    .OrderBy(item => item.RequirementNumber)
                    .Select(item => new OnyxLoanWeeklyRequirementDto
                    {
                        RequirementNumber = item.RequirementNumber,
                        MinimumAmount = item.MinimumAmount,
                        CreditedAmount = item.CreditedAmount,
                        DueAt = item.DueAt,
                        Status = GetRequirementStatusLabel(item.Status),
                        SatisfiedAt = item.SatisfiedAt,
                        MarkedOverdueAt = item.MarkedOverdueAt
                    })
                    .ToList(),
                Repayments = agreement.Repayments
                    .OrderByDescending(item => item.ReceivedAt)
                    .Select(item => new OnyxLoanRepaymentDto
                    {
                        PaymentId = item.PaymentId,
                        Amount = item.Amount,
                        WeeklyRequirementNumber =
                            item.WeeklyRequirementNumber,
                        ReceivedAt = item.ReceivedAt
                    })
                    .ToList()
            };
        }

        private static string GetStatusLabel(OnyxLoanAgreementStatus status) =>
            status switch
            {
                OnyxLoanAgreementStatus.AwaitingMemberAcceptance =>
                    "Awaiting your acceptance",
                OnyxLoanAgreementStatus.AwaitingAdministratorApproval =>
                    "Awaiting Club approval",
                OnyxLoanAgreementStatus.Active => "Active",
                OnyxLoanAgreementStatus.Overdue => "Overdue",
                OnyxLoanAgreementStatus.Settled => "Paid in full",
                _ => status.ToString()
            };

        private static string GetRequirementStatusLabel(
            OnyxLoanWeeklyRequirementStatus status) =>
            status switch
            {
                OnyxLoanWeeklyRequirementStatus.Due => "Due",
                OnyxLoanWeeklyRequirementStatus.Overdue => "Overdue",
                OnyxLoanWeeklyRequirementStatus.Satisfied => "Paid",
                _ => status.ToString()
            };
    }
}

using System;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.EntryMonthlyObligations.Dto
{
    public class EntryMonthlyObligationDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public decimal AmountDue { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Currency { get; set; }
        public string TermsVersion { get; set; }
        public DateTime DueAt { get; set; }
        public DateTime GracePeriodEndsAt { get; set; }
        public string Status { get; set; }
        public DateTime? MarkedOverdueAt { get; set; }
        public Guid? PaymentId { get; set; }
        public DateTime? PaidAt { get; set; }
        public bool IsOwnPayoutEligible { get; set; }
    }

    public class AdminEntryMonthlyObligationListInput
        : PagedResultRequestDto
    {
        public int? TenantId { get; set; }
        public string Keyword { get; set; }
    }

    internal static class EntryMonthlyObligationDtoMapper
    {
        public static EntryMonthlyObligationDto Map(
            EntryMonthlyObligation obligation,
            string customerName,
            string email) =>
            new EntryMonthlyObligationDto
            {
                Id = obligation.Id,
                TenantId = obligation.TenantId,
                CustomerId = obligation.CustomerId,
                CustomerName = customerName,
                Email = email,
                PeriodYear = obligation.PeriodYear,
                PeriodMonth = obligation.PeriodMonth,
                AmountDue = obligation.AmountDue,
                OutstandingAmount = obligation.OutstandingAmount,
                Currency = obligation.Currency,
                TermsVersion = obligation.TermsVersion,
                DueAt = obligation.DueAt,
                GracePeriodEndsAt = obligation.GracePeriodEndsAt,
                Status = obligation.Status switch
                {
                    EntryMonthlyObligationStatus.Due => "Payment due",
                    EntryMonthlyObligationStatus.GracePeriod =>
                        "Grace period",
                    EntryMonthlyObligationStatus.Overdue => "Overdue",
                    EntryMonthlyObligationStatus.Paid => "Paid",
                    _ => obligation.Status.ToString()
                },
                MarkedOverdueAt = obligation.MarkedOverdueAt,
                PaymentId = obligation.PaymentId,
                PaidAt = obligation.PaidAt,
                IsOwnPayoutEligible = obligation.IsOwnPayoutEligible
            };
    }
}

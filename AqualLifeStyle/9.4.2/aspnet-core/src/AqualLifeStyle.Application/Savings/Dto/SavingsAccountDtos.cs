using System;
using System.Collections.Generic;
using System.Linq;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Domain.Savings;

namespace AqualLifeStyle.Application.Savings.Dto
{
    public class SavingsContributionDto
    {
        public Guid PaymentId { get; set; }
        public decimal Amount { get; set; }
        public DateTime ContributedAt { get; set; }
        public decimal InterestRatePercent { get; set; }
        public decimal InterestAmount { get; set; }
    }

    public class SavingsAccountDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public DateTime OpenedAt { get; set; }
        public DateTime MaturesAt { get; set; }
        public DateTime? MaturedAt { get; set; }
        public string Status { get; set; }
        public bool RequiresMaturityProcessing { get; set; }
        public decimal PrincipalBalance { get; set; }
        public decimal ProjectedInterestAmount { get; set; }
        public decimal ProjectedMaturityAmount { get; set; }
        public decimal? MaturityPrincipalAmount { get; set; }
        public decimal? MaturityInterestAmount { get; set; }
        public decimal? MaturityPayoutAmount { get; set; }
        public decimal MinimumContributionAmount { get; set; }
        public decimal MaturityInterestRatePercent { get; set; }
        public int ContributionWindowStartDay { get; set; }
        public int ContributionWindowEndDay { get; set; }
        public string Currency { get; set; }
        public string TermsVersion { get; set; }
        public IReadOnlyList<SavingsContributionDto> Contributions { get; set; }
    }

    public class MySavingsAccountDto
    {
        public SavingsAccountDto Account { get; set; }
    }

    public class AdminSavingsAccountListInput : PagedResultRequestDto
    {
        public int? TenantId { get; set; }
        public string Keyword { get; set; }
    }

    internal static class SavingsAccountDtoMapper
    {
        public static SavingsAccountDto Map(
            SavingsAccount account,
            string customerName,
            string email,
            DateTime asOf)
        {
            var requiresMaturityProcessing =
                account.Status == SavingsAccountStatus.Active &&
                asOf >= account.MaturesAt;

            return new SavingsAccountDto
            {
                Id = account.Id,
                TenantId = account.TenantId,
                CustomerId = account.CustomerId,
                CustomerName = customerName,
                Email = email,
                OpenedAt = account.OpenedAt,
                MaturesAt = account.MaturesAt,
                MaturedAt = account.MaturedAt,
                Status = requiresMaturityProcessing
                    ? "Maturity processing due"
                    : account.Status == SavingsAccountStatus.Matured
                        ? "Matured"
                        : "Active",
                RequiresMaturityProcessing = requiresMaturityProcessing,
                PrincipalBalance = account.PrincipalBalance,
                ProjectedInterestAmount = account.ProjectedInterestAmount,
                ProjectedMaturityAmount = account.ProjectedMaturityAmount,
                MaturityPrincipalAmount = account.MaturityPrincipalAmount,
                MaturityInterestAmount = account.MaturityInterestAmount,
                MaturityPayoutAmount = account.MaturityPayoutAmount,
                MinimumContributionAmount = account.MinimumContributionAmount,
                MaturityInterestRatePercent =
                    account.MaturityInterestRatePercent,
                ContributionWindowStartDay =
                    account.ContributionWindowStartDay,
                ContributionWindowEndDay =
                    account.ContributionWindowEndDay,
                Currency = account.Currency,
                TermsVersion = account.TermsVersion,
                Contributions = account.Contributions
                    .OrderByDescending(contribution =>
                        contribution.ContributedAt)
                    .Select(contribution => new SavingsContributionDto
                    {
                        PaymentId = contribution.PaymentId,
                        Amount = contribution.Amount,
                        ContributedAt = contribution.ContributedAt,
                        InterestRatePercent =
                            contribution.InterestRatePercent,
                        InterestAmount = contribution.InterestAmount
                    })
                    .ToList()
            };
        }
    }
}

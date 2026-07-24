using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Abp.Application.Services.Dto;

namespace AqualLifeStyle.Application.Admin.Commissions.Dto
{
    public enum AdminCommissionProgramme
    {
        Entry = 0,
        Onyx = 1
    }

    public class AdminCommissionListInput : PagedResultRequestDto
    {
        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }

        public AdminCommissionProgramme Programme { get; set; }
    }

    public class CalculateLatestClosedCommissionWeekInput
    {
        [Range(1, int.MaxValue)]
        public int TenantId { get; set; }

        public AdminCommissionProgramme Programme { get; set; }
    }

    public class ReleaseWeeklyEarningInput
    {
        public Guid Id { get; set; }

        public AdminCommissionProgramme Programme { get; set; }

        [Required, StringLength(500, MinimumLength = 3)]
        public string Justification { get; set; }
    }

    public class RecordWeeklyEarningPaymentInput
    {
        public Guid Id { get; set; }

        public AdminCommissionProgramme Programme { get; set; }

        [Required, StringLength(128, MinimumLength = 3)]
        public string PaymentReference { get; set; }

        [Required, StringLength(500, MinimumLength = 3)]
        public string Justification { get; set; }
    }

    public class AdminCommissionComponentDto
    {
        public int Level { get; set; }
        public decimal Amount { get; set; }
    }

    public class AdminWeeklyCommissionDto
    {
        public Guid Id { get; set; }
        public int TenantId { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string Email { get; set; }
        public string ProgrammeName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string TimeZoneId { get; set; }
        public int HighestQualifiedLevel { get; set; }
        public int HighestCommissionedLevel { get; set; }
        public decimal TotalAmount { get; set; }
        public string Currency { get; set; }
        public string Status { get; set; }
        public string HoldReason { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public string ReleaseReason { get; set; }
        public DateTime? PaidAt { get; set; }
        public string PaymentReference { get; set; }
        public DateTime CalculatedAt { get; set; }
        public string RulesVersion { get; set; }
        public IReadOnlyList<AdminCommissionComponentDto> Components { get; set; }
    }

    public class CommissionCalculationResultDto
    {
        public Guid PeriodId { get; set; }
        public string ProgrammeName { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string TimeZoneId { get; set; }
        public bool WasAlreadyCalculated { get; set; }
        public int RecordsCreated { get; set; }
        public int EarnedCount { get; set; }
        public int HeldCount { get; set; }
        public decimal TotalEarnedAmount { get; set; }
        public string Currency { get; set; }
    }
}

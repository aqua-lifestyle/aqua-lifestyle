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

    public enum CommissionInventoryProgramme
    {
        AQGreen = 0,
        Onyx = 1,
        Both = 2
    }

    public enum CommissionPeriodClassification
    {
        FridayToThursday = 0,
        LegacyMondayToSunday = 1,
        Malformed = 2
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

    public class GetCommissionPeriodInventoryInput
    {
        [Range(1, int.MaxValue)]
        public int? TenantId { get; set; }

        public CommissionInventoryProgramme Programme { get; set; } =
            CommissionInventoryProgramme.Both;
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

    public class CommissionPeriodInventoryDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string ProgrammeName { get; set; }
        public Guid PeriodId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public string TimeZoneId { get; set; }
        public string RulesVersion { get; set; }
        public DateTime CalculatedAt { get; set; }
        public int CommissionCount { get; set; }
        public int NotEarnedCount { get; set; }
        public int EarnedCount { get; set; }
        public int HeldCount { get; set; }
        public int ReleasedCount { get; set; }
        public int PaidCount { get; set; }
        public int DeletedCommissionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal EarnedTotal { get; set; }
        public decimal HeldTotal { get; set; }
        public decimal ReleasedTotal { get; set; }
        public decimal PaidTotal { get; set; }
        public decimal DeletedCommissionTotal { get; set; }
        public CommissionPeriodClassification Classification { get; set; }
        public bool OverlapsFridayToThursdayCycle { get; set; }
        public bool HasExactBoundaryDuplicate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime? DeletionTime { get; set; }
    }

    public class CommissionPeriodBoundaryDto
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public string ProgrammeName { get; set; }
        public DateTime FirstNonOverlappingCycleStartUtc { get; set; }
        public IReadOnlyList<MissingCommissionCycleDto> MissingCanonicalCycles { get; set; }
    }

    public enum MissingCommissionCycleDisposition
    {
        ManualFinancialReconciliationRequired = 0
    }

    public class MissingCommissionCycleDto
    {
        public DateTime CycleStartUtc { get; set; }
        public bool IsLatestClosedCycle { get; set; }
        public MissingCommissionCycleDisposition Disposition { get; set; }
        public string Message { get; set; }
    }

    public class CommissionPeriodInventoryOutput
    {
        public DateTime LatestClosedCycleStartUtc { get; set; }
        public DateTime LatestClosedCycleEndUtc { get; set; }
        public IReadOnlyList<CommissionPeriodInventoryDto> Periods { get; set; }
        public IReadOnlyList<CommissionPeriodBoundaryDto> ProgrammeBoundaries { get; set; }
    }
}

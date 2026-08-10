using System;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.Admin.Commissions.Dto
{
    public class BootstrapInitialCommissionTermsInput
    {
        /// <summary>
        /// When true, validates and reports exactly what would be inserted
        /// without writing any financial record.
        /// </summary>
        public bool DryRun { get; set; }
    }

    public enum CommissionTermsBootstrapRowStatus
    {
        Inserted = 0,
        AlreadyPresent = 1,
        WouldInsert = 2
    }

    public class CommissionTermsBootstrapRow
    {
        public string Programme { get; set; }
        public string Version { get; set; }
        public DateTime EffectiveAtUtc { get; set; }
        public CommissionTermsBootstrapRowStatus Status { get; set; }
    }

    public class CommissionTermsBootstrapResult
    {
        public bool DryRun { get; set; }
        public List<CommissionTermsBootstrapRow> Rows { get; set; } = new();
        public bool AnyConflict => Conflicts.Count > 0;
        public List<string> Conflicts { get; set; } = new();
    }

    public enum AreaBaselinePreflightStatus
    {
        Sufficient = 0,
        Missing = 1,
        RecordedAfterTargetCutoff = 2
    }

    public class AreaBaselinePreflightRow
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public bool IsActive { get; set; }
        public AreaBaselinePreflightStatus BaselineStatus { get; set; }
        public DateTime? BaselineEffectiveAtUtc { get; set; }
        public bool WorkerWouldSkipAtCutoff { get; set; }
    }

    public enum CommissionTermsPreflightStatus
    {
        Present = 0,
        Missing = 1,
        Conflicting = 2
    }

    public class CommissionTermsPreflightRow
    {
        public string Programme { get; set; }
        public string ExpectedVersion { get; set; }
        public DateTime ExpectedEffectiveAtUtc { get; set; }
        public CommissionTermsPreflightStatus Status { get; set; }
        public string Detail { get; set; }
    }

    public class WeeklyFirstRunProjection
    {
        public DateTime PeriodStartUtc { get; set; }
        public DateTime PeriodEndUtc { get; set; }
        public int ActiveEntryParticipations { get; set; }
        public int ActiveOnyxParticipations { get; set; }
        public int EntryOverdueObligationHolds { get; set; }
        public int EntryLoanHolds { get; set; }
        public List<WeeklyFirstRunTenantProjection> Tenants { get; set; } = new();
    }

    public class WeeklyFirstRunTenantProjection
    {
        public int TenantId { get; set; }
        public string TenantName { get; set; }
        public AreaBaselinePreflightStatus AreaBaselineStatus { get; set; }
        public int ActiveEntryParticipations { get; set; }
        public int ActiveOnyxParticipations { get; set; }
        public int EntryOverdueObligationHolds { get; set; }
        public int EntryLoanHolds { get; set; }
        public int ExistingTargetPeriodConflicts { get; set; }
    }

    public class WeeklyEnablementPreflightOutput
    {
        public DateTime TargetPeriodStartUtc { get; set; }
        public DateTime TargetPeriodEndUtc { get; set; }
        public string TimeZoneId { get; set; }
        public List<CommissionTermsPreflightRow> Terms { get; set; } = new();
        public List<AreaBaselinePreflightRow> Areas { get; set; } = new();
        public bool WorkerEnabled { get; set; }
        public string TopologyStatus { get; set; }
        public string TopologyDetail { get; set; }
        public int ExistingTargetEntryPeriods { get; set; }
        public int ExistingTargetOnyxPeriods { get; set; }
        public WeeklyFirstRunProjection Projection { get; set; }
        public bool Ready => !WorkerEnabled &&
            Terms.TrueForAll(row => row.Status == CommissionTermsPreflightStatus.Present) &&
            Areas.TrueForAll(row =>
                !row.WorkerWouldSkipAtCutoff &&
                row.BaselineStatus == AreaBaselinePreflightStatus.Sufficient) &&
            ExistingTargetEntryPeriods == 0 &&
            ExistingTargetOnyxPeriods == 0;
    }

    public class MonthlyEnablementPreflightOutput
    {
        public int PeriodYear { get; set; }
        public int PeriodMonth { get; set; }
        public string PeriodName { get; set; }
        public bool WorkerEnabled { get; set; }
        public string DuePolicyStatus { get; set; }
        public string DuePolicyDetail { get; set; }
        public int? ResolvedDueDayOfMonth { get; set; }
        public string ResolvedPolicyVersion { get; set; }
        public int ExistingTargetPeriodObligations { get; set; }
        public int ExistingAugustObligations { get; set; }
        public int EligibleActiveParticipations { get; set; }
        public int ExcludedActivationMonthParticipations { get; set; }
        public int ExcludedWithoutActivatedAt { get; set; }
        public bool Ready => !WorkerEnabled &&
            string.Equals(DuePolicyStatus, "Resolved", StringComparison.Ordinal) &&
            ExistingTargetPeriodObligations == 0;
    }

    public class BootstrapSeptemberDueDatePolicyInput
    {
        public string Version { get; set; } = "2026-09-aqgreen-monthly-initial";
        public int DueDayOfMonth { get; set; }
        public bool DryRun { get; set; }
    }

    public class SeptemberDueDatePolicyBootstrapResult
    {
        public bool DryRun { get; set; }
        public string Version { get; set; }
        public DateTime EffectiveFromUtc { get; set; }
        public int DueDayOfMonth { get; set; }
        public string Status { get; set; }
        public List<string> Conflicts { get; set; } = new();
    }
}

using System;
using System.Threading.Tasks;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Resolves durable host-level evidence for recurring AQGreen due dates.
    /// </summary>
    public interface IEntryMonthlyObligationDueDatePolicy
    {
        Task<EntryMonthlyObligationDueDateResolution> ResolveDueDateAsync(
            int periodYear,
            int periodMonth);
    }

    public enum EntryMonthlyObligationDueDateResolutionStatus
    {
        Resolved = 0,
        Missing = 1,
        Ambiguous = 2,
        InvalidPolicy = 3,
        InvalidPeriod = 4
    }

    public sealed class EntryMonthlyObligationDueDateResolution
    {
        public EntryMonthlyObligationDueDateResolutionStatus Status { get; }
        public DateTime? DueAtUtc { get; }
        public string PolicyVersion { get; }
        public bool IsResolved => Status == EntryMonthlyObligationDueDateResolutionStatus.Resolved;

        private EntryMonthlyObligationDueDateResolution(
            EntryMonthlyObligationDueDateResolutionStatus status,
            DateTime? dueAtUtc,
            string policyVersion)
        {
            Status = status;
            DueAtUtc = dueAtUtc;
            PolicyVersion = policyVersion;
        }

        public static EntryMonthlyObligationDueDateResolution Resolved(
            DateTime dueAtUtc,
            string policyVersion)
        {
            return new EntryMonthlyObligationDueDateResolution(
                EntryMonthlyObligationDueDateResolutionStatus.Resolved,
                dueAtUtc,
                policyVersion);
        }

        public static EntryMonthlyObligationDueDateResolution Failed(
            EntryMonthlyObligationDueDateResolutionStatus status)
        {
            if (status == EntryMonthlyObligationDueDateResolutionStatus.Resolved)
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            return new EntryMonthlyObligationDueDateResolution(status, null, null);
        }
    }
}

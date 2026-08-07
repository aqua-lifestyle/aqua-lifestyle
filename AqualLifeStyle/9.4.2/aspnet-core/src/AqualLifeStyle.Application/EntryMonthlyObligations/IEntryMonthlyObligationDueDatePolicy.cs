using System;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Decides when each recurring AQGreen monthly obligation becomes due.
    /// This is the Product Decision PD-07 boundary: the obligation model and the
    /// recurring scheduler never invent a due date themselves; they ask this
    /// policy. Until the business defines the monthly due-date policy the policy
    /// reports "undefined" (null) and no future obligation is scheduled.
    /// </summary>
    public interface IEntryMonthlyObligationDueDatePolicy
    {
        /// <summary>
        /// Resolves the due time for a calendar period, or null when the
        /// due-date policy is not yet defined.
        /// </summary>
        DateTime? ResolveDueDate(int periodYear, int periodMonth);
    }
}

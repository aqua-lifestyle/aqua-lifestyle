using System.Threading.Tasks;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Production mechanism that creates, assesses, and settles the recurring
    /// AQGreen R600 monthly obligations. It is deliberately not a remote
    /// endpoint: it runs on the host side (config-gated background worker) and
    /// must never be callable by tenants.
    /// </summary>
    public interface IEntryMonthlyObligationScheduler
    {
        /// <summary>
        /// Ensures every active AQGreen participation has exactly one obligation
        /// for the given calendar period, using the caller-supplied due time and
        /// selected durable policy version. Returns the number created.
        /// </summary>
        Task<int> EnsureObligationsForPeriodAsync(
            int periodYear,
            int periodMonth,
            System.DateTime dueAt,
            string duePolicyVersion);

        /// <summary>
        /// Advances the status of unpaid obligations (due → grace period →
        /// overdue) against the supplied assessment time. Returns the number of
        /// obligations assessed.
        /// </summary>
        Task<int> AssessObligationsAsync(System.DateTime asOf);

    }
}

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
        /// for the given calendar period, using the caller-supplied due time.
        /// Returns the number of obligations created.
        /// </summary>
        Task<int> EnsureObligationsForPeriodAsync(
            int periodYear,
            int periodMonth,
            System.DateTime dueAt);

        /// <summary>
        /// Advances the status of unpaid obligations (due → grace period →
        /// overdue) against the supplied assessment time. Returns the number of
        /// obligations assessed.
        /// </summary>
        Task<int> AssessObligationsAsync(System.DateTime asOf);

        /// <summary>
        /// Allocates confirmed AQGreen monthly-commitment payments that are not
        /// yet linked to an obligation into the member's earliest open
        /// obligation. Returns the number of payments allocated.
        /// </summary>
        Task<int> AllocateConfirmedMonthlyPaymentsAsync();
    }
}

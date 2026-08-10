using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.EntryMonthlyObligations
{
    /// <summary>
    /// Selects the single latest effective host policy for an obligation month.
    /// Missing, tied, or invalid evidence is returned explicitly and fails closed.
    /// </summary>
    public class PersistedEntryMonthlyObligationDueDatePolicy
        : IEntryMonthlyObligationDueDatePolicy, ITransientDependency
    {
        private readonly IRepository<EntryMonthlyObligationDuePolicy, Guid> _policyRepository;

        /// <summary>
        /// Creates a resolver backed by the append-only host policy repository.
        /// </summary>
        public PersistedEntryMonthlyObligationDueDatePolicy(
            IRepository<EntryMonthlyObligationDuePolicy, Guid> policyRepository)
        {
            _policyRepository = policyRepository;
        }

        /// <inheritdoc />
        [UnitOfWork]
        public virtual async Task<EntryMonthlyObligationDueDateResolution> ResolveDueDateAsync(
            int periodYear,
            int periodMonth)
        {
            if (periodYear < 2000 || periodYear > 9999 ||
                periodMonth < 1 || periodMonth > 12)
            {
                return EntryMonthlyObligationDueDateResolution.Failed(
                    EntryMonthlyObligationDueDateResolutionStatus.InvalidPeriod);
            }

            var periodStartUtc = EntryMonthlyObligationDuePolicy
                .JohannesburgMonthStartUtc(periodYear, periodMonth);
            var applicable = await _policyRepository.GetAll()
                .AsNoTracking()
                .Where(policy => policy.EffectiveFrom <= periodStartUtc)
                .OrderByDescending(policy => policy.EffectiveFrom)
                .ToListAsync();

            if (applicable.Count == 0)
            {
                return EntryMonthlyObligationDueDateResolution.Failed(
                    EntryMonthlyObligationDueDateResolutionStatus.Missing);
            }

            var latestEffectiveFrom = applicable[0].EffectiveFrom;
            var latest = applicable
                .Where(policy => policy.EffectiveFrom == latestEffectiveFrom)
                .ToList();
            if (latest.Count != 1)
            {
                return EntryMonthlyObligationDueDateResolution.Failed(
                    EntryMonthlyObligationDueDateResolutionStatus.Ambiguous);
            }

            var selected = latest[0];
            if (!selected.HasValidEvidence())
            {
                return EntryMonthlyObligationDueDateResolution.Failed(
                    EntryMonthlyObligationDueDateResolutionStatus.InvalidPolicy);
            }

            return EntryMonthlyObligationDueDateResolution.Resolved(
                selected.ResolveDueAtUtc(periodYear, periodMonth),
                selected.Version);
        }
    }
}

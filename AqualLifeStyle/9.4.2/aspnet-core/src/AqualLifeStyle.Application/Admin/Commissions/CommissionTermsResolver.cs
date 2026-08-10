using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    /// <summary>
    /// Resolves the single immutable commission terms version applicable to a
    /// closed commission cycle. The applicable version is the one whose
    /// effective boundary is the latest canonical Friday 00:00
    /// Africa/Johannesburg cycle start at or before the cycle's closing
    /// boundary; equality at the boundary is inclusive, so a version effective
    /// exactly at a cycle's cutoff governs that cycle while the immediately
    /// preceding cycle resolves the previous version.
    ///
    /// Resolution never consults current/configured terms and never falls back:
    /// a cycle without an applicable persisted version fails closed. Programmes
    /// are isolated structurally through separate persisted version sets.
    /// </summary>
    public interface ICommissionTermsResolver : ITransientDependency
    {
        Task<EntryCommissionTerms> ResolveEntryTermsAsync(
            ClosedCommissionWeek closedWeek);

        Task<OnyxCommissionTerms> ResolveOnyxTermsAsync(
            ClosedCommissionWeek closedWeek);
    }

    public sealed class CommissionTermsResolver : ICommissionTermsResolver
    {
        private readonly IRepository<EntryCommissionTermsVersion, Guid>
            _entryTermsRepository;
        private readonly IRepository<OnyxCommissionTermsVersion, Guid>
            _onyxTermsRepository;
        private readonly LatestClosedCommissionWeekResolver _cycleResolver;

        public CommissionTermsResolver(
            IRepository<EntryCommissionTermsVersion, Guid> entryTermsRepository,
            IRepository<OnyxCommissionTermsVersion, Guid> onyxTermsRepository,
            LatestClosedCommissionWeekResolver cycleResolver)
        {
            _entryTermsRepository = entryTermsRepository;
            _onyxTermsRepository = onyxTermsRepository;
            _cycleResolver = cycleResolver;
        }

        public async Task<EntryCommissionTerms> ResolveEntryTermsAsync(
            ClosedCommissionWeek closedWeek)
        {
            var boundary = _cycleResolver.ResolveFirstCycleStartAfter(
                closedWeek.PeriodEndUtc);
            var version = await _entryTermsRepository.GetAll()
                .Where(terms => terms.EffectiveAt <= boundary)
                .OrderByDescending(terms => terms.EffectiveAt)
                .FirstOrDefaultAsync();
            if (version == null)
            {
                throw new InvalidOperationException(
                    "No AQGreen commission terms version is effective for the cycle closing at " +
                    $"{boundary:O}. Authorised effective-dated terms are required; calculation refuses to use current terms.");
            }

            return version.ToTerms();
        }

        public async Task<OnyxCommissionTerms> ResolveOnyxTermsAsync(
            ClosedCommissionWeek closedWeek)
        {
            var boundary = _cycleResolver.ResolveFirstCycleStartAfter(
                closedWeek.PeriodEndUtc);
            var version = await _onyxTermsRepository.GetAll()
                .Where(terms => terms.EffectiveAt <= boundary)
                .OrderByDescending(terms => terms.EffectiveAt)
                .FirstOrDefaultAsync();
            if (version == null)
            {
                throw new InvalidOperationException(
                    "No Onyx commission terms version is effective for the cycle closing at " +
                    $"{boundary:O}. Authorised effective-dated terms are required; calculation refuses to use current terms.");
            }

            return version.ToTerms();
        }
    }
}

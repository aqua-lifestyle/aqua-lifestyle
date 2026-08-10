using System;
using System.Collections.Generic;
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
    /// Host-side production mechanism behind the recurring AQGreen R600 monthly
    /// obligations. Every operation is idempotent and can be re-run safely: a
    /// member never receives two obligations for the same period and a confirmed
    /// payment is never applied twice. The scheduler never invents due dates; the
    /// caller (or the due-date policy) supplies them.
    /// </summary>
    public class EntryMonthlyObligationScheduler
        : IEntryMonthlyObligationScheduler, ITransientDependency
    {
        private readonly IRepository<EntryMonthlyObligation, Guid> _obligationRepository;
        private readonly IRepository<EntryParticipation, Guid> _participationRepository;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public EntryMonthlyObligationScheduler(
            IRepository<EntryMonthlyObligation, Guid> obligationRepository,
            IRepository<EntryParticipation, Guid> participationRepository,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _obligationRepository = obligationRepository;
            _participationRepository = participationRepository;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork]
        public virtual async Task<int> EnsureObligationsForPeriodAsync(
            int periodYear,
            int periodMonth,
            DateTime dueAt,
            string duePolicyVersion)
        {
            ValidatePeriod(periodYear, periodMonth);
            ValidateUtc(dueAt, nameof(dueAt));
            ValidateDuePolicyVersion(duePolicyVersion);

            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var existingParticipationIds = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        obligation.PeriodYear == periodYear &&
                        obligation.PeriodMonth == periodMonth)
                    .Select(obligation => obligation.EntryParticipationId)
                    .ToListAsync();
                var existingSet = new HashSet<Guid>(existingParticipationIds);

                var activeParticipations = await _participationRepository.GetAll()
                    .Where(participation =>
                        participation.Status == EntryParticipationStatus.Active)
                    .ToListAsync();

                var created = 0;
                foreach (var participation in activeParticipations)
                {
                    if (existingSet.Contains(participation.Id))
                    {
                        continue;
                    }

                    if (!IsObligationMonthAfterActivation(
                            participation,
                            periodYear,
                            periodMonth))
                    {
                        continue;
                    }

                    await _obligationRepository.InsertAsync(
                        EntryMonthlyObligation.Create(
                            participation,
                            periodYear,
                            periodMonth,
                            dueAt,
                            duePolicyVersion));
                    existingSet.Add(participation.Id);
                    created++;
                }

                if (created > 0)
                {
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                return created;
            }
        }

        [UnitOfWork]
        public virtual async Task<int> AssessObligationsAsync(DateTime asOf)
        {
            ValidateUtc(asOf, nameof(asOf));

            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                var unpaid = await _obligationRepository.GetAll()
                    .Where(obligation =>
                        obligation.Status != EntryMonthlyObligationStatus.Paid)
                    .ToListAsync();

                foreach (var obligation in unpaid)
                {
                    obligation.AssessStatus(asOf);
                }

                if (unpaid.Count > 0)
                {
                    await _unitOfWorkManager.Current.SaveChangesAsync();
                }

                return unpaid.Count;
            }
        }

        private static void ValidatePeriod(int periodYear, int periodMonth)
        {
            if (periodYear < 2000 || periodYear > 9999)
            {
                throw new ArgumentOutOfRangeException(nameof(periodYear));
            }

            if (periodMonth < 1 || periodMonth > 12)
            {
                throw new ArgumentOutOfRangeException(nameof(periodMonth));
            }
        }

        private static void ValidateUtc(DateTime value, string parameterName)
        {
            if (value == default)
            {
                throw new ArgumentException(
                    "A UTC timestamp is required.",
                    parameterName);
            }

            if (value.Kind != DateTimeKind.Utc)
            {
                throw new ArgumentException(
                    "The timestamp must be UTC.",
                    parameterName);
            }
        }

        private static void ValidateDuePolicyVersion(string duePolicyVersion)
        {
            if (string.IsNullOrWhiteSpace(duePolicyVersion) ||
                duePolicyVersion.Trim().Length > EntryMonthlyObligationDuePolicy.MaxVersionLength)
            {
                throw new ArgumentException(
                    "A valid due-policy version is required.",
                    nameof(duePolicyVersion));
            }
        }

        private static bool IsObligationMonthAfterActivation(
            EntryParticipation participation,
            int periodYear,
            int periodMonth)
        {
            if (!participation.ActivatedAt.HasValue)
            {
                return false;
            }

            var activationMonth = EntryMonthlyObligationDuePolicy
                .JohannesburgMonth(participation.ActivatedAt.Value);
            var activationMonthNumber = activationMonth.Year * 12 + activationMonth.Month;
            var obligationMonthNumber = periodYear * 12 + periodMonth;
            return obligationMonthNumber > activationMonthNumber;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    /// <summary>
    /// Authoritative, reusable weekly-commission calculation engine for a single
    /// tenant and closed commission week. The same logic powers the manual admin
    /// calculation path and the automated weekly scheduler so that there is a
    /// single source of truth for commission outcomes (no second calculator).
    ///
    /// Idempotency boundary: for a given programme, tenant, and closed cycle the
    /// period row is inserted exactly once. The EntryCommissionPeriods and
    /// OnyxCommissionPeriods unique constraints on
    /// (TenantId, PeriodStart, PeriodEnd) are the authoritative duplicate guards.
    ///
    /// This service only records calculated commission state
    /// (Earned/Held/NotEarned). It never releases, marks Paid, synchronizes
    /// travel benefits, or invokes payment services.
    /// </summary>
    public interface IWeeklyCommissionCalculator : ITransientDependency
    {
        Task<CommissionCalculationResultDto> CalculateEntryAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt);

        Task<CommissionCalculationResultDto> CalculateOnyxAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt);
    }

    public sealed class WeeklyCommissionCalculator : IWeeklyCommissionCalculator
    {
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid> _entryObligationRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _loanAgreementRepository;
        private readonly IRepository<EntryCommissionPeriod, Guid> _entryPeriodRepository;
        private readonly IRepository<OnyxCommissionPeriod, Guid> _onyxPeriodRepository;
        private readonly IRepository<EntryWeeklyCommission, Guid> _entryCommissionRepository;
        private readonly IRepository<OnyxWeeklyCommission, Guid> _onyxCommissionRepository;
        private readonly ICommissionTermsResolver _termsResolver;
        private readonly IAreaActivationStateResolver _areaActivationStateResolver;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public WeeklyCommissionCalculator(
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<EntryMonthlyObligation, Guid> entryObligationRepository,
            IRepository<OnyxLoanAgreement, Guid> loanAgreementRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            ICommissionTermsResolver termsResolver,
            IAreaActivationStateResolver areaActivationStateResolver,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _entryObligationRepository = entryObligationRepository;
            _loanAgreementRepository = loanAgreementRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _termsResolver = termsResolver;
            _areaActivationStateResolver = areaActivationStateResolver;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<CommissionCalculationResultDto> CalculateEntryAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            var existingPeriod = await _entryPeriodRepository.FirstOrDefaultAsync(period =>
                period.TenantId == tenantId &&
                period.PeriodStart == closedWeek.PeriodStartUtc &&
                period.PeriodEnd == closedWeek.PeriodEndUtc);
            if (existingPeriod != null)
            {
                var existingCommissions = await _entryCommissionRepository.GetAll()
                    .Where(commission =>
                        commission.CommissionPeriodId == existingPeriod.Id)
                    .ToListAsync();
                return BuildResult(
                    existingPeriod.Id,
                    "AQGreen",
                    existingPeriod.PeriodStart,
                    existingPeriod.PeriodEnd,
                    existingPeriod.TimeZoneId,
                    existingCommissions.Select(commission =>
                        new CommissionSummaryRow(
                            commission.TotalAmount,
                            commission.PayoutStatus)),
                    existingCommissions.Select(commission => commission.Currency)
                        .Distinct()
                        .SingleOrDefault() ?? "ZAR",
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            await _areaActivationStateResolver.EnsureActiveAsync(
                tenantId,
                closedWeek.PeriodEndUtc);
            var terms = await _termsResolver.ResolveEntryTermsAsync(closedWeek);
            List<EntryParticipation> networkParticipations;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                networkParticipations = await _entryParticipationRepository
                    .GetAllIncluding(participation => participation.RecruiterCorrections)
                    .Where(participation =>
                        participation.TenantId == tenantId &&
                        participation.Status == EntryParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= closedWeek.PeriodEndUtc))
                    .ToListAsync();
            }
            EnsureNoDeletedParticipationEvidence(networkParticipations, "AQGreen");
            var effectiveNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                tenantId,
                networkParticipations,
                closedWeek.PeriodEndUtc);
            var targetParticipations = networkParticipations;
            var targetParticipationIds = targetParticipations
                .Select(participation => participation.Id)
                .ToList();
            var obligations = await _entryObligationRepository.GetAll()
                .Where(obligation =>
                    targetParticipationIds.Contains(obligation.EntryParticipationId))
                .ToListAsync();
            var loanAgreements = await _loanAgreementRepository
                .GetAllIncluding(agreement => agreement.WeeklyRequirements)
                .Where(agreement =>
                    targetParticipationIds.Contains(agreement.EntryParticipationId))
                .ToListAsync();

            var period = EntryCommissionPeriod.CreateClosedPeriod(
                tenantId,
                closedWeek.PeriodStartUtc,
                closedWeek.PeriodEndUtc,
                closedWeek.TimeZoneId,
                calculatedAt,
                terms);
            await _entryPeriodRepository.InsertAsync(period);
            var calculator = new EntryWeeklyCommissionCalculator(
                new EntryNetworkQualificationEvaluator());
            var commissions = targetParticipations
                .Select(participation => calculator.Calculate(
                    participation,
                    period,
                    terms,
                    effectiveNetwork,
                    obligations,
                    loanAgreements))
                .ToList();
            foreach (var commission in commissions)
            {
                await _entryCommissionRepository.InsertAsync(commission);
            }

            await _unitOfWorkManager.Current.SaveChangesAsync();
            return BuildResult(
                period.Id,
                "AQGreen",
                period.PeriodStart,
                period.PeriodEnd,
                period.TimeZoneId,
                commissions.Select(commission =>
                    new CommissionSummaryRow(
                        commission.TotalAmount,
                        commission.PayoutStatus)),
                terms.Currency,
                wasAlreadyCalculated: false,
                recordsCreated: commissions.Count);
        }

        public async Task<CommissionCalculationResultDto> CalculateOnyxAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            var existingPeriod = await _onyxPeriodRepository.FirstOrDefaultAsync(period =>
                period.TenantId == tenantId &&
                period.PeriodStart == closedWeek.PeriodStartUtc &&
                period.PeriodEnd == closedWeek.PeriodEndUtc);
            if (existingPeriod != null)
            {
                var existingCommissions = await _onyxCommissionRepository.GetAll()
                    .Where(commission =>
                        commission.CommissionPeriodId == existingPeriod.Id)
                    .ToListAsync();
                return BuildResult(
                    existingPeriod.Id,
                    "Onyx",
                    existingPeriod.PeriodStart,
                    existingPeriod.PeriodEnd,
                    existingPeriod.TimeZoneId,
                    existingCommissions.Select(commission =>
                        new CommissionSummaryRow(
                            commission.TotalAmount,
                            commission.PayoutStatus)),
                    existingCommissions.Select(commission => commission.Currency)
                        .Distinct()
                        .SingleOrDefault() ?? "ZAR",
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            await _areaActivationStateResolver.EnsureActiveAsync(
                tenantId,
                closedWeek.PeriodEndUtc);
            var terms = await _termsResolver.ResolveOnyxTermsAsync(closedWeek);
            List<OnyxParticipation> networkParticipations;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                networkParticipations = await _onyxParticipationRepository
                    .GetAllIncluding(participation => participation.RecruiterCorrections)
                    .Where(participation =>
                        participation.TenantId == tenantId &&
                        participation.Status == OnyxParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= closedWeek.PeriodEndUtc))
                    .ToListAsync();
            }
            EnsureNoDeletedParticipationEvidence(networkParticipations, "Onyx");
            var effectiveNetwork = EffectiveProgrammeNetwork.BuildOnyx(
                tenantId,
                networkParticipations,
                closedWeek.PeriodEndUtc);

            var targetParticipations = networkParticipations;
            var period = OnyxCommissionPeriod.CreateClosedPeriod(
                tenantId,
                closedWeek.PeriodStartUtc,
                closedWeek.PeriodEndUtc,
                closedWeek.TimeZoneId,
                calculatedAt,
                terms);
            await _onyxPeriodRepository.InsertAsync(period);
            var calculator = new OnyxWeeklyCommissionCalculator(
                new OnyxNetworkQualificationEvaluator());
            var commissions = targetParticipations
                .Select(participation => calculator.Calculate(
                    participation,
                    period,
                    terms,
                    effectiveNetwork))
                .ToList();
            foreach (var commission in commissions)
            {
                await _onyxCommissionRepository.InsertAsync(commission);
            }

            await _unitOfWorkManager.Current.SaveChangesAsync();
            return BuildResult(
                period.Id,
                "Onyx",
                period.PeriodStart,
                period.PeriodEnd,
                period.TimeZoneId,
                commissions.Select(commission =>
                    new CommissionSummaryRow(
                        commission.TotalAmount,
                        commission.PayoutStatus)),
                terms.Currency,
                wasAlreadyCalculated: false,
                recordsCreated: commissions.Count);
        }

        private static CommissionCalculationResultDto BuildResult(
            Guid periodId,
            string programmeName,
            DateTime periodStart,
            DateTime periodEnd,
            string timeZoneId,
            IEnumerable<CommissionSummaryRow> commissions,
            string currency,
            bool wasAlreadyCalculated,
            int recordsCreated)
        {
            var rows = commissions.ToList();
            return new CommissionCalculationResultDto
            {
                PeriodId = periodId,
                ProgrammeName = programmeName,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                TimeZoneId = timeZoneId,
                WasAlreadyCalculated = wasAlreadyCalculated,
                RecordsCreated = recordsCreated,
                EarnedCount = rows.Count(row => row.Amount > 0m),
                HeldCount = rows.Count(row =>
                    row.Status == WeeklyCommissionPayoutStatus.Held),
                TotalEarnedAmount = rows.Sum(row => row.Amount),
                Currency = currency
            };
        }

        private static void EnsureNoDeletedParticipationEvidence<TParticipation>(
            IEnumerable<TParticipation> participations,
            string programmeName)
            where TParticipation : Abp.Domain.Entities.Auditing.IFullAudited
        {
            if (participations.Any(participation => participation.IsDeleted))
            {
                throw new InvalidOperationException(
                    $"{programmeName} calculation cannot prove cutoff participation state because deleted network evidence exists.");
            }
        }

        private sealed class CommissionSummaryRow
        {
            public CommissionSummaryRow(decimal amount, WeeklyCommissionPayoutStatus status)
            {
                Amount = amount;
                Status = status;
            }

            public decimal Amount { get; }
            public WeeklyCommissionPayoutStatus Status { get; }
        }
    }
}

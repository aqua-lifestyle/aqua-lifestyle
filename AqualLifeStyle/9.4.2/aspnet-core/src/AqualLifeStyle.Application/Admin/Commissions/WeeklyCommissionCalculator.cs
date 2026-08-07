using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    /// <summary>
    /// Authoritative, reusable weekly-commission calculation engine for a single
    /// tenant and closed commission week. The same logic powers the manual admin
    /// calculation path and the automated weekly scheduler so that there is a
    /// single source of truth for commission outcomes (no second calculator).
    ///
    /// Idempotency boundary: for a given (TenantId, closed week) the period row
    /// is inserted exactly once; the database unique constraint on
    /// <c>EntryCommissionPeriods(TenantId, PeriodStart, PeriodEnd)</c> is the
    /// authoritative guard against duplicate outcomes even across concurrent
    /// attempts. A re-invocation with an existing period returns without
    /// creating duplicate commission rows.
    ///
    /// This service only <em>creates Earned commission records</em> (status
    /// Earned/Held/NotEarned). It never releases or marks Paid.
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
        private readonly ICurrentCommissionTermsProvider _termsProvider;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly OnyxTravelBenefitEligibilityProcessor _travelBenefitEligibilityProcessor;
        private readonly ILogger<WeeklyCommissionCalculator> _logger;

        public WeeklyCommissionCalculator(
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<EntryMonthlyObligation, Guid> entryObligationRepository,
            IRepository<OnyxLoanAgreement, Guid> loanAgreementRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            ICurrentCommissionTermsProvider termsProvider,
            IUnitOfWorkManager unitOfWorkManager,
            OnyxTravelBenefitEligibilityProcessor travelBenefitEligibilityProcessor,
            ILogger<WeeklyCommissionCalculator> logger)
        {
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _entryObligationRepository = entryObligationRepository;
            _loanAgreementRepository = loanAgreementRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _termsProvider = termsProvider;
            _unitOfWorkManager = unitOfWorkManager;
            _travelBenefitEligibilityProcessor = travelBenefitEligibilityProcessor;
            _logger = logger;
        }

        public async Task<CommissionCalculationResultDto> CalculateEntryAsync(
            int tenantId,
            ClosedCommissionWeek closedWeek,
            DateTime calculatedAt)
        {
            var terms = _termsProvider.GetEntryTerms();
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
                    terms.Currency,
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            var networkParticipations = await _entryParticipationRepository.GetAll()
                .Where(participation =>
                    participation.Status == EntryParticipationStatus.Active &&
                    participation.ActivatedAt <= closedWeek.PeriodEndUtc)
                .ToListAsync();
            var targetParticipations = networkParticipations
                .Where(participation => participation.TenantId == tenantId)
                .ToList();
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
                    networkParticipations,
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
            var terms = _termsProvider.GetOnyxTerms();
            var networkParticipations = await _onyxParticipationRepository.GetAll()
                .Where(participation =>
                    participation.Status == OnyxParticipationStatus.Active &&
                    participation.ActivatedAt <= closedWeek.PeriodEndUtc)
                .ToListAsync();
            var travelBenefitResult =
                await _travelBenefitEligibilityProcessor.SynchronizeAsync(
                    tenantId,
                    networkParticipations,
                    calculatedAt);
            if (travelBenefitResult.GrantedCount > 0 ||
                travelBenefitResult.ActivatedCount > 0)
            {
                _logger.LogInformation(
                    "Onyx travel benefits synchronized tenant={TenantId} granted={Granted} activated={Activated}",
                    tenantId,
                    travelBenefitResult.GrantedCount,
                    travelBenefitResult.ActivatedCount);
            }

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
                    terms.Currency,
                    wasAlreadyCalculated: true,
                    recordsCreated: 0);
            }

            var targetParticipations = networkParticipations
                .Where(participation => participation.TenantId == tenantId)
                .ToList();
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
                    networkParticipations))
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

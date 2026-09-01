using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.AQGreen;
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
        private readonly IRepository<AQGreenV2WeeklyCommissionEvidence, Guid>
            _entryV2EvidenceRepository;
        private readonly IRepository<OnyxWeeklyCommission, Guid> _onyxCommissionRepository;
        private readonly ICommissionTermsResolver _termsResolver;
        private readonly IAreaActivationStateResolver _areaActivationStateResolver;
        private readonly IAQGreenCommissionStructuralModelSelector
            _entryStructuralModelSelector;
        private readonly IAQGreenCommissionStructuralEvidenceEvaluator
            _entryStructuralEvidenceEvaluator;
        private readonly IAQGreenWeeklySalesEligibilityDecisionReader
            _weeklySalesDecisionReader;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public WeeklyCommissionCalculator(
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<EntryMonthlyObligation, Guid> entryObligationRepository,
            IRepository<OnyxLoanAgreement, Guid> loanAgreementRepository,
            IRepository<EntryCommissionPeriod, Guid> entryPeriodRepository,
            IRepository<OnyxCommissionPeriod, Guid> onyxPeriodRepository,
            IRepository<EntryWeeklyCommission, Guid> entryCommissionRepository,
            IRepository<AQGreenV2WeeklyCommissionEvidence, Guid> entryV2EvidenceRepository,
            IRepository<OnyxWeeklyCommission, Guid> onyxCommissionRepository,
            ICommissionTermsResolver termsResolver,
            IAreaActivationStateResolver areaActivationStateResolver,
            IAQGreenCommissionStructuralModelSelector entryStructuralModelSelector,
            IAQGreenCommissionStructuralEvidenceEvaluator entryStructuralEvidenceEvaluator,
            IAQGreenWeeklySalesEligibilityDecisionReader weeklySalesDecisionReader,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _entryObligationRepository = entryObligationRepository;
            _loanAgreementRepository = loanAgreementRepository;
            _entryPeriodRepository = entryPeriodRepository;
            _onyxPeriodRepository = onyxPeriodRepository;
            _entryCommissionRepository = entryCommissionRepository;
            _entryV2EvidenceRepository = entryV2EvidenceRepository;
            _onyxCommissionRepository = onyxCommissionRepository;
            _termsResolver = termsResolver;
            _areaActivationStateResolver = areaActivationStateResolver;
            _entryStructuralModelSelector = entryStructuralModelSelector;
            _entryStructuralEvidenceEvaluator = entryStructuralEvidenceEvaluator;
            _weeklySalesDecisionReader = weeklySalesDecisionReader;
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
                    existingPeriod.RulesVersion,
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
            var structuralModel = await _entryStructuralModelSelector.SelectAsync(
                tenantId,
                closedWeek.PeriodEndUtc);
            if (structuralModel != AQGreenCommissionStructuralModel.LegacyV1 &&
                structuralModel != AQGreenCommissionStructuralModel.PlacementV2)
                throw new InvalidOperationException(
                    "The selected AQGreen commission structural model is unsupported.");
            List<EntryParticipation> networkParticipations;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
            {
                var participationQuery = structuralModel ==
                        AQGreenCommissionStructuralModel.LegacyV1
                    ? _entryParticipationRepository.GetAllIncluding(
                        participation => participation.RecruiterCorrections)
                    : _entryParticipationRepository.GetAll();
                networkParticipations = await participationQuery
                    .Where(participation =>
                        participation.TenantId == tenantId &&
                        participation.Status == EntryParticipationStatus.Active &&
                        (!participation.ActivatedAt.HasValue ||
                         participation.ActivatedAt <= closedWeek.PeriodEndUtc))
                    .ToListAsync();
            }
            EnsureNoDeletedParticipationEvidence(networkParticipations, "AQGreen");
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

            EffectiveProgrammeNetwork effectiveNetwork = null;
            var placementV2Inputs = new List<PlacementV2CommissionInput>();
            if (structuralModel == AQGreenCommissionStructuralModel.LegacyV1)
            {
                effectiveNetwork = EffectiveProgrammeNetwork.BuildAQGreen(
                    tenantId,
                    networkParticipations,
                    closedWeek.PeriodEndUtc);
            }
            else
            {
                foreach (var participation in targetParticipations)
                {
                    var structuralEvidence = await _entryStructuralEvidenceEvaluator
                        .EvaluateAsync(
                            tenantId,
                            participation.Id,
                            closedWeek.PeriodEndUtc);
                    AQGreenWeeklySalesEligibilitySnapshot salesDecision = null;
                    if (structuralEvidence.StructuralCompletionLevel !=
                        AQGreenStructuralCompletionLevel.Level0)
                    {
                        salesDecision = await _weeklySalesDecisionReader
                            .GetFinalDecisionAsync(
                                tenantId,
                                participation.Id,
                                closedWeek.PeriodStartUtc,
                                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                    }
                    placementV2Inputs.Add(new PlacementV2CommissionInput(
                        participation,
                        structuralEvidence,
                        salesDecision));
                }
            }

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
            var commissions = new List<EntryWeeklyCommission>();
            if (structuralModel == AQGreenCommissionStructuralModel.LegacyV1)
            {
                commissions.AddRange(targetParticipations.Select(participation =>
                    calculator.Calculate(
                        participation,
                        period,
                        terms,
                        effectiveNetwork,
                        obligations,
                        loanAgreements)));
                foreach (var commission in commissions)
                    await _entryCommissionRepository.InsertAsync(commission);
            }
            else
            {
                foreach (var input in placementV2Inputs)
                {
                    var qualifiedLevel = (EntryNetworkLevel)(int)input
                        .StructuralEvidence.StructuralCompletionLevel;
                    var commissionedLevel = input.SalesDecision != null &&
                                            input.SalesDecision.ReviewStatus ==
                                                AQGreenWeeklySalesReviewStatus.Confirmed &&
                                            input.SalesDecision.ThresholdResult ==
                                                AQGreenWeeklySalesThresholdResult.Met
                        ? qualifiedLevel
                        : EntryNetworkLevel.None;
                    var commission = calculator.CalculatePlacementV2(
                        input.Participation,
                        period,
                        terms,
                        qualifiedLevel,
                        commissionedLevel,
                        obligations,
                        loanAgreements);
                    var evidence = AQGreenV2WeeklyCommissionEvidence.Capture(
                        commission,
                        period,
                        input.StructuralEvidence,
                        input.SalesDecision);
                    commissions.Add(commission);
                    await _entryCommissionRepository.InsertAsync(commission);
                    await _entryV2EvidenceRepository.InsertAsync(evidence);
                }
            }

            await _unitOfWorkManager.Current.SaveChangesAsync();
            return BuildResult(
                period.Id,
                "AQGreen",
                period.PeriodStart,
                period.PeriodEnd,
                period.TimeZoneId,
                period.RulesVersion,
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
                    existingPeriod.RulesVersion,
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
                period.RulesVersion,
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
            string rulesVersion,
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
                RulesVersion = rulesVersion,
                WasAlreadyCalculated = wasAlreadyCalculated,
                EvaluatedCount = rows.Count,
                RecordsCreated = recordsCreated,
                NotEarnedCount = rows.Count(row =>
                    row.Status == WeeklyCommissionPayoutStatus.NotEarned),
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

        private sealed class PlacementV2CommissionInput
        {
            public PlacementV2CommissionInput(
                EntryParticipation participation,
                AQGreenCommissionStructuralEvidenceResult structuralEvidence,
                AQGreenWeeklySalesEligibilitySnapshot salesDecision)
            {
                Participation = participation;
                StructuralEvidence = structuralEvidence;
                SalesDecision = salesDecision;
            }

            public EntryParticipation Participation { get; }
            public AQGreenCommissionStructuralEvidenceResult StructuralEvidence { get; }
            public AQGreenWeeklySalesEligibilitySnapshot SalesDecision { get; }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Transactions;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    [Audited]
    [AbpAuthorize(
        AquaPermissions.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility)]
    public class AdminAQGreenWeeklySalesEligibilityAppService
        : AdminAppServiceBase, IAdminAQGreenWeeklySalesEligibilityAppService
    {
        private readonly IRepository<AQGreenWeeklySalesEligibilityDecision, Guid>
            _decisionRepository;
        private readonly IRepository<EntryParticipation, Guid> _participationRepository;
        private readonly IAQGreenWeeklySalesEligibilityMutationLock _mutationLock;
        private readonly IAQGreenWeeklySalesEligibilityClock _clock;
        private readonly IAQGreenWeeklySalesReviewGate _reviewGate;
        private readonly IAQGreenWeeklySalesReviewScopePolicy _scopePolicy;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminAQGreenWeeklySalesEligibilityAppService(
            IRepository<AQGreenWeeklySalesEligibilityDecision, Guid> decisionRepository,
            IRepository<EntryParticipation, Guid> participationRepository,
            IAQGreenWeeklySalesEligibilityMutationLock mutationLock,
            IAQGreenWeeklySalesEligibilityClock clock,
            IAQGreenWeeklySalesReviewGate reviewGate,
            IAQGreenWeeklySalesReviewScopePolicy scopePolicy,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _decisionRepository = decisionRepository;
            _participationRepository = participationRepository;
            _mutationLock = mutationLock;
            _clock = clock;
            _reviewGate = reviewGate;
            _scopePolicy = scopePolicy;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork(IsDisabled = true)]
        public Task<AQGreenWeeklySalesEligibilityDecisionDto> BeginReviewAsync(
            BeginAQGreenWeeklySalesReviewInput input) =>
            ExecuteAsync(input, ReviewOperation.Begin, null, null, null);

        [UnitOfWork(IsDisabled = true)]
        public Task<AQGreenWeeklySalesEligibilityDecisionDto> ConfirmAsync(
            ConfirmAQGreenWeeklySalesEligibilityInput input) => input == null
            ? ExecuteAsync(null, ReviewOperation.Confirm, null, null, null)
            : ExecuteAsync(
                input,
                ReviewOperation.Confirm,
                new AQGreenWeeklySalesQuantities(
                    input.SprayQuantity,
                    input.OneLitreQuantity,
                    input.FiveLitreQuantity),
                input.EvidenceReferences,
                null);

        [UnitOfWork(IsDisabled = true)]
        public Task<AQGreenWeeklySalesEligibilityDecisionDto> RejectAsync(
            RejectAQGreenWeeklySalesEligibilityInput input) =>
            ExecuteAsync(
                input,
                ReviewOperation.Reject,
                null,
                input?.EvidenceReferences,
                input?.RejectionReason);

        private async Task<AQGreenWeeklySalesEligibilityDecisionDto> ExecuteAsync(
            BeginAQGreenWeeklySalesReviewInput input,
            ReviewOperation operation,
            AQGreenWeeklySalesQuantities quantities,
            IReadOnlyCollection<string> evidenceReferences,
            string rejectionReason)
        {
            if (input == null)
                throw Failed("AQGreen weekly-sales review", "Review details are required.");
            if (input.TenantId <= 0 || input.ParticipantId == Guid.Empty)
                throw Failed(
                    "AQGreen weekly-sales review",
                    "An explicit Tenant and AQGreen participation are required.");

            AQGreenCommissionWeek week;
            try
            {
                week = AQGreenCommissionWeek.FromStartUtc(
                    input.CommissionWeekStartUtc);
            }
            catch (ArgumentException exception)
            {
                throw Failed("AQGreen weekly-sales review", exception.Message);
            }

            if (AbpSession.TenantId.HasValue)
                throw new AbpAuthorizationException(
                    "AQGreen weekly-sales review is unavailable to Area-scoped administrators until the Area ownership policy is confirmed.");
            if (!await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Host AQGreen weekly-sales review requires permission to manage all Areas.");
            if (!await _scopePolicy.CanReviewAsync(input.TenantId))
                throw new AbpAuthorizationException(
                    "The AQGreen weekly-sales review is outside the authorized scope.");
            if (!await _reviewGate.IsEnabledAsync(input.TenantId))
                throw new UserFriendlyException(
                    "AQGreen weekly-sales review is disabled.",
                    "The detailed manual-review, Area ownership and evidence-retention policies are not yet approved.");

            var normalizedEvidence = operation == ReviewOperation.Begin
                ? Array.Empty<string>()
                : NormalizeEvidence(evidenceReferences);
            var normalizedReason = rejectionReason?.Trim();

            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true,
                // The transaction-scoped advisory lock owns serialization. Read
                // Committed is intentional: a waiter must establish a fresh
                // statement snapshot after acquiring the lock and see the winner.
                IsolationLevel = IsolationLevel.ReadCommitted
            }))
            using (CurrentUnitOfWork.DisableFilter(
                       AbpDataFilters.MayHaveTenant,
                       AbpDataFilters.MustHaveTenant))
            {
                await _mutationLock.AcquireAsync(
                    input.TenantId,
                    input.ParticipantId,
                    week.StartUtc,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);

                var existing = await FindAsync(
                    input.TenantId,
                    input.ParticipantId,
                    week.StartUtc);
                if (existing != null &&
                    existing.ReviewStatus !=
                    AQGreenWeeklySalesReviewStatus.HeldForEvidence)
                {
                    EnsureExactRetry(
                        existing,
                        operation,
                        quantities,
                        normalizedEvidence,
                        normalizedReason);
                    await uow.CompleteAsync();
                    return Map(existing);
                }

                var participantExists = await _participationRepository.GetAll()
                    .AnyAsync(participant =>
                        participant.TenantId == input.TenantId &&
                        participant.Id == input.ParticipantId);
                if (!participantExists)
                    throw Failed(
                        "AQGreen weekly-sales review",
                        "The AQGreen participation was not found in the target Tenant.");

                var decision = existing ?? AQGreenWeeklySalesEligibilityDecision.Begin(
                    input.TenantId,
                    input.ParticipantId,
                    week,
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                if (existing == null)
                {
                    await _decisionRepository.InsertAsync(decision);
                    await CurrentUnitOfWork.SaveChangesAsync();
                }

                if (operation != ReviewOperation.Begin)
                {
                    var reviewedAt = await _clock.GetUtcNowAsync();
                    foreach (var evidenceReference in normalizedEvidence)
                        decision.AddManualEvidence(evidenceReference, reviewedAt);
                    await CurrentUnitOfWork.SaveChangesAsync();

                    if (operation == ReviewOperation.Confirm)
                        decision.Confirm(quantities, AbpSession.GetUserId(), reviewedAt);
                    else
                        decision.Reject(
                            normalizedReason,
                            AbpSession.GetUserId(),
                            reviewedAt);
                    await CurrentUnitOfWork.SaveChangesAsync();
                }

                await uow.CompleteAsync();
                Logger.Info(
                    $"AQGreen weekly-sales review actor={AbpSession.GetUserId()} " +
                    $"tenant={input.TenantId} participant={input.ParticipantId} " +
                    $"week={week.StartUtc:O} operation={operation} status={decision.ReviewStatus}");
                return Map(decision);
            }
        }

        private Task<AQGreenWeeklySalesEligibilityDecision> FindAsync(
            int tenantId,
            Guid participantId,
            DateTime weekStartUtc) =>
            _decisionRepository.GetAllIncluding(item => item.EvidenceReferences)
                .SingleOrDefaultAsync(item =>
                    item.TenantId == tenantId &&
                    item.ParticipantId == participantId &&
                    item.CommissionWeekStartUtc == weekStartUtc &&
                    item.SalesEligibilityRulesVersion ==
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);

        private static string[] NormalizeEvidence(
            IReadOnlyCollection<string> evidenceReferences)
        {
            if (evidenceReferences == null || evidenceReferences.Count == 0)
                throw Failed(
                    "AQGreen weekly-sales review",
                    "At least one opaque technical evidence reference is required.");
            var normalized = evidenceReferences
                .Select(item => item?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            if (normalized.Length == 0 || normalized.Any(item =>
                    item.Length >
                    AQGreenWeeklySalesEvidenceReference.MaximumTechnicalReferenceLength))
            {
                throw Failed(
                    "AQGreen weekly-sales review",
                    "Evidence references must be nonblank and within the technical-reference length limit.");
            }
            return normalized;
        }

        private static void EnsureExactRetry(
            AQGreenWeeklySalesEligibilityDecision existing,
            ReviewOperation operation,
            AQGreenWeeklySalesQuantities quantities,
            IReadOnlyCollection<string> evidence,
            string rejectionReason)
        {
            var storedEvidence = existing.EvidenceReferences
                .Select(item => item.TechnicalReference)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
            var evidenceMatches = storedEvidence.SequenceEqual(evidence);
            var evaluatedResult = quantities == null
                ? (AQGreenWeeklySalesThresholdResult?)null
                : AQGreenWeeklySalesEligibilityEvaluator.Evaluate(
                    existing.SalesEligibilityRulesVersion,
                    quantities);
            var exact = operation == ReviewOperation.Confirm &&
                        existing.ReviewStatus == AQGreenWeeklySalesReviewStatus.Confirmed &&
                        existing.ReviewedSprayQuantity == quantities?.Spray &&
                        existing.ReviewedOneLitreQuantity == quantities?.OneLitre &&
                        existing.ReviewedFiveLitreQuantity == quantities?.FiveLitre &&
                        existing.ThresholdResult == evaluatedResult &&
                        evidenceMatches ||
                        operation == ReviewOperation.Reject &&
                        existing.ReviewStatus == AQGreenWeeklySalesReviewStatus.Rejected &&
                        string.Equals(
                            existing.RejectionReason,
                            rejectionReason,
                            StringComparison.Ordinal) &&
                        evidenceMatches;
            if (!exact)
                throw Failed(
                    "AQGreen weekly-sales review",
                    "A conflicting finalized decision already exists for this participant, week and rules version.");
        }

        private static AQGreenWeeklySalesEligibilityDecisionDto Map(
            AQGreenWeeklySalesEligibilityDecision decision) => new()
        {
            Id = decision.Id,
            TenantId = decision.TenantId,
            ParticipantId = decision.ParticipantId,
            CommissionWeekStartUtc = decision.CommissionWeekStartUtc,
            SalesEligibilityRulesVersion = decision.SalesEligibilityRulesVersion,
            ReviewStatus = decision.ReviewStatus,
            ReviewedSprayQuantity = decision.ReviewedSprayQuantity,
            ReviewedOneLitreQuantity = decision.ReviewedOneLitreQuantity,
            ReviewedFiveLitreQuantity = decision.ReviewedFiveLitreQuantity,
            ThresholdResult = decision.ThresholdResult,
            ReviewedAt = decision.ReviewedAt,
            ReviewedByUserId = decision.ReviewedByUserId,
            RejectionReason = decision.RejectionReason
        };

        private enum ReviewOperation
        {
            Begin,
            Confirm,
            Reject
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.MultiTenancy;
using Castle.MicroKernel.Registration;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public sealed class AQGreenCommissionInfrastructureTests
        : AqualLifeStyleTestBase
    {
        private readonly TestSelector _selector = new();
        private readonly TestStructuralEvidenceEvaluator _evaluator = new();
        private readonly TestSalesDecisionReader _salesDecisionReader = new();
        private readonly IAdminCommissionAppService _service;

        public AQGreenCommissionInfrastructureTests()
        {
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenCommissionStructuralModelSelector>()
                    .Instance(_selector)
                    .Named($"b5.4-selector-{Guid.NewGuid():N}")
                    .IsDefault());
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenCommissionStructuralEvidenceEvaluator>()
                    .Instance(_evaluator)
                    .Named($"b5.4-evaluator-{Guid.NewGuid():N}")
                    .IsDefault());
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenWeeklySalesEligibilityDecisionReader>()
                    .Instance(_salesDecisionReader)
                    .Named($"b5.4-sales-reader-{Guid.NewGuid():N}")
                    .IsDefault());
            _service = Resolve<IAdminCommissionAppService>();
        }

        [Fact]
        public async Task ProductionSelector_RemainsDormantLegacyV1()
        {
            var selector = new LegacyV1AQGreenCommissionStructuralModelSelector();

            var result = await selector.SelectAsync(
                1,
                new DateTime(2035, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            result.ShouldBe(AQGreenCommissionStructuralModel.LegacyV1);
        }

        [Fact]
        public async Task SelectedPlacementV2_Level0_BypassesSalesAndRetryIsIdempotent()
        {
            var fixture = await CreateFixtureAsync(SalesOutcome.Missing);
            _selector.Model = AQGreenCommissionStructuralModel.PlacementV2;
            ConfigureEvaluator(fixture);
            LoginAsHostAdmin();

            var first = await CalculateAsync();
            var retry = await CalculateAsync();

            first.WasAlreadyCalculated.ShouldBeFalse();
            first.RecordsCreated.ShouldBe(1);
            retry.WasAlreadyCalculated.ShouldBeTrue();
            retry.PeriodId.ShouldBe(first.PeriodId);
            _evaluator.CallCount.ShouldBe(1);
            _salesDecisionReader.CallCount.ShouldBe(0);
            await UsingDbContextAsync(null, async context =>
            {
                var commission = await context.EntryWeeklyCommissions
                    .IgnoreQueryFilters()
                    .SingleAsync();
                commission.StructuralModel.ShouldBe(
                    AQGreenCommissionStructuralModel.PlacementV2);
                commission.CommissionDecisionRulesVersion.ShouldBe(
                    AQGreenCommissionDecisionRules.CurrentVersion);
                commission.HighestQualifiedNetworkLevel.ShouldBe(0);
                commission.HighestCommissionedLevel.ShouldBe(0);
                var evidence = await context.AQGreenV2WeeklyCommissionEvidence
                    .IgnoreQueryFilters()
                    .Include(item => item.Nodes)
                    .SingleAsync();
                evidence.SalesApplicability.ShouldBe(
                    AQGreenWeeklySalesApplicability.NotApplicable);
                evidence.WeeklySalesEligibilityDecisionId.ShouldBeNull();
                evidence.SalesEligibilityRulesVersion.ShouldBeNull();
                evidence.SalesReviewStatus.ShouldBeNull();
                evidence.SalesThresholdResult.ShouldBeNull();
                evidence.SalesReviewedAt.ShouldBeNull();
                evidence.SalesReviewedByUserId.ShouldBeNull();
                evidence.Nodes.Single().SourcePlacementId.ShouldBe(
                    fixture.RootPlacementId);
            });
        }

        [Theory]
        [InlineData(SalesOutcome.ConfirmedNotMet)]
        [InlineData(SalesOutcome.Rejected)]
        public async Task SelectedPlacementV2_Level0_IsNotEarnedWithoutSalesOrHold(
            SalesOutcome outcome)
        {
            var fixture = await CreateFixtureAsync(outcome);
            _selector.Model = AQGreenCommissionStructuralModel.PlacementV2;
            ConfigureEvaluator(fixture);
            LoginAsHostAdmin();

            var result = await CalculateAsync();

            result.NotEarnedCount.ShouldBe(1);
            result.HeldCount.ShouldBe(0);
            await UsingDbContextAsync(null, async context =>
            {
                var commission = await context.EntryWeeklyCommissions
                    .IgnoreQueryFilters()
                    .SingleAsync();
                commission.PayoutStatus.ShouldBe(
                    WeeklyCommissionPayoutStatus.NotEarned);
                commission.HoldReason.ShouldBeNull();
                var evidence = await context.AQGreenV2WeeklyCommissionEvidence
                    .IgnoreQueryFilters()
                    .SingleAsync();
                evidence.SalesApplicability.ShouldBe(
                    AQGreenWeeklySalesApplicability.NotApplicable);
                evidence.WeeklySalesEligibilityDecisionId.ShouldBeNull();
                evidence.SalesReviewStatus.ShouldBeNull();
                evidence.SalesThresholdResult.ShouldBeNull();
            });
        }

        [Theory]
        [InlineData(SalesOutcome.Held)]
        [InlineData(SalesOutcome.Missing)]
        public async Task SelectedPlacementV2_CandidateUnfinalizedSales_RollsBackWithoutLegacyFallback(
            SalesOutcome outcome)
        {
            var fixture = await CreateFixtureAsync(outcome);
            _selector.Model = AQGreenCommissionStructuralModel.PlacementV2;
            ConfigureEvaluator(fixture, AQGreenStructuralCompletionLevel.Level1);
            LoginAsHostAdmin();

            await Should.ThrowAsync<AQGreenWeeklySalesEligibilityUnavailableException>(
                CalculateAsync);

            _evaluator.CallCount.ShouldBe(1);
            _salesDecisionReader.CallCount.ShouldBe(1);
            await UsingDbContextAsync(null, async context =>
            {
                (await context.EntryCommissionPeriods.IgnoreQueryFilters().AnyAsync())
                    .ShouldBeFalse();
                (await context.EntryWeeklyCommissions.IgnoreQueryFilters().AnyAsync())
                    .ShouldBeFalse();
                (await context.AQGreenV2WeeklyCommissionEvidence
                    .IgnoreQueryFilters().AnyAsync()).ShouldBeFalse();
            });
        }

        private Task<CommissionCalculationResultDto> CalculateAsync() =>
            _service.CalculateLatestClosedWeekAsync(
                new CalculateLatestClosedCommissionWeekInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry
                });

        private void ConfigureEvaluator(
            Fixture fixture,
            AQGreenStructuralCompletionLevel level = AQGreenStructuralCompletionLevel.Level0)
        {
            _evaluator.ResultFactory = cutoff =>
                new AQGreenCommissionStructuralEvidenceResult(
                    fixture.ParticipationId,
                    fixture.ScopeId,
                    cutoff,
                    level,
                    level == AQGreenStructuralCompletionLevel.Level1 ? 5 : 0,
                    0,
                    0,
                    AQGreenPlacementRules.CurrentVersion,
                    AQGreenStructuralQualificationRules.CurrentVersion,
                    new[]
                    {
                        new AQGreenCommissionStructuralEvidenceObservation
                        {
                            CanonicalOrdinal = 0,
                            SourcePlacementId = fixture.RootPlacementId,
                            ParticipationStatusObserved = EntryParticipationStatus.Active,
                            ParticipationActivatedAtObserved = fixture.ActivatedAt,
                            CustomerIdObserved = fixture.CustomerId,
                            CustomerTenantMatchedObserved = true,
                            CustomerIsActiveObserved = true,
                            UserIdObserved = fixture.UserId,
                            UserTenantMatchedObserved = true,
                            UserIsActiveObserved = true
                        }
                    });
        }

        private async Task<Fixture> CreateFixtureAsync(SalesOutcome outcome)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var resolver = Resolve<LatestClosedCommissionWeekResolver>();
            var closedWeek = resolver.Resolve(DateTime.UtcNow);
            var startedAt = closedWeek.PeriodStartUtc.AddDays(-14);
            var userId = await CreateTestUserAsync(
                1,
                $"b54-{suffix}",
                $"b54-{suffix}@example.test");
            var fixture = await UsingDbContextAsync(1, async context =>
            {
                var area = await context.Areas.SingleAsync(item =>
                    item.TenantId == 1 && item.Code == "JHB");
                var customer = Customer.Create(
                    1,
                    userId,
                    $"B5.4 Customer {suffix}",
                    new EmailAddress($"b54-customer-{suffix}@example.test"));
                customer.AssignInitialArea(
                    area,
                    startedAt,
                    "B5.4 commission application test");
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var programmeTerms = EntryProgrammeTerms.Create(
                    $"b54-entry-{suffix}",
                    startedAt,
                    600m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    programmeTerms,
                    startedAt);
                foreach (var purpose in new[]
                         {
                             MemberPaymentPurpose.EntryRegistration,
                             MemberPaymentPurpose.EntryActivation
                         })
                {
                    var payment = MemberPayment.CreatePending(
                        1,
                        customer.Id,
                        purpose,
                        600m,
                        "Yoco",
                        $"b54-{purpose}-{suffix}",
                        startedAt);
                    payment.Confirm(startedAt.AddMinutes(1));
                    participation.ApplyConfirmedActivationPayment(payment);
                    context.MemberPayments.Add(payment);
                }
                participation.ApproveByAdministrator(1, startedAt.AddMinutes(2));
                context.EntryParticipations.Add(participation);
                var scope = AQGreenPlacementTreeScope.Create(1);
                var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    participation.Id,
                    startedAt,
                    AQGreenPlacementRules.CurrentVersion);
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.Add(rootPlacement);
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        1,
                        true,
                        startedAt,
                        startedAt,
                        null,
                        "B5.4 active Area baseline",
                        AreaActivationStateRecordKind.ObservedBaseline));
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        $"b54-terms-{suffix}",
                        closedWeek.PeriodStartUtc,
                        150m,
                        250m,
                        1250m));
                await context.SaveChangesAsync();
                return new Fixture(
                    participation.Id,
                    customer.Id,
                    userId,
                    participation.ActivatedAt.Value,
                    scope.Id,
                    rootPlacement.Id,
                    closedWeek.PeriodStartUtc);
            });

            if (outcome == SalesOutcome.Missing) return fixture;
            await UsingDbContextAsync(1, async context =>
            {
                var decision = AQGreenWeeklySalesEligibilityDecision.Begin(
                    1,
                    fixture.ParticipationId,
                    AQGreenCommissionWeek.FromStartUtc(fixture.WeekStart),
                    AQGreenWeeklySalesEligibilityRules.CurrentVersion);
                context.AQGreenWeeklySalesEligibilityDecisions.Add(decision);
                await context.SaveChangesAsync();
                decision.AddManualEvidence(
                    $"b54-sales-{suffix}",
                    fixture.WeekStart.AddDays(7));
                await context.SaveChangesAsync();
                if (outcome == SalesOutcome.ConfirmedMet)
                    decision.Confirm(
                        new AQGreenWeeklySalesQuantities(5, 5, 5),
                        1,
                        fixture.WeekStart.AddDays(7));
                else if (outcome == SalesOutcome.ConfirmedNotMet)
                    decision.Confirm(
                        new AQGreenWeeklySalesQuantities(5, 4, 5),
                        1,
                        fixture.WeekStart.AddDays(7));
                else if (outcome == SalesOutcome.Rejected)
                    decision.Reject(
                        "Evidence could not be substantiated.",
                        1,
                        fixture.WeekStart.AddDays(7));
                await context.SaveChangesAsync();
                fixture.SalesDecisionId = decision.Id;
            });
            return fixture;
        }

        public enum SalesOutcome
        {
            ConfirmedMet,
            ConfirmedNotMet,
            Rejected,
            Held,
            Missing
        }

        private sealed class TestSelector : IAQGreenCommissionStructuralModelSelector
        {
            public AQGreenCommissionStructuralModel Model { get; set; } =
                AQGreenCommissionStructuralModel.LegacyV1;

            public Task<AQGreenCommissionStructuralModel> SelectAsync(
                int tenantId,
                DateTime commissionCutoffUtc) => Task.FromResult(Model);
        }

        private sealed class TestStructuralEvidenceEvaluator
            : IAQGreenCommissionStructuralEvidenceEvaluator
        {
            private int _callCount;
            public Func<DateTime, AQGreenCommissionStructuralEvidenceResult>
                ResultFactory { get; set; }
            public int CallCount => Volatile.Read(ref _callCount);

            public Task<AQGreenCommissionStructuralEvidenceResult> EvaluateAsync(
                int tenantId,
                Guid participantId,
                DateTime cutoff,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _callCount);
                return Task.FromResult(ResultFactory?.Invoke(cutoff) ??
                    throw new InvalidOperationException(
                        "The B5.4 structural evaluator was not configured."));
            }
        }

        private sealed class TestSalesDecisionReader
            : IAQGreenWeeklySalesEligibilityDecisionReader
        {
            private int _callCount;

            public int CallCount => Volatile.Read(ref _callCount);

            public Task<AQGreenWeeklySalesEligibilitySnapshot> GetFinalDecisionAsync(
                int tenantId,
                Guid participantId,
                DateTime commissionWeekStartUtc,
                string salesEligibilityRulesVersion,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _callCount);
                throw new AQGreenWeeklySalesEligibilityUnavailableException(
                    "The test sales decision reader intentionally has no finalized decision.");
            }
        }

        private sealed class Fixture
        {
            public Fixture(
                Guid participationId,
                int customerId,
                long userId,
                DateTime activatedAt,
                Guid scopeId,
                Guid rootPlacementId,
                DateTime weekStart)
            {
                ParticipationId = participationId;
                CustomerId = customerId;
                UserId = userId;
                ActivatedAt = activatedAt;
                ScopeId = scopeId;
                RootPlacementId = rootPlacementId;
                WeekStart = weekStart;
            }

            public Guid ParticipationId { get; }
            public int CustomerId { get; }
            public long UserId { get; }
            public DateTime ActivatedAt { get; }
            public Guid ScopeId { get; }
            public Guid RootPlacementId { get; }
            public DateTime WeekStart { get; }
            public Guid SalesDecisionId { get; set; }
        }
    }
}

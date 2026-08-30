using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Castle.MicroKernel.Registration;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class AQGreenGraduationApplicationCollection
    {
        public const string Name = "AQGreen graduation application";
    }

    [Collection(AQGreenGraduationApplicationCollection.Name)]
    public sealed class AdminProgrammeParticipationV2AppServiceTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly EntryProgrammeTerms Terms =
            EntryProgrammeTerms.Create(
                "entry-b5.2-v1",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);
        private readonly TestSelector _selector = new();
        private readonly TestEvidenceEvaluator _evaluator = new();
        private readonly TrackingCurrentProgrammeTermsProvider _currentTerms = new();
        private readonly IAdminProgrammeParticipationAppService _service;

        public AdminProgrammeParticipationV2AppServiceTests()
        {
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenGraduationStructuralModelSelector>()
                    .Instance(_selector)
                    .Named($"b5.2-selector-{Guid.NewGuid():N}")
                    .IsDefault());
            LocalIocManager.IocContainer.Register(
                Component.For<IAQGreenGraduationStructuralEvidenceEvaluator>()
                    .Instance(_evaluator)
                    .Named($"b5.2-evaluator-{Guid.NewGuid():N}")
                    .IsDefault());
            LocalIocManager.IocContainer.Register(
                Component.For<ICurrentProgrammeTermsProvider>()
                    .Instance(_currentTerms)
                    .Named($"b5.2-current-terms-{Guid.NewGuid():N}")
                    .IsDefault());
            _service = Resolve<IAdminProgrammeParticipationAppService>();
        }

        [Fact]
        public async Task PlacementV2LevelTwo_PersistsOneCoherentDecisionEvidenceGraph()
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = AQGreenGraduationStructuralModel.PlacementV2;
            _evaluator.Failures.Enqueue(
                new AQGreenStructuralContributionPolicyRequiredException(
                    fixture.RootParticipationId));

            await Should.ThrowAsync<AQGreenStructuralContributionPolicyRequiredException>(() =>
                _service.GraduateAQGreenToOnyxAsync(
                    new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = fixture.LoanAgreementId,
                        Justification = "D08 must fail closed without partial graduation"
                    }));
            await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxGraduationDecisions.AnyAsync()).ShouldBeFalse();
                (await context.AQGreenV2GraduationEvidence.AnyAsync()).ShouldBeFalse();
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == fixture.RootCustomerId)).ShouldBeFalse();
            });

            _evaluator.CreateResult = cutoff => CreateEvidenceResult(
                fixture,
                cutoff,
                AQGreenStructuralCompletionLevel.Level2,
                5,
                25,
                includeObservations: true);
            _evaluator.Failures.Enqueue(PostgreSqlSerializationFailure());

            var result = await _service.GraduateAQGreenToOnyxAsync(
                new GraduateAQGreenToOnyxInput
                {
                    LoanAgreementId = fixture.LoanAgreementId,
                    Justification = "Approved from bounded Placement V2 Level 2 evidence"
                });

            result.StructuralModel.ShouldBe(AQGreenGraduationStructuralModel.PlacementV2);
            result.EvaluatedNetworkLevel.ShouldBeNull();
            _evaluator.CallCount.ShouldBe(3);
            await UsingDbContextAsync(1, async context =>
            {
                var decision = await context.OnyxGraduationDecisions
                    .SingleAsync(item => item.Id == result.DecisionId);
                decision.StructuralModel.ShouldBe(
                    AQGreenGraduationStructuralModel.PlacementV2);
                decision.GraduationRulesVersion.ShouldBe(OnyxGraduationRules.CurrentVersion);
                decision.EvaluatedLoanTermsVersion.ShouldBe(fixture.LoanTermsVersion);
                var evidence = await context.AQGreenV2GraduationEvidence
                    .Include(item => item.Nodes)
                    .SingleAsync(item => item.Id == result.DecisionId);
                evidence.EvidenceNodeCount.ShouldBe(31);
                evidence.Nodes.Count.ShouldBe(31);
                evidence.Nodes.OrderBy(item => item.CanonicalOrdinal)
                    .Select(item => item.SourcePlacementId)
                    .ShouldBe(fixture.Placements.Select(item => item.Id));
                (await context.OnyxParticipations.CountAsync(item =>
                    item.CustomerId == fixture.RootCustomerId)).ShouldBe(1);
            });
        }

        [Fact]
        public async Task DefaultLegacyPath_KeepsLevelTwoSemanticsAndNeverInvokesV2Evaluator()
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = AQGreenGraduationStructuralModel.LegacyV1;
            _evaluator.Failures.Enqueue(new InvalidOperationException(
                "The V2 evaluator must not be called by Legacy V1."));

            var result = await _service.GraduateAQGreenToOnyxAsync(
                new GraduateAQGreenToOnyxInput
                {
                    LoanAgreementId = fixture.LoanAgreementId,
                    Justification = "Legacy recruiter/correction Level 2 remains authoritative"
                });
            var retry = await _service.GraduateAQGreenToOnyxAsync(
                new GraduateAQGreenToOnyxInput
                {
                    LoanAgreementId = fixture.LoanAgreementId,
                    Justification = "Identical retry returns the durable Legacy V1 result"
                });

            result.StructuralModel.ShouldBe(AQGreenGraduationStructuralModel.LegacyV1);
            result.EvaluatedNetworkLevel.ShouldBe(EntryNetworkLevel.Level2);
            retry.DecisionId.ShouldBe(result.DecisionId);
            _evaluator.CallCount.ShouldBe(0);
            await UsingDbContextAsync(1, async context =>
            {
                var decision = await context.OnyxGraduationDecisions
                    .SingleAsync(item => item.Id == result.DecisionId);
                decision.EvaluatedLoanTermsVersion.ShouldBe(fixture.LoanTermsVersion);
                (await context.AQGreenV2GraduationEvidence.AnyAsync()).ShouldBeFalse();
            });
        }

        [Fact]
        public async Task CurrentLegacyRetry_RejectsUnsupportedGraduationRulesVersion()
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = AQGreenGraduationStructuralModel.LegacyV1;
            var result = await _service.GraduateAQGreenToOnyxAsync(
                new GraduateAQGreenToOnyxInput
                {
                    LoanAgreementId = fixture.LoanAgreementId,
                    Justification = "Create a versioned Legacy V1 graduation"
                });
            await UsingDbContextAsync(1, context =>
                context.OnyxGraduationDecisions
                    .Where(item => item.Id == result.DecisionId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        item => item.GraduationRulesVersion,
                        "unsupported-graduation-version")));

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.GraduateAQGreenToOnyxAsync(
                    new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = fixture.LoanAgreementId,
                        Justification = "Unsupported historical rules must fail closed"
                    }));

            exception.Message.ShouldBe("Onyx graduation requires reconciliation.");
            exception.Details.ShouldContain("unsupported graduation rules version");
            _evaluator.CallCount.ShouldBe(0);
        }

        [Theory]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1)]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2)]
        public async Task Graduation_UsesAcceptedAgreementAfterCurrentCatalogPriceChanges(
            AQGreenGraduationStructuralModel model)
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = model;
            if (model == AQGreenGraduationStructuralModel.PlacementV2)
                ConfigureLevelTwoEvidence(fixture);
            _currentTerms.DirectOnyxTerms = OnyxPlanTerms.Create(
                "current-catalog-y",
                EffectiveFrom.AddDays(20),
                directEntryAmount: 9999m,
                currency: "ZAR");

            var result = await _service.GraduateAQGreenToOnyxAsync(
                new GraduateAQGreenToOnyxInput
                {
                    LoanAgreementId = fixture.LoanAgreementId,
                    Justification =
                        "Accepted agreement X remains authoritative after catalog Y"
                });

            result.StructuralModel.ShouldBe(model);
            result.EvaluatedNetworkLevel.ShouldBe(
                model == AQGreenGraduationStructuralModel.LegacyV1
                    ? EntryNetworkLevel.Level2
                    : null);
            _evaluator.CallCount.ShouldBe(
                model == AQGreenGraduationStructuralModel.PlacementV2 ? 1 : 0);
            _currentTerms.DirectOnyxTermsCallCount.ShouldBe(0);
            await UsingDbContextAsync(1, async context =>
            {
                var loan = await context.OnyxLoanAgreements
                    .SingleAsync(item => item.Id == fixture.LoanAgreementId);
                var decision = await context.OnyxGraduationDecisions
                    .SingleAsync(item => item.Id == result.DecisionId);
                var onyx = await context.OnyxParticipations
                    .SingleAsync(item => item.Id == decision.OnyxParticipationId);
                loan.TermsVersion.ShouldBe(fixture.LoanTermsVersion);
                loan.Currency.ShouldBe("ZAR");
                decision.EvaluatedLoanTermsVersion.ShouldBe(fixture.LoanTermsVersion);
                decision.EvaluatedFundingAmount.ShouldBe(6120m);
                decision.EvaluatedFundingCurrency.ShouldBe("ZAR");
                onyx.TermsVersion.ShouldBe(fixture.LoanTermsVersion);
                onyx.DirectEntryAmount.ShouldBe(6120m);
                onyx.Currency.ShouldBe("ZAR");
            });
        }

        [Theory]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, " accepted-v1 ", null)]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, "accepted-v1 ", null)]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, " accepted-v1", null)]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, null, "zar")]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, null, " ZAR")]
        [InlineData(AQGreenGraduationStructuralModel.LegacyV1, null, "ZAR ")]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, " accepted-v1 ", null)]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, "accepted-v1 ", null)]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, " accepted-v1", null)]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, null, "zar")]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, null, " ZAR")]
        [InlineData(AQGreenGraduationStructuralModel.PlacementV2, null, "ZAR ")]
        public async Task Graduation_CorruptAcceptedAgreementFailsClosed(
            AQGreenGraduationStructuralModel model,
            string termsVersion,
            string currency)
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = model;
            await UsingDbContextAsync(1, async context =>
            {
                var loans = context.OnyxLoanAgreements
                    .Where(item => item.Id == fixture.LoanAgreementId);
                if (termsVersion != null)
                    await loans.ExecuteUpdateAsync(setters => setters.SetProperty(
                        item => item.TermsVersion,
                        termsVersion));
                else if (currency != null)
                    await loans.ExecuteUpdateAsync(setters => setters.SetProperty(
                        item => item.Currency,
                        currency));
                else
                    throw new InvalidOperationException("A corrupt contractual value is required.");
            });

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.GraduateAQGreenToOnyxAsync(
                    new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = fixture.LoanAgreementId,
                        Justification =
                            "Corrupt accepted terms must not be fabricated"
                    }));

            exception.Details.ShouldContain("accepted loan agreement terms");
            _evaluator.CallCount.ShouldBe(0);
            await UsingDbContextAsync(1, async context =>
            {
                var loan = await context.OnyxLoanAgreements
                    .SingleAsync(item => item.Id == fixture.LoanAgreementId);
                if (termsVersion != null) loan.TermsVersion.ShouldBe(termsVersion);
                if (currency != null) loan.Currency.ShouldBe(currency);
                (await context.OnyxParticipations.CountAsync()).ShouldBe(0);
                (await context.OnyxGraduationDecisions.CountAsync()).ShouldBe(0);
                (await context.AQGreenV2GraduationEvidence.CountAsync()).ShouldBe(0);
                (await context.AQGreenV2GraduationEvidenceNodes.CountAsync()).ShouldBe(0);
            });
        }

        [Fact]
        public async Task SelectedPlacementV2LevelOne_FailsWithoutLegacyFallbackOrPartialState()
        {
            var fixture = await CreatePlacementV2FixtureAsync();
            _selector.Model = AQGreenGraduationStructuralModel.PlacementV2;
            _evaluator.CreateResult = cutoff => CreateEvidenceResult(
                fixture,
                cutoff,
                AQGreenStructuralCompletionLevel.Level1,
                5,
                24,
                includeObservations: false);

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _service.GraduateAQGreenToOnyxAsync(
                    new GraduateAQGreenToOnyxInput
                    {
                        LoanAgreementId = fixture.LoanAgreementId,
                        Justification = "Must remain in the selected V2 path"
                    }));

            exception.Details.ShouldContain("no longer satisfies AQGreen Level 2");
            _evaluator.CallCount.ShouldBe(1);
            await UsingDbContextAsync(1, async context =>
            {
                (await context.OnyxGraduationDecisions.AnyAsync(item =>
                    item.LoanAgreementId == fixture.LoanAgreementId)).ShouldBeFalse();
                (await context.AQGreenV2GraduationEvidence.AnyAsync()).ShouldBeFalse();
                (await context.OnyxParticipations.AnyAsync(item =>
                    item.CustomerId == fixture.RootCustomerId)).ShouldBeFalse();
            });
        }

        private async Task<PlacementV2Fixture> CreatePlacementV2FixtureAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            for (var index = 0; index < 31; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"b52-v2-{index}-{suffix}",
                    $"b52-v2-{index}-{suffix}@example.test"));
            }

            return await UsingDbContextAsync(1, async context =>
            {
                var area = await context.Areas.SingleAsync(item =>
                    item.TenantId == 1 && item.Code == "JHB");
                var customers = userIds.Select((userId, index) =>
                {
                    var customer = Customer.Create(
                        1,
                        userId,
                        $"B5.2 V2 Customer {index} {suffix}",
                        new EmailAddress($"b52-v2-customer-{index}-{suffix}@example.test"));
                    customer.AssignInitialArea(
                        area,
                        EffectiveFrom,
                        "B5.2 Placement V2 application test");
                    return customer;
                }).ToList();
                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();

                var root = EntryParticipation.StartIndependently(
                    1,
                    customers[0].Id,
                    Terms,
                    EffectiveFrom);
                var participations = new List<EntryParticipation> { root };
                var payments = new List<MemberPayment>();
                payments.AddRange(Activate(
                    root,
                    customers[0].Id,
                    $"b52-v2-0-{suffix}"));
                var depthOne = new List<EntryParticipation>();
                for (var index = 1; index <= 5; index++)
                {
                    var child = EntryParticipation.StartUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        Terms,
                        EffectiveFrom);
                    payments.AddRange(Activate(
                        child,
                        customers[index].Id,
                        $"b52-v2-{index}-{suffix}"));
                    depthOne.Add(child);
                    participations.Add(child);
                }
                var customerIndex = 6;
                foreach (var parent in depthOne)
                {
                    for (var slot = 1; slot <= 5; slot++)
                    {
                        var child = EntryParticipation.StartUnderRecruiter(
                            1,
                            customers[customerIndex++].Id,
                            parent,
                            Terms,
                            EffectiveFrom);
                        payments.AddRange(Activate(
                            child,
                            child.CustomerId,
                            $"b52-v2-{participations.Count}-{suffix}"));
                        participations.Add(child);
                    }
                }

                context.MemberPayments.AddRange(payments);
                context.EntryParticipations.AddRange(participations);
                await context.SaveChangesAsync();

                var scope = AQGreenPlacementTreeScope.Create(1);
                var rootPlacement = AQGreenNetworkPlacement.CreateRoot(
                    scope,
                    root.Id,
                    EffectiveFrom.AddDays(1),
                    AQGreenPlacementRules.CurrentVersion);
                var placements = new List<AQGreenNetworkPlacement> { rootPlacement };
                for (var index = 0; index < depthOne.Count; index++)
                {
                    placements.Add(AQGreenNetworkPlacement.CreateChild(
                        rootPlacement,
                        depthOne[index].Id,
                        index + 1,
                        EffectiveFrom.AddDays(1),
                        AQGreenPlacementRules.CurrentVersion));
                }
                var participationIndex = 6;
                for (var parentIndex = 0; parentIndex < 5; parentIndex++)
                {
                    var parentPlacement = placements[parentIndex + 1];
                    for (var slot = 1; slot <= 5; slot++)
                    {
                        placements.Add(AQGreenNetworkPlacement.CreateChild(
                            parentPlacement,
                            participations[participationIndex++].Id,
                            slot,
                            EffectiveFrom.AddDays(1),
                            AQGreenPlacementRules.CurrentVersion));
                    }
                }
                context.AQGreenPlacementTreeScopes.Add(scope);
                context.AQGreenNetworkPlacements.AddRange(placements);

                if (!await context.Memberships.AnyAsync(item =>
                        item.MembershipType == MembershipType.Onyx && item.IsActive))
                {
                    context.Memberships.Add(Membership.Create(
                        1,
                        $"B5.2 Onyx {suffix}",
                        "B5.2 graduation test membership",
                        MembershipType.Onyx));
                }

                var loanTerms = OnyxLoanTerms.Create(
                    $"b52-{suffix.Substring(0, 8)}",
                    EffectiveFrom,
                    6120m,
                    30m,
                    3,
                    4,
                    200m);
                var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                    root,
                    participations,
                    new EntryNetworkQualificationEvaluator(),
                    loanTerms,
                    EffectiveFrom.AddDays(2));
                loan.AcceptByMember(
                    customers[0].UserId,
                    "Accepted B5.2 terms",
                    EffectiveFrom.AddDays(3));
                loan.ApproveByAdministrator(1, EffectiveFrom.AddDays(4));
                context.OnyxLoanAgreements.Add(loan);
                await context.SaveChangesAsync();

                return new PlacementV2Fixture
                {
                    RootCustomerId = customers[0].Id,
                    RootParticipationId = root.Id,
                    LoanAgreementId = loan.Id,
                    LoanTermsVersion = loan.TermsVersion,
                    Participations = participations,
                    Customers = customers,
                    Placements = placements
                };
            });
        }

        private static AQGreenGraduationStructuralEvidenceResult CreateEvidenceResult(
            PlacementV2Fixture fixture,
            DateTime cutoff,
            AQGreenStructuralCompletionLevel level,
            int depthOneCount,
            int depthTwoCount,
            bool includeObservations)
        {
            var observations = includeObservations
                ? fixture.Placements.Select((placement, ordinal) =>
                    new AQGreenGraduationStructuralEvidenceObservation
                    {
                        CanonicalOrdinal = ordinal,
                        SourcePlacementId = placement.Id,
                        ParticipationStatusObserved = EntryParticipationStatus.Active,
                        ParticipationActivatedAtObserved =
                            fixture.Participations[ordinal].ActivatedAt,
                        ParticipationIsDeletedObserved = false,
                        CustomerIdObserved = fixture.Customers[ordinal].Id,
                        CustomerTenantMatchedObserved = true,
                        CustomerIsActiveObserved = true,
                        CustomerIsDeletedObserved = false,
                        UserIdObserved = fixture.Customers[ordinal].UserId,
                        UserTenantMatchedObserved = true,
                        UserIsActiveObserved = true,
                        UserIsDeletedObserved = false
                    }).ToList()
                : new List<AQGreenGraduationStructuralEvidenceObservation>();
            return new AQGreenGraduationStructuralEvidenceResult(
                fixture.RootParticipationId,
                fixture.Placements[0].PlacementTreeScopeId,
                cutoff,
                level,
                depthOneCount,
                depthTwoCount,
                AQGreenStructuralQualificationRules.CurrentVersion,
                observations);
        }

        private void ConfigureLevelTwoEvidence(PlacementV2Fixture fixture) =>
            _evaluator.CreateResult = cutoff => CreateEvidenceResult(
                fixture,
                cutoff,
                AQGreenStructuralCompletionLevel.Level2,
                5,
                25,
                includeObservations: true);

        private static IEnumerable<MemberPayment> Activate(
            EntryParticipation participation,
            int customerId,
            string reference)
        {
            var registration = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                "Test",
                $"{reference}-registration",
                EffectiveFrom);
            registration.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);
            var activation = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.EntryActivation,
                600m,
                "Test",
                $"{reference}-activation",
                EffectiveFrom);
            activation.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1, EffectiveFrom.AddMinutes(3));
            return new[] { registration, activation };
        }

        private static PostgresException PostgreSqlSerializationFailure() =>
            new(
                "simulated serialization failure",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.SerializationFailure,
                detail: null,
                hint: null,
                position: 0,
                internalPosition: 0,
                internalQuery: null,
                where: null,
                schemaName: "public",
                tableName: "OnyxGraduationDecisions",
                columnName: null,
                dataTypeName: null,
                constraintName: null,
                file: "test.c",
                line: "1",
                routine: "test");

        private sealed class TestSelector : IAQGreenGraduationStructuralModelSelector
        {
            public AQGreenGraduationStructuralModel Model { get; set; } =
                AQGreenGraduationStructuralModel.LegacyV1;

            public Task<AQGreenGraduationStructuralModel> SelectAsync(
                int tenantId,
                Guid entryParticipationId) => Task.FromResult(Model);
        }

        private sealed class TestEvidenceEvaluator
            : IAQGreenGraduationStructuralEvidenceEvaluator
        {
            public Func<DateTime, AQGreenGraduationStructuralEvidenceResult> CreateResult
                { get; set; }
            public Queue<Exception> Failures { get; } = new();
            public int CallCount { get; private set; }

            public Task<AQGreenGraduationStructuralEvidenceResult> EvaluateAsync(
                int tenantId,
                Guid participantId,
                DateTime cutoff,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                if (Failures.Count > 0) throw Failures.Dequeue();
                return Task.FromResult(CreateResult(cutoff));
            }
        }

        private sealed class TrackingCurrentProgrammeTermsProvider
            : ICurrentProgrammeTermsProvider
        {
            public OnyxPlanTerms DirectOnyxTerms { get; set; } = OnyxPlanTerms.Create(
                "current-catalog-initial",
                EffectiveFrom,
                6120m,
                "ZAR");
            public int DirectOnyxTermsCallCount { get; private set; }

            public EntryProgrammeTerms GetEntryTerms() => Terms;

            public OnyxPlanTerms GetDirectOnyxTerms()
            {
                DirectOnyxTermsCallCount++;
                return DirectOnyxTerms;
            }
        }

        private sealed class PlacementV2Fixture
        {
            public int RootCustomerId { get; init; }
            public Guid RootParticipationId { get; init; }
            public Guid LoanAgreementId { get; init; }
            public string LoanTermsVersion { get; init; }
            public IReadOnlyList<EntryParticipation> Participations { get; init; }
            public IReadOnlyList<Customer> Customers { get; init; }
            public IReadOnlyList<AQGreenNetworkPlacement> Placements { get; init; }
        }
    }
}

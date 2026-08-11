using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using Castle.Core.Logging;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Authorization.Roles;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Shouldly;
using Xunit;
using RolePermissionSetting = Abp.Authorization.Roles.RolePermissionSetting;
using UserRole = Abp.Authorization.Users.UserRole;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminCommissionAppServiceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime CommissionTermsBoundary =
            new DateTime(2026, 7, 16, 22, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms LegacySplitPaymentTerms =
            EntryProgrammeTerms.Create(
                "entry-2026-07",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

        private static readonly OnyxPlanTerms OnyxTerms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            6120m);

        private readonly IAdminCommissionAppService _service;

        public AdminCommissionAppServiceTests()
        {
            UsingDbContext(null, context =>
            {
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        1,
                        true,
                        EffectiveFrom,
                        EffectiveFrom,
                        null,
                        "Test cutoff Area baseline",
                        AreaActivationStateRecordKind.ObservedBaseline));
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "test-entry-commission-2026-07",
                        CommissionTermsBoundary,
                        150m,
                        250m,
                        1250m));
                context.OnyxCommissionTermsVersions.Add(
                    OnyxCommissionTermsVersion.Create(
                        "test-onyx-commission-2026-07",
                        CommissionTermsBoundary,
                        50m,
                        20m,
                        12.62m,
                        5m,
                        4m));
                context.SaveChanges();
            });
            _service = Resolve<IAdminCommissionAppService>();
        }

        [Fact]
        public async Task HostAdministrator_CanCalculateAndReviewEntryEarningsIdempotently()
        {
            await CreateQualifiedLevelOneEntryNetworkAsync();
            LoginAsHostAdmin();

            var input = new CalculateLatestClosedCommissionWeekInput
            {
                TenantId = 1,
                Programme = AdminCommissionProgramme.Entry
            };
            var firstCalculation =
                await _service.CalculateLatestClosedWeekAsync(input);
            var repeatedCalculation =
                await _service.CalculateLatestClosedWeekAsync(input);
            var review = await _service.GetAllAsync(
                new AdminCommissionListInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry,
                    MaxResultCount = 20
                });

            firstCalculation.WasAlreadyCalculated.ShouldBeFalse();
            firstCalculation.RecordsCreated.ShouldBe(6);
            firstCalculation.EarnedCount.ShouldBe(1);
            firstCalculation.TotalEarnedAmount.ShouldBe(150m);
            repeatedCalculation.WasAlreadyCalculated.ShouldBeTrue();
            repeatedCalculation.RecordsCreated.ShouldBe(0);
            repeatedCalculation.PeriodId.ShouldBe(firstCalculation.PeriodId);
            review.TotalCount.ShouldBe(6);

            var earnedCommission = review.Items.Single(item =>
                item.TotalAmount > 0m);
            earnedCommission.ProgrammeName.ShouldBe("AQGreen");
            earnedCommission.HighestQualifiedLevel.ShouldBe(1);
            earnedCommission.HighestCommissionedLevel.ShouldBe(1);
            earnedCommission.TotalAmount.ShouldBe(150m);
            earnedCommission.Currency.ShouldBe("ZAR");
            earnedCommission.Status.ShouldBe("Earned — awaiting release");
            earnedCommission.Components.Single().Level.ShouldBe(1);

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.AQGreen
                });
            var inventoryPeriod = inventory.Periods.Single();
            inventoryPeriod.CommissionCount.ShouldBe(6);
            inventoryPeriod.NotEarnedCount.ShouldBe(5);
            inventoryPeriod.EarnedCount.ShouldBe(1);
            inventoryPeriod.TotalAmount.ShouldBe(150m);
            inventoryPeriod.EarnedTotal.ShouldBe(150m);
            inventoryPeriod.DeletedCommissionCount.ShouldBe(0);

            var releaseService = (AdminCommissionAppService)_service;
            var logger = new Mock<ILogger>();
            releaseService.Logger = logger.Object;
            await _service.ReleaseAsync(new ReleaseWeeklyEarningInput
            {
                Id = earnedCommission.Id,
                Programme = AdminCommissionProgramme.Entry,
                Justification = "Approved after reviewing the weekly calculation."
            });
            await _service.ReleaseAsync(new ReleaseWeeklyEarningInput
            {
                Id = earnedCommission.Id,
                Programme = AdminCommissionProgramme.Entry,
                Justification = "Repeated request after reviewing the weekly calculation."
            });
            logger.Verify(
                item => item.Info(It.Is<string>(message =>
                    message.Contains("released for payment") &&
                    message.Contains("programme=AQGreen"))),
                Times.Once);

            var releasedReview = await _service.GetAllAsync(
                new AdminCommissionListInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry,
                    MaxResultCount = 20
                });
            var releasedCommission = releasedReview.Items.Single(item =>
                item.Id == earnedCommission.Id);
            releasedCommission.Status.ShouldBe("Released — awaiting payment");
            releasedCommission.ReleasedAt.ShouldNotBeNull();

            await _service.RecordPaymentAsync(
                new RecordWeeklyEarningPaymentInput
                {
                    Id = earnedCommission.Id,
                    Programme = AdminCommissionProgramme.Entry,
                    PaymentReference = "bank-payment-2026-07-entry-1",
                    Justification = "Recorded after confirming the external bank payment."
                });
            await _service.RecordPaymentAsync(
                new RecordWeeklyEarningPaymentInput
                {
                    Id = earnedCommission.Id,
                    Programme = AdminCommissionProgramme.Entry,
                    PaymentReference = "bank-payment-2026-07-entry-1",
                    Justification = "Repeated after confirming the external bank payment."
                });

            var paidReview = await _service.GetAllAsync(
                new AdminCommissionListInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry,
                    MaxResultCount = 20
                });
            var paidCommission = paidReview.Items.Single(item =>
                item.Id == earnedCommission.Id);
            paidCommission.Status.ShouldBe("Paid");
            paidCommission.PaymentReference.ShouldBe(
                "bank-payment-2026-07-entry-1");
            paidCommission.PaidAt.ShouldNotBeNull();
        }

        [Fact]
        public async Task HostCalculation_ExcludesCrossTenantFifthRecruitAndLedger()
        {
            var network = await CreateCrossTenantFifthRecruitEntryNetworkAsync();
            LoginAsHostAdmin();

            var result = await _service.CalculateLatestClosedWeekAsync(
                new CalculateLatestClosedCommissionWeekInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry
                });

            result.RecordsCreated.ShouldBe(5);
            result.EarnedCount.ShouldBe(0);
            result.TotalEarnedAmount.ShouldBe(0m);
            await UsingDbContextAsync(null, async context =>
            {
                var commissions = await context.EntryWeeklyCommissions
                    .IgnoreQueryFilters()
                    .Where(item => item.CommissionPeriodId == result.PeriodId)
                    .ToListAsync();
                commissions.Count.ShouldBe(5);
                commissions.All(item => item.TenantId == 1).ShouldBeTrue();
                commissions.ShouldNotContain(item =>
                    item.EntryParticipationId == network.NewRecruiterParticipationId);
                commissions.Single(item =>
                        item.EntryParticipationId ==
                        network.OriginalRecruiterParticipationId)
                    .HighestQualifiedNetworkLevel.ShouldBe(0);
            });
        }

        [Fact]
        public async Task TermsVersioning_ComposesWithCutoffEffectiveHolds()
        {
            var network = await CreateQualifiedLevelOneEntryNetworkAsync();
            LoginAsHostAdmin();
            var closedWeek = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var dueAt = closedWeek.PeriodStartUtc.AddDays(-5);
            await UsingDbContextAsync(1, async context =>
            {
                var root = context.EntryParticipations.Single(
                    participation =>
                        participation.Id == network.OriginalRecruiterParticipationId);
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "test-due-policy-v1",
                        1,
                        EntryMonthlyObligationDuePolicy
                            .JohannesburgMonthStartUtc(2026, 8)));
                context.EntryMonthlyObligations.Add(
                    EntryMonthlyObligation.Create(
                        root,
                        2026,
                        8,
                        dueAt,
                        "test-due-policy-v1"));
                await context.SaveChangesAsync();
            });

            var input = new CalculateLatestClosedCommissionWeekInput
            {
                TenantId = 1,
                Programme = AdminCommissionProgramme.Entry
            };
            var first = await _service.CalculateLatestClosedWeekAsync(input);
            first.WasAlreadyCalculated.ShouldBeFalse();
            first.HeldCount.ShouldBe(1);
            first.EarnedCount.ShouldBe(1);

            var cureTime = closedWeek.PeriodEndUtc.AddHours(2);
            await UsingDbContextAsync(1, async context =>
            {
                var root = await context.EntryParticipations.SingleAsync(
                    participation =>
                        participation.Id == network.OriginalRecruiterParticipationId);
                var payment = MemberPayment.CreatePending(
                    1,
                    root.CustomerId,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    600m,
                    "Yoco",
                    $"post-cutoff-cure-{Guid.NewGuid():N}",
                    cureTime);
                payment.Confirm(cureTime);
                var obligation = context.EntryMonthlyObligations.Single(
                    item =>
                        item.EntryParticipationId ==
                        network.OriginalRecruiterParticipationId);
                obligation.ApplyConfirmedPayment(payment);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });

            var laterBoundary = Resolve<LatestClosedCommissionWeekResolver>()
                .ResolveFirstCycleStartAfter(closedWeek.PeriodEndUtc)
                .AddDays(7);
            await UsingDbContextAsync(null, async context =>
            {
                context.EntryCommissionTermsVersions.Add(
                    EntryCommissionTermsVersion.Create(
                        "test-entry-commission-2026-08",
                        laterBoundary,
                        999m,
                        999m,
                        999m));
                await context.SaveChangesAsync();
            });

            var replayed = await _service.CalculateLatestClosedWeekAsync(input);
            replayed.WasAlreadyCalculated.ShouldBeTrue();
            replayed.RecordsCreated.ShouldBe(0);
            replayed.PeriodId.ShouldBe(first.PeriodId);

            await UsingDbContextAsync(1, async context =>
            {
                var period = await context.EntryCommissionPeriods.SingleAsync(
                    item => item.Id == replayed.PeriodId);
                period.RulesVersion.ShouldBe("test-entry-commission-2026-07");
                var commission = await context.EntryWeeklyCommissions.SingleAsync(
                    item =>
                        item.EntryParticipationId ==
                        network.OriginalRecruiterParticipationId);
                commission.PayoutStatus.ShouldBe(
                    WeeklyCommissionPayoutStatus.Held);
                commission.RulesVersion.ShouldBe("test-entry-commission-2026-07");
            });
        }

        [Fact]
        public async Task HostAdministrator_CanCalculateOnyxIdempotentlyWithoutTravelSideEffects()
        {
            await CreateQualifiedLevelOneOnyxNetworkAsync();
            LoginAsHostAdmin();

            var input = new CalculateLatestClosedCommissionWeekInput
            {
                TenantId = 1,
                Programme = AdminCommissionProgramme.Onyx
            };

            var first = await _service.CalculateLatestClosedWeekAsync(input);
            var repeated = await _service.CalculateLatestClosedWeekAsync(input);

            first.ProgrammeName.ShouldBe("Onyx");
            first.RecordsCreated.ShouldBe(6);
            first.EarnedCount.ShouldBe(1);
            first.TotalEarnedAmount.ShouldBe(250m);
            repeated.WasAlreadyCalculated.ShouldBeTrue();
            repeated.RecordsCreated.ShouldBe(0);
            repeated.PeriodId.ShouldBe(first.PeriodId);

            var review = await _service.GetAllAsync(new AdminCommissionListInput
            {
                TenantId = 1,
                Programme = AdminCommissionProgramme.Onyx,
                MaxResultCount = 20
            });
            var earned = review.Items.Single(item => item.TotalAmount > 0m);
            earned.Status.ShouldBe("Earned — awaiting release");
            earned.ReleasedAt.ShouldBeNull();
            earned.PaidAt.ShouldBeNull();
            earned.PaymentReference.ShouldBeNull();

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.Onyx
                });
            var inventoryPeriod = inventory.Periods.Single();
            inventoryPeriod.CommissionCount.ShouldBe(6);
            inventoryPeriod.NotEarnedCount.ShouldBe(5);
            inventoryPeriod.EarnedCount.ShouldBe(1);
            inventoryPeriod.TotalAmount.ShouldBe(250m);
            inventoryPeriod.EarnedTotal.ShouldBe(250m);

            var entitlementCount = await UsingDbContextAsync(1, async context =>
                await context.OnyxTravelBenefitEntitlements.CountAsync());
            entitlementCount.ShouldBe(0);
        }

        [Fact]
        public async Task EntryCalculation_UsesPlacementAtTheClosedCycleCutoff()
        {
            var network = await CreateQualifiedLevelOneEntryNetworkAsync(
                correctOneRecruitAfterCutoff: true);
            LoginAsHostAdmin();

            await _service.CalculateLatestClosedWeekAsync(
                new CalculateLatestClosedCommissionWeekInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry
                });

            await UsingDbContextAsync(1, async context =>
            {
                var commissions = await context.EntryWeeklyCommissions.ToListAsync();
                commissions.Single(item =>
                        item.EntryParticipationId == network.OriginalRecruiterParticipationId)
                    .TotalAmount.ShouldBe(150m);
                commissions.Single(item =>
                        item.EntryParticipationId == network.NewRecruiterParticipationId)
                    .TotalAmount.ShouldBe(0m);
            });
        }

        [Fact]
        public async Task OnyxCalculation_UsesPlacementAtTheClosedCycleCutoff()
        {
            var network = await CreateQualifiedLevelOneOnyxNetworkAsync(
                correctOneRecruitAfterCutoff: true);
            LoginAsHostAdmin();

            await _service.CalculateLatestClosedWeekAsync(
                new CalculateLatestClosedCommissionWeekInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Onyx
                });

            await UsingDbContextAsync(1, async context =>
            {
                var commissions = await context.OnyxWeeklyCommissions.ToListAsync();
                commissions.Single(item =>
                        item.OnyxParticipationId == network.OriginalRecruiterParticipationId)
                    .TotalAmount.ShouldBe(250m);
                commissions.Single(item =>
                        item.OnyxParticipationId == network.NewRecruiterParticipationId)
                    .TotalAmount.ShouldBe(0m);
            });
        }

        [Fact]
        public async Task HostReviewerWithoutAllAreas_CannotReviewOneArea()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userName = $"host-earnings-reviewer-{suffix}";
            var userId = await CreateTestUserAsync(
                null,
                userName,
                $"{userName}@example.com");
            await UsingDbContextAsync(null, async context =>
            {
                var role = new Role(
                    null,
                    $"EarningsReviewer-{suffix}",
                    $"Earnings Reviewer {suffix}");
                context.Roles.Add(role);
                await context.SaveChangesAsync();
                context.UserRoles.RemoveRange(
                    context.UserRoles.Where(item => item.UserId == userId));
                context.UserRoles.Add(new UserRole(null, userId, role.Id));
                context.Permissions.Add(new RolePermissionSetting
                {
                    TenantId = null,
                    Name = AquaPermissions.Admin.Commissions.View,
                    IsGranted = true,
                    RoleId = role.Id
                });
                await context.SaveChangesAsync();
            });
            LoginAsHost(userName);

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.GetAllAsync(new AdminCommissionListInput
                {
                    TenantId = 1,
                    Programme = AdminCommissionProgramme.Entry,
                    MaxResultCount = 20
                }));
        }

        [Fact]
        public async Task HostAdministrator_CanInventoryLegacyPeriodsWithoutMutation()
        {
            var resolver = Resolve<LatestClosedCommissionWeekResolver>();
            var latestClosed = resolver.Resolve(DateTime.UtcNow);
            var legacyStart = latestClosed.PeriodStartUtc.AddDays(-4);
            var legacyEnd = legacyStart.AddDays(7).AddTicks(-1);
            var terms = Resolve<ICurrentCommissionTermsProvider>().GetEntryTerms();
            await UsingDbContextAsync(1, async context =>
            {
                var legacyPeriod = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    legacyStart,
                    legacyEnd,
                    LatestClosedCommissionWeekResolver.CommissionTimeZoneId,
                    DateTime.UtcNow,
                    terms);
                legacyPeriod.IsDeleted = true;
                context.EntryCommissionPeriods.Add(legacyPeriod);
                await context.SaveChangesAsync();
            });
            var before = await UsingDbContextAsync(1, async context =>
                new
                {
                    Periods = await context.EntryCommissionPeriods
                        .IgnoreQueryFilters()
                        .CountAsync(),
                    Commissions = await context.EntryWeeklyCommissions.CountAsync()
                });
            LoginAsHostAdmin();

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.AQGreen
                });

            var period = inventory.Periods.Single();
            period.ProgrammeName.ShouldBe("AQGreen");
            period.Classification.ShouldBe(
                CommissionPeriodClassification.LegacyMondayToSunday);
            period.OverlapsFridayToThursdayCycle.ShouldBeTrue();
            period.CommissionCount.ShouldBe(0);
            period.IsDeleted.ShouldBeTrue();
            var boundary = inventory.ProgrammeBoundaries.Single();
            boundary.FirstNonOverlappingCycleStartUtc
                .ShouldBe(latestClosed.PeriodStartUtc.AddDays(7));
            var missingCycle = boundary.MissingCanonicalCycles.Single();
            missingCycle.CycleStartUtc.ShouldBe(latestClosed.PeriodStartUtc);
            missingCycle.IsLatestClosedCycle.ShouldBeTrue();
            missingCycle.Disposition.ShouldBe(
                MissingCommissionCycleDisposition.PendingCalculation);
            missingCycle.Message.ShouldContain("pending for the latest closed cycle");
            var after = await UsingDbContextAsync(1, async context =>
                new
                {
                    Periods = await context.EntryCommissionPeriods
                        .IgnoreQueryFilters()
                        .CountAsync(),
                    Commissions = await context.EntryWeeklyCommissions.CountAsync()
                });
            after.Periods.ShouldBe(before.Periods);
            after.Commissions.ShouldBe(before.Commissions);
        }

        [Fact]
        public async Task Inventory_ClassifiesOlderCanonicalGapsForManualReconciliation()
        {
            var latestClosed = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var terms = Resolve<ICurrentCommissionTermsProvider>().GetEntryTerms();
            await UsingDbContextAsync(1, async context =>
            {
                foreach (var periodStart in new[]
                {
                    latestClosed.PeriodStartUtc.AddDays(-14),
                    latestClosed.PeriodStartUtc
                })
                {
                    context.EntryCommissionPeriods.Add(
                        EntryCommissionPeriod.CreateClosedPeriod(
                            1,
                            periodStart,
                            periodStart.AddDays(7).AddTicks(-1),
                            LatestClosedCommissionWeekResolver.CommissionTimeZoneId,
                            DateTime.UtcNow,
                            terms));
                }

                await context.SaveChangesAsync();
            });
            LoginAsHostAdmin();

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.AQGreen
                });

            var missingCycle = inventory.ProgrammeBoundaries.Single()
                .MissingCanonicalCycles.Single();
            missingCycle.CycleStartUtc.ShouldBe(
                latestClosed.PeriodStartUtc.AddDays(-7));
            missingCycle.IsLatestClosedCycle.ShouldBeFalse();
            missingCycle.Disposition.ShouldBe(
                MissingCommissionCycleDisposition
                    .ManualFinancialReconciliationRequired);
            missingCycle.Message.ShouldContain("Historical calculation is unavailable");
        }

        [Fact]
        public async Task Inventory_ClassifiesLatestClosedMissingCycleAsPendingCalculation()
        {
            var latestClosed = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var terms = Resolve<ICurrentCommissionTermsProvider>().GetEntryTerms();
            await UsingDbContextAsync(1, async context =>
            {
                foreach (var periodStart in new[]
                {
                    latestClosed.PeriodStartUtc.AddDays(-14),
                    latestClosed.PeriodStartUtc.AddDays(-7)
                })
                {
                    context.EntryCommissionPeriods.Add(
                        EntryCommissionPeriod.CreateClosedPeriod(
                            1,
                            periodStart,
                            periodStart.AddDays(7).AddTicks(-1),
                            LatestClosedCommissionWeekResolver.CommissionTimeZoneId,
                            DateTime.UtcNow,
                            terms));
                }

                await context.SaveChangesAsync();
            });
            LoginAsHostAdmin();

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.AQGreen
                });

            var missingCycle = inventory.ProgrammeBoundaries.Single()
                .MissingCanonicalCycles.Single();
            missingCycle.CycleStartUtc.ShouldBe(latestClosed.PeriodStartUtc);
            missingCycle.IsLatestClosedCycle.ShouldBeTrue();
            missingCycle.Disposition.ShouldBe(
                MissingCommissionCycleDisposition.PendingCalculation);
            missingCycle.Message.ShouldContain("pending for the latest closed cycle");
        }

        [Fact]
        public async Task Inventory_DistinguishesOlderGapFromPendingLatestClosedCycle()
        {
            var latestClosed = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var terms = Resolve<ICurrentCommissionTermsProvider>().GetEntryTerms();
            await UsingDbContextAsync(1, async context =>
            {
                context.EntryCommissionPeriods.Add(
                    EntryCommissionPeriod.CreateClosedPeriod(
                        1,
                        latestClosed.PeriodStartUtc.AddDays(-14),
                        latestClosed.PeriodStartUtc.AddDays(-14).AddDays(7).AddTicks(-1),
                        LatestClosedCommissionWeekResolver.CommissionTimeZoneId,
                        DateTime.UtcNow,
                        terms));
                await context.SaveChangesAsync();
            });
            LoginAsHostAdmin();

            var inventory = await _service.GetPeriodInventoryAsync(
                new GetCommissionPeriodInventoryInput
                {
                    TenantId = 1,
                    Programme = CommissionInventoryProgramme.AQGreen
                });

            var missingCycles = inventory.ProgrammeBoundaries.Single()
                .MissingCanonicalCycles
                .OrderBy(cycle => cycle.CycleStartUtc)
                .ToList();
            missingCycles.Count.ShouldBe(2);
            missingCycles[0].CycleStartUtc.ShouldBe(
                latestClosed.PeriodStartUtc.AddDays(-7));
            missingCycles[0].IsLatestClosedCycle.ShouldBeFalse();
            missingCycles[0].Disposition.ShouldBe(
                MissingCommissionCycleDisposition
                    .ManualFinancialReconciliationRequired);
            missingCycles[1].CycleStartUtc.ShouldBe(latestClosed.PeriodStartUtc);
            missingCycles[1].IsLatestClosedCycle.ShouldBeTrue();
            missingCycles[1].Disposition.ShouldBe(
                MissingCommissionCycleDisposition.PendingCalculation);
        }

        [Fact]
        public async Task TenantAdministrator_CannotAccessPeriodInventory()
        {
            LoginAsDefaultTenantAdmin();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                _service.GetPeriodInventoryAsync(
                    new GetCommissionPeriodInventoryInput
                    {
                        TenantId = 1,
                        Programme = CommissionInventoryProgramme.Both
                    }));
        }

        private async Task<CommissionNetworkIds> CreateQualifiedLevelOneEntryNetworkAsync(
            bool correctOneRecruitAfterCutoff = false)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            var customerCount = correctOneRecruitAfterCutoff ? 7 : 6;
            for (var index = 0; index < customerCount; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"commission-{index}-{suffix}",
                    $"commission-{index}-{suffix}@example.com"));
            }

            var closedWeek = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var activatedAt = closedWeek.PeriodStartUtc.AddMinutes(1);
            var programmeTerms = LegacySplitPaymentTerms;

            return await UsingDbContextAsync(1, async context =>
            {
                var customers = userIds.Select((userId, index) =>
                    Customer.Create(
                        1,
                        userId,
                        $"Commission Club Member {index}",
                        new EmailAddress(
                            $"commission-{index}-{suffix}@example.com")))
                    .ToList();
                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();

                var root = EntryParticipation.StartIndependently(
                    1,
                    customers[0].Id,
                    programmeTerms,
                    activatedAt.AddMinutes(-1));
                Activate(root, programmeTerms, activatedAt, suffix, 0, context);
                context.EntryParticipations.Add(root);
                var directRecruits = new List<EntryParticipation>();

                for (var index = 1; index <= 5; index++)
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        programmeTerms,
                        activatedAt.AddMinutes(-1));
                    Activate(
                        recruit,
                        programmeTerms,
                        activatedAt,
                        suffix,
                        index,
                        context);
                    context.EntryParticipations.Add(recruit);
                    directRecruits.Add(recruit);
                }

                if (!correctOneRecruitAfterCutoff)
                {
                    return new CommissionNetworkIds(root.Id, Guid.Empty);
                }

                var newRecruiter = EntryParticipation.StartIndependently(
                    1,
                    customers[6].Id,
                    programmeTerms,
                    activatedAt.AddMinutes(-1));
                Activate(newRecruiter, programmeTerms, activatedAt, suffix, 6, context);
                context.EntryParticipations.Add(newRecruiter);
                directRecruits[0].CorrectRecruiter(
                    newRecruiter,
                    1,
                    "Correct placement after the closed-cycle cutoff.",
                    closedWeek.PeriodEndUtc.AddMinutes(1));
                return new CommissionNetworkIds(root.Id, newRecruiter.Id);
            });
        }

        private async Task<CommissionNetworkIds>
            CreateCrossTenantFifthRecruitEntryNetworkAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            await UsingDbContextAsync(null, async context =>
            {
                if (!await context.Tenants.AnyAsync(tenant => tenant.Id == 2))
                {
                    context.Tenants.Add(new Tenant("OtherTenant", "Other Tenant"));
                    await context.SaveChangesAsync();
                }
            });

            var tenantOneUserIds = new List<long>();
            for (var index = 0; index < 5; index++)
            {
                tenantOneUserIds.Add(await CreateTestUserAsync(
                    1,
                    $"tenant-one-{index}-{suffix}",
                    $"tenant-one-{index}-{suffix}@example.com"));
            }
            var tenantTwoUserId = await CreateTestUserAsync(
                2,
                $"tenant-two-fifth-{suffix}",
                $"tenant-two-fifth-{suffix}@example.com");
            var closedWeek = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var activatedAt = closedWeek.PeriodStartUtc.AddMinutes(1);

            var rootAndCustomerId = await UsingDbContextAsync(1, async context =>
            {
                var customers = tenantOneUserIds.Select((userId, index) =>
                    Customer.Create(
                        1,
                        userId,
                        $"Tenant One Member {index}",
                        new EmailAddress(
                            $"tenant-one-{index}-{suffix}@example.com")))
                    .ToList();
                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();

                var root = EntryParticipation.StartIndependently(
                    1,
                    customers[0].Id,
                    LegacySplitPaymentTerms,
                    activatedAt.AddMinutes(-1));
                Activate(
                    root,
                    LegacySplitPaymentTerms,
                    activatedAt,
                    suffix,
                    0,
                    context);
                context.EntryParticipations.Add(root);
                for (var index = 1; index < 5; index++)
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        LegacySplitPaymentTerms,
                        activatedAt.AddMinutes(-1));
                    Activate(
                        recruit,
                        LegacySplitPaymentTerms,
                        activatedAt,
                        suffix,
                        index,
                        context);
                    context.EntryParticipations.Add(recruit);
                }
                await context.SaveChangesAsync();
                return (root.Id, root.CustomerId);
            });

            var crossTenantParticipationId = await UsingDbContextAsync(
                2,
                async context =>
                {
                    var customer = Customer.Create(
                        2,
                        tenantTwoUserId,
                        "Tenant Two Fifth Recruit",
                        new EmailAddress(
                            $"tenant-two-fifth-{suffix}@example.com"));
                    context.Customers.Add(customer);
                    await context.SaveChangesAsync();
                    var participation = EntryParticipation.StartIndependently(
                        2,
                        customer.Id,
                        LegacySplitPaymentTerms,
                        activatedAt.AddMinutes(-1));
                    var recruiterProperty = typeof(EntryParticipation)
                        .GetProperty(nameof(EntryParticipation.RecruiterCustomerId));
                    recruiterProperty.ShouldNotBeNull();
                    recruiterProperty.SetValue(
                        participation,
                        rootAndCustomerId.CustomerId);
                    Activate(
                        participation,
                        LegacySplitPaymentTerms,
                        activatedAt,
                        suffix,
                        5,
                        context);
                    context.EntryParticipations.Add(participation);
                    await context.SaveChangesAsync();
                    return participation.Id;
                });

            return new CommissionNetworkIds(
                rootAndCustomerId.Id,
                crossTenantParticipationId);
        }

        private async Task<CommissionNetworkIds> CreateQualifiedLevelOneOnyxNetworkAsync(
            bool correctOneRecruitAfterCutoff = false)
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            var customerCount = correctOneRecruitAfterCutoff ? 7 : 6;
            for (var index = 0; index < customerCount; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"onyx-commission-{index}-{suffix}",
                    $"onyx-commission-{index}-{suffix}@example.com"));
            }

            var closedWeek = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var activatedAt = closedWeek.PeriodStartUtc.AddMinutes(1);

            return await UsingDbContextAsync(1, async context =>
            {
                var customers = userIds.Select((userId, index) =>
                    Customer.Create(
                        1,
                        userId,
                        $"Onyx Commission Club Member {index}",
                        new EmailAddress(
                            $"onyx-commission-{index}-{suffix}@example.com")))
                    .ToList();
                context.Customers.AddRange(customers);
                await context.SaveChangesAsync();

                var root = OnyxParticipation.StartDirectIndependently(
                    1,
                    customers[0].Id,
                    1,
                    OnyxTerms,
                    activatedAt.AddMinutes(-1));
                ActivateOnyx(root, activatedAt, suffix, 0, context);
                context.OnyxParticipations.Add(root);
                var directRecruits = new List<OnyxParticipation>();

                for (var index = 1; index <= 5; index++)
                {
                    var recruit = OnyxParticipation.StartDirectUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        1,
                        OnyxTerms,
                        activatedAt.AddMinutes(-1));
                    ActivateOnyx(recruit, activatedAt, suffix, index, context);
                    context.OnyxParticipations.Add(recruit);
                    directRecruits.Add(recruit);
                }

                if (!correctOneRecruitAfterCutoff)
                {
                    return new CommissionNetworkIds(root.Id, Guid.Empty);
                }

                var newRecruiter = OnyxParticipation.StartDirectIndependently(
                    1,
                    customers[6].Id,
                    1,
                    OnyxTerms,
                    activatedAt.AddMinutes(-1));
                ActivateOnyx(newRecruiter, activatedAt, suffix, 6, context);
                context.OnyxParticipations.Add(newRecruiter);
                directRecruits[0].CorrectRecruiter(
                    newRecruiter,
                    1,
                    "Correct placement after the closed-cycle cutoff.",
                    closedWeek.PeriodEndUtc.AddMinutes(1));
                return new CommissionNetworkIds(root.Id, newRecruiter.Id);
            });
        }

        private static void ActivateOnyx(
            OnyxParticipation participation,
            DateTime confirmedAt,
            string suffix,
            int index,
            AqualLifeStyleDbContext context)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                OnyxTerms.DirectEntryAmount,
                "Test",
                $"onyx-commission-{index}-{suffix}",
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            participation.ApplyConfirmedDirectEntryPayment(payment);
            participation.ApproveByAdministrator(1L, confirmedAt);
            context.MemberPayments.Add(payment);
        }

        private static void Activate(
            EntryParticipation participation,
            EntryProgrammeTerms terms,
            DateTime confirmedAt,
            string suffix,
            int index,
            AqualLifeStyleDbContext context)
        {
            var registration = ConfirmPayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                terms.RegistrationPaymentAmount,
                confirmedAt,
                $"commission-registration-{index}-{suffix}");
            var activation = ConfirmPayment(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                terms.ActivationPaymentAmount,
                confirmedAt,
                $"commission-activation-{index}-{suffix}");
            participation.ApplyConfirmedActivationPayment(registration);
            participation.ApplyConfirmedActivationPayment(activation);
            participation.ApproveByAdministrator(1L, confirmedAt);
            context.MemberPayments.AddRange(registration, activation);
        }

        private static MemberPayment ConfirmPayment(
            int tenantId,
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            DateTime confirmedAt,
            string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                amount,
                "Test",
                externalReference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }

        private sealed class CommissionNetworkIds
        {
            public CommissionNetworkIds(
                Guid originalRecruiterParticipationId,
                Guid newRecruiterParticipationId)
            {
                OriginalRecruiterParticipationId = originalRecruiterParticipationId;
                NewRecruiterParticipationId = newRecruiterParticipationId;
            }

            public Guid OriginalRecruiterParticipationId { get; }
            public Guid NewRecruiterParticipationId { get; }
        }
    }
}

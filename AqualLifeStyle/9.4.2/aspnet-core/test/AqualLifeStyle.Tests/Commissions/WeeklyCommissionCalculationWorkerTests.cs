using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.EntityFrameworkCore;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Web.Host.Commissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Commissions
{
    public class WeeklyCommissionCalculationWorkerTests
        : AqualLifeStyleTestBase
    {
        private sealed class TestableWeeklyCommissionCalculationWorker
            : WeeklyCommissionCalculationWorker
        {
            public TestableWeeklyCommissionCalculationWorker(
                AbpAsyncTimer timer,
                IConfiguration configuration,
                IUnitOfWorkManager unitOfWorkManager,
                IRepository<Tenant, int> tenantRepository,
                LatestClosedCommissionWeekResolver closedWeekResolver,
                IWeeklyCommissionCalculator commissionCalculator,
                IOnyxTravelBenefitSynchronizer travelBenefitSynchronizer,
                IAreaActivationStateResolver areaActivationStateResolver,
                IWeeklyCommissionCalculationLock calculationLock,
                Microsoft.Extensions.Logging.ILogger<WeeklyCommissionCalculationWorker> logger)
                : base(timer, configuration, unitOfWorkManager, tenantRepository,
                    closedWeekResolver, commissionCalculator,
                    travelBenefitSynchronizer, areaActivationStateResolver,
                    calculationLock, logger)
            {
            }

            public Task RunOnceAsync() => DoWorkAsync();
            public bool RunsOnStart => Timer.RunOnStart;
        }

        private static IConfiguration BuildConfiguration(bool enabled)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        "App:WeeklyCommissions:Enabled",
                        enabled ? "true" : "false"),
                    new KeyValuePair<string, string>(
                        "App:WeeklyCommissions:IntervalMinutes",
                        "1440")
                })
                .Build();
        }

        private static CommissionCalculationResultDto BuildResult(
            bool wasAlreadyCalculated,
            int recordsCreated = 0)
        {
            return new CommissionCalculationResultDto
            {
                WasAlreadyCalculated = wasAlreadyCalculated,
                RulesVersion = "worker-test-rules",
                EvaluatedCount = recordsCreated,
                RecordsCreated = recordsCreated,
                NotEarnedCount = recordsCreated,
                PeriodStart = new DateTime(2026, 8, 13, 22, 0, 0, DateTimeKind.Utc),
                PeriodEnd = new DateTime(2026, 8, 20, 21, 59, 59, DateTimeKind.Utc)
            };
        }

        [Fact]
        public async Task DisabledByDefault_DoesNotCalculateAnyTenant()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var travelSynchronizer = new Mock<IOnyxTravelBenefitSynchronizer>();
            var worker = CreateWorker(
                BuildConfiguration(false),
                calculator.Object,
                travelSynchronizer.Object);

            await worker.RunOnceAsync();

            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Never);
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Never);
            travelSynchronizer.Verify(
                service => service.SynchronizeAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public void Worker_RunsImmediatelyAfterApplicationStartup()
        {
            var worker = CreateWorker(
                BuildConfiguration(true),
                new Mock<IWeeklyCommissionCalculator>().Object);

            worker.RunsOnStart.ShouldBeTrue();
        }

        [Fact]
        public async Task Enabled_CalculatesLatestClosedWeekForAllActiveTenants()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            ClosedCommissionWeek aqGreenWeek = null;
            ClosedCommissionWeek onyxWeek = null;
            ClosedCommissionWeek travelWeek = null;
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .Callback<int, ClosedCommissionWeek, DateTime>((_, week, __) =>
                    aqGreenWeek = week)
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .Callback<int, ClosedCommissionWeek, DateTime>((_, week, __) =>
                    onyxWeek = week)
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));
            var travelSynchronizer = new Mock<IOnyxTravelBenefitSynchronizer>();
            travelSynchronizer
                .Setup(service => service.SynchronizeAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .Callback<int, ClosedCommissionWeek, DateTime>((_, week, __) =>
                    travelWeek = week)
                .ReturnsAsync(new OnyxTravelBenefitEligibilityResult(0, 0));

            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                travelSynchronizer.Object);

            await worker.RunOnceAsync();

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            aqGreenWeek.ShouldBeSameAs(onyxWeek);
            aqGreenWeek.ShouldBeSameAs(travelWeek);
            aqGreenWeek.TimeZoneId.ShouldBe("Africa/Johannesburg");
            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));

            foreach (var tenantId in activeTenantIds)
            {
                calculator.Verify(
                    service => service.CalculateEntryAsync(
                        tenantId,
                        It.IsAny<ClosedCommissionWeek>(),
                        It.IsAny<DateTime>()),
                    Times.Once);
                calculator.Verify(
                    service => service.CalculateOnyxAsync(
                        tenantId,
                        It.IsAny<ClosedCommissionWeek>(),
                        It.IsAny<DateTime>()),
                    Times.Once);

                calculator.Verify(
                    service => service.CalculateEntryAsync(
                        tenantId,
                        It.Is<ClosedCommissionWeek>(week => week.PeriodEndUtc < DateTime.UtcNow),
                        It.IsAny<DateTime>()),
                    Times.Once);
            }
        }

        [Fact]
        public async Task Enabled_UsesCutoffAreaStateInsteadOfCurrentTenantState()
        {
            var tenant = await CreateTenantAsync(
                "inactive-now-active-at-cutoff",
                isActive: false);
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false));

            var worker = CreateWorker(BuildConfiguration(true), calculator.Object);
            await worker.RunOnceAsync();

            calculator.Verify(
                service => service.CalculateEntryAsync(
                    tenant.Id,
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Once);
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    tenant.Id,
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Once);
        }

        [Theory]
        [InlineData(AreaActivationStateResolutionStatus.Unknown)]
        [InlineData(AreaActivationStateResolutionStatus.Inactive)]
        public async Task Enabled_DoesNotProcessAnAreaThatIsNotActiveAtCutoff(
            AreaActivationStateResolutionStatus status)
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var travelSynchronizer = new Mock<IOnyxTravelBenefitSynchronizer>();
            var areaStateResolver = new Mock<IAreaActivationStateResolver>();
            var resolution = status == AreaActivationStateResolutionStatus.Unknown
                ? AreaActivationStateResolution.Unknown()
                : AreaActivationStateResolution.Resolved(false, DateTime.UtcNow);
            areaStateResolver
                .Setup(service => service.ResolveAsync(
                    It.IsAny<int>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(resolution);
            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                travelSynchronizer.Object,
                areaStateResolver.Object);

            await worker.RunOnceAsync();

            calculator.VerifyNoOtherCalls();
            travelSynchronizer.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task IsIdempotentForAlreadyCalculatedTenantWeek()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: true, recordsCreated: 0));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: true, recordsCreated: 0));

            var worker = CreateWorker(BuildConfiguration(true), calculator.Object);
            await worker.RunOnceAsync();
            await worker.RunOnceAsync();

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length * 2));
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length * 2));
        }

        [Fact]
        public async Task IsolatesPerTenantFailures_OtherTenantsStillCalculated()
        {
            var secondTenant = await CreateActiveTenantAsync("second-isolation-tenant");

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            activeTenantIds.Length.ShouldBeGreaterThanOrEqualTo(2);

            var failingTenantId = activeTenantIds.First(id => id == secondTenant.Id);
            var healthyTenantIds = activeTenantIds.Except(new[] { failingTenantId }).ToArray();

            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    failingTenantId,
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("tenant-exploded"));
            foreach (var tenantId in healthyTenantIds)
            {
                calculator
                    .Setup(service => service.CalculateEntryAsync(
                        tenantId,
                        It.IsAny<ClosedCommissionWeek>(),
                        It.IsAny<DateTime>()))
                    .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));
            }

            var worker = CreateWorker(BuildConfiguration(true), calculator.Object);

            await worker.RunOnceAsync();

            foreach (var tenantId in healthyTenantIds)
            {
                calculator.Verify(
                    service => service.CalculateEntryAsync(
                        tenantId,
                        It.IsAny<ClosedCommissionWeek>(),
                        It.IsAny<DateTime>()),
                    Times.Once);
            }
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    failingTenantId,
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Once);
        }

        [Fact]
        public async Task FailedProgramme_EmitsCalculationFailedEvent_AndSummaryStillReports()
        {
            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("programme-exploded"));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 0));
            var logger = new Payments.TestLogger<WeeklyCommissionCalculationWorker>();
            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                suppliedLogger: logger);

            await worker.RunOnceAsync();

            logger.Entries.ShouldContain(entry =>
                entry.Level == Microsoft.Extensions.Logging.LogLevel.Error &&
                entry.Message.Contains("weekly_commission_calculation_failed") &&
                entry.Message.Contains("programme=AQGreen"));
            logger.Entries.ShouldContain(entry =>
                entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                entry.Message.Contains("weekly_commission_calculation_run") &&
                entry.Message.Contains($"failed={activeTenantIds.Length}"));
            logger.Entries.ShouldAllBe(entry =>
                !entry.Message.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                !entry.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task OnyxFailure_DoesNotSuppressAQGreenOrTravelSynchronization()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("onyx-exploded"));
            var travelSynchronizer = new Mock<IOnyxTravelBenefitSynchronizer>();
            travelSynchronizer
                .Setup(service => service.SynchronizeAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(new OnyxTravelBenefitEligibilityResult(0, 0));

            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                travelSynchronizer.Object);
            await worker.RunOnceAsync();

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));
            travelSynchronizer.Verify(
                service => service.SynchronizeAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));
        }

        [Fact]
        public async Task TravelFailure_DoesNotSuppressEitherCommissionProgramme()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false));
            var travelSynchronizer = new Mock<IOnyxTravelBenefitSynchronizer>();
            travelSynchronizer
                .Setup(service => service.SynchronizeAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ThrowsAsync(new InvalidOperationException("travel-exploded"));

            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                travelSynchronizer.Object);
            await worker.RunOnceAsync();

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));
            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Exactly(activeTenantIds.Length));
        }

        [Fact]
        public async Task SuccessfulRun_EmitsStructuredProgrammeAndSummaryEvidence()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(false, 3));
            calculator
                .Setup(service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(true, 0));
            var logger = new Payments.TestLogger<WeeklyCommissionCalculationWorker>();
            var worker = CreateWorker(
                BuildConfiguration(true),
                calculator.Object,
                suppliedLogger: logger);

            await worker.RunOnceAsync();

            logger.Entries.ShouldContain(entry =>
                entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                entry.Message.Contains("weekly_commission_programme_completed") &&
                entry.Message.Contains("rulesVersion=worker-test-rules") &&
                entry.Message.Contains("evaluated=3") &&
                entry.Message.Contains("created=3"));
            logger.Entries.ShouldContain(entry =>
                entry.Level == Microsoft.Extensions.Logging.LogLevel.Information &&
                entry.Message.Contains("weekly_commission_calculation_run") &&
                entry.Message.Contains("durationMs="));
            logger.Entries.ShouldAllBe(entry =>
                !entry.Message.Contains("password", StringComparison.OrdinalIgnoreCase) &&
                !entry.Message.Contains("token", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void LockKey_IsDistinctFromObligationLockKey()
        {
            WeeklyCommissionCalculationLock.LockKey
                .ShouldBe(0x41514757434F4D50);
            EntryMonthlyObligationSchedulingLock.LockKey
                .ShouldBe(0x415147524F424C);
        }

        private TestableWeeklyCommissionCalculationWorker CreateWorker(
            IConfiguration configuration,
            IWeeklyCommissionCalculator commissionCalculator,
            IOnyxTravelBenefitSynchronizer travelBenefitSynchronizer = null,
            IAreaActivationStateResolver suppliedAreaStateResolver = null,
            Payments.TestLogger<WeeklyCommissionCalculationWorker> suppliedLogger = null)
        {
            var timer = new AbpAsyncTimer();
            var lockMock = new Mock<IWeeklyCommissionCalculationLock>();
            lockMock.Setup(service => service.AcquireAsync())
                .Returns(Task.CompletedTask);
            var logger = suppliedLogger ??
                new Payments.TestLogger<WeeklyCommissionCalculationWorker>();
            if (suppliedAreaStateResolver == null)
            {
                var areaStateResolver = new Mock<IAreaActivationStateResolver>();
                areaStateResolver
                    .Setup(service => service.ResolveAsync(
                        It.IsAny<int>(),
                        It.IsAny<DateTime>()))
                    .ReturnsAsync((int _, DateTime cutoff) =>
                        AreaActivationStateResolution.Resolved(true, cutoff));
                suppliedAreaStateResolver = areaStateResolver.Object;
            }
            if (travelBenefitSynchronizer == null)
            {
                var travelMock = new Mock<IOnyxTravelBenefitSynchronizer>();
                travelMock
                    .Setup(service => service.SynchronizeAsync(
                        It.IsAny<int>(),
                        It.IsAny<ClosedCommissionWeek>(),
                        It.IsAny<DateTime>()))
                    .ReturnsAsync(new OnyxTravelBenefitEligibilityResult(0, 0));
                travelBenefitSynchronizer = travelMock.Object;
            }

            return new TestableWeeklyCommissionCalculationWorker(
                timer,
                configuration,
                Resolve<IUnitOfWorkManager>(),
                Resolve<IRepository<Tenant, int>>(),
                Resolve<LatestClosedCommissionWeekResolver>(),
                commissionCalculator,
                travelBenefitSynchronizer,
                suppliedAreaStateResolver,
                lockMock.Object,
                logger);
        }

        private async Task<int[]> ResolveActiveTenantIdsAsync()
        {
            var activeTenantIds = await UsingDbContextAsync(
                (int?)null,
                async context =>
                    await context.Tenants
                        .IgnoreQueryFilters()
                        .Where(tenant => tenant.IsActive)
                        .Select(tenant => tenant.Id)
                        .ToListAsync());
            return activeTenantIds.ToArray();
        }

        private async Task<Tenant> CreateActiveTenantAsync(string tenancyName)
        {
            return await CreateTenantAsync(tenancyName, isActive: true);
        }

        private async Task<Tenant> CreateTenantAsync(
            string tenancyName,
            bool isActive)
        {
            var tenant = new Tenant(tenancyName, tenancyName)
            {
                IsActive = isActive
            };
            await UsingDbContextAsync((int?)null, async context =>
            {
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
            });
            return tenant;
        }

    }
}

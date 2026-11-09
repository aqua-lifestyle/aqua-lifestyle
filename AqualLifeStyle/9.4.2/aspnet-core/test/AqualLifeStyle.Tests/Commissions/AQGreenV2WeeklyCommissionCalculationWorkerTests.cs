using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Configuration;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Web.Host.Commissions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Commissions
{
    public sealed class AQGreenV2WeeklyCommissionCalculationWorkerTests : AqualLifeStyleTestBase
    {
        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private sealed class TestableWorker : AQGreenV2WeeklyCommissionCalculationWorker
        {
            public TestableWorker(
                AbpAsyncTimer timer,
                IConfiguration configuration,
                IUnitOfWorkManager uows,
                LatestClosedCommissionWeekResolver resolver,
                IWeeklyCommissionCalculator calculator,
                IWeeklyCommissionCalculationLock calculationLock,
                IYocoCutoverStore cutover,
                IAQGreenV2WeeklyCommissionReadiness readiness)
                : base(timer, configuration, uows, resolver, calculator, calculationLock, cutover,
                    readiness, NullLogger<AQGreenV2WeeklyCommissionCalculationWorker>.Instance) { }

            public Task RunOnceAsync() => DoWorkAsync();
            public bool RunsOnStart => Timer.RunOnStart;
        }

        private static IConfiguration Configuration(bool enabled = true) =>
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string>
            {
                ["App:Recovery:Enabled"] = "false",
                ["App:AQGreenV2:Commissioning:Enabled"] = "true",
                ["App:AQGreenV2:Commissioning:AdmissionMode"] = "CLEAN_START",
                ["App:WeeklyCommissions:Enabled"] = enabled ? "true" : "false",
                ["App:BackgroundJobs:Enabled"] = "false",
                ["App:FinancialOperations:Enabled"] = "true",
                ["App:EntryMonthlyObligations:Enabled"] = "false",
                ["App:AutomaticPayouts:Enabled"] = "false",
                ["AQGreenV2Demo:Enabled"] = "false",
                ["AQGreenV2Pilot:Enabled"] = "false",
                ["SEED_DEMO_DATA"] = "false",
                ["Yoco:Mode"] = "live",
                ["Bird:Enabled"] = "true",
                [YocoCutoverOptions.Prefix + "Enabled"] = "true",
                [YocoCutoverOptions.Prefix + "OperatorUserId"] = "7",
                ["App:WeeklyCommissions:IntervalMinutes"] = "1440"
            }).Build();

        private TestableWorker CreateWorker(
            IConfiguration configuration,
            Mock<IWeeklyCommissionCalculator> calculator,
            Mock<IYocoCutoverStore> cutover = null,
            Mock<IAQGreenV2WeeklyCommissionReadiness> readiness = null,
            Mock<IWeeklyCommissionCalculationLock> calculationLock = null)
        {
            if (cutover == null)
            {
                cutover = new Mock<IYocoCutoverStore>();
                cutover.Setup(x => x.EnterBusinessAsync()).ReturnsAsync(new Lease());
            }
            if (readiness == null)
            {
                readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
                readiness.Setup(x => x.RequireEligibleDecisionAsync()).ReturnsAsync(new AQGreenD10Decision
                {
                    TenantId = 1,
                    V2EffectiveAtUtc = DateTime.UtcNow.AddDays(-14)
                });
            }
            calculationLock ??= new Mock<IWeeklyCommissionCalculationLock>();
            calculationLock.Setup(x => x.AcquireAsync()).Returns(Task.CompletedTask);

            return new TestableWorker(new AbpAsyncTimer(), configuration, Resolve<IUnitOfWorkManager>(),
                Resolve<LatestClosedCommissionWeekResolver>(), calculator.Object, calculationLock.Object,
                cutover.Object, readiness.Object);
        }

        private static CommissionCalculationResultDto Result(bool alreadyCalculated = false, int created = 1) =>
            new()
            {
                WasAlreadyCalculated = alreadyCalculated,
                EvaluatedCount = created,
                RecordsCreated = created,
                HeldCount = 0
            };

        [Fact]
        public async Task RunsOnStartAndCalculatesOnlyAQGreenWithGlobalJobsHeld()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator.Setup(x => x.CalculateEntryAsync(1, It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Result());
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            readiness.Setup(x => x.RequireEligibleDecisionAsync()).ReturnsAsync(new AQGreenD10Decision
                { TenantId = 1, V2EffectiveAtUtc = DateTime.UtcNow.AddDays(-14) });
            var worker = CreateWorker(Configuration(), calculator, readiness: readiness);

            worker.RunsOnStart.ShouldBeTrue();
            RuntimeOperationGuards.CanRunBackgroundWork(Configuration()).ShouldBeFalse();
            await worker.RunOnceAsync();

            calculator.Verify(x => x.CalculateEntryAsync(1, It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()), Times.Once);
            calculator.Verify(x => x.CalculateOnyxAsync(It.IsAny<int>(), It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()), Times.Never);
        }

        [Fact]
        public async Task DisabledOptInDoesNotAcquireBusinessLeaseOrCalculate()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var cutover = new Mock<IYocoCutoverStore>();
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            var worker = CreateWorker(Configuration(enabled: false), calculator, cutover, readiness);

            await worker.RunOnceAsync();

            cutover.Verify(x => x.EnterBusinessAsync(), Times.Never);
            readiness.Verify(x => x.RequireEligibleDecisionAsync(), Times.Never);
            calculator.VerifyNoOtherCalls();
        }

        [Theory]
        [InlineData("App:BackgroundJobs:Enabled", "true")]
        [InlineData("App:EntryMonthlyObligations:Enabled", "true")]
        [InlineData("App:AutomaticPayouts:Enabled", "true")]
        [InlineData("App:FinancialOperations:Enabled", "false")]
        [InlineData("AQGreenV2Demo:Enabled", "true")]
        [InlineData("AQGreenV2Pilot:Enabled", "true")]
        [InlineData("SEED_DEMO_DATA", "true")]
        public async Task AnyRequiredHoldOrFinancialGateMismatchPreventsCalculation(string key, string value)
        {
            var config = (IConfigurationRoot)Configuration();
            config[key] = value;
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var cutover = new Mock<IYocoCutoverStore>();
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            var worker = CreateWorker(config, calculator, cutover, readiness);

            await worker.RunOnceAsync();

            cutover.Verify(x => x.EnterBusinessAsync(), Times.Never);
            readiness.Verify(x => x.RequireEligibleDecisionAsync(), Times.Never);
            calculator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task HeldOrUncommittedNormalCannotReachTheCalculator()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var cutover = new Mock<IYocoCutoverStore>();
            cutover.Setup(x => x.EnterBusinessAsync())
                .ThrowsAsync(new InvalidOperationException("YOCO_CUTOVER_BUSINESS_WRITES_FENCED"));
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            var worker = CreateWorker(Configuration(), calculator, cutover, readiness);

            await worker.RunOnceAsync();

            readiness.Verify(x => x.RequireEligibleDecisionAsync(), Times.Never);
            calculator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExpiredUnusedOrInvalidD10ReadinessCannotReachTheCalculator()
        {
            foreach (var error in new[] { "D10_AUTHORITY_EXPIRED", "D10_COMMITTED_ACTIVATION_INVALID" })
            {
                var calculator = new Mock<IWeeklyCommissionCalculator>();
                var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
                readiness.Setup(x => x.RequireEligibleDecisionAsync())
                    .ThrowsAsync(new InvalidOperationException(error));
                var worker = CreateWorker(Configuration(), calculator, readiness: readiness);

                await worker.RunOnceAsync();

                calculator.VerifyNoOtherCalls();
            }
        }

        [Fact]
        public async Task ClosedWeekBeforeD10EffectiveBoundaryIsDeferred()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            readiness.Setup(x => x.RequireEligibleDecisionAsync()).ReturnsAsync(new AQGreenD10Decision
            {
                TenantId = 1,
                V2EffectiveAtUtc = DateTime.UtcNow.AddDays(14)
            });
            var worker = CreateWorker(Configuration(), calculator, readiness: readiness);

            await worker.RunOnceAsync();

            calculator.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task FirstEligibleClosedWeekIsCalculatedAtTheD10EffectiveCutoff()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator.Setup(x => x.CalculateEntryAsync(1, It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()))
                .ReturnsAsync(Result());
            var resolver = Resolve<LatestClosedCommissionWeekResolver>();
            var closedWeek = resolver.Resolve(DateTime.UtcNow);
            var readiness = new Mock<IAQGreenV2WeeklyCommissionReadiness>();
            readiness.Setup(x => x.RequireEligibleDecisionAsync()).ReturnsAsync(new AQGreenD10Decision
            {
                TenantId = 1,
                V2EffectiveAtUtc = closedWeek.PeriodEndUtc
            });
            var worker = CreateWorker(Configuration(), calculator, readiness: readiness);

            await worker.RunOnceAsync();

            calculator.Verify(x => x.CalculateEntryAsync(1,
                It.Is<ClosedCommissionWeek>(week => week.PeriodEndUtc == closedWeek.PeriodEndUtc),
                It.IsAny<DateTime>()), Times.Once);
        }

        [Fact]
        public async Task FailedCalculationRetriesSameClosedWeekAcrossWorkerRestart()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            ClosedCommissionWeek firstWeek = null;
            ClosedCommissionWeek secondWeek = null;
            var attempts = 0;
            calculator.Setup(x => x.CalculateEntryAsync(1,
                    It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()))
                .Returns((int _, ClosedCommissionWeek week, DateTime __) =>
                {
                    if (attempts == 0) firstWeek = week;
                    else secondWeek = week;
                    return ++attempts == 1
                        ? Task.FromException<CommissionCalculationResultDto>(new InvalidOperationException("synthetic-calculation-failure"))
                        : Task.FromResult(Result(alreadyCalculated: true, created: 0));
                });
            var firstWorker = CreateWorker(Configuration(), calculator);
            var restartedWorker = CreateWorker(Configuration(), calculator);

            await firstWorker.RunOnceAsync();
            await restartedWorker.RunOnceAsync();

            calculator.Verify(x => x.CalculateEntryAsync(1, It.IsAny<ClosedCommissionWeek>(), It.IsAny<DateTime>()), Times.Exactly(2));
            firstWeek.ShouldNotBeNull();
            secondWeek.ShouldNotBeNull();
            secondWeek.PeriodStartUtc.ShouldBe(firstWeek.PeriodStartUtc);
            secondWeek.PeriodEndUtc.ShouldBe(firstWeek.PeriodEndUtc);
            firstWeek.PeriodEndUtc.ShouldBeLessThan(DateTime.UtcNow);
        }
    }
}

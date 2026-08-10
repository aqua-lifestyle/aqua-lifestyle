using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Domain.Uow;
using Abp.Threading.Timers;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Web.Host.ProgrammeEngine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.ProgrammeEngine
{
    public class EntryMonthlyObligationWorkerTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime FixedNowUtc =
            new(2026, 7, 31, 22, 30, 0, DateTimeKind.Utc);

        private sealed class TestableEntryMonthlyObligationWorker
            : EntryMonthlyObligationWorker
        {
            public TestableEntryMonthlyObligationWorker(
                AbpAsyncTimer timer,
                IConfiguration configuration,
                IUnitOfWorkManager unitOfWorkManager,
                IEntryMonthlyObligationSchedulingLock schedulingLock,
                IEntryMonthlyObligationDueDatePolicy dueDatePolicy,
                IEntryMonthlyObligationScheduler scheduler,
                ILogger<EntryMonthlyObligationWorker> logger)
                : base(
                    timer,
                    configuration,
                    unitOfWorkManager,
                    schedulingLock,
                    dueDatePolicy,
                    scheduler,
                    logger)
            {
            }

            public Task RunOnceAsync() => DoWorkAsync();

            protected override DateTime GetUtcNow() => FixedNowUtc;
        }

        [Fact]
        public async Task MissingPolicy_WarnsAndDoesNotCreateAnObligation()
        {
            var policy = new Mock<IEntryMonthlyObligationDueDatePolicy>();
            policy.Setup(service => service.ResolveDueDateAsync(2026, 8))
                .ReturnsAsync(EntryMonthlyObligationDueDateResolution.Failed(
                    EntryMonthlyObligationDueDateResolutionStatus.Missing));
            var scheduler = CreateScheduler();
            var logger = new Payments.TestLogger<EntryMonthlyObligationWorker>();
            var worker = CreateWorker(policy.Object, scheduler.Object, logger);

            await worker.RunOnceAsync();

            scheduler.Verify(service => service.EnsureObligationsForPeriodAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<DateTime>(),
                It.IsAny<string>()), Times.Never);
            logger.Entries.ShouldContain(entry =>
                entry.Level == LogLevel.Warning &&
                entry.Message.Contains("Missing", StringComparison.Ordinal));
        }

        [Fact]
        public async Task ResolvedPolicy_UsesCurrentJohannesburgMonth()
        {
            var dueAtUtc = new DateTime(2026, 8, 9, 22, 0, 0, DateTimeKind.Utc);
            var policy = new Mock<IEntryMonthlyObligationDueDatePolicy>();
            policy.Setup(service => service.ResolveDueDateAsync(2026, 8))
                .ReturnsAsync(EntryMonthlyObligationDueDateResolution.Resolved(
                    dueAtUtc,
                    "due-policy-v1"));
            var scheduler = CreateScheduler();
            var worker = CreateWorker(
                policy.Object,
                scheduler.Object,
                new Payments.TestLogger<EntryMonthlyObligationWorker>());

            await worker.RunOnceAsync();

            policy.Verify(service => service.ResolveDueDateAsync(2026, 8), Times.Once);
            scheduler.Verify(service => service.EnsureObligationsForPeriodAsync(
                2026,
                8,
                dueAtUtc,
                "due-policy-v1"), Times.Once);
        }

        private TestableEntryMonthlyObligationWorker CreateWorker(
            IEntryMonthlyObligationDueDatePolicy policy,
            IEntryMonthlyObligationScheduler scheduler,
            ILogger<EntryMonthlyObligationWorker> logger)
        {
            var schedulingLock = new Mock<IEntryMonthlyObligationSchedulingLock>();
            schedulingLock.Setup(service => service.AcquireAsync())
                .Returns(Task.CompletedTask);
            return new TestableEntryMonthlyObligationWorker(
                new AbpAsyncTimer(),
                BuildConfiguration(),
                Resolve<IUnitOfWorkManager>(),
                schedulingLock.Object,
                policy,
                scheduler,
                logger);
        }

        private static Mock<IEntryMonthlyObligationScheduler> CreateScheduler()
        {
            var scheduler = new Mock<IEntryMonthlyObligationScheduler>();
            scheduler.Setup(service => service.EnsureObligationsForPeriodAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<DateTime>(),
                    It.IsAny<string>()))
                .ReturnsAsync(0);
            scheduler.Setup(service => service.AssessObligationsAsync(FixedNowUtc))
                .ReturnsAsync(0);
            return scheduler;
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        "App:EntryMonthlyObligations:Enabled",
                        "true"),
                    new KeyValuePair<string, string>(
                        "App:EntryMonthlyObligations:IntervalMinutes",
                        "60")
                })
                .Build();
        }
    }
}

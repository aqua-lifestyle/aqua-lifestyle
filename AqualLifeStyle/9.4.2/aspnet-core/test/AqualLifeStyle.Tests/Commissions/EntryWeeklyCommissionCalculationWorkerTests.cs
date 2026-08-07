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
    public class EntryWeeklyCommissionCalculationWorkerTests
        : AqualLifeStyleTestBase
    {
        private const int TestTenantOneId = 1;

        private sealed class TestableEntryWeeklyCommissionCalculationWorker
            : EntryWeeklyCommissionCalculationWorker
        {
            public TestableEntryWeeklyCommissionCalculationWorker(
                AbpAsyncTimer timer,
                IConfiguration configuration,
                IUnitOfWorkManager unitOfWorkManager,
                IRepository<Tenant, int> tenantRepository,
                LatestClosedCommissionWeekResolver closedWeekResolver,
                IWeeklyCommissionCalculator commissionCalculator,
                IEntryWeeklyCommissionCalculationLock calculationLock,
                Microsoft.Extensions.Logging.ILogger<EntryWeeklyCommissionCalculationWorker> logger)
                : base(timer, configuration, unitOfWorkManager, tenantRepository,
                    closedWeekResolver, commissionCalculator, calculationLock, logger)
            {
            }

            public Task RunOnceAsync() => DoWorkAsync();
        }

        private static IConfiguration BuildConfiguration(bool enabled)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>(
                        "App:EntryWeeklyCommissions:Enabled",
                        enabled ? "true" : "false"),
                    new KeyValuePair<string, string>(
                        "App:EntryWeeklyCommissions:IntervalMinutes",
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
                RecordsCreated = recordsCreated
            };
        }

        [Fact]
        public async Task DisabledByDefault_DoesNotCalculateAnyTenant()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            var worker = CreateWorker(BuildConfiguration(false), calculator.Object);

            await worker.RunOnceAsync();

            calculator.Verify(
                service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public async Task Enabled_CalculatesLatestClosedWeekForAllActiveTenants()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));

            var worker = CreateWorker(BuildConfiguration(true), calculator.Object);

            await worker.RunOnceAsync();

            var activeTenantIds = await ResolveActiveTenantIdsAsync();
            calculator.Verify(
                service => service.CalculateEntryAsync(
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
                    service => service.CalculateEntryAsync(
                        tenantId,
                        It.Is<ClosedCommissionWeek>(week => week.PeriodEndUtc < DateTime.UtcNow),
                        It.IsAny<DateTime>()),
                    Times.Once);
            }
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
        }

        [Fact]
        public async Task CalculatesEntryOnly_NeverReleasesOrMarksPaid()
        {
            var calculator = new Mock<IWeeklyCommissionCalculator>();
            calculator
                .Setup(service => service.CalculateEntryAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()))
                .ReturnsAsync(BuildResult(wasAlreadyCalculated: false, recordsCreated: 1));

            var worker = CreateWorker(BuildConfiguration(true), calculator.Object);
            await worker.RunOnceAsync();

            calculator.Verify(
                service => service.CalculateOnyxAsync(
                    It.IsAny<int>(),
                    It.IsAny<ClosedCommissionWeek>(),
                    It.IsAny<DateTime>()),
                Times.Never);
        }

        [Fact]
        public void LockKey_IsDistinctFromObligationLockKey()
        {
            EntryWeeklyCommissionCalculationLock.LockKey
                .ShouldBe(0x41514757434F4D50);
            EntryMonthlyObligationSchedulingLock.LockKey
                .ShouldBe(0x415147524F424C);
        }

        private TestableEntryWeeklyCommissionCalculationWorker CreateWorker(
            IConfiguration configuration,
            IWeeklyCommissionCalculator commissionCalculator)
        {
            var timer = new AbpAsyncTimer();
            var lockMock = new Mock<IEntryWeeklyCommissionCalculationLock>();
            lockMock.Setup(service => service.AcquireAsync())
                .Returns(Task.CompletedTask);
            var logger = new Payments.TestLogger<EntryWeeklyCommissionCalculationWorker>();

            return new TestableEntryWeeklyCommissionCalculationWorker(
                timer,
                configuration,
                Resolve<IUnitOfWorkManager>(),
                Resolve<IRepository<Tenant, int>>(),
                Resolve<LatestClosedCommissionWeekResolver>(),
                commissionCalculator,
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
            var tenant = new Tenant(tenancyName, tenancyName);
            await UsingDbContextAsync((int?)null, async context =>
            {
                context.Tenants.Add(tenant);
                await context.SaveChangesAsync();
            });
            return tenant;
        }
    }
}

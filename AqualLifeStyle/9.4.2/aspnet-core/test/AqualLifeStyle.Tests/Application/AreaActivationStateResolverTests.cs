using System;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AreaActivationStateResolverTests : AqualLifeStyleTestBase
    {
        [Fact]
        public async Task Resolve_ReturnsUnknownBeforeFirstObservedState()
        {
            var effectiveAt = new DateTime(
                2026,
                8,
                7,
                0,
                0,
                0,
                DateTimeKind.Utc);
            await AddRecordAsync(true, effectiveAt);
            var resolver = Resolve<IAreaActivationStateResolver>();

            var before = await resolver.ResolveAsync(1, effectiveAt.AddTicks(-1));
            var atBoundary = await resolver.ResolveAsync(1, effectiveAt);

            before.Status.ShouldBe(AreaActivationStateResolutionStatus.Unknown);
            atBoundary.Status.ShouldBe(AreaActivationStateResolutionStatus.Active);
            atBoundary.EffectiveAt.ShouldBe(effectiveAt);
        }

        [Fact]
        public async Task Resolve_UsesLatestStateAtCutoff_NotCurrentTenantProjection()
        {
            var activeAt = new DateTime(
                2026,
                8,
                7,
                0,
                0,
                0,
                DateTimeKind.Utc);
            var inactiveAt = activeAt.AddDays(7);
            await AddRecordAsync(true, activeAt);
            await AddRecordAsync(false, inactiveAt);
            await UsingDbContextAsync(null, async context =>
            {
                var tenant = await context.Tenants.SingleAsync(item => item.Id == 1);
                tenant.IsActive = false;
                await context.SaveChangesAsync();
            });
            var resolver = Resolve<IAreaActivationStateResolver>();

            var closedBeforeChange = await resolver.ResolveAsync(
                1,
                inactiveAt.AddTicks(-1));
            var afterChange = await resolver.ResolveAsync(1, inactiveAt);

            closedBeforeChange.Status.ShouldBe(
                AreaActivationStateResolutionStatus.Active);
            afterChange.Status.ShouldBe(
                AreaActivationStateResolutionStatus.Inactive);
        }

        [Fact]
        public async Task Calculation_WithUnknownAreaState_CreatesNoLedger()
        {
            LoginAsHostAdmin();
            var service = Resolve<IAdminCommissionAppService>();

            var exception = await Should.ThrowAsync<AreaActivationStateUnavailableException>(
                () => service.CalculateLatestClosedWeekAsync(
                    new CalculateLatestClosedCommissionWeekInput
                    {
                        TenantId = 1,
                        Programme = AdminCommissionProgramme.Onyx
                    }));

            exception.Status.ShouldBe(AreaActivationStateResolutionStatus.Unknown);
            await UsingDbContextAsync(null, async context =>
            {
                (await context.OnyxCommissionPeriods.CountAsync()).ShouldBe(0);
                (await context.OnyxWeeklyCommissions.CountAsync()).ShouldBe(0);
            });
        }

        [Fact]
        public async Task Calculation_WithInactiveAreaAtCutoff_CreatesNoLedger()
        {
            await AddRecordAsync(
                false,
                new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
            LoginAsHostAdmin();
            var service = Resolve<IAdminCommissionAppService>();

            var exception = await Should.ThrowAsync<AreaActivationStateUnavailableException>(
                () => service.CalculateLatestClosedWeekAsync(
                    new CalculateLatestClosedCommissionWeekInput
                    {
                        TenantId = 1,
                        Programme = AdminCommissionProgramme.Entry
                    }));

            exception.Status.ShouldBe(AreaActivationStateResolutionStatus.Inactive);
            await UsingDbContextAsync(null, async context =>
            {
                (await context.EntryCommissionPeriods.CountAsync()).ShouldBe(0);
                (await context.EntryWeeklyCommissions.CountAsync()).ShouldBe(0);
            });
        }

        private Task AddRecordAsync(bool isActive, DateTime effectiveAt)
        {
            return UsingDbContextAsync(null, async context =>
            {
                context.AreaActivationStateRecords.Add(
                    AreaActivationStateRecord.Record(
                        Guid.NewGuid(),
                        1,
                        isActive,
                        effectiveAt,
                        effectiveAt,
                        null,
                        "Test Area state evidence",
                        AreaActivationStateRecordKind.Changed));
                await context.SaveChangesAsync();
            });
        }
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Authorization;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public sealed class AdminAQGreenWeeklySalesEligibilityReadTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime WeekStartUtc =
            new(2026, 8, 20, 22, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms EntryTerms =
            EntryProgrammeTerms.CreateSingleJoiningPayment(
                "weekly-sales-read-tests",
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                1200m,
                600m,
                7);

        [Fact]
        public async Task HostReadQueue_ExposesMemberWeekEvidenceAndSystemResult()
        {
            var subject = await CreateSubjectAsync("queue");
            var decision = AQGreenWeeklySalesEligibilityDecision.Begin(
                1,
                subject.ParticipantId,
                AQGreenCommissionWeek.FromStartUtc(WeekStartUtc),
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            var reviewedAt = AQGreenCommissionWeek.FromStartUtc(WeekStartUtc)
                .EndExclusiveUtc.AddMinutes(1);
            decision.AddManualEvidence("ticket:weekly-sales-queue", reviewedAt);
            decision.Confirm(
                new AQGreenWeeklySalesQuantities(5, 5, 4),
                1L,
                reviewedAt);
            await UsingDbContextAsync(1, async context =>
            {
                context.AQGreenWeeklySalesEligibilityDecisions.Add(decision);
                await context.SaveChangesAsync();
            });
            LoginAsHostAdmin();
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            var result = await service.GetAllAsync(
                new AQGreenWeeklySalesReviewListInput
                {
                    MaxResultCount = 100,
                    ReviewStatus = AQGreenWeeklySalesReviewStatus.Confirmed
                });

            var row = result.Items.Single(item => item.DecisionId == decision.Id);
            row.TenantId.ShouldBe(1);
            row.ParticipantId.ShouldBe(subject.ParticipantId);
            row.CustomerName.ShouldBe("Weekly Sales queue");
            row.AreaName.ShouldBe("Weekly Sales Test Area queue");
            row.CommissionWeekStartUtc.ShouldBe(WeekStartUtc);
            row.CommissionWeekEndUtc.ShouldBe(
                AQGreenCommissionWeek.FromStartUtc(WeekStartUtc)
                    .EndExclusiveUtc.AddTicks(-1));
            row.ReviewStatus.ShouldBe(AQGreenWeeklySalesReviewStatus.Confirmed);
            row.ReviewedSprayQuantity.ShouldBe(5);
            row.ReviewedOneLitreQuantity.ShouldBe(5);
            row.ReviewedFiveLitreQuantity.ShouldBe(4);
            row.ThresholdResult.ShouldBe(AQGreenWeeklySalesThresholdResult.NotMet);
            row.ReviewedByUserId.ShouldBe(1L);
            row.EvidenceReferences.ShouldBe(
                new[] { "ticket:weekly-sales-queue" });
        }

        [Fact]
        public async Task HostLatestClosedWeekRead_DoesNotRequireProductionWriteGate()
        {
            var subject = await CreateSubjectAsync("latest");
            LoginAsHostAdmin();
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            var result = await service.GetLatestClosedWeekAsync(
                new AQGreenWeeklySalesReviewTargetInput
                {
                    TenantId = 1,
                    ParticipantId = subject.ParticipantId
                });

            result.DecisionId.ShouldBeNull();
            result.ReviewStatus.ShouldBeNull();
            result.EvidenceReferences.ShouldBeEmpty();
            result.CustomerName.ShouldBe("Weekly Sales latest");
            result.SalesEligibilityRulesVersion.ShouldBe(
                AQGreenWeeklySalesEligibilityRules.CurrentVersion);
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(
                result.CommissionWeekStartUtc,
                TimeZoneInfo.FindSystemTimeZoneById(
                    AQGreenCommissionWeek.TimeZoneId));
            localStart.DayOfWeek.ShouldBe(DayOfWeek.Friday);
            localStart.TimeOfDay.ShouldBe(TimeSpan.Zero);
        }

        [Fact]
        public async Task TenantAdministrator_CannotReadHostWeeklySalesReviewQueue()
        {
            LoginAsDefaultTenantAdmin();
            var service = Resolve<IAdminAQGreenWeeklySalesEligibilityAppService>();

            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                service.GetAllAsync(new AQGreenWeeklySalesReviewListInput()));
        }

        private async Task<(Guid ParticipantId, int CustomerId)> CreateSubjectAsync(
            string suffix)
        {
            var userId = await CreateTestUserAsync(
                1,
                $"weekly-sales-{suffix}",
                $"weekly-sales-{suffix}@example.com");
            return await UsingDbContextAsync(1, async context =>
            {
                var area = Area.Create(
                    1,
                    $"WS{suffix[..Math.Min(6, suffix.Length)]}",
                    $"Weekly Sales Test Area {suffix}");
                var customer = Customer.Create(
                    1,
                    userId,
                    $"Weekly Sales {suffix}",
                    new EmailAddress($"weekly-sales-customer-{suffix}@example.com"));
                customer.AssignInitialArea(
                    area,
                    EntryTerms.EffectiveFrom,
                    "Weekly sales read fixture");
                context.Areas.Add(area);
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryTerms,
                    EntryTerms.EffectiveFrom);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return (participation.Id, customer.Id);
            });
        }
    }
}

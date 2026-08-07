using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class EntryMonthlyObligationSchedulerTests
        : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly IEntryMonthlyObligationScheduler _scheduler;

        public EntryMonthlyObligationSchedulerTests()
        {
            _scheduler = Resolve<IEntryMonthlyObligationScheduler>();
        }

        [Fact]
        public async Task ActiveParticipant_GetsOneObligationPerPeriod_RepeatedRunsAreIdempotent()
        {
            var customerId = await CreateActiveParticipantAsync(
                $"active-{Guid.NewGuid():N}");

            var dueAt = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);
            var first = await _scheduler.EnsureObligationsForPeriodAsync(2026, 8, dueAt);
            var second = await _scheduler.EnsureObligationsForPeriodAsync(2026, 8, dueAt);

            first.ShouldBe(1);
            second.ShouldBe(0);

            var persisted = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations
                    .Where(obligation => obligation.CustomerId == customerId)
                    .ToListAsync());
            persisted.Count.ShouldBe(1);
            persisted[0].PeriodYear.ShouldBe(2026);
            persisted[0].PeriodMonth.ShouldBe(8);
            persisted[0].AmountDue.ShouldBe(600m);
            persisted[0].OutstandingAmount.ShouldBe(600m);
            persisted[0].Status.ShouldBe(EntryMonthlyObligationStatus.Due);
            persisted[0].Currency.ShouldBe("ZAR");
        }

        [Fact]
        public async Task InactiveParticipant_DoesNotGetAnObligation()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    (await CreateTestUserAsync(1, $"inactive-{suffix}", $"inactive-{suffix}@example.com")),
                    "Inactive Entry Member",
                    new AqualLifeStyle.Domain.Common.EmailAddress($"inactive-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.Create(
                    "2026-07",
                    EffectiveFrom,
                    600m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    EffectiveFrom);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return customer.Id;
            });

            var created = await _scheduler.EnsureObligationsForPeriodAsync(
                2026,
                8,
                new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc));

            created.ShouldBe(0);
            var count = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations.CountAsync(
                    obligation => obligation.CustomerId == customerId));
            count.ShouldBe(0);
        }

        [Fact]
        public async Task UnpaidObligations_AreAssessed_IntoOverdue()
        {
            var customerId = await CreateActiveParticipantAsync(
                $"assess-{Guid.NewGuid():N}");
            await CreateObligationAsync(customerId, 2026, 8);

            var assessed = await _scheduler.AssessObligationsAsync(
                new DateTime(2026, 8, 20, 0, 0, 0, DateTimeKind.Utc));

            assessed.ShouldBe(1);
            var obligation = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations.SingleAsync(
                    item => item.CustomerId == customerId));
            obligation.Status.ShouldBe(EntryMonthlyObligationStatus.Overdue);
            obligation.MarkedOverdueAt.ShouldNotBeNull();
            obligation.IsOwnPayoutEligible.ShouldBeFalse();
        }

        [Fact]
        public async Task ConfirmedMonthlyPayment_IsAllocatedToEarliestOpenObligation_Once()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var customerId = await CreateActiveParticipantAsync($"alloc-{suffix}");
            await CreateObligationAsync(customerId, 2026, 8);
            await CreateObligationAsync(customerId, 2026, 9);
            await CreateConfirmedMonthlyPaymentAsync(customerId, $"monthly-{suffix}");

            var allocated = await _scheduler.AllocateConfirmedMonthlyPaymentsAsync();
            var secondRun = await _scheduler.AllocateConfirmedMonthlyPaymentsAsync();

            allocated.ShouldBe(1);
            secondRun.ShouldBe(0);

            var obligations = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations
                    .Where(obligation => obligation.CustomerId == customerId)
                    .OrderBy(obligation => obligation.PeriodMonth)
                    .ToListAsync());
            obligations[0].PeriodMonth.ShouldBe(8);
            obligations[0].Status.ShouldBe(EntryMonthlyObligationStatus.Paid);
            obligations[0].OutstandingAmount.ShouldBe(0m);
            obligations[0].PaymentId.ShouldNotBeNull();
            obligations[0].IsOwnPayoutEligible.ShouldBeTrue();
            obligations[1].PeriodMonth.ShouldBe(9);
            obligations[1].Status.ShouldBe(EntryMonthlyObligationStatus.Due);
            obligations[1].PaymentId.ShouldBeNull();
        }

        [Fact]
        public async Task MismatchedMonthlyPayment_IsNotAllocated()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var customerId = await CreateActiveParticipantAsync($"mismatch-{suffix}");
            await CreateObligationAsync(customerId, 2026, 8);

            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                MemberPaymentPurpose.EntryMonthlyCommitment,
                800m,
                "Test",
                $"mismatch-monthly-{suffix}",
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddHours(1));
            await UsingDbContextAsync(1, async context =>
            {
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });

            var allocated = await _scheduler.AllocateConfirmedMonthlyPaymentsAsync();

            allocated.ShouldBe(0);
            var obligation = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations.SingleAsync(
                    item => item.CustomerId == customerId));
            obligation.Status.ShouldBe(EntryMonthlyObligationStatus.Due);
            obligation.PaymentId.ShouldBeNull();
        }

        private async Task<int> CreateActiveParticipantAsync(string suffix)
        {
            var email = $"{suffix}@example.com";
            return await UsingDbContextAsync(1, async context =>
            {
                var userId = await CreateTestUserAsync(1, suffix, email);
                var customer = Customer.Create(
                    1,
                    userId,
                    "Entry Club Member",
                    new AqualLifeStyle.Domain.Common.EmailAddress(email));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.Create(
                    "2026-07",
                    EffectiveFrom,
                    600m,
                    600m,
                    600m,
                    7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    EffectiveFrom);
                var registration = ConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    "registration-" + suffix);
                participation.ApplyConfirmedActivationPayment(registration);
                var activation = ConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    "activation-" + suffix);
                participation.ApplyConfirmedActivationPayment(activation);
                participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
                context.MemberPayments.AddRange(registration, activation);
                context.EntryParticipations.Add(participation);
                await context.SaveChangesAsync();
                return customer.Id;
            });
        }

        private async Task CreateObligationAsync(int customerId, int periodYear, int periodMonth)
        {
            await UsingDbContextAsync(1, async context =>
            {
                var participation = await context.EntryParticipations.SingleAsync(
                    item => item.CustomerId == customerId);
                var obligation = EntryMonthlyObligation.Create(
                    participation,
                    periodYear,
                    periodMonth,
                    new DateTime(
                        periodYear,
                        periodMonth,
                        10,
                        0,
                        0,
                        0,
                        DateTimeKind.Utc));
                context.EntryMonthlyObligations.Add(obligation);
                await context.SaveChangesAsync();
            });
        }

        private async Task CreateConfirmedMonthlyPaymentAsync(int customerId, string reference)
        {
            var payment = ConfirmedPayment(
                customerId,
                MemberPaymentPurpose.EntryMonthlyCommitment,
                reference);
            await UsingDbContextAsync(1, async context =>
            {
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });
        }

        private static MemberPayment ConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            string reference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                600m,
                "Test",
                reference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddHours(1));
            return payment;
        }
    }

    public class ConfigurationEntryMonthlyObligationDueDatePolicyTests
    {
        [Theory]
        [InlineData("10", 2026, 8)]
        [InlineData("1", 2026, 8)]
        [InlineData("28", 2026, 8)]
        public void ConfiguredDay_ResolvesUtcDueDate(
            string configuredDay,
            int periodYear,
            int periodMonth)
        {
            var policy = CreatePolicy(configuredDay);

            var dueAt = policy.ResolveDueDate(periodYear, periodMonth);

            dueAt.ShouldNotBeNull();
            dueAt.Value.Year.ShouldBe(periodYear);
            dueAt.Value.Month.ShouldBe(periodMonth);
            dueAt.Value.Day.ShouldBe(int.Parse(configuredDay));
            dueAt.Value.Kind.ShouldBe(DateTimeKind.Utc);
        }

        [Theory]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("29")]
        [InlineData("not-a-number")]
        public void UndefinedOrInvalidDay_ReturnsNull(string configuredDay)
        {
            var policy = CreatePolicy(configuredDay);

            policy.ResolveDueDate(2026, 8).ShouldBeNull();
        }

        private static ConfigurationEntryMonthlyObligationDueDatePolicy CreatePolicy(
            string configuredDay)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        "App:EntryMonthlyObligations:DueDayOfMonth",
                        configuredDay)
                })
                .Build();
            return new ConfigurationEntryMonthlyObligationDueDatePolicy(configuration);
        }
    }
}

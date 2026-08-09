using System;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.EntryMonthlyObligations;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
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

            var dueAt = new DateTime(2026, 8, 9, 22, 0, 0, DateTimeKind.Utc);
            var first = await _scheduler.EnsureObligationsForPeriodAsync(
                2026, 8, dueAt, "due-policy-v1");
            var second = await _scheduler.EnsureObligationsForPeriodAsync(
                2026, 8, dueAt, "due-policy-v1");

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
            persisted[0].DuePolicyVersion.ShouldBe("due-policy-v1");
        }

        [Fact]
        public async Task ActivationMonthIsSkipped_AndFollowingMonthIsCreated()
        {
            var customerId = await CreateActiveParticipantAsync(
                $"activation-month-{Guid.NewGuid():N}");

            var activationMonthCreated = await _scheduler.EnsureObligationsForPeriodAsync(
                2026,
                7,
                new DateTime(2026, 7, 9, 22, 0, 0, DateTimeKind.Utc),
                "due-policy-v1");
            var followingMonthCreated = await _scheduler.EnsureObligationsForPeriodAsync(
                2026,
                8,
                new DateTime(2026, 8, 9, 22, 0, 0, DateTimeKind.Utc),
                "due-policy-v1");

            activationMonthCreated.ShouldBe(0);
            followingMonthCreated.ShouldBe(1);
            var obligation = await UsingDbContextAsync(1, async context =>
                await context.EntryMonthlyObligations.SingleAsync(
                    item => item.CustomerId == customerId));
            obligation.PeriodYear.ShouldBe(2026);
            obligation.PeriodMonth.ShouldBe(8);
            obligation.DuePolicyVersion.ShouldBe("due-policy-v1");
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
                new DateTime(2026, 8, 9, 22, 0, 0, DateTimeKind.Utc),
                "due-policy-v1");

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

        private async Task<int> CreateActiveParticipantAsync(string suffix)
        {
            var email = $"{suffix}@example.com";
            return await UsingDbContextAsync(1, async context =>
            {
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "due-policy-v1",
                        10,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));
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
                        DateTimeKind.Utc),
                    "due-policy-v1");
                context.EntryMonthlyObligations.Add(obligation);
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

    public class PersistedEntryMonthlyObligationDueDatePolicyTests
        : AqualLifeStyleTestBase
    {
        private readonly IEntryMonthlyObligationDueDatePolicy _resolver;

        public PersistedEntryMonthlyObligationDueDatePolicyTests()
        {
            _resolver = Resolve<IEntryMonthlyObligationDueDatePolicy>();
        }

        [Fact]
        public async Task EmptyPolicyTable_FailsClosedAsMissing()
        {
            var result = await _resolver.ResolveDueDateAsync(2026, 8);

            result.Status.ShouldBe(
                EntryMonthlyObligationDueDateResolutionStatus.Missing);
            result.IsResolved.ShouldBeFalse();
            result.DueAtUtc.ShouldBeNull();
            result.PolicyVersion.ShouldBeNull();
        }

        [Fact]
        public async Task LatestApplicableVersion_IsSelectedAndConvertedFromJohannesburg()
        {
            await InsertPoliciesAsync(
                EntryMonthlyObligationDuePolicy.Create(
                    "due-v1",
                    1,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)),
                EntryMonthlyObligationDuePolicy.Create(
                    "due-v2",
                    28,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 10)));

            var september = await _resolver.ResolveDueDateAsync(2026, 9);
            var november = await _resolver.ResolveDueDateAsync(2026, 11);

            september.Status.ShouldBe(
                EntryMonthlyObligationDueDateResolutionStatus.Resolved);
            september.PolicyVersion.ShouldBe("due-v1");
            september.DueAtUtc.ShouldBe(
                new DateTime(2026, 8, 31, 22, 0, 0, DateTimeKind.Utc));
            november.PolicyVersion.ShouldBe("due-v2");
            november.DueAtUtc.ShouldBe(
                new DateTime(2026, 11, 27, 22, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public async Task TiedLatestEffectiveVersions_FailClosedAsAmbiguous()
        {
            var effectiveFrom = EntryMonthlyObligationDuePolicy
                .JohannesburgMonthStartUtc(2026, 8);
            await InsertPoliciesAsync(
                EntryMonthlyObligationDuePolicy.Create("due-a", 1, effectiveFrom),
                EntryMonthlyObligationDuePolicy.Create("due-b", 28, effectiveFrom));

            var result = await _resolver.ResolveDueDateAsync(2026, 8);

            result.Status.ShouldBe(
                EntryMonthlyObligationDueDateResolutionStatus.Ambiguous);
            result.IsResolved.ShouldBeFalse();
        }

        [Fact]
        public async Task ExistingPolicy_CannotBeDeletedThroughPersistence()
        {
            await InsertPoliciesAsync(
                EntryMonthlyObligationDuePolicy.Create(
                    "append-only-v1",
                    10,
                    EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));

            using var context = LocalIocManager.Resolve<
                AqualLifeStyle.EntityFrameworkCore.AqualLifeStyleDbContext>();
            var policy = await context.EntryMonthlyObligationDuePolicies.SingleAsync();
            context.EntryMonthlyObligationDuePolicies.Remove(policy);

            await Should.ThrowAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
        }

        private Task InsertPoliciesAsync(
            params EntryMonthlyObligationDuePolicy[] policies)
        {
            return UsingDbContextAsync(null, async context =>
            {
                context.EntryMonthlyObligationDuePolicies.AddRange(policies);
                await context.SaveChangesAsync();
            });
        }
    }
}

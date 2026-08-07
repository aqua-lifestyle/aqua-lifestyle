using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class ClubMemberProgrammeProgressAppServiceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms EntryTerms =
            EntryProgrammeTerms.CreateSingleJoiningPayment(
                version: "2026-07",
                effectiveFrom: EffectiveFrom,
                joiningPaymentAmount: 1200m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

        private static readonly EntryCommissionTerms CommissionTerms =
            EntryCommissionTerms.Create(
                "2026-07",
                EffectiveFrom,
                150m,
                250m,
                1250m);

        private readonly IClubMemberProgrammeProgressAppService _progressService;

        public ClubMemberProgrammeProgressAppServiceTests()
        {
            _progressService =
                Resolve<IClubMemberProgrammeProgressAppService>();
        }

        [Fact]
        public async Task ProgressForInactiveCustomer_IsUnavailable()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-inactive-{suffix}",
                $"progress-inactive-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Inactive Progress Member",
                    new EmailAddress($"progress-inactive-customer-{suffix}@example.com"));
                customer.Deactivate();
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });
            SetCurrentUser(userId, 1);

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _progressService.GetMyProgressAsync());

            exception.Details.ShouldContain("active Club Member");
        }

        [Fact]
        public async Task ActiveParticipantWithFullNetwork_SeesLevelCommissionsAndNextAction()
        {
            var persisted = await CreateActiveMemberWithNetworkAsync();
            SetCurrentUser(persisted, 1);

            var progress = await _progressService.GetMyProgressAsync();

            progress.HasEntryParticipation.ShouldBeTrue();
            progress.QualifiedLevelLabel.ShouldBe("Level 1");
            progress.QualifiedLevel.ShouldBe(1);
            progress.NextLevelLabel.ShouldBe("Level 2");
            progress.DirectRecruits.ShouldBe(
                EntryNetworkQualificationEvaluator.BranchSize);
            progress.DirectRecruitsRequired.ShouldBe(5);
            progress.RecruitsRemaining.ShouldBe(0);
            progress.RecruitmentProgressPercent.ShouldBe(100);
            progress.Currency.ShouldBe("ZAR");
            progress.TotalEarned.ShouldBe(150m);
            progress.EarnedAwaitingRelease.ShouldBe(150m);
            progress.OnHold.ShouldBe(0m);
            progress.ReleasedAwaitingPayment.ShouldBe(0m);
            progress.Paid.ShouldBe(0m);

            var earning = progress.RecentEarnings.Single();
            earning.TotalAmount.ShouldBe(150m);
            earning.HighestLevel.ShouldBe(1);
            earning.Status.ShouldBe("Earned — awaiting release");
            earning.Components.Single().Level.ShouldBe(1);
            earning.Components.Single().Amount.ShouldBe(150m);

            progress.MonthlyObligationStatus.ShouldBe("Payment due");
            progress.MonthlyObligationAmount.ShouldBe(600m);
            progress.MonthlyObligationDueAt.ShouldNotBeNull();
            progress.MonthlyObligationOutstanding.ShouldBe(600m);
            progress.NextAction.ShouldContain("Pay your AQGreen monthly subscription");
            progress.NextActionAmount.ShouldBe(600m);

            progress.FuneralCoverIncluded.ShouldBeTrue();
            progress.FuneralCoverBenefitAmount.ShouldBe(30000m);
            progress.Education.Count.ShouldBe(4);
        }

        [Fact]
        public async Task HeldCommission_ReportsHoldReasonAndOverdueObligation()
        {
            var persisted = await CreateActiveMemberWithOverdueObligationAsync();
            SetCurrentUser(persisted, 1);

            var progress = await _progressService.GetMyProgressAsync();

            progress.OnHold.ShouldBe(150m);
            progress.EarnedAwaitingRelease.ShouldBe(0m);
            progress.TotalEarned.ShouldBe(150m);
            progress.RecentEarnings.Single().Status.ShouldBe("On hold");
            progress.RecentEarnings.Single().HoldReason
                .ShouldContain("monthly commitment is overdue");
            progress.MonthlyObligationStatus.ShouldBe("Overdue");
            progress.NextAction.ShouldContain("restore your weekly earnings");
            progress.NextActionAmount.ShouldBe(600m);
        }

        [Fact]
        public async Task PaidCommission_ReportsReleasedAndPaidTotals()
        {
            var persisted = await CreateActiveMemberWithPaidCommissionAsync();
            SetCurrentUser(persisted, 1);

            var progress = await _progressService.GetMyProgressAsync();

            progress.ReleasedAwaitingPayment.ShouldBe(0m);
            progress.Paid.ShouldBe(150m);
            progress.RecentEarnings.Single().Status.ShouldBe("Paid");
            progress.RecentEarnings.Single().Components.Count.ShouldBe(1);
        }

        private async Task<long> CreateActiveMemberWithNetworkAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-member-{suffix}",
                $"progress-member-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Progress Member",
                    new EmailAddress($"progress-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });

            var participation = await CreateActiveParticipationAsync(
                customerId,
                $"progress-{suffix}");
            var recruitCustomerIds = await CreateDirectRecruitsAsync(
                participation,
                $"recruit-{suffix}");
            var obligations = await CreateObligationAsync(participation, "Due");
            await CreateCommissionRecordAsync(
                participation,
                recruitCustomerIds,
                $"progress-week-{suffix}",
                obligations);
            await CreateFuneralCoverAsync(participation);

            return userId;
        }

        private async Task<long> CreateActiveMemberWithOverdueObligationAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-held-{suffix}",
                $"progress-held-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Held Member",
                    new EmailAddress($"progress-held-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });

            var participation = await CreateActiveParticipationAsync(
                customerId,
                $"progress-held-{suffix}");
            var recruitCustomerIds = await CreateDirectRecruitsAsync(
                participation,
                $"held-recruit-{suffix}");
            var obligations = await CreateObligationAsync(participation, "Overdue");
            await CreateCommissionRecordAsync(
                participation,
                recruitCustomerIds,
                $"held-week-{suffix}",
                obligations);
            await CreateFuneralCoverAsync(participation);

            return userId;
        }

        private async Task<long> CreateActiveMemberWithPaidCommissionAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-paid-{suffix}",
                $"progress-paid-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Paid Member",
                    new EmailAddress($"progress-paid-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });

            var participation = await CreateActiveParticipationAsync(
                customerId,
                $"progress-paid-{suffix}");
            var recruitCustomerIds = await CreateDirectRecruitsAsync(
                participation,
                $"paid-recruit-{suffix}");
            var obligations = await CreateObligationAsync(participation, "Paid");
            await CreateCommissionRecordAsync(
                participation,
                recruitCustomerIds,
                $"paid-week-{suffix}",
                obligations,
                paid: true);
            await CreateFuneralCoverAsync(participation);

            return userId;
        }

        private async Task<EntryParticipation> CreateActiveParticipationAsync(
            int customerId,
            string suffix)
        {
            return await UsingDbContextAsync(1, async context =>
            {
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customerId,
                    EntryTerms,
                    EffectiveFrom);
                var joiningPayment = MemberPayment.CreatePending(
                    1,
                    customerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    1200m,
                    "Test",
                    $"joining-{suffix}",
                    EffectiveFrom);
                joiningPayment.Confirm(EffectiveFrom.AddHours(1));
                participation.ApplyConfirmedJoiningPayment(joiningPayment);
                participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(5));
                context.EntryParticipations.Add(participation);
                context.MemberPayments.Add(joiningPayment);
                await context.SaveChangesAsync();
                return participation;
            });
        }

        private async Task<List<int>> CreateDirectRecruitsAsync(
            EntryParticipation recruiterParticipation,
            string suffix)
        {
            var recruitCustomerIds = new List<int>();
            for (var index = 0;
                 index < EntryNetworkQualificationEvaluator.BranchSize;
                 index++)
            {
                var recruitUserId = await CreateTestUserAsync(
                    1,
                    $"recruit-{index}-{suffix}",
                    $"recruit-{index}-{suffix}@example.com");
                var recruitCustomerId = await UsingDbContextAsync(
                    1,
                    async context =>
                    {
                        var customer = Customer.Create(
                            1,
                            recruitUserId,
                            "Recruit Member",
                            new EmailAddress($"recruit-customer-{index}-{suffix}@example.com"));
                        context.Customers.Add(customer);
                        await context.SaveChangesAsync();
                        return customer.Id;
                    });
                await UsingDbContextAsync(1, async context =>
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        recruitCustomerId,
                        recruiterParticipation,
                        EntryTerms,
                        EffectiveFrom.AddMinutes(1));
                    var payment = MemberPayment.CreatePending(
                        1,
                        recruitCustomerId,
                        MemberPaymentPurpose.AQGreenJoining,
                        1200m,
                        "Test",
                        $"recruit-joining-{index}-{suffix}",
                        EffectiveFrom.AddMinutes(2));
                    payment.Confirm(EffectiveFrom.AddMinutes(3));
                    recruit.ApplyConfirmedJoiningPayment(payment);
                    recruit.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(4));
                    context.EntryParticipations.Add(recruit);
                    context.MemberPayments.Add(payment);
                    await context.SaveChangesAsync();
                });
                recruitCustomerIds.Add(recruitCustomerId);
            }

            return recruitCustomerIds;
        }

        private async Task CreateCommissionRecordAsync(
            EntryParticipation participation,
            List<int> recruitCustomerIds,
            string suffix,
            IReadOnlyCollection<EntryMonthlyObligation> obligations,
            bool paid = false)
        {
            await UsingDbContextAsync(1, async context =>
            {
                var allParticipations = await context.EntryParticipations
                    .ToListAsync();
                var network = allParticipations
                    .Where(item =>
                        item.CustomerId == participation.CustomerId ||
                        recruitCustomerIds.Contains(item.CustomerId))
                    .ToList();

                var periodStart = EffectiveFrom.AddDays(5);
                var periodEnd = periodStart.AddDays(7).AddTicks(-1);
                var period = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    periodStart,
                    periodEnd,
                    "Africa/Johannesburg",
                    periodEnd.AddMinutes(1),
                    CommissionTerms);
                var commission = new EntryWeeklyCommissionCalculator(
                        new EntryNetworkQualificationEvaluator())
                    .Calculate(
                        participation,
                        period,
                        CommissionTerms,
                        network,
                        obligations ?? Array.Empty<EntryMonthlyObligation>());
                if (paid)
                {
                    commission.ReleaseEligiblePayout(periodEnd.AddHours(1));
                    commission.MarkPaid(periodEnd.AddHours(2), $"payment-{suffix}");
                }

                context.EntryCommissionPeriods.Add(period);
                context.EntryWeeklyCommissions.Add(commission);
                await context.SaveChangesAsync();
            });
        }

        private async Task<IReadOnlyCollection<EntryMonthlyObligation>> CreateObligationAsync(
            EntryParticipation participation,
            string status)
        {
            return await UsingDbContextAsync(1, async context =>
            {
                var dueAt = EffectiveFrom.AddMonths(1);
                var obligation = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    8,
                    dueAt);
                if (status == "Overdue")
                {
                    obligation.AssessStatus(dueAt.AddDays(8));
                }
                else if (status == "Paid")
                {
                    var payment = MemberPayment.CreatePending(
                        1,
                        participation.CustomerId,
                        MemberPaymentPurpose.EntryMonthlyCommitment,
                        600m,
                        "Test",
                        $"monthly-{Guid.NewGuid():N}",
                        dueAt.AddHours(1));
                    payment.Confirm(dueAt.AddHours(2));
                    obligation.ApplyConfirmedPayment(payment);
                    context.MemberPayments.Add(payment);
                }

                context.EntryMonthlyObligations.Add(obligation);
                await context.SaveChangesAsync();
                return new List<EntryMonthlyObligation> { obligation };
            });
        }

        private async Task CreateFuneralCoverAsync(
            EntryParticipation participation)
        {
            await UsingDbContextAsync(1, async context =>
            {
                var terms = AQGreenFuneralCoverTerms.Create(
                    "2026-08-funeral-30000",
                    EffectiveFrom,
                    30000m);
                var entitlement =
                    AQGreenFuneralCoverEntitlement.GrantForJoiningCompletion(
                        participation,
                        terms,
                        EffectiveFrom.AddDays(1));
                context.AQGreenFuneralCoverEntitlements.Add(entitlement);
                await context.SaveChangesAsync();
            });
        }
    }
}

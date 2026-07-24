using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Admin.Commissions;
using AqualLifeStyle.Application.Admin.Commissions.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace AqualLifeStyle.Tests.Application
{
    public class AdminCommissionAppServiceTests : AqualLifeStyleTestBase
    {
        private readonly IAdminCommissionAppService _service;

        public AdminCommissionAppServiceTests()
        {
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
            earnedCommission.ProgrammeName.ShouldBe("Entry");
            earnedCommission.HighestQualifiedLevel.ShouldBe(1);
            earnedCommission.TotalAmount.ShouldBe(150m);
            earnedCommission.Currency.ShouldBe("ZAR");
            earnedCommission.Status.ShouldBe("Earned — awaiting release");
            earnedCommission.Components.Single().Level.ShouldBe(1);
        }

        private async Task CreateQualifiedLevelOneEntryNetworkAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            for (var index = 0; index < 6; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"commission-{index}-{suffix}",
                    $"commission-{index}-{suffix}@example.com"));
            }

            var closedWeek = Resolve<LatestClosedCommissionWeekResolver>()
                .Resolve(DateTime.UtcNow);
            var activatedAt = closedWeek.PeriodStartUtc.AddMinutes(1);
            var programmeTerms =
                Resolve<ICurrentProgrammeTermsProvider>().GetEntryTerms();

            await UsingDbContextAsync(1, async context =>
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

                for (var index = 1; index < customers.Count; index++)
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
                }
            });
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
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                terms.RegistrationPaymentAmount,
                confirmedAt,
                $"commission-registration-{index}-{suffix}");
            var activation = ConfirmPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                terms.ActivationPaymentAmount,
                confirmedAt,
                $"commission-activation-{index}-{suffix}");
            participation.ApplyConfirmedActivationPayment(registration);
            participation.ApplyConfirmedActivationPayment(activation);
            context.MemberPayments.AddRange(registration, activation);
        }

        private static MemberPayment ConfirmPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            DateTime confirmedAt,
            string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Test",
                externalReference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
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

        private static readonly OnyxCommissionTerms ApprovedOnyxCommissionTerms =
            OnyxCommissionTerms.Create(
                "onyx-commission-2026-07-levels-1-5",
                EffectiveFrom,
                50m,
                20m,
                12.62m,
                5m,
                4m);

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
            earning.HighestQualifiedLevel.ShouldBe(1);
            earning.HighestCommissionedLevel.ShouldBe(1);
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
            progress.Education.Single(item => item.Title == "Build your network")
                .Body.ShouldContain("Level 3 is the final AQGreen level");
            progress.Education.Single(item => item.Title == "Weekly earnings")
                .Body.ShouldContain("AQGreen ends at Level 3");
        }

        [Fact]
        public async Task DisabledV2Gate_PreservesLegacyMaxValueNetworkProjection()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-v1-cutoff-{suffix}",
                $"progress-v1-cutoff-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "V1 Cutoff Member",
                    new EmailAddress($"progress-v1-cutoff-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });
            var participation = await CreateActiveParticipationAsync(
                customerId,
                $"v1-cutoff-{suffix}");
            await CreateDirectRecruitsAsync(
                participation,
                $"v1-cutoff-recruit-{suffix}",
                DateTime.UtcNow.AddDays(1));
            SetCurrentUser(userId, 1);

            var progress = await _progressService.GetMyProgressAsync();

            progress.QualifiedLevel.ShouldBe(1);
            progress.StructuralProgress.ShouldBeNull();
            progress.DirectRecruits.ShouldBe(5);
        }

        [Fact]
        public async Task LevelThreeEarning_ExposesTheAQGreenMaximum()
        {
            var userId = await CreateActiveMemberWithLevelThreeEarningAsync();
            SetCurrentUser(userId, 1);

            var progress = await _progressService.GetMyProgressAsync();

            var earning = progress.RecentEarnings.Single();
            earning.HighestLevel.ShouldBe(3);
            earning.HighestQualifiedLevel.ShouldBe(3);
            earning.HighestCommissionedLevel.ShouldBe(3);
            earning.TotalAmount.ShouldBe(1650m);
            earning.Components.Select(component => component.Level)
                .ShouldBe(new[] { 1, 2, 3 });
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

        [Fact]
        public async Task Journey_UsesLevelSpecificNetworkProgressAfterLevelOne()
        {
            var userId = await CreateActiveMemberWithNetworkAsync();
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var aqGreen = journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN");

            aqGreen.QualifiedLevel.ShouldBe(1);
            aqGreen.Levels[0].State.ShouldBe("Complete");
            aqGreen.Levels[0].AchievedCount.ShouldBe(5);
            aqGreen.Levels[1].State.ShouldBe("Current");
            aqGreen.Levels[1].AchievedCount.ShouldBe(0);
            aqGreen.Levels[1].RequiredCount.ShouldBe(25);
            aqGreen.Levels[1].RemainingCount.ShouldBe(25);
            aqGreen.Earnings.LatestRecordedWeek.Components.Single().Amount
                .ShouldBe(150m);
            aqGreen.Benefits.Single().State.ShouldBe("Included");

            var onyx = journey.Programmes.Single(item => item.ProgrammeCode == "ONYX");
            onyx.HasParticipation.ShouldBeFalse();
            onyx.Levels.Count.ShouldBe(5);
            onyx.Levels[0].CommissionRate.ShouldBe(50m);
            onyx.Levels[4].RequiredCount.ShouldBe(3125);
        }

        [Fact]
        public async Task Journey_NoParticipation_ShowsJoiningRequirementsWithoutInventingEarnings()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-new-{suffix}",
                $"journey-new-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                context.Customers.Add(Customer.Create(
                    1,
                    userId,
                    "New Journey Member",
                    new EmailAddress($"journey-customer-{suffix}@example.com")));
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();

            journey.Programmes.Count.ShouldBe(2);
            journey.Programmes.ShouldAllBe(item => !item.HasParticipation);
            journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN")
                .Joining.RequiredAmount.ShouldBe(1200m);
            journey.Programmes.Single(item => item.ProgrammeCode == "ONYX")
                .Joining.RequiredAmount.ShouldBe(6120m);
            journey.Programmes.ShouldAllBe(item => item.Earnings.LatestRecordedWeek == null);
        }

        [Fact]
        public async Task Journey_DoesNotResolveACustomerFromAnotherTenant()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-tenant-boundary-{suffix}",
                $"journey-tenant-boundary-{suffix}@example.com");
            await UsingDbContextAsync(2, async context =>
            {
                context.Customers.Add(Customer.Create(
                    2,
                    userId,
                    "Other Tenant Journey Member",
                    new EmailAddress($"journey-other-tenant-{suffix}@example.com")));
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var exception = await Should.ThrowAsync<Abp.UI.UserFriendlyException>(() =>
                _progressService.GetMyJourneyAsync());

            exception.Details.ShouldContain("active Club Member");
        }

        [Fact]
        public async Task Journey_HistoricalAQGreenPayments_AreProjectedAsComplete()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-historical-{suffix}",
                $"journey-historical-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Historical Journey Member",
                    new EmailAddress($"journey-historical-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var historicalTerms = EntryProgrammeTerms.Create(
                    "entry-historical",
                    EffectiveFrom,
                    registrationPaymentAmount: 600m,
                    activationPaymentAmount: 600m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    historicalTerms,
                    EffectiveFrom);
                var registration = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    $"historical-registration-{suffix}",
                    EffectiveFrom.AddMinutes(1));
                var activation = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    $"historical-activation-{suffix}",
                    EffectiveFrom.AddMinutes(2));
                participation.ApplyConfirmedActivationPayment(registration);
                participation.ApplyConfirmedActivationPayment(activation);
                participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
                context.EntryParticipations.Add(participation);
                context.MemberPayments.AddRange(registration, activation);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var aqGreen = journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN");

            aqGreen.Joining.RequiredAmount.ShouldBe(1200m);
            aqGreen.Joining.PaidAmount.ShouldBe(1200m);
            aqGreen.Joining.IsComplete.ShouldBeTrue();
            aqGreen.Joining.ScheduleLabel.ShouldBe("Historical two-stage payment");
            aqGreen.ActivationSteps.Single(item => item.Code == "Payment").State
                .ShouldBe("Complete");
            aqGreen.NextActionCode.ShouldNotBe("CompleteJoiningPayment");
            aqGreen.Benefits.Single().State.ShouldBe("Pending record");
        }

        [Fact]
        public async Task Journey_ModernAQGreenFirstInstalment_ShowsHalfPaid()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var startedAt = new DateTime(2026, 7, 26, 0, 0, 0, DateTimeKind.Utc);
            var userId = await CreateTestUserAsync(
                1,
                $"journey-instalment-{suffix}",
                $"journey-instalment-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "AQGreen Instalment Journey Member",
                    new EmailAddress($"journey-instalment-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var terms = EntryProgrammeTerms.CreateFlexibleJoiningPayment(
                    "aqgreen-instalment-journey",
                    startedAt,
                    joiningPaymentAmount: 1200m,
                    joiningInstallmentAmount: 600m,
                    monthlyCommitmentAmount: 600m,
                    gracePeriodDays: 7);
                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    terms,
                    startedAt);
                participation.SelectJoiningPaymentSchedule(
                    AQGreenJoiningPaymentSchedule.TwoInstallments);
                var payment = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    $"first-instalment-{suffix}",
                    startedAt.AddMinutes(1));
                participation.ApplyConfirmedJoiningPayment(
                    payment,
                    AQGreenJoiningPaymentStage.FirstInstallment);
                context.EntryParticipations.Add(participation);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var aqGreen = journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN");

            aqGreen.Joining.RequiredAmount.ShouldBe(1200m);
            aqGreen.Joining.PaidAmount.ShouldBe(600m);
            aqGreen.Joining.RemainingAmount.ShouldBe(600m);
            aqGreen.Joining.ProgressPercent.ShouldBe(50);
            aqGreen.Joining.IsComplete.ShouldBeFalse();
            aqGreen.Levels.ShouldAllBe(item => item.State == "Locked");
        }

        [Fact]
        public async Task Journey_DeclinedAQGreenApproval_IsTerminalAndIncludesReason()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-declined-{suffix}",
                $"journey-declined-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Declined Journey Member",
                    new EmailAddress($"journey-declined-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var participation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryTerms,
                    EffectiveFrom);
                var payment = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    $"declined-joining-{suffix}",
                    EffectiveFrom.AddMinutes(1),
                    1200m);
                participation.ApplyConfirmedJoiningPayment(payment);
                participation.RejectByAdministrator(
                    1L,
                    "Identity evidence requires correction.",
                    EffectiveFrom.AddMinutes(2));
                context.EntryParticipations.Add(participation);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var aqGreen = journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN");

            aqGreen.ParticipationStatus.ShouldBe("Declined");
            aqGreen.DecisionReason.ShouldBe("Identity evidence requires correction.");
            aqGreen.ActivationSteps.Single(item => item.Code == "Approval").State
                .ShouldBe("Declined");
            aqGreen.ActivationSteps.Single(item => item.Code == "Active").State
                .ShouldBe("Declined");
            aqGreen.ActivationSteps.Single(item => item.Code == "Active").Explanation
                .ShouldBe("Activation did not occur.");
        }

        [Fact]
        public async Task Journey_OnyxGraduation_DoesNotPresentALoanAsDirectPayment()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-graduate-{suffix}",
                $"journey-graduate-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Graduate Journey Member",
                    new EmailAddress($"journey-graduate-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-Graduate-{suffix}",
                    "Onyx graduation journey projection test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var historicalTerms = HistoricalEntryTerms();
                var root = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    historicalTerms,
                    EffectiveFrom);
                var registration = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryRegistration,
                    $"graduate-registration-{suffix}",
                    EffectiveFrom.AddMinutes(1));
                var activation = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.EntryActivation,
                    $"graduate-activation-{suffix}",
                    EffectiveFrom.AddMinutes(2));
                root.ApplyConfirmedActivationPayment(registration);
                root.ApplyConfirmedActivationPayment(activation);
                root.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));

                var transientNetwork = BuildTransientAQGreenNetwork(root, historicalTerms, 2);
                var loanTerms = OnyxLoanTerms.Create(
                    "onyx-loan-journey",
                    EffectiveFrom,
                    6120m,
                    30m,
                    3,
                    4,
                    200m);
                var loan = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                    root,
                    transientNetwork,
                    new EntryNetworkQualificationEvaluator(),
                    loanTerms,
                    EffectiveFrom.AddMinutes(4));
                loan.AcceptByMember(userId, "I accept the Onyx loan terms.", EffectiveFrom.AddMinutes(5));
                loan.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(6));
                var onyx = OnyxParticipation.GraduateFromAQGreenIndependently(
                    root,
                    loan,
                    membership.Id,
                    OnyxPlanTerms.Create("onyx-journey", EffectiveFrom, 6120m),
                    EffectiveFrom.AddMinutes(7));

                context.EntryParticipations.Add(root);
                context.MemberPayments.AddRange(registration, activation);
                context.OnyxLoanAgreements.Add(loan);
                context.OnyxParticipations.Add(onyx);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var onyx = journey.Programmes.Single(item => item.ProgrammeCode == "ONYX");

            onyx.Joining.Kind.ShouldBe("AQGreen graduation with an Onyx loan");
            onyx.Joining.RequiredAmount.ShouldBe(0m);
            onyx.Joining.PaidAmount.ShouldBe(0m);
            onyx.Joining.RemainingAmount.ShouldBe(0m);
            onyx.Joining.IsComplete.ShouldBeTrue();
            onyx.ActivationSteps.Single(item => item.Code == "Payment").Label
                .ShouldBe("Loan-backed admission");
        }

        [Fact]
        public async Task Journey_OnyxDirectPaymentAwaitingApproval_IsCompleteButInactive()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-onyx-approval-{suffix}",
                $"journey-onyx-approval-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Approval Journey Member",
                    new EmailAddress($"journey-onyx-approval-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-Approval-{suffix}",
                    "Onyx approval journey projection test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var participation = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    OnyxPlanTerms.Create("onyx-approval-journey", EffectiveFrom, 6120m),
                    EffectiveFrom);
                var payment = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    $"onyx-approval-joining-{suffix}",
                    EffectiveFrom.AddMinutes(1),
                    6120m);
                participation.ApplyConfirmedDirectEntryPayment(payment);
                context.MemberPayments.Add(payment);
                context.OnyxParticipations.Add(participation);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var onyx = journey.Programmes.Single(item => item.ProgrammeCode == "ONYX");

            onyx.ParticipationStatus.ShouldBe("Awaiting Area approval");
            onyx.IsActive.ShouldBeFalse();
            onyx.Joining.RequiredAmount.ShouldBe(6120m);
            onyx.Joining.PaidAmount.ShouldBe(6120m);
            onyx.Joining.IsComplete.ShouldBeTrue();
            onyx.NextActionCode.ShouldBe("AwaitApproval");
            onyx.Levels.ShouldAllBe(item => item.State == "Locked");
        }

        [Fact]
        public async Task Journey_OnyxEarnings_UsesPersistedCommissionLevelsAndComponents()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            for (var index = 0; index <= OnyxNetworkQualificationEvaluator.BranchSize; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"journey-onyx-earning-{index}-{suffix}",
                    $"journey-onyx-earning-{index}-{suffix}@example.com"));
            }

            await UsingDbContextAsync(1, async context =>
            {
                var customers = userIds.Select((userId, index) => Customer.Create(
                    1,
                    userId,
                    $"Onyx Earning Journey Member {index}",
                    new EmailAddress($"journey-onyx-earning-customer-{index}-{suffix}@example.com")))
                    .ToList();
                var membership = Membership.Create(
                    1,
                    $"Onyx-Earning-{suffix}",
                    "Onyx earning journey projection test",
                    MembershipType.Onyx);
                context.Customers.AddRange(customers);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var root = OnyxParticipation.StartDirectIndependently(
                    1,
                    customers[0].Id,
                    membership.Id,
                    OnyxPlanTerms.Create("onyx-earning-journey", EffectiveFrom, 6120m),
                    EffectiveFrom);
                var rootPayment = ConfirmPayment(
                    root.CustomerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    $"onyx-earning-root-{suffix}",
                    EffectiveFrom.AddMinutes(1),
                    6120m);
                root.ApplyConfirmedDirectEntryPayment(rootPayment);
                root.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));

                var network = new List<OnyxParticipation> { root };
                var payments = new List<MemberPayment> { rootPayment };
                for (var index = 1; index < customers.Count; index++)
                {
                    var recruit = OnyxParticipation.StartDirectUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        membership.Id,
                        OnyxPlanTerms.Create("onyx-earning-journey", EffectiveFrom, 6120m),
                        EffectiveFrom);
                    var payment = ConfirmPayment(
                        recruit.CustomerId,
                        MemberPaymentPurpose.OnyxDirectEntry,
                        $"onyx-earning-recruit-{index}-{suffix}",
                        EffectiveFrom.AddMinutes(1),
                        6120m);
                    recruit.ApplyConfirmedDirectEntryPayment(payment);
                    recruit.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));
                    network.Add(recruit);
                    payments.Add(payment);
                }

                var periodStart = EffectiveFrom.AddDays(5);
                var periodEnd = periodStart.AddDays(7).AddTicks(-1);
                var period = OnyxCommissionPeriod.CreateClosedPeriod(
                    1,
                    periodStart,
                    periodEnd,
                    "Africa/Johannesburg",
                    periodEnd.AddMinutes(1),
                    ApprovedOnyxCommissionTerms);
                var commission = new OnyxWeeklyCommissionCalculator(
                        new OnyxNetworkQualificationEvaluator())
                    .Calculate(root, period, ApprovedOnyxCommissionTerms, network);

                context.MemberPayments.AddRange(payments);
                context.OnyxParticipations.AddRange(network);
                context.OnyxCommissionPeriods.Add(period);
                context.OnyxWeeklyCommissions.Add(commission);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userIds[0], 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var earnings = journey.Programmes
                .Single(item => item.ProgrammeCode == "ONYX")
                .Earnings;

            earnings.TotalEarned.ShouldBe(250m);
            earnings.EarnedAwaitingRelease.ShouldBe(250m);
            earnings.LatestRecordedWeek.Status.ShouldBe("Earned — awaiting release");
            earnings.LatestRecordedWeek.QualifiedLevel.ShouldBe(1);
            earnings.LatestRecordedWeek.CommissionedLevel.ShouldBe(1);
            earnings.LatestRecordedWeek.Components.Single().Level.ShouldBe(1);
            earnings.LatestRecordedWeek.Components.Single().Amount.ShouldBe(250m);
        }

        [Fact]
        public async Task Journey_OnyxTravelBenefit_UsesPersistedEntitlementState()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"journey-travel-{suffix}",
                $"journey-travel-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Travel Journey Member",
                    new EmailAddress($"journey-travel-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-Travel-Journey-{suffix}",
                    "Onyx travel journey projection test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var onyxTerms = OnyxPlanTerms.Create("onyx-travel-journey", EffectiveFrom, 6120m);
                var participation = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    onyxTerms,
                    EffectiveFrom);
                var payment = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    $"onyx-travel-joining-{suffix}",
                    EffectiveFrom.AddMinutes(1),
                    6120m);
                participation.ApplyConfirmedDirectEntryPayment(payment);
                participation.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(2));
                var benefit = OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                    participation,
                    OnyxNetworkLevel.Level3,
                    OnyxTravelBenefitTerms.Create(
                        "onyx-travel-benefit-journey",
                        EffectiveFrom,
                        OnyxNetworkLevel.Level3,
                        3,
                        10m),
                    EffectiveFrom.AddDays(1));

                context.MemberPayments.Add(payment);
                context.OnyxParticipations.Add(participation);
                context.OnyxTravelBenefitEntitlements.Add(benefit);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(userId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var onyx = journey.Programmes
                .Single(item => item.ProgrammeCode == "ONYX");
            var benefitProjection = onyx.Benefits.Single();

            onyx.IsActive.ShouldBeTrue();
            onyx.Joining.RequiredAmount.ShouldBe(6120m);
            onyx.Joining.PaidAmount.ShouldBe(6120m);
            onyx.Joining.IsComplete.ShouldBeTrue();
            onyx.Levels.Count.ShouldBe(5);
            onyx.Levels[4].RequiredCount.ShouldBe(3125);
            onyx.NextActionCode.ShouldBe("InviteMembers");
            benefitProjection.State.ShouldBe("Waiting period");
            benefitProjection.UnlockedAt.ShouldBe(EffectiveFrom.AddDays(1));
            benefitProjection.AvailableAt.ShouldBe(EffectiveFrom.AddDays(1).AddMonths(3));
            benefitProjection.Description.ShouldContain("contribute 10%");
        }

        [Fact]
        public async Task Journey_ActiveCrossAreaRecruit_ContributesToNetworkProgress()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var rootUserId = await CreateTestUserAsync(
                1,
                $"journey-area-root-{suffix}",
                $"journey-area-root-{suffix}@example.com");
            var rootCustomerId = await UsingDbContextAsync(1, async context =>
            {
                var rootArea = Area.Create(1, $"R{suffix[..6]}", "Root Area");
                var customer = Customer.Create(
                    1,
                    rootUserId,
                    "Root Area Journey Member",
                    new EmailAddress($"journey-area-root-customer-{suffix}@example.com"));
                customer.AssignInitialArea(rootArea, EffectiveFrom, "Test baseline");
                context.Areas.Add(rootArea);
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });
            var root = await CreateActiveParticipationAsync(
                rootCustomerId,
                $"area-root-{suffix}");

            var recruitUserId = await CreateTestUserAsync(
                1,
                $"journey-area-recruit-{suffix}",
                $"journey-area-recruit-{suffix}@example.com");
            await UsingDbContextAsync(1, async context =>
            {
                var recruitArea = Area.Create(1, $"C{suffix[..6]}", "Recruit Area");
                var customer = Customer.Create(
                    1,
                    recruitUserId,
                    "Recruit Area Journey Member",
                    new EmailAddress($"journey-area-recruit-customer-{suffix}@example.com"));
                customer.AssignInitialArea(recruitArea, EffectiveFrom, "Test baseline");
                context.Areas.Add(recruitArea);
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var crossAreaRecruit = EntryParticipation.StartUnderRecruiter(
                    1,
                    customer.Id,
                    root,
                    EntryTerms,
                    EffectiveFrom.AddMinutes(1));
                var payment = ConfirmPayment(
                    customer.Id,
                    MemberPaymentPurpose.AQGreenJoining,
                    $"tenant-two-joining-{suffix}",
                    EffectiveFrom.AddMinutes(2),
                    1200m,
                    1);
                crossAreaRecruit.ApplyConfirmedJoiningPayment(payment);
                crossAreaRecruit.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
                context.EntryParticipations.Add(crossAreaRecruit);
                context.MemberPayments.Add(payment);
                await context.SaveChangesAsync();
            });
            SetCurrentUser(rootUserId, 1);

            var journey = await _progressService.GetMyJourneyAsync();
            var aqGreen = journey.Programmes.Single(item => item.ProgrammeCode == "AQGREEN");

            aqGreen.Levels[0].AchievedCount.ShouldBe(1);
            aqGreen.QualifiedLevel.ShouldBe(0);
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

        private async Task<long> CreateActiveMemberWithLevelThreeEarningAsync()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"progress-level-three-{suffix}",
                $"progress-level-three-{suffix}@example.com");
            var customerId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Level Three Progress Member",
                    new EmailAddress(
                        $"progress-level-three-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();
                return customer.Id;
            });
            var participation = await CreateActiveParticipationAsync(
                customerId,
                $"progress-level-three-{suffix}");
            var network = BuildInMemoryNetwork(
                participation,
                maxDepth: EntryNetworkQualificationEvaluator.MaximumLevel,
                suffix);

            await UsingDbContextAsync(1, async context =>
            {
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
                        Array.Empty<EntryMonthlyObligation>());

                context.EntryCommissionPeriods.Add(period);
                context.EntryWeeklyCommissions.Add(commission);
                await context.SaveChangesAsync();
            });

            return userId;
        }

        private static IReadOnlyCollection<EntryParticipation> BuildInMemoryNetwork(
            EntryParticipation root,
            int maxDepth,
            string suffix)
        {
            var participations = new List<EntryParticipation> { root };
            var currentLevel = new List<EntryParticipation> { root };
            var nextCustomerId = 100000;

            for (var depth = 1; depth <= maxDepth; depth++)
            {
                var nextLevel = new List<EntryParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < EntryNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var recruit = EntryParticipation.StartUnderRecruiter(
                            1,
                            nextCustomerId,
                            recruiter,
                            EntryTerms,
                            EffectiveFrom.AddMinutes(depth));
                        var payment = MemberPayment.CreatePending(
                            1,
                            nextCustomerId,
                            MemberPaymentPurpose.AQGreenJoining,
                            1200m,
                            "Test",
                            $"level-three-{suffix}-{nextCustomerId}",
                            EffectiveFrom.AddMinutes(depth));
                        payment.Confirm(EffectiveFrom.AddMinutes(depth + 1));
                        recruit.ApplyConfirmedJoiningPayment(payment);
                        recruit.ApproveByAdministrator(
                            1L,
                            EffectiveFrom.AddMinutes(depth + 2));
                        participations.Add(recruit);
                        nextLevel.Add(recruit);
                        nextCustomerId++;
                    }
                }

                currentLevel = nextLevel;
            }

            return participations;
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

        private static MemberPayment ConfirmPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            string reference,
            DateTime confirmedAt,
            decimal amount = 600m,
            int tenantId = 1)
        {
            var payment = MemberPayment.CreatePending(
                tenantId,
                customerId,
                purpose,
                amount,
                "Test",
                reference,
                confirmedAt.AddMinutes(-1));
            payment.Confirm(confirmedAt);
            return payment;
        }

        private static EntryProgrammeTerms HistoricalEntryTerms() =>
            EntryProgrammeTerms.Create(
                "entry-historical-journey",
                EffectiveFrom,
                registrationPaymentAmount: 600m,
                activationPaymentAmount: 600m,
                monthlyCommitmentAmount: 600m,
                gracePeriodDays: 7);

        private static IReadOnlyCollection<EntryParticipation> BuildTransientAQGreenNetwork(
            EntryParticipation root,
            EntryProgrammeTerms terms,
            int maximumDepth)
        {
            var all = new List<EntryParticipation> { root };
            var current = new List<EntryParticipation> { root };
            var customerId = 100000;
            for (var depth = 1; depth <= maximumDepth; depth++)
            {
                var next = new List<EntryParticipation>();
                foreach (var recruiter in current)
                {
                    for (var index = 0; index < EntryNetworkQualificationEvaluator.BranchSize; index++)
                    {
                        var recruit = EntryParticipation.StartUnderRecruiter(
                            1,
                            customerId++,
                            recruiter,
                            terms,
                            EffectiveFrom);
                        var registration = ConfirmPayment(
                            recruit.CustomerId,
                            MemberPaymentPurpose.EntryRegistration,
                            $"transient-registration-{recruit.CustomerId}",
                            EffectiveFrom.AddMinutes(1));
                        var activation = ConfirmPayment(
                            recruit.CustomerId,
                            MemberPaymentPurpose.EntryActivation,
                            $"transient-activation-{recruit.CustomerId}",
                            EffectiveFrom.AddMinutes(2));
                        recruit.ApplyConfirmedActivationPayment(registration);
                        recruit.ApplyConfirmedActivationPayment(activation);
                        recruit.ApproveByAdministrator(1L, EffectiveFrom.AddMinutes(3));
                        all.Add(recruit);
                        next.Add(recruit);
                    }
                }
                current = next;
            }
            return all;
        }

        private async Task<List<int>> CreateDirectRecruitsAsync(
            EntryParticipation recruiterParticipation,
            string suffix,
            DateTime? activationAtBase = null)
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
                    var startedAt = activationAtBase.HasValue
                        ? activationAtBase.Value.AddMinutes(-3)
                        : EffectiveFrom.AddMinutes(1);
                    var confirmedAt = activationAtBase.HasValue
                        ? activationAtBase.Value.AddMinutes(-1)
                        : EffectiveFrom.AddMinutes(3);
                    var approvedAt = activationAtBase ?? EffectiveFrom.AddMinutes(4);
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        recruitCustomerId,
                        recruiterParticipation,
                        EntryTerms,
                        startedAt);
                    var payment = MemberPayment.CreatePending(
                        1,
                        recruitCustomerId,
                        MemberPaymentPurpose.AQGreenJoining,
                        1200m,
                        "Test",
                        $"recruit-joining-{index}-{suffix}",
                        confirmedAt.AddMinutes(-1));
                    payment.Confirm(confirmedAt);
                    recruit.ApplyConfirmedJoiningPayment(payment);
                    recruit.ApproveByAdministrator(1L, approvedAt);
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
                var dueAt = status == "Overdue"
                    ? EffectiveFrom.AddDays(1)
                    : EffectiveFrom.AddMonths(1);
                context.EntryMonthlyObligationDuePolicies.Add(
                    EntryMonthlyObligationDuePolicy.Create(
                        "due-policy-v1",
                        1,
                        EntryMonthlyObligationDuePolicy.JohannesburgMonthStartUtc(2026, 8)));
                var obligation = EntryMonthlyObligation.Create(
                    participation,
                    2026,
                    8,
                    dueAt,
                    "due-policy-v1");
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

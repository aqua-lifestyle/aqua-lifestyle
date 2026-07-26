using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AqualLifeStyle.Tests.EntityFrameworkCore
{
    public class OnyxProgrammePersistenceTests : AqualLifeStyleTestBase
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly EntryProgrammeTerms EntryTerms = EntryProgrammeTerms.Create(
            version: "2026-07",
            effectiveFrom: EffectiveFrom,
            registrationPaymentAmount: 600m,
            activationPaymentAmount: 600m,
            monthlyCommitmentAmount: 600m,
            gracePeriodDays: 7);

        private static readonly OnyxPlanTerms OnyxTerms = OnyxPlanTerms.Create(
            version: "2026-07",
            effectiveFrom: EffectiveFrom,
            directEntryAmount: 6120m);

        private static readonly EntryCommissionTerms CommissionTerms =
            EntryCommissionTerms.Create(
                "2026-07",
                EffectiveFrom,
                150m,
                250m,
                1250m);

        private static readonly OnyxLoanTerms LoanTerms =
            OnyxLoanTerms.Create(
                "2026-07",
                EffectiveFrom,
                6120m,
                30m,
                3,
                4,
                200m);

        private static readonly OnyxCommissionTerms ApprovedOnyxCommissionTerms =
            OnyxCommissionTerms.Create(
                "onyx-commission-2026-07-levels-1-5",
                EffectiveFrom,
                50m,
                20m,
                12.62m,
                5m,
                4m);

        private static readonly OnyxTravelBenefitTerms TravelBenefitTerms =
            OnyxTravelBenefitTerms.Create(
                "onyx-travel-2026-07",
                EffectiveFrom,
                OnyxNetworkLevel.Level3,
                3,
                10m);

        [Fact]
        public async Task ProgrammeParticipationAndPaymentHistory_RoundTripsAndRejectsDuplicateProviderReference()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var recruiterUserId = await CreateTestUserAsync(
                1,
                $"recruiter-{suffix}",
                $"recruiter-{suffix}@example.com");
            var participantUserId = await CreateTestUserAsync(
                1,
                $"participant-{suffix}",
                $"participant-{suffix}@example.com");

            var persisted = await UsingDbContextAsync(1, async context =>
            {
                var recruiterCustomer = Customer.Create(
                    1,
                    recruiterUserId,
                    "Entry Recruiter",
                    new EmailAddress($"recruiter-customer-{suffix}@example.com"));
                var participantCustomer = Customer.Create(
                    1,
                    participantUserId,
                    "Independent Participant",
                    new EmailAddress($"participant-customer-{suffix}@example.com"));
                var onyxMembership = Membership.Create(
                    1,
                    $"Onyx-{suffix}",
                    "Onyx direct-entry plan",
                    MembershipType.Onyx);

                context.Customers.AddRange(recruiterCustomer, participantCustomer);
                context.Memberships.Add(onyxMembership);
                await context.SaveChangesAsync();

                var recruiterEntry = EntryParticipation.StartIndependently(
                    1,
                    recruiterCustomer.Id,
                    EntryTerms,
                    EffectiveFrom);
                var recruiterPayments = CreateAndApplyEntryPayments(
                    recruiterEntry,
                    $"entry-recruiter-registration-{suffix}",
                    $"entry-recruiter-activation-{suffix}");

                var participantEntry = EntryParticipation.StartUnderRecruiter(
                    1,
                    participantCustomer.Id,
                    recruiterEntry,
                    EntryTerms,
                    EffectiveFrom);
                var participantPayments = CreateAndApplyEntryPayments(
                    participantEntry,
                    $"entry-participant-registration-{suffix}",
                    $"entry-participant-activation-{suffix}");
                participantEntry.CorrectToIndependent(
                    recruiterUserId,
                    "The customer confirmed that they joined independently.",
                    EffectiveFrom.AddDays(1));

                var onyxParticipation = OnyxParticipation.StartDirectIndependently(
                    1,
                    participantCustomer.Id,
                    onyxMembership.Id,
                    OnyxTerms,
                    EffectiveFrom);
                var onyxPayment = CreateConfirmedPayment(
                    participantCustomer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    $"onyx-direct-{suffix}");
                onyxParticipation.ApplyConfirmedDirectEntryPayment(onyxPayment);

                var monthlyObligation = EntryMonthlyObligation.Create(
                    participantEntry,
                    2026,
                    8,
                    EffectiveFrom.AddMonths(1));
                monthlyObligation.AssessStatus(EffectiveFrom.AddMonths(1).AddDays(8));
                var monthlyPayment = CreateConfirmedPayment(
                    participantCustomer.Id,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    600m,
                    $"entry-monthly-{suffix}",
                    EffectiveFrom.AddMonths(1).AddDays(9));
                monthlyObligation.ApplyConfirmedPayment(monthlyPayment);

                var commissionPeriodStart = EffectiveFrom.AddDays(5);
                var commissionPeriodEnd = commissionPeriodStart.AddDays(7).AddTicks(-1);
                var commissionPeriod = EntryCommissionPeriod.CreateClosedPeriod(
                    1,
                    commissionPeriodStart,
                    commissionPeriodEnd,
                    "Africa/Johannesburg",
                    commissionPeriodEnd.AddMinutes(1),
                    CommissionTerms);
                var network = new List<EntryParticipation> { participantEntry };
                for (var index = 0; index < EntryNetworkQualificationEvaluator.BranchSize; index++)
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        10000 + index,
                        participantEntry,
                        EntryTerms,
                        EffectiveFrom);
                    var recruitPayments = CreateAndApplyEntryPayments(
                        recruit,
                        $"transient-registration-{index}-{suffix}",
                        $"transient-activation-{index}-{suffix}");
                    network.Add(recruit);
                }

                var weeklyCommission = new EntryWeeklyCommissionCalculator(
                    new EntryNetworkQualificationEvaluator())
                    .Calculate(
                        participantEntry,
                        commissionPeriod,
                        CommissionTerms,
                        network,
                        new[] { monthlyObligation });

                context.MemberPayments.AddRange(
                    recruiterPayments.Registration,
                    recruiterPayments.Activation,
                    participantPayments.Registration,
                    participantPayments.Activation,
                    onyxPayment,
                    monthlyPayment);
                context.EntryParticipations.AddRange(recruiterEntry, participantEntry);
                context.EntryMonthlyObligations.Add(monthlyObligation);
                context.EntryCommissionPeriods.Add(commissionPeriod);
                context.EntryWeeklyCommissions.Add(weeklyCommission);
                context.OnyxParticipations.Add(onyxParticipation);
                await context.SaveChangesAsync();

                return new
                {
                    ParticipantCustomerId = participantCustomer.Id,
                    ParticipantEntryId = participantEntry.Id,
                    MonthlyObligationId = monthlyObligation.Id,
                    WeeklyCommissionId = weeklyCommission.Id,
                    OnyxParticipationId = onyxParticipation.Id,
                    OnyxPaymentReference = onyxPayment.ExternalReference
                };
            });

            await UsingDbContextAsync(1, async context =>
            {
                var entry = await context.EntryParticipations
                    .Include(participation => participation.RecruiterCorrections)
                    .SingleAsync(participation => participation.Id == persisted.ParticipantEntryId);
                var onyx = await context.OnyxParticipations
                    .SingleAsync(participation => participation.Id == persisted.OnyxParticipationId);
                var monthlyObligation = await context.EntryMonthlyObligations
                    .SingleAsync(obligation => obligation.Id == persisted.MonthlyObligationId);
                var weeklyCommission = await context.EntryWeeklyCommissions
                    .Include(commission => commission.Components)
                    .SingleAsync(commission => commission.Id == persisted.WeeklyCommissionId);
                var payments = await context.MemberPayments
                    .Where(payment => payment.CustomerId == persisted.ParticipantCustomerId)
                    .ToListAsync();

                Assert.True(entry.JoinedIndependently);
                Assert.Equal(EntryParticipationStatus.Active, entry.Status);
                var correction = Assert.Single(entry.RecruiterCorrections);
                Assert.NotNull(correction.PreviousRecruiterCustomerId);
                Assert.Null(correction.NewRecruiterCustomerId);

                Assert.True(onyx.JoinedIndependently);
                Assert.Equal(OnyxParticipationStatus.Active, onyx.Status);
                Assert.Equal(EntryMonthlyObligationStatus.Paid, monthlyObligation.Status);
                Assert.Equal(0m, monthlyObligation.OutstandingAmount);
                Assert.NotNull(monthlyObligation.MarkedOverdueAt);
                Assert.True(monthlyObligation.IsOwnPayoutEligible);
                Assert.Equal(WeeklyCommissionPayoutStatus.Earned, weeklyCommission.PayoutStatus);
                Assert.Equal(150m, weeklyCommission.TotalAmount);
                var component = Assert.Single(weeklyCommission.Components);
                Assert.Equal(1, component.Level);
                Assert.Equal(150m, component.Amount);
                Assert.Equal(4, payments.Count);
                Assert.All(payments, payment => Assert.Equal("YOCO", payment.Provider));
            });

            using var duplicateContext = LocalIocManager.Resolve<AqualLifeStyleDbContext>();
            var duplicatePayment = MemberPayment.CreatePending(
                1,
                persisted.ParticipantCustomerId,
                MemberPaymentPurpose.SavingsContribution,
                100m,
                "yoco",
                persisted.OnyxPaymentReference,
                EffectiveFrom.AddDays(2));
            duplicateContext.MemberPayments.Add(duplicatePayment);

            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        }

        [Fact]
        public async Task LoanAgreement_RequirementsAndRepayment_RoundTrip()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var memberUserId = await CreateTestUserAsync(
                1,
                $"funded-member-{suffix}",
                $"funded-member-{suffix}@example.com");

            var loanAgreementId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    memberUserId,
                    "Funded Entry Member",
                    new EmailAddress($"funded-customer-{suffix}@example.com"));
                context.Customers.Add(customer);
                await context.SaveChangesAsync();

                var entryParticipation = EntryParticipation.StartIndependently(
                    1,
                    customer.Id,
                    EntryTerms,
                    EffectiveFrom);
                var activationPayments = CreateAndApplyEntryPayments(
                    entryParticipation,
                    $"loan-entry-registration-{suffix}",
                    $"loan-entry-activation-{suffix}");

                var network = BuildLevelTwoNetwork(entryParticipation, suffix);
                var agreement = OnyxLoanAgreement.OfferToEligibleEntryParticipant(
                    entryParticipation,
                    network,
                    new EntryNetworkQualificationEvaluator(),
                    LoanTerms,
                    EffectiveFrom.AddDays(1));
                agreement.AcceptByMember(
                    memberUserId,
                    "I accept the Onyx loan terms.",
                    EffectiveFrom.AddDays(2));
                agreement.ApproveByAdministrator(1, EffectiveFrom.AddDays(3));

                var repayment = CreateConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.OnyxLoanRepayment,
                    200m,
                    $"loan-repayment-{suffix}",
                    EffectiveFrom.AddDays(4));
                agreement.ApplyConfirmedRepayment(repayment, weeklyRequirementNumber: 1);

                context.MemberPayments.AddRange(
                    activationPayments.Registration,
                    activationPayments.Activation,
                    repayment);
                context.EntryParticipations.Add(entryParticipation);
                context.OnyxLoanAgreements.Add(agreement);
                await context.SaveChangesAsync();

                return agreement.Id;
            });

            await UsingDbContextAsync(1, async context =>
            {
                var agreement = await context.OnyxLoanAgreements
                    .Include(item => item.WeeklyRequirements)
                    .Include(item => item.Repayments)
                    .SingleAsync(item => item.Id == loanAgreementId);

                Assert.Equal(OnyxLoanAgreementStatus.Active, agreement.Status);
                Assert.Equal(7756m, agreement.OutstandingAmount);
                Assert.Equal(4, agreement.WeeklyRequirements.Count);
                Assert.Equal(
                    OnyxLoanWeeklyRequirementStatus.Satisfied,
                    agreement.WeeklyRequirements.Single(
                        requirement => requirement.RequirementNumber == 1).Status);
                var repayment = Assert.Single(agreement.Repayments);
                Assert.Equal(1, repayment.WeeklyRequirementNumber);
            });
        }

        [Fact]
        public async Task OnyxLevelOneCommission_RoundTripsInItsOwnLedger()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userIds = new List<long>();
            for (var index = 0; index <= OnyxNetworkQualificationEvaluator.BranchSize; index++)
            {
                userIds.Add(await CreateTestUserAsync(
                    1,
                    $"onyx-network-{index}-{suffix}",
                    $"onyx-network-{index}-{suffix}@example.com"));
            }

            var commissionId = await UsingDbContextAsync(1, async context =>
            {
                var customers = userIds
                    .Select((userId, index) => Customer.Create(
                        1,
                        userId,
                        $"Onyx Member {index}",
                        new EmailAddress($"onyx-member-{index}-{suffix}@example.com")))
                    .ToList();
                var membership = Membership.Create(
                    1,
                    $"Onyx-Network-{suffix}",
                    "Onyx network persistence test",
                    MembershipType.Onyx);
                context.Customers.AddRange(customers);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var root = OnyxParticipation.StartDirectIndependently(
                    1,
                    customers[0].Id,
                    membership.Id,
                    OnyxTerms,
                    EffectiveFrom);
                var rootPayment = CreateConfirmedPayment(
                    root.CustomerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    $"onyx-network-root-{suffix}");
                root.ApplyConfirmedDirectEntryPayment(rootPayment);

                var network = new List<OnyxParticipation> { root };
                var payments = new List<MemberPayment> { rootPayment };
                for (var index = 1; index < customers.Count; index++)
                {
                    var recruit = OnyxParticipation.StartDirectUnderRecruiter(
                        1,
                        customers[index].Id,
                        root,
                        membership.Id,
                        OnyxTerms,
                        EffectiveFrom);
                    var payment = CreateConfirmedPayment(
                        recruit.CustomerId,
                        MemberPaymentPurpose.OnyxDirectEntry,
                        6120m,
                        $"onyx-network-recruit-{index}-{suffix}");
                    recruit.ApplyConfirmedDirectEntryPayment(payment);
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

                return commission.Id;
            });

            await UsingDbContextAsync(1, async context =>
            {
                var commission = await context.OnyxWeeklyCommissions
                    .Include(item => item.Components)
                    .SingleAsync(item => item.Id == commissionId);

                Assert.Equal(1, commission.HighestQualifiedNetworkLevel);
                Assert.Equal(1, commission.HighestCommissionedLevel);
                Assert.Equal(250m, commission.TotalAmount);
                Assert.Equal(WeeklyCommissionPayoutStatus.Earned, commission.PayoutStatus);
                var component = Assert.Single(commission.Components);
                Assert.Equal(1, component.Level);
                Assert.Equal(250m, component.Amount);
            });
        }

        [Fact]
        public async Task OnyxTravelBenefitEntitlement_RoundTripsAfterWaitingPeriod()
        {
            var suffix = Guid.NewGuid().ToString("N");
            var userId = await CreateTestUserAsync(
                1,
                $"onyx-traveller-{suffix}",
                $"onyx-traveller-{suffix}@example.com");

            var entitlementId = await UsingDbContextAsync(1, async context =>
            {
                var customer = Customer.Create(
                    1,
                    userId,
                    "Onyx Traveller",
                    new EmailAddress($"onyx-traveller-customer-{suffix}@example.com"));
                var membership = Membership.Create(
                    1,
                    $"Onyx-Travel-{suffix}",
                    "Onyx travel benefit persistence test",
                    MembershipType.Onyx);
                context.Customers.Add(customer);
                context.Memberships.Add(membership);
                await context.SaveChangesAsync();

                var root = OnyxParticipation.StartDirectIndependently(
                    1,
                    customer.Id,
                    membership.Id,
                    OnyxTerms,
                    EffectiveFrom);
                var rootPayment = CreateConfirmedPayment(
                    customer.Id,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    6120m,
                    $"onyx-travel-root-{suffix}");
                root.ApplyConfirmedDirectEntryPayment(rootPayment);
                var network = BuildTransientCompleteOnyxNetwork(
                    root,
                    membership.Id,
                    maximumDepth: 3,
                    referenceSuffix: suffix);
                var entitlement =
                    OnyxTravelBenefitEntitlement.GrantForQualifiedParticipant(
                        root,
                        network,
                        new OnyxNetworkQualificationEvaluator(),
                        TravelBenefitTerms,
                        EffectiveFrom.AddDays(10));
                entitlement.ActivateAfterWaitingPeriod(
                    entitlement.WaitingPeriodEndsAt);

                context.MemberPayments.Add(rootPayment);
                context.OnyxParticipations.Add(root);
                context.OnyxTravelBenefitEntitlements.Add(entitlement);
                await context.SaveChangesAsync();

                return entitlement.Id;
            });

            await UsingDbContextAsync(1, async context =>
            {
                var entitlement = await context.OnyxTravelBenefitEntitlements
                    .SingleAsync(item => item.Id == entitlementId);

                Assert.Equal(OnyxNetworkLevel.Level3, entitlement.QualifiedNetworkLevel);
                Assert.Equal(OnyxTravelBenefitStatus.Active, entitlement.Status);
                Assert.Equal(10m, entitlement.MemberTripContributionPercent);
                Assert.Equal(
                    entitlement.WaitingPeriodEndsAt,
                    entitlement.ActivatedAt);
            });
        }

        private static List<OnyxParticipation> BuildTransientCompleteOnyxNetwork(
            OnyxParticipation root,
            int membershipId,
            int maximumDepth,
            string referenceSuffix)
        {
            var network = new List<OnyxParticipation> { root };
            var currentLevel = new List<OnyxParticipation> { root };
            var nextCustomerId = 30000;

            for (var depth = 1; depth <= maximumDepth; depth++)
            {
                var nextLevel = new List<OnyxParticipation>();
                foreach (var recruiter in currentLevel)
                {
                    for (var index = 0;
                         index < OnyxNetworkQualificationEvaluator.BranchSize;
                         index++)
                    {
                        var recruit = OnyxParticipation.StartDirectUnderRecruiter(
                            1,
                            nextCustomerId++,
                            recruiter,
                            membershipId,
                            OnyxTerms,
                            EffectiveFrom);
                        var payment = CreateConfirmedPayment(
                            recruit.CustomerId,
                            MemberPaymentPurpose.OnyxDirectEntry,
                            6120m,
                            $"onyx-travel-network-{recruit.CustomerId}-{referenceSuffix}");
                        recruit.ApplyConfirmedDirectEntryPayment(payment);
                        network.Add(recruit);
                        nextLevel.Add(recruit);
                    }
                }

                currentLevel = nextLevel;
            }

            return network;
        }

        private static List<EntryParticipation> BuildLevelTwoNetwork(
            EntryParticipation root,
            string referenceSuffix)
        {
            var network = new List<EntryParticipation> { root };
            var firstLevel = new List<EntryParticipation>();
            var nextCustomerId = 20000;

            for (var index = 0; index < EntryNetworkQualificationEvaluator.BranchSize; index++)
            {
                var recruit = EntryParticipation.StartUnderRecruiter(
                    1,
                    nextCustomerId++,
                    root,
                    EntryTerms,
                    EffectiveFrom);
                CreateAndApplyEntryPayments(
                    recruit,
                    $"loan-network-l1-registration-{index}-{referenceSuffix}",
                    $"loan-network-l1-activation-{index}-{referenceSuffix}");
                network.Add(recruit);
                firstLevel.Add(recruit);
            }

            foreach (var recruiter in firstLevel)
            {
                for (var index = 0; index < EntryNetworkQualificationEvaluator.BranchSize; index++)
                {
                    var recruit = EntryParticipation.StartUnderRecruiter(
                        1,
                        nextCustomerId++,
                        recruiter,
                        EntryTerms,
                        EffectiveFrom);
                    CreateAndApplyEntryPayments(
                        recruit,
                        $"loan-network-l2-registration-{nextCustomerId}-{referenceSuffix}",
                        $"loan-network-l2-activation-{nextCustomerId}-{referenceSuffix}");
                    network.Add(recruit);
                }
            }

            return network;
        }

        private static (MemberPayment Registration, MemberPayment Activation)
            CreateAndApplyEntryPayments(
                EntryParticipation participation,
                string registrationReference,
                string activationReference)
        {
            var registration = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                registrationReference);
            participation.ApplyConfirmedActivationPayment(registration);

            var activation = CreateConfirmedPayment(
                participation.CustomerId,
                MemberPaymentPurpose.EntryActivation,
                600m,
                activationReference);
            participation.ApplyConfirmedActivationPayment(activation);

            return (registration, activation);
        }

        private static MemberPayment CreateConfirmedPayment(
            int customerId,
            MemberPaymentPurpose purpose,
            decimal amount,
            string externalReference,
            DateTime? confirmedAt = null)
        {
            var payment = MemberPayment.CreatePending(
                1,
                customerId,
                purpose,
                amount,
                "Yoco",
                externalReference,
                EffectiveFrom);
            payment.Confirm(confirmedAt ?? EffectiveFrom.AddMinutes(1));
            return payment;
        }
    }
}

using System;
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

                context.MemberPayments.AddRange(
                    recruiterPayments.Registration,
                    recruiterPayments.Activation,
                    participantPayments.Registration,
                    participantPayments.Activation,
                    onyxPayment,
                    monthlyPayment);
                context.EntryParticipations.AddRange(recruiterEntry, participantEntry);
                context.EntryMonthlyObligations.Add(monthlyObligation);
                context.OnyxParticipations.Add(onyxParticipation);
                await context.SaveChangesAsync();

                return new
                {
                    ParticipantCustomerId = participantCustomer.Id,
                    ParticipantEntryId = participantEntry.Id,
                    MonthlyObligationId = monthlyObligation.Id,
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

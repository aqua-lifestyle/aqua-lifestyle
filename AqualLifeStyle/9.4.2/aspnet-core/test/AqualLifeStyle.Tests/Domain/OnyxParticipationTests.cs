using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class OnyxParticipationTests
    {
        private static readonly DateTime EffectiveFrom = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly OnyxPlanTerms Terms = OnyxPlanTerms.Create(
            "onyx-2026-07",
            EffectiveFrom,
            directEntryAmount: 6120m);

        [Fact]
        public void StartingDirectOnyxParticipation_DoesNotActivateSelectedMembership()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.Equal(OnyxAdmissionRoute.DirectPayment, participation.AdmissionRoute);
            Assert.Equal(OnyxParticipationStatus.AwaitingDirectEntryPayment, participation.Status);
            Assert.Null(participation.ActivatedAt);
            Assert.Null(participation.EntryParticipationId);
            Assert.True(participation.JoinedIndependently);
        }

        [Fact]
        public void DirectOnyxParticipation_RequiresAConfirmedSixThousandOneHundredTwentyRandPayment()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);
            var payment = MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                MemberPaymentPurpose.OnyxDirectEntry,
                amount: 6120m,
                provider: "Yoco",
                externalReference: "onyx-direct-10",
                initiatedAt: EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedDirectEntryPayment(payment));

            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);

            Assert.Equal(OnyxParticipationStatus.Active, participation.Status);
            Assert.Equal(payment.Id, participation.DirectEntryPaymentId);
            Assert.Equal(payment.ConfirmedAt, participation.ActivatedAt);
            Assert.Equal("onyx-2026-07", participation.TermsVersion);
        }

        [Fact]
        public void DirectOnyxActivation_RejectsAnIncorrectPaymentAmount()
        {
            var participation = OnyxParticipation.StartDirectIndependently(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);
            var payment = MemberPayment.CreatePending(
                tenantId: 1,
                customerId: 10,
                MemberPaymentPurpose.OnyxDirectEntry,
                amount: 600m,
                provider: "Yoco",
                externalReference: "onyx-direct-underpayment-10",
                initiatedAt: EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));

            Assert.Throws<InvalidOperationException>(() =>
                participation.ApplyConfirmedDirectEntryPayment(payment));
            Assert.Equal(OnyxParticipationStatus.AwaitingDirectEntryPayment, participation.Status);
        }

        [Fact]
        public void RecordedRecruiter_MustHaveActiveOnyxParticipation()
        {
            var recruiter = OnyxParticipation.StartDirectIndependently(
                tenantId: 2,
                customerId: 20,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.Throws<InvalidOperationException>(() =>
                OnyxParticipation.StartDirectUnderRecruiter(
                    tenantId: 1,
                    customerId: 10,
                    recruiter,
                    onyxMembershipId: 7,
                    Terms,
                    EffectiveFrom));

            Activate(recruiter, "onyx-recruiter-20");
            var participation = OnyxParticipation.StartDirectUnderRecruiter(
                tenantId: 1,
                customerId: 10,
                recruiter,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.False(participation.JoinedIndependently);
            Assert.Equal(20, participation.RecruiterCustomerId);
        }

        private static void Activate(OnyxParticipation participation, string externalReference)
        {
            var payment = MemberPayment.CreatePending(
                participation.TenantId,
                participation.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                6120m,
                "Yoco",
                externalReference,
                EffectiveFrom);
            payment.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedDirectEntryPayment(payment);
        }
    }
}

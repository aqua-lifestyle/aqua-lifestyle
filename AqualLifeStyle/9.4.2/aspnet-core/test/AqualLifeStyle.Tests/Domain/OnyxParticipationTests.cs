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
            var participation = OnyxParticipation.StartDirect(
                tenantId: 1,
                customerId: 10,
                onyxMembershipId: 7,
                Terms,
                EffectiveFrom);

            Assert.Equal(OnyxAdmissionRoute.DirectPayment, participation.AdmissionRoute);
            Assert.Equal(OnyxParticipationStatus.AwaitingDirectEntryPayment, participation.Status);
            Assert.Null(participation.ActivatedAt);
            Assert.Null(participation.EntryParticipationId);
        }

        [Fact]
        public void DirectOnyxParticipation_RequiresAConfirmedSixThousandOneHundredTwentyRandPayment()
        {
            var participation = OnyxParticipation.StartDirect(
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
            var participation = OnyxParticipation.StartDirect(
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
    }
}

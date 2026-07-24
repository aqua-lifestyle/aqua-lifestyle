using System;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using Xunit;

namespace AqualLifeStyle.Tests.Domain
{
    public class EntryMonthlyObligationTests
    {
        private static readonly DateTime EffectiveFrom =
            new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly DateTime DueAt =
            new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void ActiveEntryParticipant_GetsVersionedMonthlyObligation()
        {
            var obligation = CreateObligation();

            Assert.Equal(600m, obligation.AmountDue);
            Assert.Equal(600m, obligation.OutstandingAmount);
            Assert.Equal("2026-07", obligation.TermsVersion);
            Assert.Equal(DueAt.AddDays(7), obligation.GracePeriodEndsAt);
            Assert.Equal(EntryMonthlyObligationStatus.Due, obligation.Status);
            Assert.True(obligation.IsOwnPayoutEligible);
        }

        [Fact]
        public void SevenDayGrace_EndsBeforeObligationBecomesOverdue()
        {
            var obligation = CreateObligation();

            obligation.AssessStatus(DueAt.AddDays(7));

            Assert.Equal(EntryMonthlyObligationStatus.GracePeriod, obligation.Status);
            Assert.True(obligation.IsOwnPayoutEligible);

            obligation.AssessStatus(DueAt.AddDays(7).AddTicks(1));

            Assert.Equal(EntryMonthlyObligationStatus.Overdue, obligation.Status);
            Assert.False(obligation.IsOwnPayoutEligible);
        }

        [Fact]
        public void OverdueObligation_PreservesDebtAndNetworkParticipation()
        {
            var participation = CreateActiveParticipation();
            var obligation = EntryMonthlyObligation.Create(
                participation,
                2026,
                8,
                DueAt);

            obligation.AssessStatus(DueAt.AddDays(8));

            Assert.Equal(600m, obligation.OutstandingAmount);
            Assert.Equal(participation.Id, obligation.EntryParticipationId);
            Assert.Equal(participation.CustomerId, obligation.CustomerId);
            Assert.NotNull(obligation.MarkedOverdueAt);
            Assert.True(participation.IsQualifiedForNetwork);
        }

        [Fact]
        public void Assessment_CannotMoveAnOverdueObligationBackIntoGrace()
        {
            var obligation = CreateObligation();
            obligation.AssessStatus(DueAt.AddDays(8));

            Assert.Throws<InvalidOperationException>(
                () => obligation.AssessStatus(DueAt.AddDays(2)));
            Assert.Equal(EntryMonthlyObligationStatus.Overdue, obligation.Status);
        }

        [Fact]
        public void ConfirmedLatePayment_SettlesDebtAndRestoresOwnPayoutEligibility()
        {
            var obligation = CreateObligation();
            obligation.AssessStatus(DueAt.AddDays(8));
            var markedOverdueAt = obligation.MarkedOverdueAt;
            var payment = CreateMonthlyPayment();
            payment.Confirm(DueAt.AddDays(9));

            obligation.ApplyConfirmedPayment(payment);
            obligation.ApplyConfirmedPayment(payment);

            Assert.Equal(EntryMonthlyObligationStatus.Paid, obligation.Status);
            Assert.Equal(0m, obligation.OutstandingAmount);
            Assert.Equal(payment.Id, obligation.PaymentId);
            Assert.Equal(payment.ConfirmedAt, obligation.PaidAt);
            Assert.Equal(markedOverdueAt, obligation.MarkedOverdueAt);
            Assert.True(obligation.IsOwnPayoutEligible);
        }

        [Fact]
        public void UnconfirmedPayment_CannotSettleMonthlyObligation()
        {
            var obligation = CreateObligation();
            var payment = CreateMonthlyPayment();

            Assert.Throws<InvalidOperationException>(
                () => obligation.ApplyConfirmedPayment(payment));
            Assert.Equal(600m, obligation.OutstandingAmount);
        }

        private static EntryMonthlyObligation CreateObligation()
        {
            return EntryMonthlyObligation.Create(
                CreateActiveParticipation(),
                2026,
                8,
                DueAt);
        }

        private static EntryParticipation CreateActiveParticipation()
        {
            var terms = EntryProgrammeTerms.Create(
                "2026-07",
                EffectiveFrom,
                600m,
                600m,
                600m,
                7);
            var participation = EntryParticipation.StartIndependently(
                1,
                10,
                terms,
                EffectiveFrom);

            var registration = MemberPayment.CreatePending(
                1,
                10,
                MemberPaymentPurpose.EntryRegistration,
                600m,
                "Yoco",
                $"registration-{Guid.NewGuid():N}",
                EffectiveFrom);
            registration.Confirm(EffectiveFrom.AddMinutes(1));
            participation.ApplyConfirmedActivationPayment(registration);

            var activation = MemberPayment.CreatePending(
                1,
                10,
                MemberPaymentPurpose.EntryActivation,
                600m,
                "Yoco",
                $"activation-{Guid.NewGuid():N}",
                EffectiveFrom);
            activation.Confirm(EffectiveFrom.AddMinutes(2));
            participation.ApplyConfirmedActivationPayment(activation);

            return participation;
        }

        private static MemberPayment CreateMonthlyPayment()
        {
            return MemberPayment.CreatePending(
                1,
                10,
                MemberPaymentPurpose.EntryMonthlyCommitment,
                600m,
                "Yoco",
                $"monthly-{Guid.NewGuid():N}",
                DueAt);
        }
    }
}

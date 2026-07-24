using System;
using AqualLifeStyle.Domain.Onyx;

namespace AqualLifeStyle.Application.ProgrammeParticipations
{
    internal sealed class ProgrammeParticipationStatusDetails
    {
        public string Status { get; init; }
        public bool IsActive { get; init; }
        public decimal? NextPaymentAmount { get; init; }
        public string NextPaymentDescription { get; init; }
        public bool CanRecruit { get; init; }
    }

    internal static class ProgrammeParticipationStatusPresenter
    {
        public static ProgrammeParticipationStatusDetails Describe(
            EntryParticipation participation)
        {
            var awaitingRegistration =
                participation.Status == EntryParticipationStatus.AwaitingRegistrationPayment;
            var awaitingActivation =
                participation.Status == EntryParticipationStatus.AwaitingActivationPayment;
            return new ProgrammeParticipationStatusDetails
            {
                Status = participation.Status switch
                {
                    EntryParticipationStatus.AwaitingRegistrationPayment =>
                        "Awaiting registration payment",
                    EntryParticipationStatus.AwaitingActivationPayment =>
                        "Awaiting activation payment",
                    EntryParticipationStatus.Active => "Active",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(participation.Status),
                        participation.Status,
                        null)
                },
                IsActive = participation.Status == EntryParticipationStatus.Active,
                NextPaymentAmount = awaitingRegistration
                    ? participation.RegistrationPaymentAmount
                    : awaitingActivation
                        ? participation.ActivationPaymentAmount
                        : null,
                NextPaymentDescription = awaitingRegistration
                    ? "Registration payment"
                    : awaitingActivation
                        ? "Activation payment"
                        : null,
                CanRecruit = participation.IsQualifiedForNetwork
            };
        }

        public static ProgrammeParticipationStatusDetails Describe(
            OnyxParticipation participation)
        {
            var awaitingPayment =
                participation.Status == OnyxParticipationStatus.AwaitingDirectEntryPayment;
            return new ProgrammeParticipationStatusDetails
            {
                Status = participation.Status switch
                {
                    OnyxParticipationStatus.AwaitingDirectEntryPayment =>
                        "Awaiting full payment",
                    OnyxParticipationStatus.Active => "Active",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(participation.Status),
                        participation.Status,
                        null)
                },
                IsActive = participation.Status == OnyxParticipationStatus.Active,
                NextPaymentAmount = awaitingPayment ? participation.DirectEntryAmount : null,
                NextPaymentDescription = awaitingPayment
                    ? "Full Onyx participation payment"
                    : null,
                CanRecruit = participation.Status == OnyxParticipationStatus.Active
            };
        }
    }
}

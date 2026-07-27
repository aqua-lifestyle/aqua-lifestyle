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
                    EntryParticipationStatus.AwaitingJoiningPayment =>
                        participation.JoiningPaymentAmount > 0m
                            ? "Awaiting joining payment"
                            : "Awaiting registration payment",
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
                    ? participation.JoiningPaymentAmount > 0m
                        ? participation.JoiningPaymentAmount
                        : participation.RegistrationPaymentAmount
                    : awaitingActivation
                        ? participation.ActivationPaymentAmount
                        : null,
                NextPaymentDescription = awaitingRegistration
                    ? participation.JoiningPaymentAmount > 0m
                        ? "Full AQGreen joining payment"
                        : "Registration payment"
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

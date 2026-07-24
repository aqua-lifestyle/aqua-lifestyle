using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;

namespace AqualLifeStyle.Payments
{
    public enum ProgrammeParticipationKind
    {
        Entry = 0,
        Onyx = 1
    }

    public sealed class ProgrammePaymentConfirmationResult
    {
        public Guid PaymentId { get; }
        public Guid ParticipationId { get; }
        public ProgrammeParticipationKind ParticipationKind { get; }
        public bool WasAlreadyProcessed { get; }

        public ProgrammePaymentConfirmationResult(
            Guid paymentId,
            Guid participationId,
            ProgrammeParticipationKind participationKind,
            bool wasAlreadyProcessed)
        {
            PaymentId = paymentId;
            ParticipationId = participationId;
            ParticipationKind = participationKind;
            WasAlreadyProcessed = wasAlreadyProcessed;
        }
    }

    /// <summary>
    /// Reconciles a verified provider confirmation and applies the corresponding
    /// Entry or direct Onyx activation transition atomically.
    /// </summary>
    /// <remarks>
    /// This is deliberately not an ABP application service and is therefore not
    /// exposed as a remote endpoint. A provider adapter must verify callback
    /// authenticity before invoking it.
    /// </remarks>
    public class ProgrammePaymentConfirmationProcessor : ITransientDependency
    {
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly ActiveProgrammeParticipantRoleSynchronizer _participantRoleSynchronizer;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ProgrammePaymentConfirmationProcessor(
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            ActiveProgrammeParticipantRoleSynchronizer participantRoleSynchronizer,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _paymentRepository = paymentRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _participantRoleSynchronizer = participantRoleSynchronizer;
            _unitOfWorkManager = unitOfWorkManager;
        }

        [UnitOfWork]
        public virtual async Task<ProgrammePaymentConfirmationResult> ProcessAsync(
            ConfirmedProgrammePayment confirmation)
        {
            if (confirmation == null)
            {
                throw new ArgumentNullException(nameof(confirmation));
            }

            EnsureSupportedPurpose(confirmation.Purpose);

            var candidate = MemberPayment.CreatePending(
                confirmation.TenantId,
                confirmation.CustomerId,
                confirmation.Purpose,
                confirmation.Amount,
                confirmation.Provider,
                confirmation.ExternalReference,
                confirmation.InitiatedAt,
                confirmation.Currency);
            candidate.Confirm(confirmation.ConfirmedAt);

            using (_unitOfWorkManager.Current.SetTenantId(confirmation.TenantId))
            {
                var existingPayment = await _paymentRepository.FirstOrDefaultAsync(payment =>
                    payment.Provider == candidate.Provider &&
                    payment.ExternalReference == candidate.ExternalReference);
                var wasAlreadyProcessed =
                    existingPayment?.Status == MemberPaymentStatus.Confirmed;
                var payment = existingPayment ?? candidate;

                if (existingPayment != null)
                {
                    EnsureMatchingPaymentFacts(existingPayment, candidate);
                    existingPayment.Confirm(confirmation.ConfirmedAt);
                }
                else
                {
                    await _paymentRepository.InsertAsync(candidate);
                }

                var participation = await ApplyToParticipationAsync(payment);
                if (participation.IsActive)
                {
                    await _participantRoleSynchronizer.PromoteGuestToMemberAsync(
                        payment.CustomerId);
                }
                await _unitOfWorkManager.Current.SaveChangesAsync();

                return new ProgrammePaymentConfirmationResult(
                    payment.Id,
                    participation.Id,
                    participation.Kind,
                    wasAlreadyProcessed);
            }
        }

        private async Task<(Guid Id, ProgrammeParticipationKind Kind, bool IsActive)> ApplyToParticipationAsync(
            MemberPayment payment)
        {
            if (payment.Purpose == MemberPaymentPurpose.OnyxDirectEntry)
            {
                var onyxParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    participation =>
                        participation.TenantId == payment.TenantId &&
                        participation.CustomerId == payment.CustomerId);
                if (onyxParticipation == null)
                {
                    throw new UserFriendlyException(
                        "No Onyx participation was found for this customer.");
                }

                onyxParticipation.ApplyConfirmedDirectEntryPayment(payment);
                return (
                    onyxParticipation.Id,
                    ProgrammeParticipationKind.Onyx,
                    onyxParticipation.Status == OnyxParticipationStatus.Active);
            }

            var entryParticipation = await _entryParticipationRepository.FirstOrDefaultAsync(
                participation =>
                    participation.TenantId == payment.TenantId &&
                    participation.CustomerId == payment.CustomerId);
            if (entryParticipation == null)
            {
                throw new UserFriendlyException(
                    "No Entry participation was found for this customer.");
            }

            entryParticipation.ApplyConfirmedActivationPayment(payment);
            return (
                entryParticipation.Id,
                ProgrammeParticipationKind.Entry,
                entryParticipation.Status == EntryParticipationStatus.Active);
        }

        private static void EnsureSupportedPurpose(MemberPaymentPurpose purpose)
        {
            if (purpose != MemberPaymentPurpose.EntryRegistration &&
                purpose != MemberPaymentPurpose.EntryActivation &&
                purpose != MemberPaymentPurpose.OnyxDirectEntry)
            {
                throw new NotSupportedException(
                    $"Payment purpose '{purpose}' is not a programme activation payment.");
            }
        }

        private static void EnsureMatchingPaymentFacts(
            MemberPayment existingPayment,
            MemberPayment candidate)
        {
            if (existingPayment.TenantId != candidate.TenantId ||
                existingPayment.CustomerId != candidate.CustomerId ||
                existingPayment.Purpose != candidate.Purpose ||
                existingPayment.Amount != candidate.Amount ||
                !string.Equals(existingPayment.Currency, candidate.Currency, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "The payment provider reference is already associated with different payment facts.");
            }
        }
    }
}

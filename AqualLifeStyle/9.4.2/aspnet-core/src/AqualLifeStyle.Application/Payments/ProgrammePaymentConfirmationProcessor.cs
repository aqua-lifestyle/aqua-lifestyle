using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.UI;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
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
        private readonly IRepository<DirectOnyxCheckoutIntent, Guid> _directOnyxCheckoutIntentRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IProgrammeInvitationResolver _invitationResolver;
        private readonly ActiveProgrammeParticipantRoleSynchronizer _participantRoleSynchronizer;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ProgrammePaymentConfirmationProcessor(
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<DirectOnyxCheckoutIntent, Guid> directOnyxCheckoutIntentRepository,
            ICustomerRepository customerRepository,
            IMembershipRepository membershipRepository,
            IProgrammeInvitationResolver invitationResolver,
            ActiveProgrammeParticipantRoleSynchronizer participantRoleSynchronizer,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _paymentRepository = paymentRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _directOnyxCheckoutIntentRepository = directOnyxCheckoutIntentRepository;
            _customerRepository = customerRepository;
            _membershipRepository = membershipRepository;
            _invitationResolver = invitationResolver;
            _participantRoleSynchronizer = participantRoleSynchronizer;
            _unitOfWorkManager = unitOfWorkManager;
        }

        /// <summary>
        /// Creates direct Onyx participation only after a verified provider
        /// confirmation has been matched to the persisted checkout intent.
        /// </summary>
        [UnitOfWork]
        public virtual async Task<ProgrammePaymentConfirmationResult>
            ProcessDirectOnyxCheckoutAsync(
                Guid checkoutIntentId,
                string provider,
                string externalPaymentReference,
                decimal amount,
                string currency,
                DateTime confirmedAt)
        {
            if (checkoutIntentId == Guid.Empty)
                throw new ArgumentException("A checkout intent is required.", nameof(checkoutIntentId));

            DirectOnyxCheckoutIntent intent;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                intent = await _directOnyxCheckoutIntentRepository.FirstOrDefaultAsync(checkoutIntentId);
            }
            if (intent == null)
                throw new UserFriendlyException("The Onyx payment could not be matched to a checkout.");

            using (_unitOfWorkManager.Current.SetTenantId(intent.TenantId))
            {
                if (intent.Status == DirectOnyxCheckoutIntentStatus.Completed)
                    return await GetCompletedIntentResultAsync(
                        intent,
                        provider,
                        externalPaymentReference,
                        amount,
                        currency);

                EnsureCheckoutPaymentFacts(intent, amount, currency);
                await RevalidatePlacementAsync(intent);

                var candidate = MemberPayment.CreatePending(
                    intent.TenantId,
                    intent.CustomerId,
                    MemberPaymentPurpose.OnyxDirectEntry,
                    amount,
                    provider,
                    externalPaymentReference,
                    intent.CheckoutCreatedAt ?? intent.CreatedAt,
                    currency);
                candidate.Confirm(confirmedAt);

                var existingPayment = await _paymentRepository.FirstOrDefaultAsync(payment =>
                    payment.Provider == candidate.Provider &&
                    payment.ExternalReference == candidate.ExternalReference);
                var wasAlreadyProcessed =
                    existingPayment?.Status == MemberPaymentStatus.Confirmed;
                var payment = existingPayment ?? candidate;
                if (existingPayment == null)
                    await _paymentRepository.InsertAsync(payment);
                else
                {
                    EnsureMatchingPaymentFacts(existingPayment, candidate);
                    existingPayment.Confirm(confirmedAt);
                }

                var existingParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    participation => participation.CustomerId == intent.CustomerId);
                if (existingParticipation != null)
                    throw new InvalidOperationException(
                        "An Onyx participation already exists for this checkout customer.");

                OnyxParticipation participation;
                if (intent.RecruiterCustomerId.HasValue)
                {
                    OnyxParticipation recruiter;
                    using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
                    {
                        recruiter = await _onyxParticipationRepository.FirstOrDefaultAsync(candidateRecruiter =>
                            candidateRecruiter.CustomerId == intent.RecruiterCustomerId.Value &&
                            candidateRecruiter.Status == OnyxParticipationStatus.Active);
                    }
                    if (recruiter == null)
                        throw new UserFriendlyException(
                            "The Onyx payment cannot be completed.",
                            "The selected recruiter is no longer eligible. Contact the club team for assistance.");

                    participation = OnyxParticipation.StartDirectUnderRecruiter(
                        intent.TenantId,
                        intent.CustomerId,
                        recruiter,
                        intent.OnyxMembershipId,
                        intent.RestoreTerms(),
                        confirmedAt);
                }
                else
                {
                    participation = OnyxParticipation.StartDirectIndependently(
                        intent.TenantId,
                        intent.CustomerId,
                        intent.OnyxMembershipId,
                        intent.RestoreTerms(),
                        confirmedAt);
                }

                participation.ApplyConfirmedDirectEntryPayment(payment);
                await _onyxParticipationRepository.InsertAsync(participation);
                await ClearLegacyOnyxMembershipAssignmentAsync(intent.CustomerId);
                intent.Complete(payment.Id, participation.Id, confirmedAt);
                await _participantRoleSynchronizer.PromoteGuestToMemberAsync(intent.CustomerId);
                await _unitOfWorkManager.Current.SaveChangesAsync();

                return new ProgrammePaymentConfirmationResult(
                    payment.Id,
                    participation.Id,
                    ProgrammeParticipationKind.Onyx,
                    wasAlreadyProcessed);
            }
        }

        private async Task RevalidatePlacementAsync(DirectOnyxCheckoutIntent intent)
        {
            if (string.IsNullOrWhiteSpace(intent.InviteCode))
                return;

            var resolvedRecruiter = await _invitationResolver.ResolveRecruiterForJoiningAsync(
                intent.InviteCode,
                RecruitmentProgrammeKeys.Onyx,
                intent.CustomerId,
                intent.TenantId);
            if (resolvedRecruiter != intent.RecruiterCustomerId)
                throw new InvalidOperationException(
                    "The checkout invitation no longer resolves to its recorded recruiter.");
        }

        private async Task ClearLegacyOnyxMembershipAssignmentAsync(int customerId)
        {
            var customer = await _customerRepository.GetAsync(customerId);
            if (!customer.MembershipId.HasValue)
                return;

            Membership membership;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                membership = await _membershipRepository.FirstOrDefaultAsync(customer.MembershipId.Value);
            }
            if (membership?.MembershipType != MembershipType.Onyx)
                return;

            customer.ChangeMembership(null);
            await _customerRepository.UpdateAsync(customer);
        }

        private async Task<ProgrammePaymentConfirmationResult> GetCompletedIntentResultAsync(
            DirectOnyxCheckoutIntent intent,
            string provider,
            string externalPaymentReference,
            decimal amount,
            string currency)
        {
            if (!intent.PaymentId.HasValue || !intent.ParticipationId.HasValue)
                throw new InvalidOperationException("The completed checkout is missing its result references.");
            var payment = await _paymentRepository.GetAsync(intent.PaymentId.Value);
            var candidate = MemberPayment.CreatePending(
                intent.TenantId,
                intent.CustomerId,
                MemberPaymentPurpose.OnyxDirectEntry,
                amount,
                provider,
                externalPaymentReference,
                intent.CheckoutCreatedAt ?? intent.CreatedAt,
                currency);
            EnsureMatchingPaymentFacts(payment, candidate);
            return new ProgrammePaymentConfirmationResult(
                payment.Id,
                intent.ParticipationId.Value,
                ProgrammeParticipationKind.Onyx,
                true);
        }

        private static void EnsureCheckoutPaymentFacts(
            DirectOnyxCheckoutIntent intent,
            decimal amount,
            string currency)
        {
            if (intent.Status != DirectOnyxCheckoutIntentStatus.AwaitingPayment ||
                string.IsNullOrWhiteSpace(intent.ProviderCheckoutId))
                throw new InvalidOperationException("The Onyx checkout is not awaiting payment confirmation.");
            if (intent.Amount != amount ||
                !string.Equals(intent.Currency, currency?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The confirmed payment does not match the Onyx checkout amount.");
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
                    "No AQGreen participation was found for this customer.");
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

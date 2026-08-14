using System;
using System.Linq;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.UI;
using Abp.Domain.Uow;
using AqualLifeStyle.Application.Recruitment;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Payments.Yoco;
using Microsoft.EntityFrameworkCore;

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
        public bool AwaitingAdministrativeApproval { get; }
        public bool AllocationReconciliationRequired { get; }

        public ProgrammePaymentConfirmationResult(
            Guid paymentId,
            Guid participationId,
            ProgrammeParticipationKind participationKind,
            bool wasAlreadyProcessed,
            bool awaitingAdministrativeApproval = false,
            bool allocationReconciliationRequired = false)
        {
            PaymentId = paymentId;
            ParticipationId = participationId;
            ParticipationKind = participationKind;
            WasAlreadyProcessed = wasAlreadyProcessed;
            AwaitingAdministrativeApproval = awaitingAdministrativeApproval;
            AllocationReconciliationRequired = allocationReconciliationRequired;
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
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _aqGreenJoiningCheckoutRepository;
        private readonly IRepository<AQGreenMonthlyObligationCheckout, Guid>
            _aqGreenMonthlyCheckoutRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid> _monthlyObligationRepository;
        private readonly IEntryMonthlyObligationSchedulingLock _monthlyObligationLock;
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<CustomerAreaAssignment, Guid>
            _customerAreaAssignmentRepository;
        private readonly IMembershipRepository _membershipRepository;
        private readonly IProgrammeInvitationResolver _invitationResolver;
        private readonly AQGreenFuneralCoverInclusionProcessor _funeralCoverInclusionProcessor;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;
        private readonly ProgrammeApprovalNotificationScheduler _approvalNotificationScheduler;

        public ProgrammePaymentConfirmationProcessor(
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<DirectOnyxCheckoutIntent, Guid> directOnyxCheckoutIntentRepository,
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenJoiningCheckoutRepository,
            IRepository<AQGreenMonthlyObligationCheckout, Guid> aqGreenMonthlyCheckoutRepository,
            IRepository<EntryMonthlyObligation, Guid> monthlyObligationRepository,
            IEntryMonthlyObligationSchedulingLock monthlyObligationLock,
            ICustomerRepository customerRepository,
            IRepository<CustomerAreaAssignment, Guid> customerAreaAssignmentRepository,
            IMembershipRepository membershipRepository,
            IProgrammeInvitationResolver invitationResolver,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock,
            IUnitOfWorkManager unitOfWorkManager,
            ProgrammeApprovalNotificationScheduler approvalNotificationScheduler,
            AQGreenFuneralCoverInclusionProcessor funeralCoverInclusionProcessor)
        {
            _paymentRepository = paymentRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _directOnyxCheckoutIntentRepository = directOnyxCheckoutIntentRepository;
            _aqGreenJoiningCheckoutRepository = aqGreenJoiningCheckoutRepository;
            _aqGreenMonthlyCheckoutRepository = aqGreenMonthlyCheckoutRepository;
            _monthlyObligationRepository = monthlyObligationRepository;
            _monthlyObligationLock = monthlyObligationLock;
            _customerRepository = customerRepository;
            _customerAreaAssignmentRepository = customerAreaAssignmentRepository;
            _membershipRepository = membershipRepository;
            _invitationResolver = invitationResolver;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
            _funeralCoverInclusionProcessor = funeralCoverInclusionProcessor;
            _unitOfWorkManager = unitOfWorkManager;
            _approvalNotificationScheduler = approvalNotificationScheduler;
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
                string providerCheckoutId,
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
                if (intent.Status == HostedPaymentCheckoutStatus.Completed)
                    return await GetCompletedIntentResultAsync(
                        intent,
                        provider,
                        externalPaymentReference,
                        providerCheckoutId,
                        amount,
                        currency);

                EnsureCheckoutPaymentFacts(intent, providerCheckoutId, amount, currency, "Onyx");
                var invitationPlacement = await RevalidatePlacementAsync(intent);

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
                {
                    try
                    {
                        await _paymentRepository.InsertAsync(payment);
                    }
                    catch (DbUpdateException)
                    {
                        payment = await _paymentRepository.FirstOrDefaultAsync(p =>
                            p.Provider == candidate.Provider &&
                            p.ExternalReference == candidate.ExternalReference);
                        if (payment == null) throw;
                        wasAlreadyProcessed = payment.Status == MemberPaymentStatus.Confirmed;
                    }
                }
                else
                {
                    EnsureMatchingPaymentFacts(existingPayment, candidate);
                    existingPayment.Confirm(confirmedAt);
                }

                var existingParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    participation => participation.CustomerId == intent.CustomerId);
                if (existingParticipation != null)
                {
                    if (existingParticipation.DirectEntryPaymentId.HasValue)
                    {
                        var recoveredPayment = await _paymentRepository.GetAsync(
                            existingParticipation.DirectEntryPaymentId.Value);
                        EnsureMatchingPaymentFacts(recoveredPayment, candidate);

                        return new ProgrammePaymentConfirmationResult(
                            recoveredPayment.Id,
                            existingParticipation.Id,
                            ProgrammeParticipationKind.Onyx,
                            true);
                    }

                    throw new InvalidOperationException(
                        "An Onyx participation already exists for this checkout customer.");
                }

                OnyxParticipation participation;
                if (intent.RecruiterCustomerId.HasValue)
                {
                    OnyxParticipation recruiter;
                    using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
                    {
                        recruiter = await _onyxParticipationRepository.FirstOrDefaultAsync(candidateRecruiter =>
                            candidateRecruiter.TenantId == intent.TenantId &&
                            candidateRecruiter.CustomerId == intent.RecruiterCustomerId.Value &&
                            candidateRecruiter.Status == OnyxParticipationStatus.Active);
                    }
                    if (recruiter == null)
                        throw new UserFriendlyException(
                            "The Onyx payment cannot be completed.",
                            "The inviting Club Member is no longer eligible. Contact the club team for assistance.");

                    if (invitationPlacement != null)
                    {
                        var customer = await _customerRepository.GetAllIncluding(
                                item => item.AreaAssignments)
                            .SingleAsync(item =>
                                item.Id == intent.CustomerId &&
                                item.TenantId == intent.TenantId);
                        var previousAreaId = customer.AreaId;
                        customer.MoveToArea(
                            invitationPlacement.RecruiterArea,
                            DateTime.UtcNow,
                            "Onyx invitation placement");
                        if (customer.AreaId != previousAreaId)
                        {
                            await _customerAreaAssignmentRepository.InsertAsync(
                                customer.AreaAssignments.Single(assignment =>
                                    assignment.IsCurrent));
                        }
                    }

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

                try
                {
                    await _onyxParticipationRepository.InsertAsync(participation);
                    await ClearLegacyOnyxMembershipAssignmentAsync(intent.CustomerId);
                    intent.Complete(payment.Id, participation.Id, confirmedAt);
                    await _approvalNotificationScheduler.ScheduleAsync(
                        payment,
                        participation.Id,
                        ProgrammeParticipationKind.Onyx,
                        participation.DirectEntryAmount);
                    await _unitOfWorkManager.Current.SaveChangesAsync();

                    return new ProgrammePaymentConfirmationResult(
                        payment.Id,
                        participation.Id,
                        ProgrammeParticipationKind.Onyx,
                        wasAlreadyProcessed,
                        participation.IsAwaitingAdministrativeApproval);
                }
                catch (DbUpdateException)
                {
                    var recoveredParticipation = await _onyxParticipationRepository.FirstOrDefaultAsync(
                        p => p.CustomerId == intent.CustomerId);
                    if (recoveredParticipation == null)
                        throw;

                    var recoveredIntent = await _directOnyxCheckoutIntentRepository.GetAsync(intent.Id);
                    if (!recoveredIntent.PaymentId.HasValue || !recoveredIntent.ParticipationId.HasValue)
                        throw new InvalidOperationException("The Onyx checkout was not completed.");

                    var recoveredPayment = await _paymentRepository.GetAsync(recoveredIntent.PaymentId.Value);
                    EnsureMatchingPaymentFacts(recoveredPayment, candidate);

                    return new ProgrammePaymentConfirmationResult(
                        recoveredIntent.PaymentId.Value,
                        recoveredIntent.ParticipationId.Value,
                        ProgrammeParticipationKind.Onyx,
                        true);
                }
            }
        }

        private async Task<ProgrammeInvitationPlacement> RevalidatePlacementAsync(
            DirectOnyxCheckoutIntent intent)
        {
            if (string.IsNullOrWhiteSpace(intent.InviteCode))
                return null;

            var placement = await _invitationResolver.ResolveForJoiningAsync(
                intent.InviteCode,
                RecruitmentProgrammeKeys.Onyx,
                intent.CustomerId,
                intent.TenantId);
            if (placement.RecruiterCustomerId != intent.RecruiterCustomerId)
                throw new InvalidOperationException(
                    "The checkout invitation no longer resolves to its recorded network placement.");
            return placement;
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
            string providerCheckoutId,
            decimal amount,
            string currency)
        {
            if (!intent.PaymentId.HasValue || !intent.ParticipationId.HasValue)
                throw new InvalidOperationException("The completed checkout is missing its result references.");
            var payment = await _paymentRepository.GetAsync(intent.PaymentId.Value);
            EnsureProviderCheckoutMatches(intent, providerCheckoutId, "Onyx");
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
            HostedPaymentCheckout checkout,
            string providerCheckoutId,
            decimal amount,
            string currency,
            string programmeName)
        {
            if (checkout.Status == HostedPaymentCheckoutStatus.PreparingCheckout ||
                string.IsNullOrWhiteSpace(checkout.ProviderCheckoutId))
                throw new YocoWebhookTransientException(
                    $"The {programmeName} checkout is not yet ready for payment confirmation.");
            if (checkout.Status != HostedPaymentCheckoutStatus.AwaitingPayment)
                throw new YocoWebhookValidationException(
                    $"The {programmeName} checkout is no longer payable.");
            EnsureProviderCheckoutMatches(checkout, providerCheckoutId, programmeName);
            if (checkout.Amount != amount ||
                !string.Equals(checkout.Currency, currency?.Trim(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"The confirmed payment does not match the {programmeName} checkout amount.");
        }

        private static void EnsureProviderCheckoutMatches(
            HostedPaymentCheckout checkout,
            string providerCheckoutId,
            string programmeName)
        {
            if (string.IsNullOrWhiteSpace(providerCheckoutId) ||
                !string.Equals(
                    checkout.ProviderCheckoutId,
                    providerCheckoutId.Trim(),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"The confirmed payment does not belong to the recorded {programmeName} checkout.");
        }

        /// <summary>
        /// Applies one verified AQGreen joining checkout stage atomically.
        /// </summary>
        [UnitOfWork]
        public virtual async Task<ProgrammePaymentConfirmationResult>
            ProcessAQGreenJoiningCheckoutAsync(
                Guid checkoutId,
                string provider,
                string externalPaymentReference,
                string providerCheckoutId,
                decimal amount,
                string currency,
                DateTime confirmedAt)
        {
            if (checkoutId == Guid.Empty)
                throw new ArgumentException("An AQGreen checkout is required.", nameof(checkoutId));

            AQGreenJoiningCheckout checkout;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                checkout = await _aqGreenJoiningCheckoutRepository.FirstOrDefaultAsync(checkoutId);
            }
            if (checkout == null)
                throw new UserFriendlyException("The AQGreen payment could not be matched to a checkout.");

            using (_unitOfWorkManager.Current.SetTenantId(checkout.TenantId))
            {
                if (checkout.Status == HostedPaymentCheckoutStatus.Completed)
                {
                    if (!checkout.PaymentId.HasValue)
                        throw new InvalidOperationException("The completed AQGreen checkout is missing its payment reference.");
                    EnsureProviderCheckoutMatches(checkout, providerCheckoutId, "AQGreen");
                    var existing = await _paymentRepository.GetAsync(checkout.PaymentId.Value);
                    var repeated = MemberPayment.CreatePending(
                        checkout.TenantId,
                        checkout.CustomerId,
                        MemberPaymentPurpose.AQGreenJoining,
                        amount,
                        provider,
                        externalPaymentReference,
                        checkout.CheckoutCreatedAt ?? checkout.CreatedAt,
                        currency);
                    EnsureMatchingPaymentFacts(existing, repeated);
                    return new ProgrammePaymentConfirmationResult(
                        existing.Id,
                        checkout.ParticipationId,
                        ProgrammeParticipationKind.Entry,
                        true);
                }

                EnsureCheckoutPaymentFacts(
                    checkout,
                    providerCheckoutId,
                    amount,
                    currency,
                    "AQGreen");
                var participation = await _entryParticipationRepository.GetAsync(
                    checkout.ParticipationId);
                if (participation.CustomerId != checkout.CustomerId ||
                    participation.Status == EntryParticipationStatus.Active)
                    throw new InvalidOperationException(
                        "The AQGreen checkout no longer matches an awaiting participation.");

                var candidate = MemberPayment.CreatePending(
                    checkout.TenantId,
                    checkout.CustomerId,
                    MemberPaymentPurpose.AQGreenJoining,
                    amount,
                    provider,
                    externalPaymentReference,
                    checkout.CheckoutCreatedAt ?? checkout.CreatedAt,
                    currency);
                candidate.Confirm(confirmedAt);
                var existingPayment = await _paymentRepository.FirstOrDefaultAsync(payment =>
                    payment.Provider == candidate.Provider &&
                    payment.ExternalReference == candidate.ExternalReference);
                var wasAlreadyProcessed = existingPayment?.Status == MemberPaymentStatus.Confirmed;
                var payment = existingPayment ?? candidate;
                if (existingPayment == null)
                {
                    try
                    {
                        await _paymentRepository.InsertAsync(payment);
                    }
                    catch (DbUpdateException)
                    {
                        payment = await _paymentRepository.FirstOrDefaultAsync(p =>
                            p.Provider == candidate.Provider &&
                            p.ExternalReference == candidate.ExternalReference);
                        if (payment == null) throw;
                        wasAlreadyProcessed = payment.Status == MemberPaymentStatus.Confirmed;
                    }
                }
                else
                {
                    EnsureMatchingPaymentFacts(existingPayment, candidate);
                    existingPayment.Confirm(confirmedAt);
                }

                if (!participation.JoiningPaymentSchedule.HasValue &&
                    participation.JoiningInstallmentAmount <= 0m &&
                    checkout.Stage == AQGreenJoiningPaymentStage.Full)
                    participation.ApplyConfirmedJoiningPayment(payment);
                else
                    participation.ApplyConfirmedJoiningPayment(payment, checkout.Stage);
                await ApplyFuneralCoverInclusionIfCompletedAsync(
                    participation,
                    confirmedAt);
                checkout.Complete(payment.Id, confirmedAt);
                if (participation.IsAwaitingAdministrativeApproval)
                {
                    await _approvalNotificationScheduler.ScheduleAsync(
                        payment,
                        participation.Id,
                        ProgrammeParticipationKind.Entry,
                        participation.GetConfirmedJoiningAmount());
                }
                await _unitOfWorkManager.Current.SaveChangesAsync();

                return new ProgrammePaymentConfirmationResult(
                    payment.Id,
                    participation.Id,
                    ProgrammeParticipationKind.Entry,
                    wasAlreadyProcessed,
                    participation.IsAwaitingAdministrativeApproval);
            }
        }

        /// <summary>
        /// Applies a verified provider payment only to the obligation persisted on
        /// its AQGreen monthly checkout.
        /// </summary>
        [UnitOfWork]
        public virtual async Task<ProgrammePaymentConfirmationResult>
            ProcessAQGreenMonthlyObligationCheckoutAsync(
                Guid checkoutId,
                string provider,
                string externalPaymentReference,
                string providerCheckoutId,
                decimal amount,
                string currency,
                DateTime confirmedAt)
        {
            if (checkoutId == Guid.Empty)
                throw new ArgumentException(
                    "An AQGreen monthly checkout is required.",
                    nameof(checkoutId));

            AQGreenMonthlyObligationCheckout checkout;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                checkout = await _aqGreenMonthlyCheckoutRepository.FirstOrDefaultAsync(
                    checkoutId);
            }
            if (checkout == null)
                throw new UserFriendlyException(
                    "The AQGreen monthly payment could not be matched to a checkout.");

            using (_unitOfWorkManager.Current.SetTenantId(checkout.TenantId))
            {
                if (checkout.Status == HostedPaymentCheckoutStatus.Completed)
                {
                    if (!checkout.PaymentId.HasValue)
                        throw new InvalidOperationException(
                            "The completed AQGreen monthly checkout is missing its payment reference.");
                    EnsureProviderCheckoutMatches(
                        checkout,
                        providerCheckoutId,
                        "AQGreen monthly");
                    var existing = await _paymentRepository.GetAsync(
                        checkout.PaymentId.Value);
                    var repeated = MemberPayment.CreatePending(
                        checkout.TenantId,
                        checkout.CustomerId,
                        MemberPaymentPurpose.EntryMonthlyCommitment,
                        amount,
                        provider,
                        externalPaymentReference,
                        checkout.CheckoutCreatedAt ?? checkout.CreatedAt,
                        currency);
                    EnsureMatchingPaymentFacts(existing, repeated);
                    return new ProgrammePaymentConfirmationResult(
                        existing.Id,
                        checkout.EntryParticipationId,
                        ProgrammeParticipationKind.Entry,
                        true,
                        allocationReconciliationRequired:
                            checkout.AllocationStatus ==
                            AQGreenMonthlyPaymentAllocationStatus.ReconciliationRequired);
                }

                EnsureCheckoutPaymentFacts(
                    checkout,
                    providerCheckoutId,
                    amount,
                    currency,
                    "AQGreen monthly");
                await _monthlyObligationLock.AcquireAsync();

                var candidate = MemberPayment.CreatePending(
                    checkout.TenantId,
                    checkout.CustomerId,
                    MemberPaymentPurpose.EntryMonthlyCommitment,
                    amount,
                    provider,
                    externalPaymentReference,
                    checkout.CheckoutCreatedAt ?? checkout.CreatedAt,
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

                EntryMonthlyObligation obligation;
                EntryMonthlyObligation existingAllocation;
                using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.SoftDelete))
                {
                    obligation = await _monthlyObligationRepository.FirstOrDefaultAsync(
                        checkout.EntryMonthlyObligationId);
                    existingAllocation = await _monthlyObligationRepository.FirstOrDefaultAsync(
                        item => item.PaymentId == payment.Id);
                }

                var reconciliationReason = ReconciliationReason(
                    checkout,
                    obligation,
                    payment,
                    existingAllocation);
                if (reconciliationReason == null)
                {
                    obligation.ApplyConfirmedPayment(payment);
                    checkout.CompleteAllocation(payment.Id, confirmedAt);
                }
                else
                {
                    checkout.RequireReconciliation(
                        payment.Id,
                        confirmedAt,
                        reconciliationReason);
                }

                await _unitOfWorkManager.Current.SaveChangesAsync();
                return new ProgrammePaymentConfirmationResult(
                    payment.Id,
                    checkout.EntryParticipationId,
                    ProgrammeParticipationKind.Entry,
                    wasAlreadyProcessed,
                    allocationReconciliationRequired: reconciliationReason != null);
            }
        }

        private static string ReconciliationReason(
            AQGreenMonthlyObligationCheckout checkout,
            EntryMonthlyObligation obligation,
            MemberPayment payment,
            EntryMonthlyObligation existingAllocation)
        {
            if (existingAllocation != null &&
                existingAllocation.Id != checkout.EntryMonthlyObligationId)
                return "The provider payment is already associated with another AQGreen monthly obligation.";
            if (obligation == null || obligation.IsDeleted)
                return "The checkout's recorded AQGreen monthly obligation is unavailable.";
            if (obligation.Id != checkout.EntryMonthlyObligationId ||
                obligation.EntryParticipationId != checkout.EntryParticipationId ||
                obligation.TenantId != checkout.TenantId ||
                obligation.CustomerId != checkout.CustomerId ||
                obligation.PeriodYear != checkout.PeriodYear ||
                obligation.PeriodMonth != checkout.PeriodMonth ||
                obligation.AmountDue != checkout.Amount ||
                !string.Equals(obligation.Currency, checkout.Currency, StringComparison.Ordinal))
                return "The checkout and AQGreen monthly obligation financial identity do not match.";
            if (obligation.Status == EntryMonthlyObligationStatus.Paid ||
                obligation.PaymentId.HasValue)
                return obligation.PaymentId == payment.Id
                    ? null
                    : "The AQGreen monthly obligation was already settled by another payment.";
            if (obligation.OutstandingAmount != checkout.Amount)
                return "The checkout and AQGreen monthly obligation financial identity do not match.";
            return null;
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
                if (participation.IsAwaitingApproval)
                {
                    await _approvalNotificationScheduler.ScheduleAsync(
                        payment,
                        participation.Id,
                        participation.Kind,
                        participation.ConfirmedJoiningAmount);
                }
                await _unitOfWorkManager.Current.SaveChangesAsync();

                return new ProgrammePaymentConfirmationResult(
                    payment.Id,
                    participation.Id,
                    participation.Kind,
                    wasAlreadyProcessed,
                    participation.IsAwaitingApproval);
            }
        }

        private async Task<(
            Guid Id,
            ProgrammeParticipationKind Kind,
            bool IsAwaitingApproval,
            decimal ConfirmedJoiningAmount)> ApplyToParticipationAsync(
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
                    onyxParticipation.IsAwaitingAdministrativeApproval,
                    onyxParticipation.DirectEntryAmount);
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

            if (payment.Purpose == MemberPaymentPurpose.AQGreenJoining)
            {
                entryParticipation.ApplyConfirmedJoiningPayment(payment);
                await ApplyFuneralCoverInclusionIfCompletedAsync(
                    entryParticipation,
                    payment.ConfirmedAt ?? throw new InvalidOperationException(
                        "A confirmed AQGreen joining payment must have a confirmation time."));
            }
            else
            {
                entryParticipation.ApplyConfirmedActivationPayment(payment);
            }
            return (
                entryParticipation.Id,
                ProgrammeParticipationKind.Entry,
                entryParticipation.IsAwaitingAdministrativeApproval,
                entryParticipation.JoiningPaymentAmount > 0m
                    ? entryParticipation.GetConfirmedJoiningAmount()
                    : entryParticipation.RegistrationPaymentAmount +
                      entryParticipation.ActivationPaymentAmount);
        }

        private async Task ApplyFuneralCoverInclusionIfCompletedAsync(
            EntryParticipation participation,
            DateTime joiningCompletedAt)
        {
            if (!participation.IsJoiningObligationSatisfied)
            {
                return;
            }

            await _funeralCoverInclusionProcessor.EnsureIncludedAsync(
                participation,
                joiningCompletedAt);
        }

        private static void EnsureSupportedPurpose(MemberPaymentPurpose purpose)
        {
            if (purpose != MemberPaymentPurpose.EntryRegistration &&
                purpose != MemberPaymentPurpose.EntryActivation &&
                purpose != MemberPaymentPurpose.AQGreenJoining &&
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

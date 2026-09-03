using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Threading.Tasks;
using Abp.Application.Services.Dto;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using AqualLifeStyle.Application.Admin.ProgrammeParticipations.Dto;
using AqualLifeStyle.Application.ProgrammeParticipations;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.AQGreen;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Email;
using AqualLifeStyle.MultiTenancy;
using AqualLifeStyle.Payments;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    [Audited]
    public class AdminProgrammeParticipationAppService
        : AdminAppServiceBase, IAdminProgrammeParticipationAppService
    {
        private const int MaximumGraduationTransactionAttempts = 3;
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _onyxLoanAgreementRepository;
        private readonly IRepository<OnyxGraduationDecision, Guid> _graduationDecisionRepository;
        private readonly IRepository<AQGreenV2GraduationEvidence, Guid>
            _graduationEvidenceRepository;
        private readonly IRepository<Membership> _membershipRepository;
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _aqGreenJoiningCheckoutRepository;
        private readonly IRepository<AQGreenMonthlyObligationCheckout, Guid>
            _monthlyObligationCheckoutRepository;
        private readonly IRepository<EntryMonthlyObligation, Guid>
            _monthlyObligationRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<AreaAdminAssignment, Guid> _areaAdminAssignmentRepository;
        private readonly IProgrammeRecruiterCorrectionPolicyResolver _correctionPolicyResolver;
        private readonly IProgrammeRecruiterCorrectionLock _correctionLock;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly ActiveProgrammeParticipantRoleSynchronizer _participantRoleSynchronizer;
        private readonly ITransactionalEmailOutbox _emailOutbox;
        private readonly TransactionalEmailTemplateBuilder _emailTemplates;
        private readonly IAQGreenPlacementV2ApprovalGate _placementV2ApprovalGate;
        private readonly IAQGreenApprovalAuthorityStabilizer _approvalAuthorityStabilizer;
        private readonly IAQGreenPlacementTreeLock _placementTreeLock;
        private readonly IAQGreenPlacementClock _placementClock;
        private readonly IAQGreenPlacementAllocator _placementAllocator;
        private readonly IRepository<AQGreenRecruitmentAttribution, Guid>
            _attributionRepository;
        private readonly IRepository<AQGreenRecruitmentAttributionConfirmation, Guid>
            _attributionConfirmationRepository;
        private readonly IRepository<AQGreenNetworkPlacement, Guid> _networkPlacementRepository;
        private readonly IRepository<AQGreenPlacementTreeScope, Guid> _placementTreeScopeRepository;
        private readonly IAQGreenGraduationStructuralModelSelector
            _graduationStructuralModelSelector;
        private readonly IAQGreenGraduationStructuralEvidenceEvaluator
            _graduationStructuralEvidenceEvaluator;
        private readonly IAQGreenV2GraduationEvidenceReplayValidator
            _graduationEvidenceReplayValidator;
        private readonly IOnyxGraduationTransactionFailureClassifier
            _graduationFailureClassifier;

        public AdminProgrammeParticipationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<OnyxLoanAgreement, Guid> onyxLoanAgreementRepository,
            IRepository<OnyxGraduationDecision, Guid> graduationDecisionRepository,
            IRepository<AQGreenV2GraduationEvidence, Guid> graduationEvidenceRepository,
            IRepository<Membership> membershipRepository,
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenJoiningCheckoutRepository,
            IRepository<AQGreenMonthlyObligationCheckout, Guid>
                monthlyObligationCheckoutRepository,
            IRepository<EntryMonthlyObligation, Guid> monthlyObligationRepository,
            IRepository<Tenant> tenantRepository,
            IRepository<Area, Guid> areaRepository,
            IRepository<AreaAdminAssignment, Guid> areaAdminAssignmentRepository,
            IProgrammeRecruiterCorrectionPolicyResolver correctionPolicyResolver,
            IProgrammeRecruiterCorrectionLock correctionLock,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock,
            IUnitOfWorkManager unitOfWorkManager,
            ActiveProgrammeParticipantRoleSynchronizer participantRoleSynchronizer,
            ITransactionalEmailOutbox emailOutbox,
            TransactionalEmailTemplateBuilder emailTemplates,
            IAQGreenPlacementV2ApprovalGate placementV2ApprovalGate,
            IAQGreenApprovalAuthorityStabilizer approvalAuthorityStabilizer,
            IAQGreenPlacementTreeLock placementTreeLock,
            IAQGreenPlacementClock placementClock,
            IAQGreenPlacementAllocator placementAllocator,
            IRepository<AQGreenRecruitmentAttribution, Guid> attributionRepository,
            IRepository<AQGreenRecruitmentAttributionConfirmation, Guid>
                attributionConfirmationRepository,
            IRepository<AQGreenNetworkPlacement, Guid> networkPlacementRepository,
            IRepository<AQGreenPlacementTreeScope, Guid> placementTreeScopeRepository,
            IAQGreenGraduationStructuralModelSelector graduationStructuralModelSelector,
            IAQGreenGraduationStructuralEvidenceEvaluator graduationStructuralEvidenceEvaluator,
            IAQGreenV2GraduationEvidenceReplayValidator graduationEvidenceReplayValidator,
            IOnyxGraduationTransactionFailureClassifier graduationFailureClassifier)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _onyxLoanAgreementRepository = onyxLoanAgreementRepository;
            _graduationDecisionRepository = graduationDecisionRepository;
            _graduationEvidenceRepository = graduationEvidenceRepository;
            _membershipRepository = membershipRepository;
            _paymentRepository = paymentRepository;
            _aqGreenJoiningCheckoutRepository = aqGreenJoiningCheckoutRepository;
            _monthlyObligationCheckoutRepository = monthlyObligationCheckoutRepository;
            _monthlyObligationRepository = monthlyObligationRepository;
            _tenantRepository = tenantRepository;
            _areaRepository = areaRepository;
            _areaAdminAssignmentRepository = areaAdminAssignmentRepository;
            _correctionPolicyResolver = correctionPolicyResolver;
            _correctionLock = correctionLock;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
            _unitOfWorkManager = unitOfWorkManager;
            _participantRoleSynchronizer = participantRoleSynchronizer;
            _emailOutbox = emailOutbox;
            _emailTemplates = emailTemplates;
            _placementV2ApprovalGate = placementV2ApprovalGate;
            _approvalAuthorityStabilizer = approvalAuthorityStabilizer;
            _placementTreeLock = placementTreeLock;
            _placementClock = placementClock;
            _placementAllocator = placementAllocator;
            _attributionRepository = attributionRepository;
            _attributionConfirmationRepository = attributionConfirmationRepository;
            _networkPlacementRepository = networkPlacementRepository;
            _placementTreeScopeRepository = placementTreeScopeRepository;
            _graduationStructuralModelSelector = graduationStructuralModelSelector;
            _graduationStructuralEvidenceEvaluator = graduationStructuralEvidenceEvaluator;
            _graduationEvidenceReplayValidator = graduationEvidenceReplayValidator;
            _graduationFailureClassifier = graduationFailureClassifier;
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.TerminatePaymentCheckouts)]
        [UnitOfWork(IsDisabled = true)]
        public async Task TerminateAQGreenJoiningCheckoutAsync(
            TerminateAQGreenJoiningCheckoutInput input)
        {
            if (input == null || input.CheckoutId == Guid.Empty)
                throw new Abp.UI.UserFriendlyException(
                    "AQGreen checkout termination failed.",
                    "Select a valid AQGreen joining checkout.");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Cross-Area checkout management requires permission to manage all Areas.");

            AQGreenJoiningCheckout checkout;
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = IsolationLevel.Serializable
            }))
            using (DisableAllTenantDataFiltersForHost())
            {
                await _hostedPaymentCheckoutLock.AcquireCheckoutAsync(input.CheckoutId);
                checkout = await _aqGreenJoiningCheckoutRepository.FirstOrDefaultAsync(
                    item => item.Id == input.CheckoutId);
                if (checkout == null)
                    throw new Abp.UI.UserFriendlyException(
                        "AQGreen checkout termination failed.",
                        "The checkout was not found in your Area.");
                ValidateRequestedTenant(checkout.TenantId, "AQGreen checkout termination");
                var customer = await _customerRepository.FirstOrDefaultAsync(
                    item => item.Id == checkout.CustomerId &&
                            item.TenantId == checkout.TenantId);
                await EnsureCanAdministerAreaAsync(customer);
                checkout.TerminateByAdministrator(
                    AbpSession.GetUserId(),
                    DateTime.UtcNow,
                    input.Evidence);
                await CurrentUnitOfWork.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            Logger.Warn(
                $"AQGreen checkout administratively terminated tenant={checkout.TenantId} checkout={checkout.Id} administrator={AbpSession.GetUserId()}");
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.ViewPaymentCheckouts)]
        public async Task<PagedResultDto<AQGreenJoiningCheckoutRecoveryDto>>
            GetAQGreenJoiningCheckoutsAsync(AQGreenJoiningCheckoutListInput input)
        {
            input ??= new AQGreenJoiningCheckoutListInput();
            ValidateRequestedTenant(input.TenantId, "AQGreen checkout");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Host-wide checkout access requires permission to view all Areas.");

            using (DisableAllTenantDataFiltersForHost())
            {
                var areaScope = await GetAuthorizedAreaScopeAsync(requestedAreaId: null);
                var query =
                    from checkout in _aqGreenJoiningCheckoutRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on checkout.CustomerId equals customer.Id
                    join area in _areaRepository.GetAll()
                        on new { checkout.TenantId, customer.AreaId }
                        equals new { area.TenantId, AreaId = (Guid?)area.Id }
                    where checkout.Status == HostedPaymentCheckoutStatus.PreparingCheckout ||
                          checkout.Status == HostedPaymentCheckoutStatus.AwaitingPayment
                    select new
                    {
                        Checkout = checkout,
                        Customer = customer,
                        AreaName = area.Name
                    };
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(row => row.Checkout.TenantId == tenantId);
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(row => row.Checkout.TenantId == tenantId);
                }
                if (areaScope != null)
                {
                    query = query.Where(row =>
                        row.Customer.AreaId.HasValue &&
                        areaScope.Contains(row.Customer.AreaId.Value));
                }
                if (!string.IsNullOrWhiteSpace(input.Keyword))
                {
                    var keyword = input.Keyword.Trim();
                    query = query.Where(row =>
                        row.Customer.ClubMemberNumber.Contains(keyword) ||
                        row.Customer.Name.Contains(keyword) ||
                        row.Checkout.ProviderCheckoutId.Contains(keyword));
                }

                var total = await query.CountAsync();
                var rows = await query
                    .OrderBy(row => row.Checkout.CheckoutCreatedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                return new PagedResultDto<AQGreenJoiningCheckoutRecoveryDto>(
                    total,
                    rows.Select(row => new AQGreenJoiningCheckoutRecoveryDto
                    {
                        CheckoutId = row.Checkout.Id,
                        TenantId = row.Checkout.TenantId,
                        AreaName = row.AreaName,
                        ClubMemberNumber = row.Customer.ClubMemberNumber,
                        CustomerName = row.Customer.Name,
                        Amount = row.Checkout.Amount,
                        Currency = row.Checkout.Currency,
                        Status = row.Checkout.Status,
                        Schedule = row.Checkout.Schedule,
                        Stage = row.Checkout.Stage,
                        CreatedAt = row.Checkout.CreatedAt,
                        CheckoutCreatedAt = row.Checkout.CheckoutCreatedAt,
                        ProviderCheckoutId = row.Checkout.ProviderCheckoutId,
                        PaymentId = row.Checkout.PaymentId,
                        LockReason = row.Checkout.Status ==
                                     HostedPaymentCheckoutStatus.PreparingCheckout
                            ? "Checkout creation is still being finalised."
                            : "Awaiting authoritative provider confirmation or authorised termination."
                    }).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.GraduateToOnyx)]
        [UnitOfWork(IsDisabled = true)]
        public async Task<OnyxGraduationDecisionDto> GraduateAQGreenToOnyxAsync(
            GraduateAQGreenToOnyxInput input)
        {
            if (input == null || input.LoanAgreementId == Guid.Empty)
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation failed.",
                    "Select a valid approved loan agreement.");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Cross-Area Onyx graduation requires permission to manage all Areas.");

            for (var attempt = 1;
                 attempt <= MaximumGraduationTransactionAttempts;
                 attempt++)
            {
                var commitWasAttempted = false;
                try
                {
                    OnyxGraduationDecision decision;
                    using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                    {
                        Scope = TransactionScopeOption.RequiresNew,
                        IsTransactional = true,
                        IsolationLevel = IsolationLevel.Serializable
                    }))
                    using (DisableAllTenantDataFiltersForHost())
                    {
                        await _correctionLock.AcquireAsync(ProgrammeRecruiterNetwork.Onyx);
                        decision = await _graduationDecisionRepository.FirstOrDefaultAsync(
                            item => item.LoanAgreementId == input.LoanAgreementId);
                        if (decision != null)
                        {
                            await ValidateExistingGraduationAsync(
                                decision,
                                input.LoanAgreementId);
                            await uow.CompleteAsync();
                            return Map(decision);
                        }

                        var loan = await _onyxLoanAgreementRepository.FirstOrDefaultAsync(
                            item => item.Id == input.LoanAgreementId);
                        if (loan == null)
                            throw new Abp.UI.UserFriendlyException(
                                "Onyx graduation failed.",
                                "The approved loan agreement was not found in your Area.");
                        ValidateRequestedTenant(loan.TenantId, "Onyx graduation");
                        var customer = await _customerRepository.FirstOrDefaultAsync(
                            item => item.Id == loan.CustomerId &&
                                    item.TenantId == loan.TenantId);
                        await EnsureCanAdministerAreaAsync(customer);

                        using (_unitOfWorkManager.Current.SetTenantId(loan.TenantId))
                        {
                            var aqGreen = await _entryParticipationRepository.FirstOrDefaultAsync(
                                item => item.Id == loan.EntryParticipationId &&
                                        item.CustomerId == loan.CustomerId &&
                                        item.TenantId == loan.TenantId);
                            var acceptedAgreementTerms =
                                ValidateGraduationEligibility(aqGreen, loan);

                            var existingOnyx = await _onyxParticipationRepository
                                .FirstOrDefaultAsync(
                                    item => item.TenantId == loan.TenantId &&
                                            item.CustomerId == loan.CustomerId);
                            if (existingOnyx != null)
                                throw ReconciliationRequired(
                                    "An Onyx participation already exists without this graduation decision.");

                            Membership membership;
                            using (CurrentUnitOfWork.DisableFilter(
                                       AbpDataFilters.MayHaveTenant))
                            {
                                membership = await _membershipRepository.GetAll()
                                    .Where(item => item.IsActive &&
                                                   item.MembershipType == MembershipType.Onyx &&
                                                   (!item.TenantId.HasValue ||
                                                    item.TenantId == loan.TenantId))
                                    .OrderByDescending(item => item.TenantId.HasValue)
                                    .FirstOrDefaultAsync();
                            }
                            if (membership == null)
                                throw new Abp.UI.UserFriendlyException(
                                    "Onyx graduation failed.",
                                    "The Onyx programme has not been configured for this Area.");

                            var decidedAt = DateTime.UtcNow;
                            var model = await _graduationStructuralModelSelector.SelectAsync(
                                loan.TenantId,
                                aqGreen.Id);
                            var onyx = OnyxParticipation.GraduateFromAQGreenIndependently(
                                aqGreen,
                                loan,
                                membership.Id,
                                acceptedAgreementTerms,
                                decidedAt);
                            AQGreenV2GraduationEvidence evidence = null;
                            if (model == AQGreenGraduationStructuralModel.LegacyV1)
                            {
                                var network = await _entryParticipationRepository
                                    .GetAllIncluding(item => item.RecruiterCorrections)
                                    .Where(item => item.TenantId == loan.TenantId &&
                                                   item.Status == EntryParticipationStatus.Active)
                                    .ToListAsync();
                                var evaluatedLevel = new EntryNetworkQualificationEvaluator()
                                    .Evaluate(aqGreen.CustomerId, network);
                                if (evaluatedLevel < EntryNetworkLevel.Level2)
                                    throw IneligibleForGraduation();
                                decision = OnyxGraduationDecision.RecordApproval(
                                    aqGreen,
                                    loan,
                                    onyx,
                                    evaluatedLevel,
                                    AbpSession.GetUserId(),
                                    input.Justification,
                                    decidedAt);
                            }
                            else if (model == AQGreenGraduationStructuralModel.PlacementV2)
                            {
                                var structuralEvidence =
                                    await _graduationStructuralEvidenceEvaluator.EvaluateAsync(
                                        loan.TenantId,
                                        aqGreen.Id,
                                        decidedAt);
                                if (structuralEvidence.StructuralCompletionLevel <
                                    AQGreenStructuralCompletionLevel.Level2)
                                    throw IneligibleForGraduation();
                                decision = OnyxGraduationDecision.RecordPlacementV2Approval(
                                    aqGreen,
                                    loan,
                                    onyx,
                                    structuralEvidence,
                                    AbpSession.GetUserId(),
                                    input.Justification,
                                    decidedAt);
                                evidence = AQGreenV2GraduationEvidence.Capture(
                                    decision,
                                    structuralEvidence);
                            }
                            else
                            {
                                throw new InvalidOperationException(
                                    $"AQGreen graduation structural model '{model}' is unsupported.");
                            }

                            await _onyxParticipationRepository.InsertAsync(onyx);
                            await _graduationDecisionRepository.InsertAsync(decision);
                            if (evidence != null)
                                await _graduationEvidenceRepository.InsertAsync(evidence);
                            await CurrentUnitOfWork.SaveChangesAsync();
                        }

                        commitWasAttempted = true;
                        await uow.CompleteAsync();
                    }

                    Logger.Info(
                        $"Onyx graduation approved tenant={decision.TenantId} decision={decision.Id} participation={decision.OnyxParticipationId} model={decision.StructuralModel}");
                    return Map(decision);
                }
                catch (Exception exception)
                {
                    var failure = _graduationFailureClassifier.Classify(
                        exception,
                        commitWasAttempted);
                    if (failure.Kind ==
                            OnyxGraduationTransactionFailureKind.SerializationFailure &&
                        attempt < MaximumGraduationTransactionAttempts)
                        continue;
                    if (failure.Kind ==
                            OnyxGraduationTransactionFailureKind.KnownGraduationUniqueCollision ||
                        failure.Kind ==
                            OnyxGraduationTransactionFailureKind.CommitOutcomeUnknown)
                    {
                        var reconciled = await ReconcileGraduationAfterFailureAsync(
                            input.LoanAgreementId);
                        if (reconciled != null) return Map(reconciled);
                        if (failure.Kind ==
                                OnyxGraduationTransactionFailureKind.CommitOutcomeUnknown &&
                            attempt < MaximumGraduationTransactionAttempts)
                            continue;
                        throw ReconciliationRequired(
                            "No coherent durable graduation could be recovered after the transaction failure.");
                    }

                    throw;
                }
            }

            throw ReconciliationRequired(
                "The graduation transaction could not be completed after bounded retries.");
        }

        private static OnyxPlanTerms ValidateGraduationEligibility(
            EntryParticipation aqGreen,
            OnyxLoanAgreement loan)
        {
            if (aqGreen == null || aqGreen.Status != EntryParticipationStatus.Active)
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation failed.",
                    "The linked AQGreen participation is no longer active.");
            if (loan.Status != OnyxLoanAgreementStatus.Active ||
                !loan.EffectiveAt.HasValue ||
                !loan.MemberAcceptedAt.HasValue ||
                !loan.MemberAcceptedByUserId.HasValue ||
                !loan.ApprovedAt.HasValue ||
                !loan.ApprovedByAdministratorUserId.HasValue)
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation failed.",
                    "The loan must be active, member-accepted, and administrator-approved.");
            if (loan.TenantId != aqGreen.TenantId ||
                loan.CustomerId != aqGreen.CustomerId ||
                loan.EntryParticipationId != aqGreen.Id)
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation failed.",
                    "The accepted loan agreement terms or AQGreen linkage are invalid.");

            try
            {
                return OnyxPlanTerms.FromCanonicalAcceptedAgreement(loan);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation failed.",
                    "The accepted loan agreement terms are invalid.");
            }
        }

        private async Task ValidateExistingGraduationAsync(
            OnyxGraduationDecision decision,
            Guid requestedLoanAgreementId)
        {
            if (decision.LoanAgreementId != requestedLoanAgreementId)
                throw new Abp.UI.UserFriendlyException(
                    "Onyx graduation retry conflict.",
                    "The durable graduation belongs to a different accepted agreement.");

            ValidateRequestedTenant(decision.TenantId, "Onyx graduation");
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item => item.Id == decision.CustomerId &&
                        item.TenantId == decision.TenantId);
            await EnsureCanAdministerAreaAsync(customer);

            var isHistoricalLegacy =
                decision.StructuralModel == AQGreenGraduationStructuralModel.LegacyV1 &&
                decision.GraduationRulesVersion == null &&
                decision.EvaluatedLoanTermsVersion == null;
            if (decision.StructuralModel != AQGreenGraduationStructuralModel.LegacyV1 &&
                decision.StructuralModel != AQGreenGraduationStructuralModel.PlacementV2)
                throw ReconciliationRequired(
                    "The durable graduation uses an unsupported structural model.");
            if (!isHistoricalLegacy && !OnyxGraduationRules.IsSupportedVersion(
                    decision.GraduationRulesVersion))
                throw ReconciliationRequired(
                    "The durable graduation uses an unsupported graduation rules version.");

            if (!isHistoricalLegacy &&
                ((decision.StructuralModel == AQGreenGraduationStructuralModel.LegacyV1 &&
                 (!decision.EvaluatedNetworkLevel.HasValue ||
                  decision.EvaluatedNetworkLevel.Value < EntryNetworkLevel.Level2)) ||
                (decision.StructuralModel == AQGreenGraduationStructuralModel.PlacementV2 &&
                 decision.EvaluatedNetworkLevel.HasValue) ||
                !decision.AQGreenWasActive ||
                !decision.LoanWasActive ||
                !decision.LoanWasAccepted ||
                !decision.LoanWasAdministratorApproved))
                throw ReconciliationRequired(
                    "The durable graduation decision is incomplete.");

            var loan = await _onyxLoanAgreementRepository.FirstOrDefaultAsync(
                item => item.Id == decision.LoanAgreementId);
            var aqGreen = await _entryParticipationRepository.FirstOrDefaultAsync(
                item => item.Id == decision.EntryParticipationId);
            var onyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                item => item.Id == decision.OnyxParticipationId);
            var customerOnyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                item => item.TenantId == decision.TenantId &&
                        item.CustomerId == decision.CustomerId);
            if (loan == null || aqGreen == null || onyx == null || customerOnyx == null ||
                customerOnyx.Id != onyx.Id ||
                loan.TenantId != decision.TenantId ||
                loan.CustomerId != decision.CustomerId ||
                loan.EntryParticipationId != decision.EntryParticipationId ||
                aqGreen.TenantId != decision.TenantId ||
                aqGreen.CustomerId != decision.CustomerId ||
                onyx.TenantId != decision.TenantId ||
                onyx.CustomerId != decision.CustomerId ||
                onyx.AdmissionRoute != OnyxAdmissionRoute.EntryGraduation ||
                onyx.Status != OnyxParticipationStatus.Active ||
                onyx.StartedAt != decision.DecidedAt ||
                onyx.ActivatedAt != decision.DecidedAt ||
                onyx.EntryParticipationId != decision.EntryParticipationId ||
                onyx.LoanAgreementId != decision.LoanAgreementId)
                throw ReconciliationRequired(
                    "The durable graduation terminal graph is inconsistent.");

            if (isHistoricalLegacy)
            {
                if (!loan.EffectiveAt.HasValue ||
                    loan.EffectiveAt.Value > decision.DecidedAt ||
                    onyx.TermsEffectiveFrom > onyx.StartedAt ||
                    decision.EvaluatedFundingAmount != loan.PrincipalAmount ||
                    !string.Equals(
                        decision.EvaluatedFundingCurrency,
                        loan.Currency,
                        StringComparison.Ordinal) ||
                    onyx.DirectEntryAmount != loan.PrincipalAmount ||
                    !string.Equals(onyx.Currency, loan.Currency, StringComparison.Ordinal))
                    throw ReconciliationRequired(
                        "The historical Legacy V1 graduation terminal graph is inconsistent.");

                return;
            }

            OnyxPlanTerms acceptedAgreementTerms;
            try
            {
                acceptedAgreementTerms =
                    OnyxPlanTerms.FromCanonicalAcceptedAgreement(loan);
            }
            catch (Exception exception) when (
                exception is ArgumentException ||
                exception is InvalidOperationException)
            {
                throw ReconciliationRequired(
                    "The durable graduation accepted agreement is not canonical.");
            }

            if (decision.EvaluatedFundingAmount !=
                    acceptedAgreementTerms.DirectEntryAmount ||
                !string.Equals(
                    decision.EvaluatedFundingCurrency,
                    acceptedAgreementTerms.Currency,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    decision.EvaluatedLoanTermsVersion,
                    acceptedAgreementTerms.Version,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    onyx.TermsVersion,
                    acceptedAgreementTerms.Version,
                    StringComparison.Ordinal) ||
                onyx.TermsEffectiveFrom != acceptedAgreementTerms.EffectiveFrom ||
                onyx.DirectEntryAmount != acceptedAgreementTerms.DirectEntryAmount ||
                !string.Equals(
                    onyx.Currency,
                    acceptedAgreementTerms.Currency,
                    StringComparison.Ordinal))
                throw ReconciliationRequired(
                    "The durable graduation graph conflicts with its accepted agreement.");

            if (decision.StructuralModel == AQGreenGraduationStructuralModel.LegacyV1)
                return;

            try
            {
                await _graduationEvidenceReplayValidator.ValidateAsync(decision.Id);
            }
            catch (Exception exception) when (
                exception is AQGreenGraduationEvidenceReplayException ||
                exception is AQGreenGraduationEvidenceVersionNotSupportedException)
            {
                throw ReconciliationRequired(
                    "The durable Placement V2 evidence cannot reproduce the graduation decision.");
            }
        }

        private async Task<OnyxGraduationDecision> ReconcileGraduationAfterFailureAsync(
            Guid loanAgreementId)
        {
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                Scope = TransactionScopeOption.RequiresNew,
                IsTransactional = true,
                IsolationLevel = IsolationLevel.ReadCommitted
            }))
            using (DisableAllTenantDataFiltersForHost())
            {
                var decision = await _graduationDecisionRepository.FirstOrDefaultAsync(
                    item => item.LoanAgreementId == loanAgreementId);
                if (decision != null)
                {
                    await ValidateExistingGraduationAsync(decision, loanAgreementId);
                    await uow.CompleteAsync();
                    return decision;
                }

                var loan = await _onyxLoanAgreementRepository.FirstOrDefaultAsync(
                    item => item.Id == loanAgreementId);
                if (loan == null)
                    throw ReconciliationRequired(
                        "The accepted agreement disappeared during graduation reconciliation.");
                ValidateRequestedTenant(loan.TenantId, "Onyx graduation");
                var customer = await _customerRepository.FirstOrDefaultAsync(
                    item => item.Id == loan.CustomerId &&
                            item.TenantId == loan.TenantId);
                await EnsureCanAdministerAreaAsync(customer);
                var conflictingOnyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    item => item.TenantId == loan.TenantId &&
                            item.CustomerId == loan.CustomerId);
                if (conflictingOnyx != null)
                    throw ReconciliationRequired(
                        "An Onyx participation committed without the requested graduation decision.");

                await uow.CompleteAsync();
                return null;
            }
        }

        private static Abp.UI.UserFriendlyException IneligibleForGraduation() =>
            new(
                "Onyx graduation failed.",
                "The member no longer satisfies AQGreen Level 2 qualification.");

        private static Abp.UI.UserFriendlyException ReconciliationRequired(
            string details) =>
            new("Onyx graduation requires reconciliation.", details);

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.Approve)]
        [UnitOfWork(IsDisabled = true)]
        public async Task ApproveProgrammeParticipationAsync(
            ApproveProgrammeParticipationInput input)
        {
            if (input == null || input.ParticipationId == Guid.Empty)
                throw Failed("Participation approval", "Select a valid participation awaiting approval.");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Cross-Area participation approval requires permission to manage all Areas.");

            var usePlacementV2 = input.Programme == AdminProgrammeType.Entry &&
                                 await _placementV2ApprovalGate.IsEnabledAsync(
                                     AbpSession.TenantId,
                                     input.ParticipationId);
            Customer customer;
            Guid participationId;
            bool decisionApplied;
            using (var userLockUow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = false
            }))
            using (DisableAllTenantDataFiltersForHost())
            {
                var approvalUserId = await ResolveApprovalUserIdHintAsync(
                    input.Programme,
                    input.ParticipationId);
                await _hostedPaymentCheckoutLock
                    .AcquireProgrammeApprovalUserSessionAsync(approvalUserId);
                try
                {
                    using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
                    {
                        Scope = TransactionScopeOption.RequiresNew,
                        IsTransactional = true,
                        IsolationLevel = usePlacementV2
                            ? IsolationLevel.ReadCommitted
                            : IsolationLevel.Serializable
                    }))
                    using (DisableAllTenantDataFiltersForHost())
                    {
                        await _hostedPaymentCheckoutLock
                            .AcquireProgrammeParticipationDecisionAsync(input.ParticipationId);
                        (customer, participationId, decisionApplied) = usePlacementV2
                            ? await ApplyAQGreenV2ApprovalAsync(input.ParticipationId)
                            : await ApplyDecisionAsync(
                                input.Programme,
                                input.ParticipationId,
                                approve: true,
                                reason: null);
                        if (customer.UserId != approvalUserId)
                            throw new AQGreenPlacementConflictException(
                                "The programme participant user changed before approval could be locked.");
                        if (decisionApplied)
                        {
                            await _participantRoleSynchronizer
                                .PromoteGuestToMemberAsync(customer.Id);
                            await EnqueueDecisionEmailAsync(
                                customer,
                                input.Programme,
                                participationId,
                                approved: true,
                                reason: null);
                        }
                        await CurrentUnitOfWork.SaveChangesAsync();
                        await uow.CompleteAsync();
                    }
                }
                finally
                {
                    await _hostedPaymentCheckoutLock
                        .ReleaseProgrammeApprovalUserSessionAsync(approvalUserId);
                }
                await userLockUow.CompleteAsync();
            }

            Logger.Info(
                $"Programme participation approved programme={input.Programme} participation={participationId} administrator={AbpSession.GetUserId()}");
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.Approve)]
        [UnitOfWork(IsDisabled = true)]
        public async Task RejectProgrammeParticipationAsync(
            RejectProgrammeParticipationInput input)
        {
            if (input == null || input.ParticipationId == Guid.Empty)
                throw Failed("Participation rejection", "Select a valid participation awaiting approval.");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Cross-Area participation rejection requires permission to manage all Areas.");

            var usePlacementV2 = input.Programme == AdminProgrammeType.Entry &&
                                 await _placementV2ApprovalGate.IsEnabledAsync(
                                     AbpSession.TenantId,
                                     input.ParticipationId);
            Customer customer;
            Guid participationId;
            bool decisionApplied;
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = usePlacementV2
                    ? IsolationLevel.ReadCommitted
                    : IsolationLevel.Serializable
            }))
            using (DisableAllTenantDataFiltersForHost())
            {
                await _hostedPaymentCheckoutLock
                    .AcquireProgrammeParticipationDecisionAsync(input.ParticipationId);
                (customer, participationId, decisionApplied) = usePlacementV2
                    ? await ApplyAQGreenV2RejectionAsync(
                        input.ParticipationId,
                        input.Reason)
                    : await ApplyDecisionAsync(
                        input.Programme,
                        input.ParticipationId,
                        approve: false,
                        reason: input.Reason);
                if (decisionApplied)
                {
                    await EnqueueDecisionEmailAsync(
                        customer,
                        input.Programme,
                        participationId,
                        approved: false,
                        reason: input.Reason);
                }
                await CurrentUnitOfWork.SaveChangesAsync();
                await uow.CompleteAsync();
            }

            Logger.Warn(
                $"Programme participation rejected programme={input.Programme} participation={participationId} administrator={AbpSession.GetUserId()}");
        }

        private async Task<(Customer Customer, Guid ParticipationId, bool DecisionApplied)>
            ApplyAQGreenV2ApprovalAsync(Guid participationId)
        {
            var hint = await _entryParticipationRepository.GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == participationId);
            if (hint == null)
                throw Failed(
                    "Participation approval",
                    "The participation was not found in your Area.");
            ValidateRequestedTenant(hint.TenantId, "Participation approval");

            var administratorUserId = AbpSession.GetUserId();
            var stabilizedAreaId = await _approvalAuthorityStabilizer.StabilizeAsync(
                hint.TenantId,
                hint.CustomerId,
                AbpSession.TenantId.HasValue ? administratorUserId : (long?)null);

            var entry = await _entryParticipationRepository.FirstOrDefaultAsync(
                item => item.Id == participationId && item.TenantId == hint.TenantId);
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item => item.Id == hint.CustomerId && item.TenantId == hint.TenantId);
            if (entry == null || customer == null ||
                entry.CustomerId != customer.Id ||
                customer.AreaId != stabilizedAreaId)
                throw new AQGreenPlacementConflictException(
                    "AQGreen approval facts changed before authority could be stabilized.");

            await EnsureCanAdministerAreaAsync(customer);
            await ValidateAQGreenJoiningPaymentAsync(entry);
            var attribution = await GetAQGreenAttributionAsync(entry);
            if (attribution.AttributionKind ==
                AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot)
            {
                await EnsureCanBootstrapAQGreenRootAsync();
                return await ApplyAQGreenRootBootstrapAsync(
                    customer,
                    entry,
                    attribution,
                    administratorUserId);
            }
            if (attribution.AttributionKind !=
                AQGreenRecruitmentAttributionKind.SponsoredParticipant)
                throw new AQGreenPlacementUnsupportedAttributionException(
                    attribution.AttributionKind);

            var scopeHint = await ResolveAQGreenPlacementScopeAsync(entry);
            await _placementTreeLock.AcquireAsync(scopeHint);
            var authoritativeScope = await ResolveAQGreenPlacementScopeAsync(entry);
            if (authoritativeScope != scopeHint)
                throw new AQGreenPlacementConflictException(
                    "The credited sponsor placement-tree scope changed before approval.");

            var existingPlacement = await _networkPlacementRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(placement =>
                    placement.TenantId == entry.TenantId &&
                    placement.ParticipantId == entry.Id);

            if (entry.Status == EntryParticipationStatus.Active)
            {
                if (existingPlacement == null)
                    throw new AQGreenPlacementConflictException(
                        "Active AQGreen participation is missing its permanent placement and requires reconciliation.");

                var replay = await _placementAllocator.AllocateAsync(
                    entry.TenantId,
                    entry.Id);
                if (!replay.WasAlreadyPlaced || replay.Placement.Id != existingPlacement.Id)
                    throw new AQGreenPlacementConflictException(
                        "Active AQGreen participation has conflicting placement evidence.");
                return (customer, entry.Id, false);
            }

            if (entry.Status != EntryParticipationStatus.PaymentConfirmedAwaitingApproval)
                throw Failed(
                    "Participation approval",
                    "The AQGreen participation is not awaiting administrative approval.");
            if (existingPlacement != null)
                throw new AQGreenPlacementConflictException(
                    "An awaiting AQGreen participation already has a placement and requires reconciliation.");

            var allocation = await _placementAllocator.AllocateAsync(
                entry.TenantId,
                entry.Id);
            if (allocation.WasAlreadyPlaced)
                throw new AQGreenPlacementConflictException(
                    "An awaiting AQGreen participation acquired an unexpected pre-existing placement.");

            entry.ApproveByAdministrator(administratorUserId, DateTime.UtcNow);
            return (customer, entry.Id, true);
        }

        private async Task<(Customer Customer, Guid ParticipationId, bool DecisionApplied)>
            ApplyAQGreenRootBootstrapAsync(
                Customer customer,
                EntryParticipation entry,
                AQGreenRecruitmentAttribution attribution,
                long administratorUserId)
        {
            ValidateAQGreenRootAttribution(entry, attribution);
            var confirmation = await _attributionConfirmationRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == entry.TenantId &&
                    item.AttributionId == attribution.Id);
            ValidateAQGreenRootConfirmation(attribution, confirmation);

            var existingPlacement = await _networkPlacementRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(placement =>
                    placement.TenantId == entry.TenantId &&
                    placement.ParticipantId == entry.Id);
            if (entry.Status == EntryParticipationStatus.Active)
            {
                await ValidateExistingAQGreenRootPlacementAsync(entry, existingPlacement);
                return (customer, entry.Id, false);
            }
            if (entry.Status != EntryParticipationStatus.PaymentConfirmedAwaitingApproval)
                throw Failed(
                    "Participation approval",
                    "The AQGreen participation is not awaiting administrative approval.");
            if (existingPlacement != null)
                throw new AQGreenPlacementConflictException(
                    "An awaiting AQGreen prospective root already has a placement and requires reconciliation.");

            var decidedAt = await _placementClock.GetUtcNowAsync();
            var scope = AQGreenPlacementTreeScope.Create(entry.TenantId);
            var root = AQGreenNetworkPlacement.CreateRoot(
                scope,
                entry.Id,
                decidedAt,
                AQGreenPlacementRules.CurrentVersion);
            await _placementTreeScopeRepository.InsertAsync(scope);
            await _networkPlacementRepository.InsertAsync(root);
            entry.ApproveByAdministrator(administratorUserId, decidedAt);
            return (customer, entry.Id, true);
        }

        private async Task ValidateExistingAQGreenRootPlacementAsync(
            EntryParticipation entry,
            AQGreenNetworkPlacement placement)
        {
            if (placement == null)
                throw new AQGreenPlacementConflictException(
                    "Active AQGreen prospective root is missing its permanent placement and requires reconciliation.");
            if (placement.PlacementParentParticipantId.HasValue ||
                placement.PlacementSlot.HasValue ||
                !string.Equals(placement.CanonicalPath, string.Empty, StringComparison.Ordinal) ||
                !entry.ActivatedAt.HasValue ||
                entry.ActivatedAt.Value != placement.PlacedAt)
                throw new AQGreenPlacementConflictException(
                    "Active AQGreen prospective root has conflicting placement evidence.");
            if (!await _placementTreeScopeRepository.GetAll()
                    .AsNoTracking()
                    .AnyAsync(scope =>
                        scope.Id == placement.PlacementTreeScopeId &&
                        scope.TenantId == entry.TenantId))
                throw new AQGreenPlacementConflictException(
                    "Active AQGreen prospective root is missing its placement-tree scope.");
        }

        private static void ValidateAQGreenRootAttribution(
            EntryParticipation entry,
            AQGreenRecruitmentAttribution attribution)
        {
            if (attribution.TenantId != entry.TenantId ||
                attribution.ParticipantId != entry.Id ||
                attribution.AttributionKind !=
                    AQGreenRecruitmentAttributionKind.AuthorisedProspectiveRoot ||
                attribution.CreditedSponsorParticipantId.HasValue ||
                attribution.AcquisitionSource !=
                    AQGreenAcquisitionSource.AuthorisedDirectAdmission ||
                attribution.SourceReferenceId == Guid.Empty ||
                !attribution.AttributedByUserId.HasValue ||
                attribution.AttributedByUserId.Value <= 0 ||
                string.IsNullOrWhiteSpace(attribution.AssignmentReason))
                throw new AQGreenPlacementConflictException(
                    "AQGreen prospective-root attribution is not valid bootstrap authority.");
        }

        private static void ValidateAQGreenRootConfirmation(
            AQGreenRecruitmentAttribution attribution,
            AQGreenRecruitmentAttributionConfirmation confirmation)
        {
            if (confirmation == null)
                throw new AQGreenPlacementAttributionNotConfirmedException();
            if (confirmation.TenantId != attribution.TenantId ||
                confirmation.AttributionId != attribution.Id ||
                confirmation.ConfirmationMethod !=
                    AQGreenAttributionConfirmationMethod.AuthorisedProspectiveRootConfirmation ||
                confirmation.EvidenceReferenceId == Guid.Empty ||
                confirmation.ConfirmedAt < attribution.AttributedAt)
                throw new AQGreenPlacementConflictException(
                    "AQGreen prospective-root confirmation conflicts with bootstrap authority.");
        }

        private async Task EnsureCanBootstrapAQGreenRootAsync()
        {
            if (AbpSession.TenantId.HasValue ||
                !await PermissionChecker.IsGrantedAsync(
                    AquaPermissions.Admin.ProgrammeParticipations.BootstrapAQGreenRoot))
                throw new AbpAuthorizationException(
                    "AQGreen root bootstrap requires dedicated host authorization.");
        }

        private async Task<AQGreenRecruitmentAttribution> GetAQGreenAttributionAsync(
            EntryParticipation participant)
        {
            var attribution = await _attributionRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == participant.TenantId &&
                    item.ParticipantId == participant.Id);
            if (attribution == null)
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.Attribution);
            return attribution;
        }

        private async Task<(Customer Customer, Guid ParticipationId, bool DecisionApplied)>
            ApplyAQGreenV2RejectionAsync(Guid participationId, string reason)
        {
            var hint = await _entryParticipationRepository.GetAll()
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == participationId);
            if (hint == null)
                throw Failed(
                    "Participation rejection",
                    "The participation was not found in your Area.");
            ValidateRequestedTenant(hint.TenantId, "Participation rejection");

            var administratorUserId = AbpSession.GetUserId();
            var stabilizedAreaId = await _approvalAuthorityStabilizer.StabilizeAsync(
                hint.TenantId,
                hint.CustomerId,
                AbpSession.TenantId.HasValue ? administratorUserId : (long?)null);
            var entry = await _entryParticipationRepository.FirstOrDefaultAsync(
                item => item.Id == participationId && item.TenantId == hint.TenantId);
            var customer = await _customerRepository.FirstOrDefaultAsync(
                item => item.Id == hint.CustomerId && item.TenantId == hint.TenantId);
            if (entry == null || customer == null || customer.AreaId != stabilizedAreaId)
                throw new AQGreenPlacementConflictException(
                    "AQGreen rejection facts changed before authority could be stabilized.");
            await EnsureCanAdministerAreaAsync(customer);

            if (entry.Status == EntryParticipationStatus.Rejected)
                return (customer, entry.Id, false);
            entry.RejectByAdministrator(administratorUserId, reason, DateTime.UtcNow);
            return (customer, entry.Id, true);
        }

        private async Task<Guid> ResolveAQGreenPlacementScopeAsync(
            EntryParticipation participant)
        {
            var attribution = await GetAQGreenAttributionAsync(participant);
            if (attribution.AttributionKind !=
                AQGreenRecruitmentAttributionKind.SponsoredParticipant)
                throw new AQGreenPlacementUnsupportedAttributionException(
                    attribution.AttributionKind);
            if (!attribution.CreditedSponsorParticipantId.HasValue)
                throw new AQGreenPlacementConflictException(
                    "Sponsored AQGreen attribution is missing its credited sponsor.");

            var confirmation = await _attributionConfirmationRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.TenantId == participant.TenantId &&
                    item.AttributionId == attribution.Id);
            if (confirmation == null)
                throw new AQGreenPlacementAttributionNotConfirmedException();
            if (confirmation.ConfirmationMethod !=
                AQGreenAttributionConfirmationMethod.MemberInvitationAcceptance)
                throw new AQGreenPlacementConflictException(
                    "AQGreen attribution confirmation conflicts with sponsored placement.");

            var sponsor = await _entryParticipationRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.Id == attribution.CreditedSponsorParticipantId.Value);
            if (sponsor == null)
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.SponsorParticipation);
            if (sponsor.TenantId != participant.TenantId)
                throw new AQGreenPlacementConflictException(
                    "AQGreen sponsorship cannot cross the Tenant boundary.");

            var sponsorPlacement = await _networkPlacementRepository.GetAll()
                .AsNoTracking()
                .SingleOrDefaultAsync(item =>
                    item.ParticipantId == sponsor.Id);
            if (sponsorPlacement == null)
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.SponsorPlacement);
            if (sponsorPlacement.TenantId != participant.TenantId)
                throw new AQGreenPlacementConflictException(
                    "The credited sponsor placement crosses the Tenant boundary.");
            if (!await _placementTreeScopeRepository.GetAll()
                    .AsNoTracking()
                    .AnyAsync(scope =>
                        scope.Id == sponsorPlacement.PlacementTreeScopeId &&
                        scope.TenantId == participant.TenantId))
                throw new AQGreenPlacementAllocationNotFoundException(
                    AQGreenPlacementMissingFact.PlacementTreeScope);
            return sponsorPlacement.PlacementTreeScopeId;
        }

        private async Task ValidateAQGreenJoiningPaymentAsync(
            EntryParticipation participation)
        {
            Guid[] paymentIds;
            if (participation.JoiningPaymentAmount > 0m)
            {
                if (!participation.IsJoiningObligationSatisfied)
                    throw new AQGreenPlacementConflictException(
                        "AQGreen approval requires a fully satisfied joining obligation.");
                paymentIds = participation.JoiningPaymentId.HasValue
                    ? new[] { participation.JoiningPaymentId.Value }
                    : new[]
                    {
                        participation.RegistrationPaymentId ?? Guid.Empty,
                        participation.ActivationPaymentId ?? Guid.Empty
                    };
            }
            else
            {
                paymentIds = new[]
                {
                    participation.RegistrationPaymentId ?? Guid.Empty,
                    participation.ActivationPaymentId ?? Guid.Empty
                };
            }

            if (paymentIds.Any(id => id == Guid.Empty) ||
                paymentIds.Distinct().Count() != paymentIds.Length)
                throw new AQGreenPlacementConflictException(
                    "AQGreen approval payment evidence is incomplete or conflicting.");

            var payments = await _paymentRepository.GetAll()
                .AsNoTracking()
                .Where(payment => paymentIds.Contains(payment.Id))
                .ToListAsync();
            if (payments.Count != paymentIds.Length || payments.Any(payment =>
                    payment.TenantId != participation.TenantId ||
                    payment.CustomerId != participation.CustomerId ||
                    payment.Status != MemberPaymentStatus.Confirmed ||
                    !payment.ConfirmedAt.HasValue ||
                    !string.Equals(
                        payment.Currency,
                        participation.Currency,
                        StringComparison.Ordinal)))
                throw new AQGreenPlacementConflictException(
                    "AQGreen approval payment evidence is not authoritative.");

            if (participation.JoiningPaymentAmount > 0m)
            {
                if (payments.Any(payment =>
                        payment.Purpose != MemberPaymentPurpose.AQGreenJoining) ||
                    payments.Sum(payment => payment.Amount) !=
                    participation.JoiningPaymentAmount)
                    throw new AQGreenPlacementConflictException(
                        "AQGreen joining payment evidence conflicts with programme terms.");
                return;
            }

            if (payments.Single(payment =>
                    payment.Id == participation.RegistrationPaymentId).Purpose !=
                    MemberPaymentPurpose.EntryRegistration ||
                payments.Single(payment =>
                    payment.Id == participation.ActivationPaymentId).Purpose !=
                    MemberPaymentPurpose.EntryActivation ||
                payments.Single(payment =>
                    payment.Id == participation.RegistrationPaymentId).Amount !=
                    participation.RegistrationPaymentAmount ||
                payments.Single(payment =>
                    payment.Id == participation.ActivationPaymentId).Amount !=
                    participation.ActivationPaymentAmount)
                throw new AQGreenPlacementConflictException(
                    "Historical AQGreen payment evidence conflicts with programme terms.");
        }

        private async Task<(Customer Customer, Guid ParticipationId, bool DecisionApplied)>
            ApplyDecisionAsync(
            AdminProgrammeType programme,
            Guid participationId,
            bool approve,
            string reason)
        {
            var decidedAt = DateTime.UtcNow;
            if (programme == AdminProgrammeType.Onyx)
            {
                var onyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                    item => item.Id == participationId);
                if (onyx == null)
                    throw Failed("Participation decision", "The participation was not found in your Area.");
                ValidateRequestedTenant(onyx.TenantId, "Participation decision");
                var onyxCustomer = await _customerRepository.GetAsync(onyx.CustomerId);
                await EnsureCanAdministerAreaAsync(onyxCustomer);
                if ((approve && onyx.Status == OnyxParticipationStatus.Active) ||
                    (!approve && onyx.Status == OnyxParticipationStatus.Rejected))
                {
                    return (onyxCustomer, onyx.Id, false);
                }
                if (approve)
                    onyx.ApproveByAdministrator(AbpSession.GetUserId(), decidedAt);
                else
                    onyx.RejectByAdministrator(AbpSession.GetUserId(), reason, decidedAt);
                return (onyxCustomer, onyx.Id, true);
            }

            var entry = await _entryParticipationRepository.FirstOrDefaultAsync(
                item => item.Id == participationId);
            if (entry == null)
                throw Failed("Participation decision", "The participation was not found in your Area.");
            ValidateRequestedTenant(entry.TenantId, "Participation decision");
            var entryCustomer = await _customerRepository.GetAsync(entry.CustomerId);
            await EnsureCanAdministerAreaAsync(entryCustomer);
            if ((approve && entry.Status == EntryParticipationStatus.Active) ||
                (!approve && entry.Status == EntryParticipationStatus.Rejected))
            {
                return (entryCustomer, entry.Id, false);
            }
            if (approve)
                entry.ApproveByAdministrator(AbpSession.GetUserId(), decidedAt);
            else
                entry.RejectByAdministrator(AbpSession.GetUserId(), reason, decidedAt);
            return (entryCustomer, entry.Id, true);
        }

        private async Task<long> ResolveApprovalUserIdHintAsync(
            AdminProgrammeType programme,
            Guid participationId)
        {
            var userId = programme == AdminProgrammeType.Onyx
                    ? await (
                            from participation in _onyxParticipationRepository.GetAll()
                                .AsNoTracking()
                            join customer in _customerRepository.GetAll().AsNoTracking()
                                on participation.CustomerId equals customer.Id
                            where participation.Id == participationId
                            select (long?)customer.UserId)
                        .SingleOrDefaultAsync()
                    : await (
                            from participation in _entryParticipationRepository.GetAll()
                                .AsNoTracking()
                            join customer in _customerRepository.GetAll().AsNoTracking()
                                on participation.CustomerId equals customer.Id
                            where participation.Id == participationId
                            select (long?)customer.UserId)
                        .SingleOrDefaultAsync();

            if (!userId.HasValue)
                throw Failed(
                    "Participation approval",
                    "The participation was not found in your Area.");
            return userId.Value;
        }

        private async Task EnqueueDecisionEmailAsync(
            Customer customer,
            AdminProgrammeType programme,
            Guid participationId,
            bool approved,
            string reason)
        {
            var programmeName = programme == AdminProgrammeType.Onyx ? "Onyx" : "AQGreen";
            var key = $"{programme}:{participationId}:{(approved ? "approved" : "declined")}";
            var email = approved
                ? _emailTemplates.ParticipationApproved(
                    customer.Name,
                    customer.Email.Value,
                    programmeName,
                    key)
                : _emailTemplates.ParticipationDeclined(
                    customer.Name,
                    customer.Email.Value,
                    programmeName,
                    reason,
                    key);
            await _emailOutbox.EnqueueAsync(customer.TenantId, "ParticipationDecision", key, email);
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.CorrectRecruiter)]
        [UnitOfWork(IsDisabled = true)]
        public async Task CorrectRecruiterAsync(CorrectProgrammeRecruiterInput input)
        {
            if (input == null)
                throw new Abp.UI.UserFriendlyException(
                    "Network placement correction failed.",
                    "The request was empty.");
            Customer target;
            Customer newRecruiter = null;
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
                IsTransactional = true,
                IsolationLevel = IsolationLevel.Serializable
            }))
            {
                if (!AbpSession.TenantId.HasValue &&
                    !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                {
                    throw new AbpAuthorizationException(
                        "Cross-Area network placement correction requires permission to manage all Areas.");
                }

                var policy = _correctionPolicyResolver.Resolve(input.Programme);
                await _correctionLock.AcquireAsync(input.Programme switch
                {
                    AdminProgrammeType.Entry => ProgrammeRecruiterNetwork.AQGreen,
                    AdminProgrammeType.Onyx => ProgrammeRecruiterNetwork.Onyx,
                    _ => throw new Abp.UI.UserFriendlyException(
                        "Network placement correction failed.",
                        "The selected programme does not support network placement corrections.")
                });

                using (DisableAllTenantDataFiltersForHost())
                using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
                {
                    var normalizedTarget = input.ClubMemberNumber.Trim().ToUpperInvariant();
                    target = await _customerRepository.GetAll()
                        .SingleOrDefaultAsync(customer =>
                            customer.ClubMemberNumber == normalizedTarget &&
                            !customer.IsDeleted);
                    if (target == null)
                        throw new Abp.UI.UserFriendlyException(
                            "Network placement correction failed.",
                            "The Club Member participation was not found.");

                    ValidateRequestedTenant(target.TenantId, "Network placement correction");
                    await EnsureCanAdministerAreaAsync(target);
                    if (!string.IsNullOrWhiteSpace(input.NewRecruiterClubMemberNumber))
                    {
                        var normalizedRecruiter = input.NewRecruiterClubMemberNumber
                            .Trim()
                            .ToUpperInvariant();
                        newRecruiter = await _customerRepository.GetAll()
                            .SingleOrDefaultAsync(customer =>
                                customer.ClubMemberNumber == normalizedRecruiter &&
                                !customer.IsDeleted);
                        if (newRecruiter == null ||
                            newRecruiter.TenantId != target.TenantId ||
                            !newRecruiter.IsActive)
                        {
                            throw new Abp.UI.UserFriendlyException(
                                "Network placement correction failed.",
                                "The new inviting Club Member must be active and within your management authority.");
                        }
                    }
                }

                await policy.CorrectAsync(
                    target.TenantId.Value,
                    target.Id,
                    newRecruiter?.Id,
                    AbpSession.GetUserId(),
                    input.Reason,
                    DateTime.UtcNow);
                await CurrentUnitOfWork.SaveChangesAsync();
                await uow.CompleteAsync();
            }
            Logger.Warn(
                $"Programme recruiter corrected programme={input.Programme} tenant={target.TenantId} member={target.ClubMemberNumber} recruiter={newRecruiter?.ClubMemberNumber ?? "independent"}");
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.View)]
        public async Task<PagedResultDto<AdminProgrammeParticipationDto>> GetAllAsync(
            AdminProgrammeParticipationListInput input)
        {
            input ??= new AdminProgrammeParticipationListInput();
            ValidateRequestedTenant(input.TenantId, "Programme participation");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide programme participation access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                return input.Programme == AdminProgrammeType.Onyx
                    ? await GetOnyxParticipationsAsync(input)
                    : await GetEntryParticipationsAsync(input);
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.View)]
        public async Task<PendingProgrammeApprovalSummaryDto>
            GetPendingApprovalSummaryAsync(PendingProgrammeApprovalSummaryInput input)
        {
            input ??= new PendingProgrammeApprovalSummaryInput();
            ValidateRequestedTenant(input.TenantId, "Pending programme approval");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
            {
                throw new AbpAuthorizationException(
                    "Host-wide pending programme approval access requires permission to view all Areas.");
            }

            using (DisableAllTenantDataFiltersForHost())
            {
                var areaScope = await GetAuthorizedAreaScopeAsync(input.AreaId);
                var entryQuery =
                    from participation in _entryParticipationRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on participation.CustomerId equals customer.Id
                    where participation.Status ==
                          EntryParticipationStatus.PaymentConfirmedAwaitingApproval
                    select new { Participation = participation, Customer = customer };
                var onyxQuery =
                    from participation in _onyxParticipationRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on participation.CustomerId equals customer.Id
                    where participation.Status ==
                          OnyxParticipationStatus.PaymentConfirmedAwaitingApproval
                    select new { Participation = participation, Customer = customer };

                var tenantId = AbpSession.TenantId ?? input.TenantId;
                if (tenantId.HasValue)
                {
                    entryQuery = entryQuery.Where(row =>
                        row.Participation.TenantId == tenantId.Value);
                    onyxQuery = onyxQuery.Where(row =>
                        row.Participation.TenantId == tenantId.Value);
                }
                if (areaScope != null)
                {
                    entryQuery = entryQuery.Where(row =>
                        row.Customer.AreaId.HasValue && areaScope.Contains(row.Customer.AreaId.Value));
                    onyxQuery = onyxQuery.Where(row =>
                        row.Customer.AreaId.HasValue && areaScope.Contains(row.Customer.AreaId.Value));
                }

                return new PendingProgrammeApprovalSummaryDto
                {
                    AQGreenCount = await entryQuery.CountAsync(),
                    OnyxCount = await onyxQuery.CountAsync()
                };
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.View)]
        public async Task<IReadOnlyList<AssignedAreaDto>> GetAssignedAreasAsync()
        {
            using (DisableAllTenantDataFiltersForHost())
            {
                var areaScope = await GetAuthorizedAreaScopeAsync(requestedAreaId: null);
                var query = _areaRepository.GetAll().Where(area => area.IsActive);
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(area => area.TenantId == tenantId);
                }
                if (areaScope != null)
                    query = query.Where(area => areaScope.Contains(area.Id));

                return await query.OrderBy(area => area.Name)
                    .Select(area => new AssignedAreaDto
                    {
                        AreaId = area.Id,
                        Code = area.Code,
                        Name = area.Name
                    })
                    .ToListAsync();
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation)]
        public async Task<PagedResultDto<LegacyAQGreenReconciliationDto>>
            GetLegacyAQGreenReconciliationAsync(
                LegacyAQGreenReconciliationListInput input)
        {
            input ??= new LegacyAQGreenReconciliationListInput();
            ValidateRequestedTenant(input.TenantId, "Legacy AQGreen reconciliation");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Host-wide legacy reconciliation access requires permission to view all Areas.");

            using (DisableAllTenantDataFiltersForHost())
            {
                var areaScope = await GetAuthorizedAreaScopeAsync(requestedAreaId: null);
                var query =
                    from participation in _entryParticipationRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on participation.CustomerId equals customer.Id
                    where participation.JoiningInstallmentAmount == 0m
                    select new EntryParticipationQueryRow
                    {
                        Participation = participation,
                        Customer = customer
                    };
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    query = query.Where(row => row.Participation.TenantId == tenantId);
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(row => row.Participation.TenantId == tenantId);
                }
                if (areaScope != null)
                {
                    query = query.Where(row =>
                        row.Customer.AreaId.HasValue &&
                        areaScope.Contains(row.Customer.AreaId.Value));
                }

                var total = await query.CountAsync();
                var rows = await query
                    .OrderBy(row => row.Participation.StartedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                var participationIds = rows.Select(row => row.Participation.Id).ToArray();
                var customerIds = rows.Select(row => row.Participation.CustomerId)
                    .Distinct()
                    .ToArray();
                var payments = await _paymentRepository.GetAll()
                    .Where(payment => customerIds.Contains(payment.CustomerId) &&
                                      payment.Status == MemberPaymentStatus.Confirmed &&
                                      (payment.Purpose == MemberPaymentPurpose.AQGreenJoining ||
                                       payment.Purpose == MemberPaymentPurpose.EntryRegistration ||
                                       payment.Purpose == MemberPaymentPurpose.EntryActivation ||
                                       payment.Purpose == MemberPaymentPurpose.EntryMonthlyCommitment))
                    .ToListAsync();
                var checkouts = await _aqGreenJoiningCheckoutRepository.GetAll()
                    .Where(checkout => participationIds.Contains(checkout.ParticipationId))
                    .OrderBy(checkout => checkout.CreatedAt)
                    .ToListAsync();

                return new PagedResultDto<LegacyAQGreenReconciliationDto>(
                    total,
                    rows.Select(row => new LegacyAQGreenReconciliationDto
                    {
                        TenantId = row.Participation.TenantId,
                        ParticipationId = row.Participation.Id,
                        ClubMemberNumber = row.Customer.ClubMemberNumber,
                        TermsVersion = row.Participation.TermsVersion,
                        JoiningAmount = row.Participation.JoiningPaymentAmount,
                        RegistrationAmount = row.Participation.RegistrationPaymentAmount,
                        ActivationAmount = row.Participation.ActivationPaymentAmount,
                        MonthlySubscriptionAmount = row.Participation.MonthlyCommitmentAmount,
                        JoiningPaymentId = row.Participation.JoiningPaymentId,
                        RegistrationPaymentId = row.Participation.RegistrationPaymentId,
                        ActivationPaymentId = row.Participation.ActivationPaymentId,
                        VerifiedPayments = payments
                            .Where(payment => payment.CustomerId == row.Participation.CustomerId)
                            .Select(payment => new LegacyAQGreenPaymentFactDto
                            {
                                PaymentId = payment.Id,
                                Purpose = payment.Purpose,
                                Amount = payment.Amount,
                                Currency = payment.Currency,
                                Provider = payment.Provider,
                                ProviderReference = payment.ExternalReference,
                                ConfirmedAt = payment.ConfirmedAt.Value
                            })
                            .ToList(),
                        CheckoutAttempts = checkouts
                            .Where(checkout => checkout.ParticipationId == row.Participation.Id)
                            .Select(checkout => new LegacyAQGreenCheckoutFactDto
                            {
                                CheckoutId = checkout.Id,
                                Amount = checkout.Amount,
                                Currency = checkout.Currency,
                                Status = checkout.Status,
                                ProviderCheckoutId = checkout.ProviderCheckoutId,
                                PaymentId = checkout.PaymentId
                            })
                            .ToList()
                    }).ToList());
            }
        }

        [AbpAuthorize(AquaPermissions.Admin.ProgrammeParticipations.ViewLegacyPaymentReconciliation)]
        public async Task<PagedResultDto<MonthlyObligationCheckoutReconciliationDto>>
            GetMonthlyObligationCheckoutReconciliationAsync(
                MonthlyObligationCheckoutReconciliationListInput input)
        {
            input ??= new MonthlyObligationCheckoutReconciliationListInput();
            ValidateRequestedTenant(input.TenantId, "Monthly obligation checkout reconciliation");
            if (!AbpSession.TenantId.HasValue &&
                !await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                throw new AbpAuthorizationException(
                    "Host-wide monthly obligation reconciliation access requires permission to view all Areas.");

            using (DisableAllTenantDataFiltersForHost())
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.SoftDelete))
            {
                var query = _monthlyObligationCheckoutRepository.GetAll()
                    .Where(checkout =>
                        !checkout.IsDeleted &&
                        checkout.Status == HostedPaymentCheckoutStatus.Completed);
                if (AbpSession.TenantId.HasValue)
                {
                    var tenantId = AbpSession.TenantId.Value;
                    var areaScope = await GetAuthorizedAreaScopeAsync(requestedAreaId: null);
                    var authorizedCustomerIds = _customerRepository.GetAll()
                        .Where(customer =>
                            !customer.IsDeleted &&
                            customer.TenantId == tenantId &&
                            customer.AreaId.HasValue &&
                            areaScope.Contains(customer.AreaId.Value))
                        .Select(customer => customer.Id);
                    query = query.Where(checkout =>
                        checkout.TenantId == tenantId &&
                        authorizedCustomerIds.Contains(checkout.CustomerId));
                }
                else if (input.TenantId.HasValue)
                {
                    var tenantId = input.TenantId.Value;
                    query = query.Where(checkout => checkout.TenantId == tenantId);
                }
                if (input.PeriodYear.HasValue)
                {
                    var periodYear = input.PeriodYear.Value;
                    query = query.Where(checkout => checkout.PeriodYear == periodYear);
                }
                if (input.PeriodMonth.HasValue)
                {
                    var periodMonth = input.PeriodMonth.Value;
                    query = query.Where(checkout => checkout.PeriodMonth == periodMonth);
                }

                var total = await query.CountAsync();
                var rows = await query
                    .OrderByDescending(checkout => checkout.CompletedAt)
                    .ThenByDescending(checkout => checkout.CreatedAt)
                    .Skip(input.SkipCount)
                    .Take(input.MaxResultCount)
                    .ToListAsync();
                var paymentIds = rows.Select(checkout => checkout.PaymentId)
                    .Where(paymentId => paymentId.HasValue)
                    .Select(paymentId => paymentId.Value)
                    .Distinct()
                    .ToArray();
                var obligationIds = rows
                    .Select(checkout => checkout.EntryMonthlyObligationId)
                    .Distinct()
                    .ToArray();
                var participationIds = rows
                    .Select(checkout => checkout.EntryParticipationId)
                    .Distinct()
                    .ToArray();
                var customerIds = rows
                    .Select(checkout => checkout.CustomerId)
                    .Distinct()
                    .ToArray();
                var payments = paymentIds.Length == 0
                    ? new Dictionary<Guid, MemberPayment>()
                    : await _paymentRepository.GetAll()
                        .Where(payment => paymentIds.Contains(payment.Id))
                        .ToDictionaryAsync(payment => payment.Id);
                var obligations = await _monthlyObligationRepository.GetAll()
                    .Where(obligation => obligationIds.Contains(obligation.Id))
                    .ToDictionaryAsync(obligation => obligation.Id);
                var participations = await _entryParticipationRepository.GetAll()
                    .Where(participation => participationIds.Contains(participation.Id))
                    .ToDictionaryAsync(participation => participation.Id);
                var customers = await _customerRepository.GetAll()
                    .Where(customer => customerIds.Contains(customer.Id))
                    .ToDictionaryAsync(customer => customer.Id);
                var areaIds = customers.Values
                    .Where(customer => !customer.IsDeleted && customer.AreaId.HasValue)
                    .Select(customer => customer.AreaId.Value)
                    .Distinct()
                    .ToArray();
                var areas = await _areaRepository.GetAll()
                    .Where(area => areaIds.Contains(area.Id))
                    .ToDictionaryAsync(area => area.Id);

                return new PagedResultDto<MonthlyObligationCheckoutReconciliationDto>(
                    total,
                    rows.Select(checkout =>
                    {
                        obligations.TryGetValue(checkout.EntryMonthlyObligationId, out var obligation);
                        participations.TryGetValue(checkout.EntryParticipationId, out var participation);
                        customers.TryGetValue(checkout.CustomerId, out var customer);
                        Area area = null;
                        if (customer != null && !customer.IsDeleted && customer.AreaId.HasValue)
                            areas.TryGetValue(customer.AreaId.Value, out area);

                        return new MonthlyObligationCheckoutReconciliationDto
                        {
                            CheckoutId = checkout.Id,
                            TenantId = checkout.TenantId,
                            AreaName = area?.Name ?? "Area unavailable",
                            ClubMemberNumber = customer?.ClubMemberNumber,
                            CustomerName = customer?.Name ?? "Customer unavailable",
                            PeriodYear = checkout.PeriodYear,
                            PeriodMonth = checkout.PeriodMonth,
                            Amount = checkout.Amount,
                            Currency = checkout.Currency,
                            Status = checkout.Status,
                            ProviderCheckoutId = checkout.ProviderCheckoutId,
                            PaymentId = checkout.PaymentId,
                            ProviderPaymentReference =
                                checkout.PaymentId.HasValue &&
                                payments.TryGetValue(checkout.PaymentId.Value, out var payment)
                                    ? payment.ExternalReference
                                    : null,
                            AllocationStatus = checkout.AllocationStatus,
                            AllocationEvidence = checkout.AllocationEvidence,
                            CreatedAt = checkout.CreatedAt,
                            CompletedAt = checkout.CompletedAt,
                            IsPaymentAllocated = checkout.PaymentId.HasValue &&
                                                 obligation?.PaymentId.HasValue == true &&
                                                 checkout.PaymentId.Value == obligation.PaymentId.Value,
                            RecordedObligationStatus = obligation?.Status,
                            ObligationAvailable = obligation != null && !obligation.IsDeleted,
                            ParticipationAvailable = participation != null && !participation.IsDeleted,
                            CustomerAvailable = customer != null && !customer.IsDeleted,
                            AreaAvailable = area != null && !area.IsDeleted && area.IsActive
                        };
                    }).ToList());
            }
        }

        private async Task<PagedResultDto<AdminProgrammeParticipationDto>>
            GetEntryParticipationsAsync(AdminProgrammeParticipationListInput input)
        {
            var query =
                from participation in _entryParticipationRepository.GetAll()
                join customer in _customerRepository.GetAllIncluding(item => item.User)
                    on participation.CustomerId equals customer.Id
                select new EntryParticipationQueryRow
                {
                    Participation = participation,
                    Customer = customer
                };
            var areaScope = await GetAuthorizedAreaScopeAsync(input.AreaId);
            query = ApplyEntryScopeAndSearch(query, input, areaScope);
            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(row => row.Participation.StartedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
            var payments = await GetPaymentsAsync(rows.SelectMany(row => new[]
            {
                row.Participation.JoiningPaymentId,
                row.Participation.RegistrationPaymentId,
                row.Participation.ActivationPaymentId
            }));
            var memberNumbers = await GetClubMemberNumbersAsync(
                rows.Select(row => row.Participation.RecruiterCustomerId));
            var areaNames = await GetAreaNamesAsync(
                rows.Select(row => row.Customer.AreaId));

            return new PagedResultDto<AdminProgrammeParticipationDto>(
                total,
                rows.Select(row => Map(row.Participation, row.Customer, payments, memberNumbers, areaNames)).ToList());
        }

        private async Task<PagedResultDto<AdminProgrammeParticipationDto>>
            GetOnyxParticipationsAsync(AdminProgrammeParticipationListInput input)
        {
            var query =
                from participation in _onyxParticipationRepository.GetAll()
                join customer in _customerRepository.GetAllIncluding(item => item.User)
                    on participation.CustomerId equals customer.Id
                select new OnyxParticipationQueryRow
                {
                    Participation = participation,
                    Customer = customer
                };
            var areaScope = await GetAuthorizedAreaScopeAsync(input.AreaId);
            query = ApplyOnyxScopeAndSearch(query, input, areaScope);
            var total = await query.CountAsync();
            var rows = await query
                .OrderByDescending(row => row.Participation.StartedAt)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount)
                .ToListAsync();
            var payments = await GetPaymentsAsync(
                rows.Select(row => row.Participation.DirectEntryPaymentId));
            var memberNumbers = await GetClubMemberNumbersAsync(
                rows.Select(row => row.Participation.RecruiterCustomerId));
            var areaNames = await GetAreaNamesAsync(
                rows.Select(row => row.Customer.AreaId));

            return new PagedResultDto<AdminProgrammeParticipationDto>(
                total,
                rows.Select(row => Map(row.Participation, row.Customer, payments, memberNumbers, areaNames)).ToList());
        }

        private async Task<IReadOnlyDictionary<Guid, string>> GetAreaNamesAsync(
            IEnumerable<Guid?> areaIds)
        {
            var ids = areaIds.Where(id => id.HasValue).Select(id => id.Value).Distinct().ToArray();
            return await _areaRepository.GetAll()
                .Where(area => ids.Contains(area.Id))
                .ToDictionaryAsync(area => area.Id, area => area.Name);
        }

        private async Task<IReadOnlyDictionary<int, string>> GetClubMemberNumbersAsync(
            IEnumerable<int?> customerIds)
        {
            var ids = customerIds.Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToArray();
            if (ids.Length == 0) return new Dictionary<int, string>();

            return await _customerRepository.GetAll()
                .Where(customer => ids.Contains(customer.Id))
                .ToDictionaryAsync(customer => customer.Id, customer => customer.ClubMemberNumber);
        }

        private IQueryable<EntryParticipationQueryRow> ApplyEntryScopeAndSearch(
            IQueryable<EntryParticipationQueryRow> query,
            AdminProgrammeParticipationListInput input,
            Guid[] areaScope)
        {
            if (AbpSession.TenantId.HasValue)
            {
                var tenantId = AbpSession.TenantId.Value;
                query = query.Where(row => row.Participation.TenantId == tenantId);
            }

            if (areaScope != null)
                query = query.Where(row => row.Customer.AreaId.HasValue &&
                                           areaScope.Contains(row.Customer.AreaId.Value));
            if (input.TenantId.HasValue)
            {
                var tenantId = input.TenantId.Value;
                query = query.Where(row => row.Participation.TenantId == tenantId);
            }

            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword.Trim().ToLower();
                query = query.Where(row =>
                    row.Customer.Name.ToLower().Contains(keyword) ||
                    row.Customer.Email.Value.ToLower().Contains(keyword));
            }

            if (input.AwaitingApprovalOnly)
            {
                query = query.Where(row =>
                    row.Participation.Status ==
                    EntryParticipationStatus.PaymentConfirmedAwaitingApproval);
            }

            return query;
        }

        private IQueryable<OnyxParticipationQueryRow> ApplyOnyxScopeAndSearch(
            IQueryable<OnyxParticipationQueryRow> query,
            AdminProgrammeParticipationListInput input,
            Guid[] areaScope)
        {
            if (AbpSession.TenantId.HasValue)
            {
                var tenantId = AbpSession.TenantId.Value;
                query = query.Where(row => row.Participation.TenantId == tenantId);
            }

            if (areaScope != null)
                query = query.Where(row => row.Customer.AreaId.HasValue &&
                                           areaScope.Contains(row.Customer.AreaId.Value));
            if (input.TenantId.HasValue)
            {
                var tenantId = input.TenantId.Value;
                query = query.Where(row => row.Participation.TenantId == tenantId);
            }

            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword.Trim().ToLower();
                query = query.Where(row =>
                    row.Customer.Name.ToLower().Contains(keyword) ||
                    row.Customer.Email.Value.ToLower().Contains(keyword));
            }

            if (input.AwaitingApprovalOnly)
            {
                query = query.Where(row =>
                    row.Participation.Status ==
                    OnyxParticipationStatus.PaymentConfirmedAwaitingApproval);
            }

            return query;
        }

        private async Task<IReadOnlyDictionary<Guid, MemberPayment>> GetPaymentsAsync(
            IEnumerable<Guid?> paymentIds)
        {
            var ids = paymentIds
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .Distinct()
                .ToArray();
            if (ids.Length == 0)
            {
                return new Dictionary<Guid, MemberPayment>();
            }

            return (await _paymentRepository.GetAll()
                    .Where(payment =>
                        ids.Contains(payment.Id) &&
                        payment.ConfirmedAt.HasValue)
                    .ToListAsync())
                .ToDictionary(payment => payment.Id);
        }

        private async Task<Guid[]> GetAuthorizedAreaScopeAsync(Guid? requestedAreaId)
        {
            if (!AbpSession.TenantId.HasValue)
            {
                if (!await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants))
                    throw new AbpAuthorizationException("Host-wide Area access is not authorised.");
                if (!requestedAreaId.HasValue) return null;

                var requestedExists = await _areaRepository.GetAll().AnyAsync(area =>
                    area.Id == requestedAreaId.Value &&
                    !area.IsDeleted &&
                    area.IsActive);
                return requestedExists ? new[] { requestedAreaId.Value } : Array.Empty<Guid>();
            }

            var tenantId = AbpSession.TenantId.Value;
            var userId = AbpSession.GetUserId();
            var query =
                from assignment in _areaAdminAssignmentRepository.GetAll()
                join area in _areaRepository.GetAll()
                    on new { assignment.TenantId, assignment.AreaId }
                    equals new { area.TenantId, AreaId = area.Id }
                where
                    assignment.TenantId == tenantId &&
                    assignment.UserId == userId &&
                    !assignment.IsDeleted &&
                    !assignment.RevokedAt.HasValue &&
                    !area.IsDeleted &&
                    area.IsActive
                select assignment;
            if (requestedAreaId.HasValue)
                query = query.Where(assignment => assignment.AreaId == requestedAreaId.Value);
            return await query.Select(assignment => assignment.AreaId).Distinct().ToArrayAsync();
        }

        private async Task EnsureCanAdministerAreaAsync(Customer customer)
        {
            if (customer == null || !customer.TenantId.HasValue || !customer.AreaId.HasValue)
                throw new AbpAuthorizationException("The participation has no valid Area assignment.");

            if (!AbpSession.TenantId.HasValue)
            {
                if (await PermissionChecker.IsGrantedAsync(AquaPermissions.Admin.AllTenants)) return;
                throw new AbpAuthorizationException("Host-wide Area administration is not authorised.");
            }

            var tenantId = AbpSession.TenantId.Value;
            if (customer.TenantId.Value != tenantId)
                throw new AbpAuthorizationException("The participation belongs to another Tenant.");

            var userId = AbpSession.GetUserId();
            var assigned = await (
                    from assignment in _areaAdminAssignmentRepository.GetAll()
                    join area in _areaRepository.GetAll()
                        on new { assignment.TenantId, assignment.AreaId }
                        equals new { area.TenantId, AreaId = area.Id }
                    where
                        assignment.TenantId == tenantId &&
                        assignment.AreaId == customer.AreaId.Value &&
                        assignment.UserId == userId &&
                        !assignment.IsDeleted &&
                        !assignment.RevokedAt.HasValue &&
                        !area.IsDeleted &&
                        area.IsActive
                    select assignment)
                .AnyAsync();
            if (!assigned)
                throw new AbpAuthorizationException("You are not assigned to administer this Area.");
        }

        private static AdminProgrammeParticipationDto Map(
            EntryParticipation participation,
            Customer customer,
            IReadOnlyDictionary<Guid, MemberPayment> payments,
            IReadOnlyDictionary<int, string> memberNumbers,
            IReadOnlyDictionary<Guid, string> areaNames)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return MapCommon(
                participation.Id,
                participation.TenantId,
                customer,
                "AQGreen",
                details,
                participation.JoinedIndependently,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt,
                participation.JoiningPaymentAmount > 0m
                    ? participation.JoiningPaymentAmount
                    : participation.RegistrationPaymentAmount +
                      participation.ActivationPaymentAmount,
                participation.Currency,
                new[]
                {
                    participation.JoiningPaymentId,
                    participation.RegistrationPaymentId,
                    participation.ActivationPaymentId
                },
                payments,
                memberNumbers,
                areaNames);
        }

        private static AdminProgrammeParticipationDto Map(
            OnyxParticipation participation,
            Customer customer,
            IReadOnlyDictionary<Guid, MemberPayment> payments,
            IReadOnlyDictionary<int, string> memberNumbers,
            IReadOnlyDictionary<Guid, string> areaNames)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return MapCommon(
                participation.Id,
                participation.TenantId,
                customer,
                "Onyx",
                details,
                participation.JoinedIndependently,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt,
                participation.DirectEntryAmount,
                participation.Currency,
                new[] { participation.DirectEntryPaymentId },
                payments,
                memberNumbers,
                areaNames);
        }

        private static AdminProgrammeParticipationDto MapCommon(
            Guid participationId,
            int tenantId,
            Customer customer,
            string programmeName,
            ProgrammeParticipationStatusDetails details,
            bool joinedIndependently,
            int? recruiterCustomerId,
            DateTime startedAt,
            DateTime? activatedAt,
            decimal expectedJoiningAmount,
            string currency,
            IEnumerable<Guid?> paymentIds,
            IReadOnlyDictionary<Guid, MemberPayment> payments,
            IReadOnlyDictionary<int, string> memberNumbers,
            IReadOnlyDictionary<Guid, string> areaNames)
        {
            return new AdminProgrammeParticipationDto
            {
                ParticipationId = participationId,
                TenantId = tenantId,
                AreaId = customer.AreaId ?? Guid.Empty,
                AreaName = customer.AreaId.HasValue &&
                    areaNames.TryGetValue(customer.AreaId.Value, out var areaName)
                    ? areaName
                    : "Unassigned",
                ClubMemberNumber = customer.ClubMemberNumber,
                CustomerName = customer.Name,
                Email = customer.Email.Value,
                ProgrammeName = programmeName,
                Status = details.Status,
                IsActive = details.IsActive,
                JoinedIndependently = joinedIndependently,
                RecruiterClubMemberNumber = recruiterCustomerId.HasValue &&
                    memberNumbers.TryGetValue(recruiterCustomerId.Value, out var memberNumber)
                        ? memberNumber
                        : null,
                StartedAt = startedAt,
                ActivatedAt = activatedAt,
                ExpectedJoiningAmount = expectedJoiningAmount,
                NextPaymentAmount = details.NextPaymentAmount,
                NextPaymentDescription = details.NextPaymentDescription,
                Currency = currency,
                ConfirmedPayments = paymentIds
                    .Where(paymentId => paymentId.HasValue && payments.ContainsKey(paymentId.Value))
                    .Select(paymentId => MapPayment(payments[paymentId.Value]))
                    .ToList()
            };
        }

        private static AdminProgrammePaymentDto MapPayment(MemberPayment payment)
        {
            return new AdminProgrammePaymentDto
            {
                Description = payment.Purpose switch
                {
                    MemberPaymentPurpose.AQGreenJoining => "Full AQGreen joining payment",
                    MemberPaymentPurpose.EntryRegistration => "AQGreen registration payment",
                    MemberPaymentPurpose.EntryActivation => "AQGreen activation payment",
                    _ => "Full Onyx participation payment"
                },
                Amount = payment.Amount,
                Currency = payment.Currency,
                Provider = payment.Provider,
                ProviderReference = payment.ExternalReference,
                ConfirmedAt = payment.ConfirmedAt.Value
            };
        }

        private static OnyxGraduationDecisionDto Map(OnyxGraduationDecision decision) =>
            new OnyxGraduationDecisionDto
            {
                DecisionId = decision.Id,
                AQGreenParticipationId = decision.EntryParticipationId,
                LoanAgreementId = decision.LoanAgreementId,
                OnyxParticipationId = decision.OnyxParticipationId,
                AdministratorUserId = decision.AdministratorUserId,
                DecidedAt = decision.DecidedAt,
                Justification = decision.Justification,
                StructuralModel = decision.StructuralModel,
                EvaluatedNetworkLevel = decision.EvaluatedNetworkLevel
            };

        private sealed class EntryParticipationQueryRow
        {
            public EntryParticipation Participation { get; init; }
            public Customer Customer { get; init; }
        }

        private sealed class OnyxParticipationQueryRow
        {
            public OnyxParticipation Participation { get; init; }
            public Customer Customer { get; init; }
        }
    }
}

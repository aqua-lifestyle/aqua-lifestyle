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
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Onyx;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Admin.ProgrammeParticipations
{
    [Audited]
    public class AdminProgrammeParticipationAppService
        : AdminAppServiceBase, IAdminProgrammeParticipationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<EntryParticipation, Guid> _entryParticipationRepository;
        private readonly IRepository<OnyxParticipation, Guid> _onyxParticipationRepository;
        private readonly IRepository<OnyxLoanAgreement, Guid> _onyxLoanAgreementRepository;
        private readonly IRepository<OnyxGraduationDecision, Guid> _graduationDecisionRepository;
        private readonly IRepository<Membership> _membershipRepository;
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly IRepository<AQGreenJoiningCheckout, Guid> _aqGreenJoiningCheckoutRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IProgrammeRecruiterCorrectionPolicyResolver _correctionPolicyResolver;
        private readonly IProgrammeRecruiterCorrectionLock _correctionLock;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;
        private readonly IUnitOfWorkManager _unitOfWorkManager;
        private readonly ICurrentProgrammeTermsProvider _termsProvider;

        public AdminProgrammeParticipationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<OnyxLoanAgreement, Guid> onyxLoanAgreementRepository,
            IRepository<OnyxGraduationDecision, Guid> graduationDecisionRepository,
            IRepository<Membership> membershipRepository,
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<AQGreenJoiningCheckout, Guid> aqGreenJoiningCheckoutRepository,
            IRepository<Tenant> tenantRepository,
            IProgrammeRecruiterCorrectionPolicyResolver correctionPolicyResolver,
            IProgrammeRecruiterCorrectionLock correctionLock,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock,
            IUnitOfWorkManager unitOfWorkManager,
            ICurrentProgrammeTermsProvider termsProvider)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _onyxLoanAgreementRepository = onyxLoanAgreementRepository;
            _graduationDecisionRepository = graduationDecisionRepository;
            _membershipRepository = membershipRepository;
            _paymentRepository = paymentRepository;
            _aqGreenJoiningCheckoutRepository = aqGreenJoiningCheckoutRepository;
            _tenantRepository = tenantRepository;
            _correctionPolicyResolver = correctionPolicyResolver;
            _correctionLock = correctionLock;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
            _unitOfWorkManager = unitOfWorkManager;
            _termsProvider = termsProvider;
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
                var query =
                    from checkout in _aqGreenJoiningCheckoutRepository.GetAll()
                    join customer in _customerRepository.GetAll()
                        on checkout.CustomerId equals customer.Id
                    join tenant in _tenantRepository.GetAll()
                        on checkout.TenantId equals tenant.Id
                    where checkout.Status == HostedPaymentCheckoutStatus.PreparingCheckout ||
                          checkout.Status == HostedPaymentCheckoutStatus.AwaitingPayment
                    select new
                    {
                        Checkout = checkout,
                        Customer = customer,
                        AreaName = tenant.TenancyName
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

            OnyxGraduationDecision decision;
            using (var uow = _unitOfWorkManager.Begin(new UnitOfWorkOptions
            {
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
                    ValidateRequestedTenant(decision.TenantId, "Onyx graduation");
                    return Map(decision);
                }

                var loan = await _onyxLoanAgreementRepository.FirstOrDefaultAsync(
                    item => item.Id == input.LoanAgreementId);
                if (loan == null)
                    throw new Abp.UI.UserFriendlyException(
                        "Onyx graduation failed.",
                        "The approved loan agreement was not found in your Area.");
                ValidateRequestedTenant(loan.TenantId, "Onyx graduation");

                using (_unitOfWorkManager.Current.SetTenantId(loan.TenantId))
                {
                    var aqGreen = await _entryParticipationRepository.FirstOrDefaultAsync(
                        item => item.Id == loan.EntryParticipationId &&
                                item.CustomerId == loan.CustomerId &&
                                item.TenantId == loan.TenantId);
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

                    var existingOnyx = await _onyxParticipationRepository.FirstOrDefaultAsync(
                        item => item.TenantId == loan.TenantId &&
                                item.CustomerId == loan.CustomerId);
                    if (existingOnyx != null)
                        throw new Abp.UI.UserFriendlyException(
                            "Onyx graduation requires reconciliation.",
                            "An Onyx participation already exists without this graduation decision.");

                    var network = await _entryParticipationRepository.GetAll()
                        .Where(item => item.TenantId == loan.TenantId &&
                                       item.Status == EntryParticipationStatus.Active)
                        .ToListAsync();
                    var evaluatedLevel = new EntryNetworkQualificationEvaluator()
                        .Evaluate(aqGreen.CustomerId, network);
                    if (evaluatedLevel < EntryNetworkLevel.Level2)
                        throw new Abp.UI.UserFriendlyException(
                            "Onyx graduation failed.",
                            "The member no longer satisfies AQGreen Level 2 qualification.");

                    var terms = _termsProvider.GetDirectOnyxTerms();
                    if (loan.PrincipalAmount != terms.DirectEntryAmount ||
                        !string.Equals(loan.Currency, terms.Currency, StringComparison.Ordinal))
                        throw new Abp.UI.UserFriendlyException(
                            "Onyx graduation failed.",
                            $"The approved funding must be {terms.Currency} {terms.DirectEntryAmount:0.00}.");

                    Membership membership;
                    using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
                    {
                        membership = await _membershipRepository.GetAll()
                            .Where(item => item.IsActive &&
                                           item.MembershipType == MembershipType.Onyx &&
                                           (!item.TenantId.HasValue || item.TenantId == loan.TenantId))
                            .OrderByDescending(item => item.TenantId.HasValue)
                            .FirstOrDefaultAsync();
                    }
                    if (membership == null)
                        throw new Abp.UI.UserFriendlyException(
                            "Onyx graduation failed.",
                            "The Onyx programme has not been configured for this Area.");

                    var decidedAt = DateTime.UtcNow;
                    var onyx = OnyxParticipation.GraduateFromAQGreenIndependently(
                        aqGreen,
                        loan,
                        membership.Id,
                        terms,
                        decidedAt);
                    decision = OnyxGraduationDecision.RecordApproval(
                        aqGreen,
                        loan,
                        onyx,
                        evaluatedLevel,
                        AbpSession.GetUserId(),
                        input.Justification,
                        decidedAt);
                    await _onyxParticipationRepository.InsertAsync(onyx);
                    await _graduationDecisionRepository.InsertAsync(decision);
                    await CurrentUnitOfWork.SaveChangesAsync();
                }
                await uow.CompleteAsync();
            }

            Logger.Info(
                $"Onyx graduation approved tenant={decision.TenantId} decision={decision.Id} participation={decision.OnyxParticipationId}");
            return Map(decision);
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
                            (AbpSession.TenantId.HasValue &&
                             newRecruiter.TenantId != target.TenantId) ||
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
            query = ApplyEntryScopeAndSearch(query, input);
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
                rows.Select(row => row.Participation.TenantId));

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
            query = ApplyOnyxScopeAndSearch(query, input);
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
                rows.Select(row => row.Participation.TenantId));

            return new PagedResultDto<AdminProgrammeParticipationDto>(
                total,
                rows.Select(row => Map(row.Participation, row.Customer, payments, memberNumbers, areaNames)).ToList());
        }

        private async Task<IReadOnlyDictionary<int, string>> GetAreaNamesAsync(
            IEnumerable<int> tenantIds)
        {
            var ids = tenantIds.Distinct().ToArray();
            return await _tenantRepository.GetAll()
                .Where(tenant => ids.Contains(tenant.Id))
                .ToDictionaryAsync(tenant => tenant.Id, tenant => tenant.TenancyName);
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
            AdminProgrammeParticipationListInput input)
        {
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

            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword.Trim().ToLower();
                query = query.Where(row =>
                    row.Customer.Name.ToLower().Contains(keyword) ||
                    row.Customer.Email.Value.ToLower().Contains(keyword));
            }

            return query;
        }

        private IQueryable<OnyxParticipationQueryRow> ApplyOnyxScopeAndSearch(
            IQueryable<OnyxParticipationQueryRow> query,
            AdminProgrammeParticipationListInput input)
        {
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

            if (!string.IsNullOrWhiteSpace(input.Keyword))
            {
                var keyword = input.Keyword.Trim().ToLower();
                query = query.Where(row =>
                    row.Customer.Name.ToLower().Contains(keyword) ||
                    row.Customer.Email.Value.ToLower().Contains(keyword));
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

        private static AdminProgrammeParticipationDto Map(
            EntryParticipation participation,
            Customer customer,
            IReadOnlyDictionary<Guid, MemberPayment> payments,
            IReadOnlyDictionary<int, string> memberNumbers,
            IReadOnlyDictionary<int, string> areaNames)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return MapCommon(
                participation.TenantId,
                customer,
                "AQGreen",
                details,
                participation.JoinedIndependently,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt,
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
            IReadOnlyDictionary<int, string> areaNames)
        {
            var details = ProgrammeParticipationStatusPresenter.Describe(participation);
            return MapCommon(
                participation.TenantId,
                customer,
                "Onyx",
                details,
                participation.JoinedIndependently,
                participation.RecruiterCustomerId,
                participation.StartedAt,
                participation.ActivatedAt,
                participation.Currency,
                new[] { participation.DirectEntryPaymentId },
                payments,
                memberNumbers,
                areaNames);
        }

        private static AdminProgrammeParticipationDto MapCommon(
            int tenantId,
            Customer customer,
            string programmeName,
            ProgrammeParticipationStatusDetails details,
            bool joinedIndependently,
            int? recruiterCustomerId,
            DateTime startedAt,
            DateTime? activatedAt,
            string currency,
            IEnumerable<Guid?> paymentIds,
            IReadOnlyDictionary<Guid, MemberPayment> payments,
            IReadOnlyDictionary<int, string> memberNumbers,
            IReadOnlyDictionary<int, string> areaNames)
        {
            return new AdminProgrammeParticipationDto
            {
                AreaName = areaNames.TryGetValue(tenantId, out var areaName)
                    ? areaName
                    : "Area",
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

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
        private readonly IRepository<MemberPayment, Guid> _paymentRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IProgrammeRecruiterCorrectionPolicyResolver _correctionPolicyResolver;
        private readonly IProgrammeRecruiterCorrectionLock _correctionLock;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public AdminProgrammeParticipationAppService(
            ICustomerRepository customerRepository,
            IRepository<EntryParticipation, Guid> entryParticipationRepository,
            IRepository<OnyxParticipation, Guid> onyxParticipationRepository,
            IRepository<MemberPayment, Guid> paymentRepository,
            IRepository<Tenant> tenantRepository,
            IProgrammeRecruiterCorrectionPolicyResolver correctionPolicyResolver,
            IProgrammeRecruiterCorrectionLock correctionLock,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _customerRepository = customerRepository;
            _entryParticipationRepository = entryParticipationRepository;
            _onyxParticipationRepository = onyxParticipationRepository;
            _paymentRepository = paymentRepository;
            _tenantRepository = tenantRepository;
            _correctionPolicyResolver = correctionPolicyResolver;
            _correctionLock = correctionLock;
            _unitOfWorkManager = unitOfWorkManager;
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

using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Domain.Areas;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Payments;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Recruitment
{
    public interface IProgrammeInvitationResolver
    {
        Task<ProgrammeInvitationPlacement> ResolveForJoiningAsync(
            string inviteCode,
            string expectedProgrammeKey,
            int inviteeCustomerId,
            int inviteeTenantId);

        Task<ProgrammeInvitationPlacement> ResolveForRegistrationAsync(
            string inviteCode,
            int? expectedTenantId);
    }

    public sealed class ProgrammeInvitationPlacement
    {
        public int RecruiterCustomerId { get; }
        public int TenantId { get; }
        public Area RecruiterArea { get; }

        public ProgrammeInvitationPlacement(
            int tenantId,
            int recruiterCustomerId,
            Area recruiterArea)
        {
            if (tenantId <= 0) throw new ArgumentOutOfRangeException(nameof(tenantId));
            TenantId = tenantId;
            RecruiterCustomerId = recruiterCustomerId;
            RecruiterArea = recruiterArea ?? throw new ArgumentNullException(nameof(recruiterArea));
        }
    }

    public sealed class ProgrammeInvitationResolver
        : IProgrammeInvitationResolver, ITransientDependency
    {
        private readonly IRepository<ProgrammeInvitation, Guid> _invitationRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<Area, Guid> _areaRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IProgrammeRecruitmentPolicyResolver _policyResolver;
        private readonly IHostedPaymentCheckoutLock _hostedPaymentCheckoutLock;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ProgrammeInvitationResolver(
            IRepository<ProgrammeInvitation, Guid> invitationRepository,
            ICustomerRepository customerRepository,
            IRepository<Area, Guid> areaRepository,
            IRepository<Tenant> tenantRepository,
            IProgrammeRecruitmentPolicyResolver policyResolver,
            IHostedPaymentCheckoutLock hostedPaymentCheckoutLock,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _invitationRepository = invitationRepository;
            _customerRepository = customerRepository;
            _areaRepository = areaRepository;
            _tenantRepository = tenantRepository;
            _policyResolver = policyResolver;
            _hostedPaymentCheckoutLock = hostedPaymentCheckoutLock;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<ProgrammeInvitationPlacement> ResolveForJoiningAsync(
            string inviteCode,
            string expectedProgrammeKey,
            int inviteeCustomerId,
            int inviteeTenantId)
        {
            var placement = await ResolveAsync(
                inviteCode,
                expectedProgrammeKey,
                inviteeTenantId,
                inviteeCustomerId);
            return placement;
        }

        public Task<ProgrammeInvitationPlacement> ResolveForRegistrationAsync(
            string inviteCode,
            int? expectedTenantId) => ResolveAsync(
                inviteCode,
                expectedProgrammeKey: null,
                expectedTenantId,
                inviteeCustomerId: null);

        private async Task<ProgrammeInvitationPlacement> ResolveAsync(
            string inviteCode,
            string expectedProgrammeKey,
            int? expectedTenantId,
            int? inviteeCustomerId)
        {
            var normalizedCode = NormalizeCode(inviteCode);
            ProgrammeInvitation invitation;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                invitation = await _invitationRepository.FirstOrDefaultAsync(
                    item => item.Code == normalizedCode);
            }

            if (invitation == null)
                throw InvalidInvitation("The invitation code was not found.");
            if (!string.IsNullOrWhiteSpace(expectedProgrammeKey) && !string.Equals(
                    invitation.ProgrammeKey,
                    expectedProgrammeKey,
                    StringComparison.Ordinal))
                throw InvalidInvitation("The invitation is for a different programme.");
            if (expectedTenantId.HasValue && invitation.TenantId != expectedTenantId.Value)
                throw InvalidInvitation(
                    "The invitation belongs to a different organisation.");
            var authoritativeTenantId = invitation.TenantId;
            var authoritativeTenant = await _tenantRepository.FirstOrDefaultAsync(
                authoritativeTenantId);
            if (authoritativeTenant == null || !authoritativeTenant.IsActive)
                throw InvalidInvitation(
                    "The invitation organisation is unavailable.");

            var policy = _policyResolver.Resolve(invitation.ProgrammeKey);
            var source = await policy.FindByParticipationAsync(
                invitation.ProgrammeParticipationId);
            if (source == null || !source.IsEligible)
                throw InvalidInvitation("The inviting Club Member is not currently eligible to invite members to this programme.");
            if (source.TenantId != authoritativeTenantId)
                throw InvalidInvitation(
                    "The inviting Club Member belongs to a different organisation.");
            if (source.CustomerId == inviteeCustomerId)
                throw InvalidInvitation("You cannot accept your own invitation.");

            var recruiterCustomerId = source.CustomerId;
            await _hostedPaymentCheckoutLock.AcquireCustomerAreaTransitionsAsync(
                inviteeCustomerId.HasValue
                    ? new[] { recruiterCustomerId, inviteeCustomerId.Value }
                    : new[] { recruiterCustomerId });
            source = await policy.FindByParticipationAsync(
                invitation.ProgrammeParticipationId);
            if (source == null || !source.IsEligible ||
                source.TenantId != authoritativeTenantId ||
                source.CustomerId != recruiterCustomerId)
                throw InvalidInvitation(
                    "The inviting Club Member is not currently eligible to invite members to this programme.");
            Customer recruiter;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                recruiter = await _customerRepository.GetAll()
                    .SingleOrDefaultAsync(customer =>
                        customer.Id == source.CustomerId &&
                        customer.TenantId == authoritativeTenantId);
            }
            if (recruiter == null || !recruiter.IsActive)
                throw InvalidInvitation(
                    "The inviting Club Member is not currently eligible to invite members to this programme.");
            if (!recruiter.AreaId.HasValue)
                throw InvalidInvitation(
                    "The inviting Club Member does not have a current business Area.");

            Area area;
            using (_unitOfWorkManager.Current.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                area = await _areaRepository.FirstOrDefaultAsync(candidate =>
                    candidate.Id == recruiter.AreaId.Value &&
                    candidate.TenantId == authoritativeTenantId &&
                    candidate.IsActive);
            }
            if (area == null)
                throw InvalidInvitation(
                    "The inviting Club Member's business Area is unavailable.");

            return new ProgrammeInvitationPlacement(
                authoritativeTenantId,
                source.CustomerId,
                area);
        }

        internal static string NormalizeCode(string inviteCode)
        {
            var code = inviteCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code) || code.Length != ProgrammeInvitation.CodeLength)
                throw InvalidInvitation("Enter a valid invitation code.");
            return code;
        }

        private static UserFriendlyException InvalidInvitation(string details) =>
            new UserFriendlyException("The invitation could not be accepted.", details);
    }
}

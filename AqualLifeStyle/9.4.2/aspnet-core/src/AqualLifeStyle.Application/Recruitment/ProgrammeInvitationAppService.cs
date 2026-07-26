using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Auditing;
using Abp.Authorization;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.Runtime.Session;
using Abp.UI;
using AqualLifeStyle.Application.Recruitment.Dto;
using AqualLifeStyle.Authorization;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Recruitment;
using AqualLifeStyle.MultiTenancy;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.Application.Recruitment
{
    [Audited]
    public class ProgrammeInvitationAppService
        : AqualLifeStyleAppServiceBase, IProgrammeInvitationAppService
    {
        private readonly ICustomerRepository _customerRepository;
        private readonly IRepository<ProgrammeInvitation, Guid> _invitationRepository;
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IProgrammeRecruitmentPolicyResolver _policyResolver;

        public ProgrammeInvitationAppService(
            ICustomerRepository customerRepository,
            IRepository<ProgrammeInvitation, Guid> invitationRepository,
            IRepository<Tenant> tenantRepository,
            IProgrammeRecruitmentPolicyResolver policyResolver)
        {
            _customerRepository = customerRepository;
            _invitationRepository = invitationRepository;
            _tenantRepository = tenantRepository;
            _policyResolver = policyResolver;
        }

        [AbpAuthorize(AquaPermissions.ProgrammeParticipations.Invite)]
        public async Task<MyProgrammeInvitationsDto> GetMyInvitationsAsync()
        {
            var customer = await GetCurrentActiveCustomerAsync();
            var invitations = new List<ProgrammeInvitationDto>();
            foreach (var policy in _policyResolver.GetAll())
            {
                var participation = await policy.FindByCustomerAsync(customer.Id);
                if (participation == null || !participation.IsEligible) continue;

                var invitation = await GetOrCreateAsync(policy, participation);
                invitations.Add(new ProgrammeInvitationDto
                {
                    Code = invitation.Code,
                    ProgrammeKey = policy.ProgrammeKey,
                    ProgrammeName = policy.ProgrammeName,
                    ClubMemberNumber = customer.ClubMemberNumber
                });
            }

            return new MyProgrammeInvitationsDto
            {
                Invitations = invitations.OrderBy(item => item.ProgrammeName).ToArray()
            };
        }

        [AbpAllowAnonymous]
        public async Task<ProgrammeInvitationPreviewDto> GetPreviewAsync(
            ProgrammeInvitationCodeInput input)
        {
            if (input == null)
                throw new UserFriendlyException("Invitation unavailable.", "The request was empty.");

            var code = ProgrammeInvitationResolver.NormalizeCode(input.InviteCode);
            ProgrammeInvitation invitation;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                invitation = await _invitationRepository.FirstOrDefaultAsync(
                    item => item.Code == code);
            }
            if (invitation == null)
                throw new UserFriendlyException("Invitation unavailable.", "This invitation link is not valid.");

            var policy = _policyResolver.Resolve(invitation.ProgrammeKey);
            var participation = await policy.FindByParticipationAsync(
                invitation.ProgrammeParticipationId);
            if (participation == null)
                throw new UserFriendlyException("Invitation unavailable.", "The programme participation no longer exists.");

            Customer recruiter;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MayHaveTenant))
            {
                recruiter = await _customerRepository.GetAll()
                    .SingleOrDefaultAsync(item =>
                        item.Id == participation.CustomerId &&
                        item.TenantId == participation.TenantId);
            }
            if (recruiter == null)
                throw new UserFriendlyException("Invitation unavailable.", "The recruiter account is unavailable.");

            var area = await _tenantRepository.FirstOrDefaultAsync(participation.TenantId);
            return new ProgrammeInvitationPreviewDto
            {
                InviteCode = invitation.Code,
                RecruiterName = recruiter.Name,
                RecruiterClubMemberNumber = recruiter.ClubMemberNumber,
                ProgrammeKey = policy.ProgrammeKey,
                ProgrammeName = policy.ProgrammeName,
                RecruiterEligible = recruiter.IsActive && participation.IsEligible,
                AreaName = area?.TenancyName
            };
        }

        private async Task<ProgrammeInvitation> GetOrCreateAsync(
            IProgrammeRecruitmentPolicy policy,
            RecruiterParticipationReference participation)
        {
            ProgrammeInvitation existing;
            using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
            {
                existing = await _invitationRepository.FirstOrDefaultAsync(item =>
                    item.ProgrammeKey == policy.ProgrammeKey &&
                    item.ProgrammeParticipationId == participation.ParticipationId);
            }
            if (existing != null) return existing;

            for (var attempt = 0; attempt < 5; attempt++)
            {
                var invitation = ProgrammeInvitation.Create(
                    participation.TenantId,
                    policy.ProgrammeKey,
                    participation.ParticipationId);
                bool codeExists;
                using (CurrentUnitOfWork.DisableFilter(AbpDataFilters.MustHaveTenant))
                {
                    codeExists = await _invitationRepository.GetAll()
                        .AnyAsync(item => item.Code == invitation.Code);
                }
                if (codeExists) continue;

                await _invitationRepository.InsertAsync(invitation);
                await CurrentUnitOfWork.SaveChangesAsync();
                Logger.Info($"Programme invitation created programme={policy.ProgrammeKey} tenant={participation.TenantId} participation={participation.ParticipationId}");
                return invitation;
            }

            throw new UserFriendlyException(
                "Invitation unavailable.",
                "A secure invitation code could not be generated. Please try again.");
        }

        private async Task<Customer> GetCurrentActiveCustomerAsync()
        {
            var tenantId = GetRequiredTenantId("Invitations are unavailable.");
            var userId = AbpSession.GetUserId();
            var customer = await _customerRepository.FirstOrDefaultAsync(item =>
                item.TenantId == tenantId && item.UserId == userId);
            if (customer == null || !customer.IsActive)
                throw new UserFriendlyException(
                    "Invitations are unavailable.",
                    "An active Club Member account is required.");
            return customer;
        }
    }
}

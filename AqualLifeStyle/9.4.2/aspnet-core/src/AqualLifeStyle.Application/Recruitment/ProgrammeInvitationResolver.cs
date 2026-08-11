using System;
using System.Threading.Tasks;
using Abp.Dependency;
using Abp.Domain.Repositories;
using Abp.Domain.Uow;
using Abp.UI;
using AqualLifeStyle.Domain.Recruitment;

namespace AqualLifeStyle.Application.Recruitment
{
    public interface IProgrammeInvitationResolver
    {
        Task<int> ResolveRecruiterForJoiningAsync(
            string inviteCode,
            string expectedProgrammeKey,
            int inviteeCustomerId,
            int inviteeTenantId);
    }

    public sealed class ProgrammeInvitationResolver
        : IProgrammeInvitationResolver, ITransientDependency
    {
        private readonly IRepository<ProgrammeInvitation, Guid> _invitationRepository;
        private readonly IProgrammeRecruitmentPolicyResolver _policyResolver;
        private readonly IUnitOfWorkManager _unitOfWorkManager;

        public ProgrammeInvitationResolver(
            IRepository<ProgrammeInvitation, Guid> invitationRepository,
            IProgrammeRecruitmentPolicyResolver policyResolver,
            IUnitOfWorkManager unitOfWorkManager)
        {
            _invitationRepository = invitationRepository;
            _policyResolver = policyResolver;
            _unitOfWorkManager = unitOfWorkManager;
        }

        public async Task<int> ResolveRecruiterForJoiningAsync(
            string inviteCode,
            string expectedProgrammeKey,
            int inviteeCustomerId,
            int inviteeTenantId)
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
            if (!string.Equals(
                    invitation.ProgrammeKey,
                    expectedProgrammeKey,
                    StringComparison.Ordinal))
                throw InvalidInvitation("The invitation is for a different programme.");
            if (invitation.TenantId != inviteeTenantId)
                throw InvalidInvitation(
                    "The invitation belongs to a different organisation.");

            var source = await _policyResolver.Resolve(invitation.ProgrammeKey)
                .FindByParticipationAsync(invitation.ProgrammeParticipationId);
            if (source == null || !source.IsEligible)
                throw InvalidInvitation("The inviting Club Member is not currently eligible to invite members to this programme.");
            if (source.TenantId != inviteeTenantId)
                throw InvalidInvitation(
                    "The inviting Club Member belongs to a different organisation.");
            if (source.CustomerId == inviteeCustomerId)
                throw InvalidInvitation("You cannot accept your own invitation.");

            return source.CustomerId;
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

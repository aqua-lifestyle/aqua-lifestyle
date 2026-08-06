using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.InternalAccounts.Dto;

namespace AqualLifeStyle.Application.InternalAccounts
{
    public interface IInternalAccountInvitationAppService : IApplicationService
    {
        Task<InternalAccountInvitationPreviewDto> ValidateAsync(
            ValidateInternalAccountInvitationInput input);

        Task<AcceptInternalAccountInvitationOutput> AcceptAsync(
            AcceptInternalAccountInvitationInput input);
    }
}

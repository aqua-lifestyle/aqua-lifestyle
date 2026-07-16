using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Authorization.Accounts.Dto;

namespace AqualLifeStyle.Authorization.Accounts
{
    public interface IAccountAppService : IApplicationService
    {
        Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input);

        Task<RegisterOutput> Register(RegisterInput input);

        Task<bool> CompletePasswordSetup(CompletePasswordSetupInput input);
    }
}

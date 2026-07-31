using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Authorization.Accounts.Dto;

namespace AqualLifeStyle.Authorization.Accounts
{
    public interface IAccountAppService : IApplicationService
    {
        Task<IsTenantAvailableOutput> IsTenantAvailable(IsTenantAvailableInput input);

        Task<GetTenantSelfRegistrationAvailabilityOutput> GetTenantSelfRegistrationAvailability(
            GetTenantSelfRegistrationAvailabilityInput input);

        Task<RegisterOutput> Register(RegisterInput input);

        Task<bool> ConfirmEmail(ConfirmEmailInput input);

        Task<AccountEmailRequestOutput> ResendEmailVerification(RequestAccountEmailInput input);

        Task<AccountEmailRequestOutput> RequestPasswordReset(RequestAccountEmailInput input);

        Task<bool> ResetPassword(CompletePasswordResetInput input);

        Task<bool> CompletePasswordSetup(CompletePasswordSetupInput input);
    }
}

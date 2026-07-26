using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.MyAccount.Dto;

namespace AqualLifeStyle.Application.MyAccount
{
    public interface IMyAccountAppService : IApplicationService
    {
        Task<MyProfileDto> GetProfileAsync();
        Task<MyProfileDto> UpdateProfileAsync(UpdateMyProfileInput input);
        Task<ChangeMyPasswordResult> ChangePasswordAsync(ChangeMyPasswordInput input);
    }
}

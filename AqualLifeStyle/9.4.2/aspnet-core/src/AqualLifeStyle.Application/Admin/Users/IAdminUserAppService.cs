using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Users.Dto;

namespace AqualLifeStyle.Application.Admin.Users
{
    public interface IAdminUserAppService : IApplicationService
    {
        Task<PagedResultDto<AdminUserDto>> GetAllAsync(AdminUserListInput input);
        Task<AdminUserDto> GetAsync(EntityDto<long> input);
        Task<AdminUserDto> CreateAsync(AdminCreateUserInput input);
        Task<AdminUserDto> UpdateAsync(AdminUpdateUserInput input);
        Task<AdminUserDto> AssignRoleAsync(AdminAssignUserRoleInput input);
        Task ResetPasswordAsync(AdminResetUserPasswordInput input);
        Task<AdminUserDto> ResendInvitationAsync(AdminUserInvitationActionInput input);
        Task<AdminUserDto> RevokeInvitationAsync(AdminUserInvitationActionInput input);
        Task DeleteAsync(AdminDeleteUserInput input);
    }
}

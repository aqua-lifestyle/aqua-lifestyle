using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Members.Dto;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.Admin.Members
{
    public interface IAdminMemberAppService : IApplicationService
    {
        Task<PagedResultDto<AdminMemberDto>> GetAllAsync(AdminMemberListInput input);
        Task<AdminMemberDto> GetAsync(EntityDto<int> input);
        Task<List<AdminMembershipOptionDto>> GetMembershipOptionsAsync(EntityDto<int> input);
        Task<AdminMemberDto> EditProfileAsync(EditMemberProfileInput input);
        Task<AdminMemberDto> SuspendAsync(SuspendMemberInput input);
        Task<AdminMemberDto> ChangeTierAsync(ChangeMemberTierInput input);
    }
}

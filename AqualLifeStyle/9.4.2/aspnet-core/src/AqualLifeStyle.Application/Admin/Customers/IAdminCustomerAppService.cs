using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Customers.Dto;
using System.Collections.Generic;

namespace AqualLifeStyle.Application.Admin.Customers
{
    public interface IAdminCustomerAppService : IApplicationService
    {
        Task<PagedResultDto<AdminCustomerDto>> GetAllAsync(AdminCustomerListInput input);
        Task<List<AdminMembershipOptionDto>> GetMembershipOptionsAsync(AdminCustomerMembershipOptionsInput input);
        Task<AdminCustomerDto> GetAsync(EntityDto<int> input);
        Task<AdminCustomerDto> CreateAsync(AdminCreateCustomerInput input);
        Task<AdminCustomerDto> UpdateAsync(AdminUpdateCustomerInput input);
        Task DeleteAsync(AdminDeleteCustomerInput input);
    }
}

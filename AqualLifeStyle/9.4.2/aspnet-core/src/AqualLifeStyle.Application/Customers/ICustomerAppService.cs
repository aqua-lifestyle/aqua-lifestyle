using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Customers.Dto;

namespace AqualLifeStyle.Application.Customers
{
    public interface ICustomerAppService : IApplicationService
    {
        Task<IReadOnlyList<CustomerDto>> GetAllAsync();
        Task<CustomerDto> GetAsync(int id);
        Task<CustomerDto> GetMyCustomerAsync();
        Task CreateAsync(CreateCustomerDto input);
        Task<CustomerDto> UpdateAsync(CustomerDto input);
    }
}

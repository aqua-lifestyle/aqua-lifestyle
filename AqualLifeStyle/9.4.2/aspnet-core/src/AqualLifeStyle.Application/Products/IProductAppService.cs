using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Products.Dto;

namespace AqualLifeStyle.Application.Products
{
    public interface IProductAppService : IApplicationService
    {
        Task<IReadOnlyList<ProductDto>> GetAllAsync();
        Task<IReadOnlyList<ProductDto>> GetAllForCustomerAsync(int? customerId);
        Task<ProductDto> GetAsync(int id);
        Task CreateAsync(CreateProductDto input);
    }
}

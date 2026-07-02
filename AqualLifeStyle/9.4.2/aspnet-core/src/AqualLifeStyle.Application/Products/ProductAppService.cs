using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Products.Dto;
using AqualLifeStyle.Domain.Products;

namespace AqualLifeStyle.Application.Products
{
    public class ProductAppService : AqualLifeStyleAppServiceBase, IProductAppService
    {
        private readonly IProductRepository _productRepository;

        public ProductAppService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync()
        {
            var products = await _productRepository.GetAllListAsync();
            return products.Select(p => new ProductDto
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                MembershipId = p.MembershipId,
                IsActive = p.IsActive
            }).ToList();
        }

        public async Task<ProductDto> GetAsync(int id)
        {
            var product = await _productRepository.GetAsync(id);
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                MembershipId = product.MembershipId,
                IsActive = product.IsActive
            };
        }

        public async Task CreateAsync(CreateProductDto input)
        {
            var product = Product.Create(input.Name, input.Price, input.MembershipId);
            await _productRepository.InsertAsync(product);
        }
    }
}

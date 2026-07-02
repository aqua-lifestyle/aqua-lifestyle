using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Products.Dto;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Products;

namespace AqualLifeStyle.Application.Products
{
    public class ProductAppService : AqualLifeStyleAppServiceBase, IProductAppService
    {
        private readonly IProductRepository _productRepository;
        private readonly ICustomerRepository _customerRepository;
        private readonly IMembershipLookup _membershipLookup;

        public ProductAppService(IProductRepository productRepository)
            : this(productRepository, null, null)
        {
        }

        public ProductAppService(IProductRepository productRepository, ICustomerRepository customerRepository, IMembershipLookup membershipLookup)
        {
            _productRepository = productRepository;
            _customerRepository = customerRepository;
            _membershipLookup = membershipLookup;
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync()
        {
            return await GetAllAsync(null);
        }

        public async Task<IReadOnlyList<ProductDto>> GetAllAsync(int? customerId)
        {
            var products = await _productRepository.GetAllListAsync();
            if (!customerId.HasValue || _customerRepository == null || _membershipLookup == null)
            {
                return MapProducts(products);
            }

            var customer = await _customerRepository.GetAsync(customerId.Value);
            if (customer == null || !customer.IsActive)
            {
                return MapProducts(products.Where(product => product.MembershipId == null).ToList());
            }

            var membership = customer.MembershipId.HasValue
                ? await _membershipLookup.GetAsync(customer.MembershipId.Value)
                : null;

            var eligibilityManager = new ProductEligibilityManager(_membershipLookup);
            var visibleProducts = new List<ProductDto>();
            foreach (var product in products)
            {
                if (await eligibilityManager.CanViewProductAsync(customer, product, membership))
                {
                    visibleProducts.Add(MapProduct(product));
                }
            }

            return visibleProducts;
        }

        public async Task<ProductDto> GetAsync(int id)
        {
            var product = await _productRepository.GetAsync(id);
            return MapProduct(product);
        }

        public async Task CreateAsync(CreateProductDto input)
        {
            var product = Product.Create(input.Name, input.Price, input.MembershipId);
            await _productRepository.InsertAsync(product);
        }

        private static ProductDto MapProduct(Product product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                MembershipId = product.MembershipId,
                IsActive = product.IsActive
            };
        }

        private static IReadOnlyList<ProductDto> MapProducts(IEnumerable<Product> products)
        {
            return products.Select(MapProduct).ToList();
        }
    }
}

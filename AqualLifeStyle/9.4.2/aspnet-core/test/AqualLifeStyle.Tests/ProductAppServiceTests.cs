using System.Collections.Generic;
using System.Threading.Tasks;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Products.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Products;
using NSubstitute;
using Xunit;

namespace AqualLifeStyle.Tests
{
    public class ProductAppServiceTests
    {
        [Fact]
        public async Task GetAllAsync_ReturnsAllProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Basic", 10m, 1),
                Product.Create("Free Product", 0.01m, null)
            };

            var repository = Substitute.For<IProductRepository>();
            repository.GetAllListAsync().Returns(products);

            var service = new ProductAppService(repository);

            var result = await service.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Basic", result[0].Name);
            Assert.Equal(1, result[0].MembershipId);
            Assert.Equal("Free Product", result[1].Name);
            Assert.Null(result[1].MembershipId);
        }

        [Fact]
        public async Task GetAllForCustomerAsync_FiltersProductsByEligibility()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2),
                Product.Create("Vip Product", 20m, 3)
            };

            var productRepository = Substitute.For<IProductRepository>();
            productRepository.GetAllListAsync().Returns(products);

            var customerRepository = Substitute.For<ICustomerRepository>();
            var customer = Customer.Create("Jane Doe", new EmailAddress("jane@example.com"), 2);
            customerRepository.GetAsync(42).Returns(customer);

            var membershipLookup = Substitute.For<IMembershipLookup>();
            membershipLookup.GetAsync(2).Returns(Membership.Create("Onyx", "Onyx membership", MembershipType.Onyx));

            var service = new ProductAppService(productRepository, customerRepository, membershipLookup);

            var result = await service.GetAllForCustomerAsync(42);

            Assert.Equal(2, result.Count);
            Assert.Equal("Free Product", result[0].Name);
            Assert.Equal("Premium Product", result[1].Name);
        }

        [Fact]
        public async Task GetAllForCustomerAsync_WithCustomerWithoutMembership_ReturnsOnlyFreeProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2)
            };

            var productRepository = Substitute.For<IProductRepository>();
            productRepository.GetAllListAsync().Returns(products);

            var customerRepository = Substitute.For<ICustomerRepository>();
            var customer = Customer.Create("John Doe", new EmailAddress("john@example.com"), null);
            customerRepository.GetAsync(99).Returns(customer);

            var membershipLookup = Substitute.For<IMembershipLookup>();

            var service = new ProductAppService(productRepository, customerRepository, membershipLookup);

            var result = await service.GetAllForCustomerAsync(99);

            Assert.Single(result);
            Assert.Equal("Free Product", result[0].Name);
        }

        [Fact]
        public async Task GetAllForCustomerAsync_WithInactiveCustomer_ReturnsOnlyFreeProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2)
            };

            var productRepository = Substitute.For<IProductRepository>();
            productRepository.GetAllListAsync().Returns(products);

            var customerRepository = Substitute.For<ICustomerRepository>();
            var customer = Customer.Create("Jane Doe", new EmailAddress("jane@example.com"), 1);
            customer.Deactivate();
            customerRepository.GetAsync(100).Returns(customer);

            var membershipLookup = Substitute.For<IMembershipLookup>();

            var service = new ProductAppService(productRepository, customerRepository, membershipLookup);

            var result = await service.GetAllForCustomerAsync(100);

            Assert.Single(result);
            Assert.Equal("Free Product", result[0].Name);
        }

        [Fact]
        public async Task CreateAsync_InsertsProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.InsertAsync(Arg.Any<Product>()).Returns(Task.FromResult(default(Product)));

            var service = new ProductAppService(repository);

            await service.CreateAsync(new CreateProductDto
            {
                Name = "New Product",
                Price = 15m,
                MembershipId = 2
            });

            await repository.Received(1).InsertAsync(Arg.Is<Product>(p =>
                p.Name == "New Product" &&
                p.Price == 15m &&
                p.MembershipId == 2));
        }
    }
}

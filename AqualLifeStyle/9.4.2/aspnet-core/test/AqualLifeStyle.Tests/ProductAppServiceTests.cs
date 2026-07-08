using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Products.Dto;
using AqualLifeStyle.Domain.Common;
using AqualLifeStyle.Domain.Customers;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Memberships;
using AqualLifeStyle.Domain.Products;

namespace AqualLifeStyle.Tests
{
    public class ProductAppServiceTests
    {
        [Fact]
        public async Task GetAllAsync_ReturnsMappedProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Test Product", 10m, 1),
                Product.Create("Free Product", 0.01m, null)
            };

            var repositoryMock = new Mock<IProductRepository>();
            repositoryMock.Setup(r => r.GetAllListAsync())
                .ReturnsAsync(products);

            var appService = new ProductAppService(repositoryMock.Object);

            var result = await appService.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Test Product", result[0].Name);
            Assert.Equal(1, result[0].MembershipId);
            Assert.Equal("Free Product", result[1].Name);
            Assert.Null(result[1].MembershipId);
        }

        [Fact]
        public async Task GetAllAsync_WithCustomerId_FiltersProductsByEligibility()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2),
                Product.Create("Vip Product", 20m, 3)
            };

            var productRepositoryMock = new Mock<IProductRepository>();
            productRepositoryMock.Setup(r => r.GetAllListAsync())
                .ReturnsAsync(products);

            var customerRepositoryMock = new Mock<ICustomerRepository>();
            var customer = Customer.Create("Jane Doe", new EmailAddress("jane@example.com"), 2);
            customerRepositoryMock.Setup(r => r.GetAsync(42))
                .ReturnsAsync(customer);

            var membershipLookupMock = new Mock<IMembershipLookup>();
            membershipLookupMock.Setup(r => r.GetAsync(2))
                .ReturnsAsync(Membership.Create("Onyx", "Onyx membership", MembershipType.Onyx));

            var appService = new ProductAppService(productRepositoryMock.Object, customerRepositoryMock.Object, membershipLookupMock.Object);

            var result = await appService.GetAllForCustomerAsync(42);

            Assert.Equal(2, result.Count);
            Assert.Equal("Free Product", result[0].Name);
            Assert.Equal("Premium Product", result[1].Name);
        }

        [Fact]
        public async Task GetAllAsync_WithCustomerWithoutMembership_ReturnsOnlyFreeProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2)
            };

            var productRepositoryMock = new Mock<IProductRepository>();
            productRepositoryMock.Setup(r => r.GetAllListAsync())
                .ReturnsAsync(products);

            var customerRepositoryMock = new Mock<ICustomerRepository>();
            var customer = Customer.Create("John Doe", new EmailAddress("john@example.com"), null);
            customerRepositoryMock.Setup(r => r.GetAsync(99))
                .ReturnsAsync(customer);

            var membershipLookupMock = new Mock<IMembershipLookup>();

            var appService = new ProductAppService(productRepositoryMock.Object, customerRepositoryMock.Object, membershipLookupMock.Object);

            var result = await appService.GetAllForCustomerAsync(99);

            Assert.Single(result);
            Assert.Equal("Free Product", result[0].Name);
        }

        [Fact]
        public async Task GetAllAsync_WithInactiveCustomer_ReturnsOnlyFreeProducts()
        {
            var products = new List<Product>
            {
                Product.Create("Free Product", 0.01m, null),
                Product.Create("Premium Product", 10m, 2)
            };

            var productRepositoryMock = new Mock<IProductRepository>();
            productRepositoryMock.Setup(r => r.GetAllListAsync())
                .ReturnsAsync(products);

            var customerRepositoryMock = new Mock<ICustomerRepository>();
            var customer = Customer.Create("Jane Doe", new EmailAddress("jane@example.com"), 1);
            customer.Deactivate();
            customerRepositoryMock.Setup(r => r.GetAsync(100))
                .ReturnsAsync(customer);

            var membershipLookupMock = new Mock<IMembershipLookup>();

            var appService = new ProductAppService(productRepositoryMock.Object, customerRepositoryMock.Object, membershipLookupMock.Object);

            var result = await appService.GetAllForCustomerAsync(100);

            Assert.Single(result);
            Assert.Equal("Free Product", result[0].Name);
        }

        [Fact]
        public async Task CreateAsync_InsertsProduct()
        {
            var repositoryMock = new Mock<IProductRepository>();
            repositoryMock.Setup(r => r.InsertAsync(It.IsAny<Product>()))
                .ReturnsAsync((Product p) => p);

            var appService = new ProductAppService(repositoryMock.Object);

            var input = new CreateProductDto
            {
                Name = "New Product",
                Price = 25m,
                MembershipId = 2
            };

            await appService.CreateAsync(input);

            repositoryMock.Verify(r => r.InsertAsync(It.Is<Product>(p => p.Name == input.Name && p.Price == input.Price && p.MembershipId == input.MembershipId)), Times.Once);
        }
    }
}

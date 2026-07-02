using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Products.Dto;
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

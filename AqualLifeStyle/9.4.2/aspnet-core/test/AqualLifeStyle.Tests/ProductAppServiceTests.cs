using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Domain.Repositories;
using AqualLifeStyle.Application.Products;
using AqualLifeStyle.Application.Products.Dto;
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
                Product.Create("Premium", 20m, 2)
            };

            var repository = Substitute.For<IProductRepository>();
            repository.GetAllListAsync().Returns(products);

            var service = new ProductAppService(repository);

            var result = await service.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Basic", result[0].Name);
            Assert.Equal("Premium", result[1].Name);
        }

        [Fact]
        public async Task CreateAsync_InsertsProduct()
        {
            var repository = Substitute.For<IProductRepository>();
            repository.InsertAsync(Arg.Any<Product>()).Returns(Task.CompletedTask);

            var service = new ProductAppService(repository);

            await service.CreateAsync(new CreateProductDto
            {
                Name = "New Product",
                Price = 15m,
                MembershipId = null
            });

            await repository.Received(1).InsertAsync(Arg.Is<Product>(p => p.Name == "New Product" && p.Price == 15m));
        }
    }
}

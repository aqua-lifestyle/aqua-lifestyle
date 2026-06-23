using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using AqualLifeStyle.Domain.Products;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class ProductRepository : AqualLifeStyleRepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<Product> GetByIdAsync(int id)
        {
            return GetAsync(id);
        }

        public Task AddAsync(Product product)
        {
            return InsertAsync(product);
        }
    }
}

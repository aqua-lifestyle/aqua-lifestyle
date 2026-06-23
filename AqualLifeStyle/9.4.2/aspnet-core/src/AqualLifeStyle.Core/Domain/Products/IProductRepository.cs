using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Products
{
    public interface IProductRepository : IRepository<Product, int>
    {
        Task<Product> GetByIdAsync(int id);
        Task AddAsync(Product product);
    }
}

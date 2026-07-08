using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Orders
{
    public interface IOrderIntentRepository : IRepository<OrderIntent, int>
    {
        Task<int> CountOpenForCustomerAsync(int customerId);
        Task<OrderIntent> GetByEnquiryIdAsync(int enquiryId);
    }
}

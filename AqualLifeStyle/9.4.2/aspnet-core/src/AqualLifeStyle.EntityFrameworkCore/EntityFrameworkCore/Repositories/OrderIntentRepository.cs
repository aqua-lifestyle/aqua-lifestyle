using System.Linq;
using System.Threading.Tasks;
using Abp.EntityFrameworkCore;
using AqualLifeStyle.Domain.Enums;
using AqualLifeStyle.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class OrderIntentRepository : AqualLifeStyleRepositoryBase<OrderIntent>, IOrderIntentRepository
    {
        public OrderIntentRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<int> CountOpenForCustomerAsync(int customerId)
        {
            return GetAll()
                .CountAsync(orderIntent =>
                    orderIntent.CustomerId == customerId &&
                    (orderIntent.Status == OrderIntentStatus.Draft || orderIntent.Status == OrderIntentStatus.Reserved));
        }

        public Task<OrderIntent> GetByEnquiryIdAsync(int enquiryId)
        {
            return GetAll().FirstOrDefaultAsync(orderIntent => orderIntent.EnquiryId == enquiryId);
        }
    }
}

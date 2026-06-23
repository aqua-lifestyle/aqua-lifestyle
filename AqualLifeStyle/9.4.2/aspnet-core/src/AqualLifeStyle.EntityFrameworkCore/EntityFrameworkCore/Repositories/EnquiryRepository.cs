using System.Threading.Tasks;
using Abp.Domain.Repositories;
using Abp.EntityFrameworkCore;
using Abp.EntityFrameworkCore.Repositories;
using AqualLifeStyle.Domain.Enquiries;

namespace AqualLifeStyle.EntityFrameworkCore.Repositories
{
    public class EnquiryRepository : AqualLifeStyleRepositoryBase<Enquiry>, IEnquiryRepository
    {
        public EnquiryRepository(IDbContextProvider<AqualLifeStyleDbContext> dbContextProvider)
            : base(dbContextProvider)
        {
        }

        public Task<Enquiry> GetByIdAsync(int id)
        {
            return GetAsync(id);
        }

        public Task AddAsync(Enquiry enquiry)
        {
            return InsertAsync(enquiry);
        }
    }
}

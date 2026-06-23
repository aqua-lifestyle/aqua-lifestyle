using System.Threading.Tasks;
using Abp.Domain.Repositories;

namespace AqualLifeStyle.Domain.Enquiries
{
    public interface IEnquiryRepository : IRepository<Enquiry, int>
    {
    }
}

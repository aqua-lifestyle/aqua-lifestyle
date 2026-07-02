using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Enquiries.Dto;

namespace AqualLifeStyle.Application.Enquiries
{
    public interface IEnquiryAppService : IApplicationService
    {
        Task<IReadOnlyList<EnquiryDto>> GetAllAsync();
        Task<EnquiryDto> GetAsync(int id);
        Task CreateAsync(CreateEnquiryDto input);
        Task<EnquiryDto> RespondAsync(int id, RespondToEnquiryDto input);
        Task<EnquiryDto> CloseAsync(int id);
        Task<EnquiryDto> ReopenAsync(int id);
    }
}

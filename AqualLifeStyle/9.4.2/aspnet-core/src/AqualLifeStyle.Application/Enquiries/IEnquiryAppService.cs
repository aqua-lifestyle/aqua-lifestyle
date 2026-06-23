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
        Task<EnquiryDto> AssignToMemberAsync(int id, AssignEnquiryDto input);
        Task<EnquiryDto> ConvertToCustomerAsync(int id, ConvertEnquiryToCustomerDto input);
        Task<EnquiryDto> ClearAssignmentAsync(int id, ClearAssignmentDto input);
        Task<EnquiryFollowUpDto> RecordFollowUpAsync(int id, CreateEnquiryFollowUpDto input);
        Task<List<EnquiryFollowUpDto>> GetFollowUpsAsync(int id);
        Task<List<EnquiryDto>> GetSalesReadyEnquiriesAsync();
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Enquiries.Dto;
using AqualLifeStyle.Domain.Enquiries;
using AqualLifeStyle.Domain.Enums;

namespace AqualLifeStyle.Application.Enquiries
{
    public class EnquiryAppService : AqualLifeStyleAppServiceBase, IEnquiryAppService
    {
        private readonly IEnquiryRepository _enquiryRepository;

        public EnquiryAppService(IEnquiryRepository enquiryRepository)
        {
            _enquiryRepository = enquiryRepository;
        }

        public async Task<IReadOnlyList<EnquiryDto>> GetAllAsync()
        {
            var enquiries = await _enquiryRepository.GetAllListAsync();
            return enquiries.Select(MapToDto).ToList();
        }

        public async Task<EnquiryDto> GetAsync(int id)
        {
            var enquiry = await _enquiryRepository.GetAsync(id);
            return MapToDto(enquiry);
        }

        public async Task CreateAsync(CreateEnquiryDto input)
        {
            var enquiry = Enquiry.Create(input.CustomerId, input.ProductId, input.Message);
            await _enquiryRepository.InsertAsync(enquiry);
        }

        public async Task<EnquiryDto> RespondAsync(int id, RespondToEnquiryDto input)
        {
            var enquiry = await _enquiryRepository.GetAsync(id);
            enquiry.MarkAsResponded(input.Response);
            await _enquiryRepository.UpdateAsync(enquiry);
            return MapToDto(enquiry);
        }

        public async Task<EnquiryDto> CloseAsync(int id)
        {
            var enquiry = await _enquiryRepository.GetAsync(id);
            enquiry.Close();
            await _enquiryRepository.UpdateAsync(enquiry);
            return MapToDto(enquiry);
        }

        public async Task<EnquiryDto> ReopenAsync(int id)
        {
            var enquiry = await _enquiryRepository.GetAsync(id);
            enquiry.Reopen();
            await _enquiryRepository.UpdateAsync(enquiry);
            return MapToDto(enquiry);
        }

        private static EnquiryDto MapToDto(Enquiry enquiry)
        {
            return new EnquiryDto
            {
                Id = enquiry.Id,
                CustomerId = enquiry.CustomerId,
                ProductId = enquiry.ProductId,
                Message = enquiry.Message,
                Response = enquiry.Response,
                Status = (int)enquiry.Status,
                CreatedAt = enquiry.CreatedAt.ToString("u"),
                IsClosed = enquiry.Status == EnquiryStatus.Closed,
                IsPending = enquiry.Status == EnquiryStatus.Pending
            };
        }
    }
}

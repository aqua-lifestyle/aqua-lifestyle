using System.Collections.Generic;
using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Orders.Dto;

namespace AqualLifeStyle.Application.Orders
{
    public interface IOrderIntentAppService : IApplicationService
    {
        Task<IReadOnlyList<OrderIntentDto>> GetAllAsync();
        Task<IReadOnlyList<OrderIntentDto>> GetMineAsync();
        Task<OrderIntentDto> GetAsync(int id);
        Task<OrderIntentDto> CreateForCurrentCustomerAsync(int productId);
        Task<OrderIntentDto> CreateFromEnquiryAsync(int enquiryId);
        Task<OrderIntentDto> CancelAsync(int id);
        Task<OrderIntentDto> CompleteAsync(int id);
    }
}

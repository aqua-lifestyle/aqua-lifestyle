using System.Threading.Tasks;
using Abp.Application.Services;
using Abp.Application.Services.Dto;
using AqualLifeStyle.Application.Admin.Commissions.Dto;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public interface IAdminAQGreenWeeklySalesEligibilityAppService
        : IApplicationService
    {
        Task<PagedResultDto<AdminAQGreenWeeklySalesReviewDto>> GetAllAsync(
            AQGreenWeeklySalesReviewListInput input);

        Task<AdminAQGreenWeeklySalesReviewDto> GetAsync(EntityDto<System.Guid> input);

        Task<AdminAQGreenWeeklySalesReviewDto> GetLatestClosedWeekAsync(
            AQGreenWeeklySalesReviewTargetInput input);

        Task<AQGreenWeeklySalesEligibilityDecisionDto> BeginReviewAsync(
            BeginAQGreenWeeklySalesReviewInput input);

        Task<AQGreenWeeklySalesEligibilityDecisionDto> ConfirmAsync(
            ConfirmAQGreenWeeklySalesEligibilityInput input);

        Task<AQGreenWeeklySalesEligibilityDecisionDto> RejectAsync(
            RejectAQGreenWeeklySalesEligibilityInput input);
    }
}

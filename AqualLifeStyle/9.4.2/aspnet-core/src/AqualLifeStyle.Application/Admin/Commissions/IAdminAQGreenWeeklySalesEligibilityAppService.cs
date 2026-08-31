using System.Threading.Tasks;
using Abp.Application.Services;
using AqualLifeStyle.Application.Admin.Commissions.Dto;

namespace AqualLifeStyle.Application.Admin.Commissions
{
    public interface IAdminAQGreenWeeklySalesEligibilityAppService
        : IApplicationService
    {
        Task<AQGreenWeeklySalesEligibilityDecisionDto> BeginReviewAsync(
            BeginAQGreenWeeklySalesReviewInput input);

        Task<AQGreenWeeklySalesEligibilityDecisionDto> ConfirmAsync(
            ConfirmAQGreenWeeklySalesEligibilityInput input);

        Task<AQGreenWeeklySalesEligibilityDecisionDto> RejectAsync(
            RejectAQGreenWeeklySalesEligibilityInput input);
    }
}

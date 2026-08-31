using System.Threading.Tasks;

namespace AqualLifeStyle.Domain.AQGreen
{
    public interface IAQGreenWeeklySalesReviewGate
    {
        Task<bool> IsEnabledAsync(int tenantId);
    }
}
